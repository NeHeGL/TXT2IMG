namespace TXT2IMG;

/// <summary>
/// Resolution / denoise tiers for DreamShaper XL Lightning.
/// </summary>
public static class GenerationProfile
{
    public const string FastLabel = "768 — Default";
    public const string QualityLabel = "896 — Sharper";

    /// <summary>Legacy PNG/metadata label; no longer shown in the UI (896 is the canvas cap for everyone).</summary>
    public const string HighVramLabel = "Max — Large GPU";

    public enum QualityTier
    {
        Fast,
        Quality,
        HighVram
    }

    public static QualityTier ParseTier(string? qualityLabel)
    {
        if (string.Equals(qualityLabel, HighVramLabel, StringComparison.Ordinal))
            return QualityTier.HighVram;
        if (string.Equals(qualityLabel, QualityLabel, StringComparison.Ordinal))
            return QualityTier.Quality;
        return QualityTier.Fast;
    }

    public static QualityTier ParseTier(object? tag, string? fallbackLabel) =>
        tag is QualityTier tier ? tier : ParseTier(fallbackLabel);

    /// <summary>896 uses the high-VRAM step/CFG recipe when the GPU qualifies — same pixel size as Quality.</summary>
    public static QualityTier ResolveEffectiveTier(QualityTier selected, bool supportsHighVramQuality) =>
        selected == QualityTier.Quality && supportsHighVramQuality
            ? QualityTier.HighVram
            : selected;

    public static int GetSampleSize(ModelDefinition model, QualityTier tier) => tier switch
    {
        QualityTier.HighVram => Math.Min(model.HighQualitySampleSize, HardwareBudget.HighTierLongEdgePixels),
        QualityTier.Quality => Math.Min(model.QualitySampleSize, HardwareBudget.MaxLongEdgePixels),
        _ => HardwareBudget.FastLongEdgePixels
    };

    public static int GetBaseSteps(ModelDefinition model, QualityTier tier) => tier switch
    {
        QualityTier.HighVram => model.HighQualityInferenceSteps,
        QualityTier.Quality => model.QualityInferenceSteps,
        _ => model.InferenceSteps
    };

    public static float GetBaseGuidance(ModelDefinition model, QualityTier tier) => tier switch
    {
        QualityTier.HighVram => model.HighQualityGuidanceScale,
        QualityTier.Quality => model.QualityGuidanceScale,
        _ => model.GuidanceScale
    };

    public static int ComputeInferenceSteps(
        int baseSteps,
        string aspectChoice,
        string style,
        bool largeGpuExtendedSteps,
        QualityTier tier)
    {
        var steps = aspectChoice == "Square" ? baseSteps : (int)Math.Round(baseSteps * 1.5);

        if (style == "Graphic Oil")
            steps = (int)Math.Round(steps * 1.5);

        // Lightning was distilled for a handful of LCM steps — very high counts tend to mush on 16 GB cards.
        var cap = HardwareBudget.LightningInferenceStepCap;
        if (largeGpuExtendedSteps && tier == QualityTier.HighVram)
            cap = HardwareBudget.LightningInferenceStepCapLargeGpuMaxTier;

        return Math.Min(steps, cap);
    }

    public static int ComputeVarySteps(int txt2ImgSteps) =>
        (int)Math.Round(txt2ImgSteps * 1.35);

    /// <summary>Img2img: new variation of the selected image (higher denoise than old “refine”).</summary>
    public const float VaryStrength = 0.62f;
}