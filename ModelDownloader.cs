using System.Net.Http;

namespace TXT2IMG;

public sealed record ModelFile(string RelativePath, long Bytes);

public sealed record ModelDefinition(
    string Id,
    string DisplayName,
    string RepoBaseUrl,
    ModelFile[] Files,
    int SampleSize,
    int InferenceSteps,
    float GuidanceScale,
    OnnxStack.StableDiffusion.Enums.SchedulerType Scheduler,
    int QualitySampleSize,
    int QualityInferenceSteps,
    float QualityGuidanceScale,
    int HighQualitySampleSize,
    int HighQualityInferenceSteps,
    float HighQualityGuidanceScale);

public static class ModelCatalog
{
    // AMD's ONNX export of Lykon's official DreamShaper XL Lightning checkpoint. Apache-2.0
    // (no hardware lock-in despite the "amdgpu" folder name — that only refers to bundled,
    // unused shader caches for AMD chips; the .onnx graphs themselves run through the same
    // DirectML execution provider as any other model). Distilled for 4-8 step / low-guidance
    // inference, so it's both smaller and faster than the base model while generally giving
    // cleaner anatomy thanks to the DreamShaper fine-tuning.
    public static readonly ModelDefinition DreamShaperXLLightning = new(
        Id: "dreamshaper-xl-lightning",
        DisplayName: "DreamShaper XL Lightning",
        RepoBaseUrl: "https://huggingface.co/amd/dreamshaper-xl-lightning_io32_amdgpu/resolve/main/",
        Files:
        [
            new ModelFile("tokenizer/vocab.json", 1_109_372),
            new ModelFile("tokenizer/merges.txt", 573_514),
            new ModelFile("tokenizer/special_tokens_map.json", 618),
            new ModelFile("tokenizer/tokenizer_config.json", 735),
            new ModelFile("tokenizer_2/vocab.json", 1_109_372),
            new ModelFile("tokenizer_2/merges.txt", 573_514),
            new ModelFile("tokenizer_2/special_tokens_map.json", 606),
            new ModelFile("tokenizer_2/tokenizer_config.json", 894),
            new ModelFile("text_encoder/model.onnx", 246_475_546),
            new ModelFile("text_encoder_2/model.onnx", 1_055_805),
            new ModelFile("text_encoder_2/model.onnx.data", 452_999_680),
            new ModelFile("unet/model.onnx", 1_403_642),
            new ModelFile("unet/model.onnx.data", 5_134_903_040),
            new ModelFile("vae_decoder/model.onnx", 102_364_513),
            new ModelFile("vae_encoder/model.onnx", 68_392_048),
        ],
        // Dropped from the model's native 1024 to 768: a single denoising/VAE step at 1024
        // (or the 1344-wide landscape bucket it reshapes into) is slow enough via DirectML on
        // this hardware to trip Windows' GPU driver watchdog (TDR), surfacing as
        // DXGI_ERROR_DEVICE_HUNG mid-batch.
        SampleSize: 768,
        // Nudged up from the model card's bare-minimum 4-6 steps / guidance 2.0 — that range
        // is tuned for speed, but leaves little room for the model to resolve tricky anatomy
        // (extra/fused limbs especially, even on Square at dynamic poses), so more headroom on
        // both helps without giving up much of the speed advantage of the Lightning distillation.
        InferenceSteps: 12,
        GuidanceScale: 2.5f,
        Scheduler: OnnxStack.StableDiffusion.Enums.SchedulerType.LCM,
        // Quality mode: more pixels + denoising budget (see HardwareBudget — capped for 16 GB VRAM).
        // Do not raise above HardwareBudget.MaxLongEdgePixels without re-validating on a 16 GB GPU.
        QualitySampleSize: HardwareBudget.MaxLongEdgePixels,
        QualityInferenceSteps: 22,
        QualityGuidanceScale: 3.0f,
        HighQualitySampleSize: HardwareBudget.HighTierLongEdgePixels,
        HighQualityInferenceSteps: 26,
        HighQualityGuidanceScale: 3.2f);

    public static readonly ModelDefinition[] All = [DreamShaperXLLightning];
}

public static class ModelDownloader
{
    public static string GetModelFolder(ModelDefinition model)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "TXT2IMG", "Models", model.Id);
    }

    public static bool IsModelPresent(ModelDefinition model)
    {
        var modelFolder = GetModelFolder(model);
        return model.Files.All(f => File.Exists(Path.Combine(modelFolder, f.RelativePath.Replace('/', Path.DirectorySeparatorChar))));
    }

    public static async Task EnsureModelAsync(ModelDefinition model, IProgress<(long BytesDownloaded, long TotalBytes)>? progress, CancellationToken cancellationToken = default)
    {
        var modelFolder = GetModelFolder(model);
        if (IsModelPresent(model))
            return;

        var totalBytes = model.Files.Sum(f => f.Bytes);
        // Count finished files + any .part leftovers so the Generate fill resumes where we left off.
        long bytesDownloaded = model.Files.Sum(f =>
        {
            var path = Path.Combine(modelFolder, f.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path))
                return new FileInfo(path).Length;
            var part = path + ".part";
            return File.Exists(part) ? new FileInfo(part).Length : 0L;
        });
        progress?.Report((bytesDownloaded, totalBytes));

        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromMinutes(30);

        foreach (var (relativePath, expectedBytes) in model.Files)
        {
            var destinationPath = Path.Combine(modelFolder, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

            if (File.Exists(destinationPath))
                continue;

            var tempPath = destinationPath + ".part";
            var existingPartBytes = File.Exists(tempPath) ? new FileInfo(tempPath).Length : 0L;

            // Already have a complete .part (e.g. crash after download, before rename).
            if (existingPartBytes > 0 && expectedBytes > 0 && existingPartBytes >= expectedBytes)
            {
                File.Move(tempPath, destinationPath, overwrite: true);
                continue;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, model.RepoBaseUrl + relativePath);
            if (existingPartBytes > 0)
                request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(existingPartBytes, null);

            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            FileMode writeMode;
            if (existingPartBytes > 0 && response.StatusCode == System.Net.HttpStatusCode.PartialContent)
            {
                writeMode = FileMode.Append;
            }
            else
            {
                // Server ignored Range (200) or no partial — start this file fresh.
                response.EnsureSuccessStatusCode();
                if (existingPartBytes > 0)
                {
                    bytesDownloaded -= existingPartBytes;
                    existingPartBytes = 0;
                    progress?.Report((bytesDownloaded, totalBytes));
                }
                writeMode = FileMode.Create;
            }

            await using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var fileStream = new FileStream(tempPath, writeMode, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[81920];
                int bytesRead;
                while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    bytesDownloaded += bytesRead;
                    progress?.Report((bytesDownloaded, totalBytes));
                }
            }

            File.Move(tempPath, destinationPath, overwrite: true);
        }
    }
}
