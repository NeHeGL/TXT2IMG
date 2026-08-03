using OnnxStack.Core.Image;
using OnnxStack.StableDiffusion.Config;
using OnnxStack.StableDiffusion.Enums;
using OnnxStack.StableDiffusion.Pipelines;

namespace TXT2IMG;

public interface IImageGenerator
{
    Task<IReadOnlyList<byte[]>> GenerateImagesAsync(
        ModelDefinition model,
        string prompt,
        byte[]? baseImageBytes = null,
        IProgress<string>? statusCallback = null,
        IProgress<(int Index, byte[] Bytes)>? imageCallback = null,
        string? extraNegativePrompt = null,
        int? widthOverride = null,
        int? heightOverride = null,
        IProgress<double>? progressFillCallback = null,
        float strength = 0.45f,
        float? guidanceScaleOverride = null,
        int? inferenceStepsOverride = null);
}

public class LocalImageGenerator : IImageGenerator
{
    // A single roll of the dice against a small model rarely matches the prompt on the
    // subject; generating a few seed variants per click and letting the user pick from
    // the results gives a much better effective hit rate than any single generation.
    // Matches the 5 fixed result slots shown in the UI.
    private const int BatchSize = 5;

    // "extra limbs" alone doesn't cover two failure modes seen in practice: two limbs (usually
    // legs) blending into one undifferentiated mass, and a leg terminating in a rounded stump
    // instead of a foot. Hands already had their own deformity terms below; feet had none at all.
    private const string BaseNegativePrompt =
        "blurry, distorted, watermark, text, low quality, extra limbs, duplicate, " +
        "deformed hands, extra fingers, fused fingers, mutated hands, bad anatomy, disfigured face, " +
        "fused limbs, merged limbs, conjoined limbs, extra legs, extra arms, " +
        "deformed feet, mutated feet, extra toes, fused toes, missing feet, disfigured feet, " +
        "amputee, cropped legs, missing limbs, stump";

    // Only one SDXL pipeline is kept loaded at a time (each is several GB of VRAM); switching
    // the selected model unloads whatever was active before loading the new one.
    private static StableDiffusionXLPipeline? _pipeline;
    private static string? _loadedModelId;

    public async Task<IReadOnlyList<byte[]>> GenerateImagesAsync(
        ModelDefinition model,
        string prompt,
        byte[]? baseImageBytes = null,
        IProgress<string>? statusCallback = null,
        IProgress<(int Index, byte[] Bytes)>? imageCallback = null,
        string? extraNegativePrompt = null,
        int? widthOverride = null,
        int? heightOverride = null,
        IProgress<double>? progressFillCallback = null,
        float strength = 0.45f,
        float? guidanceScaleOverride = null,
        int? inferenceStepsOverride = null)
    {
        if (!ModelDownloader.IsModelPresent(model))
        {
            statusCallback?.Report("Downloading...");
            var downloadProgress = new Progress<(long BytesDownloaded, long TotalBytes)>(p =>
            {
                var fraction = p.TotalBytes > 0 ? (double)p.BytesDownloaded / p.TotalBytes : 0;
                statusCallback?.Report($"Downloading {(int)(fraction * 100)}%");
                progressFillCallback?.Report(fraction);
            });
            await ModelDownloader.EnsureModelAsync(model, downloadProgress);
            progressFillCallback?.Report(1.0);
        }

        statusCallback?.Report("Loading...");
        try
        {
            if (_loadedModelId != model.Id)
            {
                if (_pipeline is not null)
                {
                    await _pipeline.UnloadAsync();
                }

                // CreatePipeline is synchronous — for a multi-GB fp32 model, constructing the ONNX
                // sessions can take minutes. Calling it directly on the caller's thread (the UI
                // thread, here) blocks the entire window for that whole time: no repaints, no
                // button feedback, nothing. It looks exactly like a hang. Task.Run keeps the UI
                // thread free so the window stays responsive while this loads in the background.
                var modelFolder = ModelDownloader.GetModelFolder(model);
                _pipeline = await Task.Run(() => StableDiffusionXLPipeline.CreatePipeline(ExecutionProviders.DirectML(), modelFolder));
                _loadedModelId = model.Id;
            }

            var batchOptions = new GenerateBatchOptions
            {
                // OnnxStack's seed-batch generator returns exactly ValueTo images (not ValueTo + 1);
                // decompiled BatchGenerator.GenerateBatch confirms count == ValueTo for ValueTo > 1.
                BatchType = BatchOptionType.Seed,
                ValueFrom = 0,
                ValueTo = BatchSize,
                Increment = 1,
                Prompt = prompt,
                NegativePrompt = string.IsNullOrEmpty(extraNegativePrompt) ? BaseNegativePrompt : $"{BaseNegativePrompt}, {extraNegativePrompt}",
                SchedulerOptions = _pipeline!.DefaultSchedulerOptions with
                {
                    Width = widthOverride ?? model.SampleSize,
                    Height = heightOverride ?? model.SampleSize,
                    InferenceSteps = inferenceStepsOverride ?? model.InferenceSteps,
                    GuidanceScale = guidanceScaleOverride ?? model.GuidanceScale,
                    SchedulerType = model.Scheduler
                }
            };

            if (baseImageBytes is not null)
            {
                batchOptions.Diffuser = DiffuserType.ImageToImage;
                batchOptions.InputImage = await OnnxImage.FromBytesAsync(baseImageBytes);
                batchOptions.SchedulerOptions = batchOptions.SchedulerOptions with { Strength = strength };
            }

            // "Generating N of BatchSize" names the one currently in flight, not the count
            // completed so far — there's no separate "started" signal from GenerateBatchAsync,
            // only completions, so as each one finishes we announce the next one starting
            // (skipped once there is no next one).
            statusCallback?.Report($"Generating 1 of {BatchSize}");
            progressFillCallback?.Report(0.0);
            var images = new List<byte[]>(BatchSize);
            await foreach (var batchResult in _pipeline.GenerateBatchAsync(batchOptions))
            {
                byte[] bytes;
                using (batchResult.Result)
                {
                    bytes = await batchResult.Result.GetImageBytesAsync();
                }
                imageCallback?.Report((images.Count, bytes));
                images.Add(bytes);
                progressFillCallback?.Report((double)images.Count / BatchSize);
                if (images.Count < BatchSize)
                {
                    statusCallback?.Report($"Generating {images.Count + 1} of {BatchSize}");
                }
            }
            return images;
        }
        catch
        {
            // A GPU-level failure (device hung/removed) leaves the D3D12 device behind this
            // pipeline permanently dead — every call that reuses it fails instantly from then
            // on, which is exactly "works once, then fails every time." Discarding the cached
            // pipeline here forces the next attempt to build a fresh one (and a fresh device)
            // instead of endlessly retrying a session that can never recover.
            _pipeline = null;
            _loadedModelId = null;
            throw;
        }
    }
}
