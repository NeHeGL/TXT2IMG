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
    OnnxStack.StableDiffusion.Enums.SchedulerType Scheduler);

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
        DisplayName: "DreamShaper XL (fast)",
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
        // Dropped from the model's native 1024 to 768 for the same reason as SdxlBase below:
        // a single denoising/VAE step at 1024 (or the 1344-wide landscape bucket it reshapes
        // into) is slow enough via DirectML on this hardware to trip Windows' GPU driver
        // watchdog (TDR), surfacing as DXGI_ERROR_DEVICE_HUNG mid-batch.
        SampleSize: 768,
        // Nudged up from the model card's bare-minimum 4-6 steps / guidance 2.0 — that range
        // is tuned for speed, but leaves little room for the model to resolve tricky anatomy
        // (extra/fused limbs especially, even on Square at dynamic poses), so more headroom on
        // both helps without giving up much of the speed advantage over the 28-step base model.
        InferenceSteps: 12,
        GuidanceScale: 2.5f,
        Scheduler: OnnxStack.StableDiffusion.Enums.SchedulerType.LCM);

    // The official, ungated stabilityai release. Only available pre-converted to ONNX as
    // fp32, so it's a much larger download and needs more VRAM than the Lightning model above,
    // but it's the un-fine-tuned baseline for users who want that instead.
    public static readonly ModelDefinition SdxlBase = new(
        Id: "sdxl-base",
        DisplayName: "SDXL Base (standard)",
        RepoBaseUrl: "https://huggingface.co/stabilityai/stable-diffusion-xl-base-1.0/resolve/main/",
        Files:
        [
            new ModelFile("tokenizer/vocab.json", 1_059_962),
            new ModelFile("tokenizer/merges.txt", 524_619),
            new ModelFile("tokenizer/special_tokens_map.json", 472),
            new ModelFile("tokenizer/tokenizer_config.json", 737),
            new ModelFile("tokenizer_2/vocab.json", 1_059_962),
            new ModelFile("tokenizer_2/merges.txt", 524_619),
            new ModelFile("tokenizer_2/special_tokens_map.json", 460),
            new ModelFile("tokenizer_2/tokenizer_config.json", 725),
            new ModelFile("text_encoder/model.onnx", 492_587_457),
            new ModelFile("text_encoder_2/model.onnx", 1_041_992),
            new ModelFile("text_encoder_2/model.onnx_data", 2_778_639_360),
            new ModelFile("unet/model.onnx", 7_293_842),
            new ModelFile("unet/model.onnx_data", 10_269_854_720),
            new ModelFile("vae_decoder/model.onnx", 198_093_688),
            new ModelFile("vae_encoder/model.onnx", 136_775_724),
        ],
        // Dropped from SDXL's native 1024 to 768 as a diagnostic/mitigation: a single fp32
        // denoising step at 1024x1024 via DirectML is slow enough on this hardware to trip
        // Windows' GPU driver watchdog (TDR), which kills any single GPU command that runs
        // too long without yielding — surfacing as DXGI_ERROR_DEVICE_HUNG. 768 cuts the
        // per-step compute to ~56% of 1024's, which should land back under that window.
        SampleSize: 768,
        InferenceSteps: 28,
        GuidanceScale: 5f,
        Scheduler: OnnxStack.StableDiffusion.Enums.SchedulerType.EulerAncestral);

    public static readonly ModelDefinition[] All = [DreamShaperXLLightning, SdxlBase];
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
        long bytesDownloaded = 0;

        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromMinutes(30);

        foreach (var (relativePath, _) in model.Files)
        {
            var destinationPath = Path.Combine(modelFolder, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

            if (File.Exists(destinationPath))
            {
                bytesDownloaded += new FileInfo(destinationPath).Length;
                progress?.Report((bytesDownloaded, totalBytes));
                continue;
            }

            var tempPath = destinationPath + ".part";
            using (var response = await httpClient.GetAsync(model.RepoBaseUrl + relativePath, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);

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
