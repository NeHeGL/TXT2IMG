using TXT2IMG;

// Headless prompt×style matrix. Uses the same StylePromptBuilder as the app.
// Usage:
//   matrix              — txt2img red boat + apple across all styles (1 img each)
//   matrix-ref          — boat reference restyles for problem styles
//   boat-ref            — create tools/test_ref_boat.png

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var model = ModelCatalog.DreamShaperXLLightning;
if (!ModelDownloader.IsModelPresent(model))
{
    Console.WriteLine("Model not downloaded.");
    return 1;
}

var mode = args.Length > 0 ? args[0] : "matrix";
var gen = new LocalImageGenerator();
var status = new Progress<string>(s => Console.WriteLine($"  {s}"));

if (mode == "boat-ref")
{
    var outPath = Path.Combine(root, "tools", "test_ref_boat.png");
    Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
    byte[]? first = null;
    var cb = new Progress<(int Index, byte[] Bytes)>(p =>
    {
        if (p.Index != 0) return;
        first = p.Bytes;
        File.WriteAllBytes(outPath, p.Bytes);
        Console.WriteLine($"Wrote {outPath}");
    });
    await gen.GenerateImagesAsync(
        model,
        "a single red wooden rowboat on calm blue water, simple photo, centered, no people",
        statusCallback: status,
        imageCallback: cb,
        extraNegativePrompt: "person, people, human, face, nude, body",
        batchSizeOverride: 1);
    return first is null ? 2 : 0;
}

string[] styles =
[
    "Cartoon", "Icon", "Comic Book", "Cel-Shaded", "Anime",
    "Watercolor", "Impasto", "Oil Painting", "Claymation", "Fantasy Art",
    "Pixel Art", "3D Render", "Photorealistic", "Vintage Photo",
];

if (mode == "ref-clay-test")
{
    // User keep/drop test: txt boat (None) → use as ref → "convert this image to claymation"
    // Uses the same BuildReferenceEditPrompt + strength the app uses with Style=None + ref.
    var outDir = Path.Combine(root, "tools", "shots", "ref_clay_test");
    Directory.CreateDirectory(outDir);

    byte[]? boatBytes = null;
    {
        Console.WriteLine("=== 1) TXT None / wooden toy boat ===");
        const string prompt = "a wooden toy boat on calm water, simple photo, centered, no people";
        var path = Path.Combine(outDir, "01_boat_none.png");
        var cb = new Progress<(int Index, byte[] Bytes)>(p =>
        {
            if (p.Index != 0) return;
            boatBytes = p.Bytes;
            File.WriteAllBytes(path, p.Bytes);
        });
        await gen.GenerateImagesAsync(
            model, prompt, null, status, cb,
            extraNegativePrompt: "person, people, human, face",
            batchSizeOverride: 1);
        Console.WriteLine($"  saved {path}");
    }

    if (boatBytes is null)
    {
        Console.WriteLine("Failed to create boat reference.");
        return 2;
    }

    {
        Console.WriteLine("=== 2) REF edit / convert this image to claymation ===");
        const string userPrompt = "convert this image to claymation";
        var full = StylePromptBuilder.BuildReferenceEditPrompt(userPrompt);
        // Match MainWindow Style=None + reference: identity negatives, strength 0.50, 1.5x steps
        var neg = StylePromptBuilder.ReferenceIdentityNegative;
        var steps = (int)Math.Round(model.InferenceSteps * 1.5);
        Console.WriteLine($"  prompt: {full}");
        Console.WriteLine($"  strength=0.50 steps={steps}");
        var path = Path.Combine(outDir, "02_clay_edit.png");
        var cb = new Progress<(int Index, byte[] Bytes)>(p =>
        {
            if (p.Index == 0) File.WriteAllBytes(path, p.Bytes);
        });
        await gen.GenerateImagesAsync(
            model, full, boatBytes, status, cb, neg,
            strength: 0.50f,
            inferenceStepsOverride: steps,
            batchSizeOverride: 1);
        Console.WriteLine($"  saved {path}");
    }

    // Higher-denoise control: same prompt stack, strength 0.85 — still person-locked wording
    {
        Console.WriteLine("=== 3) REF edit claymation @ strength 0.85 (control) ===");
        const string userPrompt = "convert this image to claymation";
        var full = StylePromptBuilder.BuildReferenceEditPrompt(userPrompt);
        var neg = StylePromptBuilder.ReferenceIdentityNegative;
        var steps = (int)Math.Round(model.InferenceSteps * 1.5);
        var path = Path.Combine(outDir, "03_clay_edit_hi.png");
        var cb = new Progress<(int Index, byte[] Bytes)>(p =>
        {
            if (p.Index == 0) File.WriteAllBytes(path, p.Bytes);
        });
        await gen.GenerateImagesAsync(
            model, full, boatBytes, status, cb, neg,
            strength: 0.85f,
            inferenceStepsOverride: steps,
            batchSizeOverride: 1);
        Console.WriteLine($"  saved {path}");
    }

    // Subject-agnostic wording + clay style (what we'd need if keeping ref for restyle)
    {
        Console.WriteLine("=== 4) REF subject-agnostic clay prompt @ 0.90 ===");
        var full =
            "claymation of a wooden toy boat, stop-motion clay animation style, sculpted clay, " +
            "visible fingerprints and tool marks, same subject as the reference photo";
        var (_, styleNeg, gMul) = StylePromptBuilder.BuildPrompt("wooden toy boat", "Claymation");
        var neg = $"{styleNeg}, {StylePromptBuilder.ReferenceSubjectNegative}";
        float? cfg = gMul == 1f ? null : model.GuidanceScale * gMul;
        var path = Path.Combine(outDir, "04_clay_subject_agnostic.png");
        var cb = new Progress<(int Index, byte[] Bytes)>(p =>
        {
            if (p.Index == 0) File.WriteAllBytes(path, p.Bytes);
        });
        await gen.GenerateImagesAsync(
            model, full, boatBytes, status, cb, neg,
            strength: 0.90f,
            guidanceScaleOverride: cfg,
            inferenceStepsOverride: (int)Math.Round(model.InferenceSteps * 2.0),
            batchSizeOverride: 1);
        Console.WriteLine($"  saved {path}");
    }

    Console.WriteLine($"Done. {outDir}");
    return 0;
}

if (mode == "style-audit")
{
    // Quick 1-img audit for styles we retuned after the red-boat uniqueness pass.
    var outDir = Path.Combine(root, "tools", "shots", "style_audit");
    Directory.CreateDirectory(outDir);
    const string prompt = "a red boat on water";
    string[] auditStyles =
    [
        "Cartoon", "Icon", "Comic Book", "Anime", "Impasto", "Oil Painting",
        "Fantasy Art", "Pixel Art", "3D Render", "Photorealistic", "Vintage Photo", "None",
    ];

    foreach (var style in auditStyles)
    {
        Console.WriteLine($"=== AUDIT {style} ===");
        var (full, neg, gMul) = StylePromptBuilder.BuildPrompt(prompt, style);
        float? cfg = gMul == 1f ? null : model.GuidanceScale * gMul;
        Console.WriteLine($"  prompt: {full}");
        var path = Path.Combine(outDir, $"{Slug(style)}.png");
        var cb = new Progress<(int Index, byte[] Bytes)>(p =>
        {
            if (p.Index == 0) File.WriteAllBytes(path, p.Bytes);
        });
        await gen.GenerateImagesAsync(
            model, full, null, status, cb, neg,
            guidanceScaleOverride: cfg,
            batchSizeOverride: 1);
        Console.WriteLine($"  saved {path}");
    }

    Console.WriteLine($"Done. {outDir}");
    return 0;
}

if (mode == "fantasy-ab")
{
    // Side-by-side: Fantasy txt2img (works) vs Fantasy + boat reference (was broken).
    var refPath = Path.Combine(root, "tools", "test_ref_boat.png");
    if (!File.Exists(refPath))
    {
        Console.WriteLine($"Missing {refPath} — run boat-ref first");
        return 1;
    }
    var refBytes = await File.ReadAllBytesAsync(refPath);
    var outDir = Path.Combine(root, "tools", "shots", "fantasy_ab");
    Directory.CreateDirectory(outDir);
    const string style = "Fantasy Art";
    const string userPrompt = "wooden toy boat";

    {
        Console.WriteLine("=== TXT Fantasy / wooden toy boat ===");
        var (full, neg, gMul) = StylePromptBuilder.BuildPrompt(userPrompt, style);
        float? cfg = gMul == 1f ? null : model.GuidanceScale * gMul;
        Console.WriteLine($"  prompt: {full}");
        var path = Path.Combine(outDir, "txt.png");
        var cb = new Progress<(int Index, byte[] Bytes)>(p =>
        {
            if (p.Index == 0) File.WriteAllBytes(path, p.Bytes);
        });
        await gen.GenerateImagesAsync(
            model, full, null, status, cb, neg,
            guidanceScaleOverride: cfg,
            batchSizeOverride: 1);
        Console.WriteLine($"  saved {path}");
    }

    {
        Console.WriteLine("=== REF Fantasy / wooden toy boat ===");
        var full = StylePromptBuilder.BuildReferenceStylePrompt($"make it a {userPrompt}", style);
        var (_, styleNeg, guidanceMul) = StylePromptBuilder.BuildPrompt(userPrompt, style);
        var (strength, cfgFloor, stepMul, extraNeg) = StylePromptBuilder.GetReferenceStyleInference(style);
        var neg =
            $"{styleNeg}, photo, photograph, photorealistic, DSLR, candid shot, " +
            $"{StylePromptBuilder.ReferenceSubjectNegative}, {extraNeg}";
        var cfg = Math.Max(model.GuidanceScale * (guidanceMul == 1f ? 1f : guidanceMul), cfgFloor);
        var steps = (int)Math.Round(model.InferenceSteps * stepMul);
        Console.WriteLine($"  prompt: {full}");
        Console.WriteLine($"  strength={strength} cfg={cfg} steps={steps}");
        var path = Path.Combine(outDir, "ref.png");
        var cb = new Progress<(int Index, byte[] Bytes)>(p =>
        {
            if (p.Index == 0) File.WriteAllBytes(path, p.Bytes);
        });
        await gen.GenerateImagesAsync(
            model, full, refBytes, status, cb, neg,
            strength: strength,
            guidanceScaleOverride: cfg,
            inferenceStepsOverride: steps,
            batchSizeOverride: 1);
        Console.WriteLine($"  saved {path}");
    }

    Console.WriteLine($"Done. {outDir}");
    return 0;
}

if (mode == "matrix-ref")
{
    var refPath = Path.Combine(root, "tools", "test_ref_boat.png");
    if (!File.Exists(refPath))
    {
        Console.WriteLine($"Missing {refPath} — run boat-ref first");
        return 1;
    }
    var refBytes = await File.ReadAllBytesAsync(refPath);
    var outDir = Path.Combine(root, "tools", "shots", "matrix_ref");
    Directory.CreateDirectory(outDir);
    var prompt = "make it a wooden toy boat";
    string[] refStyles =
    [
        "Icon", "Pixel Art", "Comic Book", "Cartoon", "Watercolor",
        "Impasto", "Oil Painting", "Fantasy Art", "Claymation", "Cel-Shaded",
    ];

    foreach (var style in refStyles)
    {
        var tag = Slug(style);
        Console.WriteLine($"=== REF {style} / {prompt} ===");
        var full = StylePromptBuilder.BuildReferenceStylePrompt(prompt, style);
        var (basePrompt, styleNeg, guidanceMul) = StylePromptBuilder.BuildPrompt(prompt, style);
        _ = basePrompt;
        var (strength, cfgFloor, stepMul, extraNeg) = StylePromptBuilder.GetReferenceStyleInference(style);
        var neg = string.IsNullOrEmpty(styleNeg)
            ? $"{StylePromptBuilder.ReferenceSubjectNegative}, {extraNeg}"
            : $"{styleNeg}, photo, photograph, photorealistic, DSLR, candid shot, {StylePromptBuilder.ReferenceSubjectNegative}, {extraNeg}";
        if (style is "Photorealistic" or "Vintage Photo")
            neg = $"{styleNeg}, {StylePromptBuilder.ReferenceSubjectNegative}, {extraNeg}";

        var cfg = Math.Max(model.GuidanceScale * (guidanceMul == 1f ? 1f : guidanceMul), cfgFloor);
        var steps = (int)Math.Round(model.InferenceSteps * stepMul);
        Console.WriteLine($"  prompt: {full}");

        var path = Path.Combine(outDir, $"{tag}.png");
        var cb = new Progress<(int Index, byte[] Bytes)>(p =>
        {
            if (p.Index == 0) File.WriteAllBytes(path, p.Bytes);
        });
        await gen.GenerateImagesAsync(
            model, full, refBytes, status, cb, neg,
            strength: strength,
            guidanceScaleOverride: cfg,
            inferenceStepsOverride: steps,
            batchSizeOverride: 1);
        Console.WriteLine($"  saved {path}");
    }
    Console.WriteLine($"Done. {outDir}");
    return 0;
}

// Default: txt2img matrix
{
    var outDir = Path.Combine(root, "tools", "shots", "matrix_txt");
    Directory.CreateDirectory(outDir);
    string[] prompts = ["a red boat on water", "a bright red apple on a white table"];

    foreach (var prompt in prompts)
    {
        var pslug = Slug(prompt);
        foreach (var style in styles)
        {
            var tag = $"{pslug}__{Slug(style)}";
            Console.WriteLine($"=== TXT {style} / {prompt} ===");
            var (full, neg, gMul) = StylePromptBuilder.BuildPrompt(prompt, style);
            float? cfg = gMul == 1f ? null : model.GuidanceScale * gMul;
            Console.WriteLine($"  prompt: {full}");
            var path = Path.Combine(outDir, $"{tag}.png");
            var cb = new Progress<(int Index, byte[] Bytes)>(p =>
            {
                if (p.Index == 0) File.WriteAllBytes(path, p.Bytes);
            });
            await gen.GenerateImagesAsync(
                model, full, null, status, cb, neg,
                guidanceScaleOverride: cfg,
                batchSizeOverride: 1);
            Console.WriteLine($"  saved {path}");
        }
    }
    Console.WriteLine($"Done. {outDir}");
    return 0;
}

static string Slug(string s)
{
    var chars = s.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_').ToArray();
    return new string(chars).Trim('_');
}
