namespace TXT2IMG;

/// <summary>
/// Hardware assumptions for shipped defaults. Tune quality caps here — not against the dev machine.
/// </summary>
public static class HardwareBudget
{
    /// <summary>Design target for VRAM (GB). Dev machines may have more; defaults must stay safe here.</summary>
    public const int TargetVramGigabytes = 16;

    /// <summary>
    /// Long-edge cap for Quality mode. SDXL fp32 via DirectML holds one pipeline (~6–8 GB weights);
    /// activations grow with resolution. 896 fits 16 GB with desktop overhead; 1024 is not enabled
    /// (TDR risk on many cards, tighter on 16 GB).
    /// </summary>
    public const int MaxLongEdgePixels = 896;

    /// <summary>Fast mode long edge — same baseline as published TXT2IMG.</summary>
    public const int FastLongEdgePixels = 768;

    /// <summary>OnnxStack generates seed batches one image at a time — never raise for "free VRAM."</summary>
    public const int MaxConcurrentGenerations = 1;

    /// <summary>
    /// Large-GPU tier still uses 896 px — 1024 repeatedly produced TDR / blank result slots on DirectML.
    /// </summary>
    public const int HighTierLongEdgePixels = 896;

    /// <summary>
    /// 20 GB-class discrete GPUs (24 GB cards, etc.). DXGI often reports slightly under marketing size.
    /// </summary>
    public const int MinDedicatedVramGigabytesForHighMode = 18;

    /// <summary>
    /// 32 GB-class cards (e.g. RTX 5090 reports ~31.4 GiB dedicated). Exact 32 GiB fails on many drivers.
    /// Raises Lightning step cap at 896.
    /// </summary>
    public const int MinDedicatedVramGigabytesForExtendedSteps = 30;

    public const int LightningInferenceStepCap = 32;

    /// <summary>32 GB-class at 896 — a few more LCM steps than the 16 GB ship cap.</summary>
    public const int LightningInferenceStepCapLargeGpuMaxTier = 44;

    public static ulong MinDedicatedVramBytesForHighMode =>
        (ulong)MinDedicatedVramGigabytesForHighMode * 1024UL * 1024UL * 1024UL;

    public static ulong MinDedicatedVramBytesForExtendedSteps =>
        (ulong)MinDedicatedVramGigabytesForExtendedSteps * 1024UL * 1024UL * 1024UL;
}