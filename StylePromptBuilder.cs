using System.Text.RegularExpressions;

namespace TXT2IMG;

// Shared style/subject prompt assembly — used by the app and headless matrix tests.
public static class StylePromptBuilder
{
    // Flat/vector styles (Cartoon, Icon, Cel-Shaded) ask the model to suppress shading/lighting
    // entirely — a much bigger jump away from its photographic training default than a painterly
    // style like Watercolor/Oil Painting, which keeps that shading. Negative-prompt wording alone
    // couldn't fix this (tried and reverted); a guidance-scale bump for just these styles forces
    // stronger adherence to the style/negative prompt without affecting the styles that already
    // work reliably.
    public const float FlatStyleGuidanceMultiplier = 1.6f;

    // Impasto: SDXL texture words help bumps, but medium-first + "palette knife" → tool macros,
    // no boat (live). Subject first; "palette-knife ridges" not the tool; hard-neg art tools.
    public const float ImpastoGuidanceMultiplier = 1.8f;

    // Live: current Impasto prompt+CFG was landing the flat hard-shadow look the user wants —
    // that recipe now belongs to Graphic Oil (swap).
    public const float GraphicPosterGuidanceMultiplier = 1.7f;

    // Photorealistic: DreamShaper leans painterly — need CFG bump + hard anti-paint negatives
    // (live Impasto lottery: sunset sailboat photos the user wants as a dedicated style).
    public const float PhotorealisticGuidanceMultiplier = 1.8f;

    // Kept for experiments; Comic currently uses FlatStyleGuidanceMultiplier again.
    public const float ComicGuidanceMultiplier = 2.0f;

    // Cartoon: same seed-drift problem — one panel flips to flat vector/icon (live: GPS-pin flag).
    public const float CartoonGuidanceMultiplier = 2.0f;

    // Subject lock for reference + style restyles. Do NOT say "person"/"face"/"gender" here —
    // that invents people when the reference is a boat/object (live Icon: boat → people).
    // Person-specific identity stays in BuildReferenceEditPrompt (Style=None) only.
    public const string ReferenceSubjectLock =
        "same subject as the reference photo, keep the same object or figure from the reference";

    public const string ReferenceSubjectNegative =
        "different subject, unrelated object, random people, invented person, face swap, wrong subject";

    // Style=None person edits only — do NOT use these on style restyles of objects.
    public const string ReferenceIdentityLock =
        "same person as the reference photo, same face, same gender, same skin tone, same hair";

    public const string ReferenceIdentityNegative =
        "different person, different face, face swap, gender change, sex change, " +
        "male to female, female to male, wrong skin tone, unrelated stranger";

    // These three need a harder medium lock on reference restyles (live failures).
    public static readonly HashSet<string> SoftPaintStyles = new(StringComparer.Ordinal)
    {
        "Watercolor", "Impasto", "Oil Painting", "Soft Oil",
    };

    // Extreme simplification — photo detail sticks hard (live Icon: photoreal portrait, not a
    // computer icon). Pixel Art has the same failure mode.
    public static readonly HashSet<string> FlatSimplifyStyles = new(StringComparer.Ordinal)
    {
        "Cubist", "Icon", "Kirigami", "Line Art", "Pixel Art",
        "Low Poly", "Minecraft", "Origami",
        "Paper Cut Collage", "Paper Quilling", "Papercut Shadow Box", "Silhouette",
        "Stacked Papercut",
        "Tribal", "Zentangle",
    };

    // Ink/graphic styles also lose to the photo on reference restyles (live Comic Book + Cartoon:
    // photoreal stranger, no ink/halftone).
    public static readonly HashSet<string> GraphicInkStyles = new(StringComparer.Ordinal)
    {
        "Anime", "Cartoon", "Cel-Shaded", "Comic Book", "Ink Wash", "Line Art",
        "Silhouette",
    };

    // Medium locks are subject-agnostic — never assume person/face.
    public static string PaintMediumLock(string style) => style switch
    {
        "Watercolor" =>
            "wet-on-wet watercolor painting of the subject, soft edges, paint bleeds into white paper, " +
            "visible paper texture, delicate brushwork, pastel tones — soft painterly watercolor throughout",
        "Impasto" =>
            "impasto painting of the subject in thick oil paint with visible brush strokes and raised paint ridges, " +
            "palette-knife ridged texture on the subject, highly textured canvas, raking light on the paint",
        "Graphic Oil" =>
            "thick textured oil painting of the subject on canvas, visible palette-knife and brush texture, " +
            "rich wet oil paint with painterly depth, clear recognizable subject, fine-art oil painting atmosphere",
        "Soft Oil" =>
            "soft atmospheric oil painting of the subject on canvas, smooth blended brushwork, " +
            "luminous painterly atmosphere — soft oil of that same subject, not a museum stranger",
        "Oil Painting" =>
            "oil painting of the subject, oil on canvas, thick impasto brushstrokes, visible canvas texture — " +
            "painterly oil of that same subject, not a museum stranger",
        _ => "",
    };

    public static string PaintMediumNegative(string style) => style switch
    {
        "Watercolor" =>
            "bold black outlines, thick ink lines, comic ink, pop art, vector flat colors, graphic poster, " +
            "high contrast blocks, collage, multi-panel, makeup palette, swatches, photoreal, DSLR, " +
            "paintbrushes in frame, art tools on the painting, physical brushes",
        "Impasto" =>
            "flat paint, smooth, airbrushed, low texture, plastic, " +
            "palette knife in frame, painting knife, paintbrush, paintbrushes, paint jar, art supplies, " +
            "macro of paint only, no subject, missing subject, split screen, diptych, photograph, DSLR",
        "Graphic Oil" =>
            "photo, photorealistic, realistic, CGI, vector art, cartoon, smooth digital art, " +
            "watercolor, airbrushed, cel shading, comic outlines, comic book, bold black outlines, " +
            "thin paint, flat digital painting, paint tube, easel, abstract paint study, color swatches, " +
            "abstract only, no subject, ray tracing, extreme close-up interior, boat interior POV, " +
            "ornate gold frame, picture frame, museum frame in image, travel poster, typography, text",
        "Soft Oil" =>
            "photograph, DSLR, ornate gold frame, oval portrait frame, museum stranger, " +
            "classical aristocrat, formal tuxedo, unrelated subject, comic outlines, flat vector, " +
            "thick impasto, heavy texture, chunky brushstrokes",
        "Oil Painting" =>
            "photograph, DSLR, ornate gold frame, oval portrait frame, museum stranger, " +
            "classical aristocrat, formal tuxedo, unrelated subject, comic outlines, flat vector, " +
            "smooth digital art, airbrushed, photo",
        _ => "",
    };

    public static string SimplifyMediumLock(string style) => style switch
    {
        "Icon" =>
            "exactly one flat vector icon of the subject, single bold silhouette centered in frame, " +
            "minimal geometric design, solid limited color palette, logo-like simplicity, one icon only",
        "Pixel Art" =>
            "the entire image is clean 16-bit RPG pixel art of the subject alone, " +
            "hard pixel edges covering the whole frame, limited color palette, " +
            "retro pixel sprite — not a photograph with small pixel stickers",
        _ => "",
    };

    public static string SimplifyMediumNegative(string style) => style switch
    {
        "Icon" =>
            "photograph, photorealistic, detailed realism, complex photo background, depth of field, " +
            "soft lighting, cinematic scene, detailed illustration, painterly, gradient shading, 3d render, " +
            "storybook, sailboat montage, neon shards, fantasy lighting, cartoon character, " +
            "two icons, dual icon, split screen, side by side, diptych, collage, multi-panel, mirrored duplicate, " +
            "circular badge, emblem frame, metallic ring, complex badge",
        "Pixel Art" =>
            "photograph, photorealistic, DSLR, high resolution, smooth gradients, anti-aliased, " +
            "cinematic photo, tiny sprite sticker, floating pixel icon, " +
            "photo with pixel art badge, painterly, 3d render, " +
            "game console, handheld console, controller, cartridge, CRT television",
        _ => "",
    };

    public static string InkMediumLock(string style) => style switch
    {
        "Comic Book" =>
            "comic book art of the subject, graphic novel illustration, bold ink outlines, " +
            "dramatic shadows, Ben-Day halftone dots, dynamic angle — not a photograph",
        "Cartoon" =>
            "cartoon illustration of the subject, bold clean outlines, bright cheerful colors, " +
            "full cartoon scene with sky and background, playful exaggerated proportions — not a photo",
        "Cel-Shaded" =>
            "cel-shaded toon render of the subject, bold black outlines, flat color regions, " +
            "hard cel shading — stylized toon, not photoreal",
        "Anime" =>
            "anime illustration of the subject, manga line art, vibrant flat colors — " +
            "Japanese animation still of that subject, not a photograph",
        _ => "",
    };

    public static string InkMediumNegative(string style) => style switch
    {
        "Comic Book" =>
            "photograph, photorealistic, DSLR, cinematic photo, " +
            "text, letters, speech bubble, dialogue balloon, caption, typography, " +
            "silhouette poster, woodblock print",
        "Cartoon" =>
            "photograph, photorealistic, DSLR, realistic, 3d render, flat vector icon, " +
            "empty white background, plain white void, isolated on white, sticker art, " +
            "abstract geometric shapes, cubist, fragmented polygons",
        "Cel-Shaded" =>
            "photograph, photorealistic, painterly, soft gradients",
        "Anime" =>
            "photograph, photorealistic, DSLR, western cartoon, cel-shaded game",
        _ => "",
    };

    // Kept for documentation / negatives. Reference Fantasy Art and Dark Fantasy use
    // BuildPrompt()'s style language — do not invent a second prompt pile per style.
    public const string FantasyMediumLock =
        "dark fantasy oil illustration of the single subject from the reference, " +
        "medieval epic atmosphere, muted earth and twilight colors, dramatic chiaroscuro — " +
        "somber painted Dark Fantasy, solo subject, not a party scene";

    public const string FantasyMediumNegative =
        "photograph, photorealistic, DSLR, lightly filtered photo, " +
        "watercolor wash, wet paper blooms, paintbrushes in frame, " +
        "cartoon outlines, comic ink, Ben-Day halftone, flat vector icon, pixel art, " +
        "neon CGI, raytraced 3d, chrome reflections, anime, cel-shaded, " +
        "multiple subjects, crowd, group, trio, couple, extra characters, " +
        "horned creature unless requested, minotaur, demon, monster, beach party, " +
        "palace crowd, epic cast, unrelated characters";


    // Reference + style: reuse the SAME style language as txt2img (BuildPrompt). Live issue:
    // Fantasy alone looks great; Fantasy + reference looked like a mix because this path used a
    // different prompt pile + "same setting as the reference" that anchored the photo.
    // Medium locks are only added for styles that lose to photo pixels without them.
    public static string BuildReferenceStylePrompt(string prompt, string style)
    {
        var edit = prompt.Trim();
        var genericRestyle = string.IsNullOrWhiteSpace(edit)
            || Regex.IsMatch(edit,
                @"\b(convert|make|turn|change|transform|restyle)\b.*\b(cartoon|comic|anime|drawing|illustration|style|icon)\b",
                RegexOptions.IgnoreCase);

        // Keep the user's full ask — stripping only "make " left broken "it a wooden toy boat" (live).
        if (!genericRestyle)
        {
            edit = Regex.Replace(edit,
                @"^(make|turn|change|render)\s+it\s+(into\s+)?((an?|the)\s+)?",
                "",
                RegexOptions.IgnoreCase).Trim();
            if (string.IsNullOrWhiteSpace(edit))
                edit = prompt.Trim();
        }

        var subjectForStyle = genericRestyle ? "the subject" : edit;
        var built = BuildPrompt(subjectForStyle, style);

        var needsPhotoEscapeMedium =
            SoftPaintStyles.Contains(style)
            || FlatSimplifyStyles.Contains(style)
            || GraphicInkStyles.Contains(style);

        // Styles that already look right from noise (Fantasy, Claymation, …): keep BuildPrompt
        // intact and only pin the reference subject. Do not rephrase the style.
        if (!needsPhotoEscapeMedium)
        {
            return
                $"{built.Prompt}, {ReferenceSubjectLock}, " +
                "not a lightly filtered photograph of the reference";
        }

        var suffixStart = built.Prompt.IndexOf(',');
        var styleSuffixText = suffixStart >= 0 ? built.Prompt[(suffixStart + 1)..].Trim() : built.Prompt;

        var mediumLead = SoftPaintStyles.Contains(style)
            ? PaintMediumLock(style) + ", "
            : FlatSimplifyStyles.Contains(style)
                ? SimplifyMediumLock(style) + ", "
                : InkMediumLock(style) + ", ";

        var subjectBit = genericRestyle
            ? ReferenceSubjectLock
            : $"{edit}, {ReferenceSubjectLock}";

        var bg = FlatSimplifyStyles.Contains(style)
            ? "simple flat icon background, "
            : "";

        return
            $"{subjectBit}, {mediumLead}{styleSuffixText}, {bg}" +
            $"clear consistent {style} look throughout, " +
            "style and subject equally important, " +
            "not a photograph, not photorealistic, not a lightly filtered photo";
    }

    // Style=None + reference: short attribute edit on the same person. Avoid race/ethnicity
    // vocabulary (live: aged white girl → East Asian adult). Face/skin words + low denoise.
    public static string BuildReferenceEditPrompt(string prompt)
    {
        var edit = prompt.Trim();
        // Drop leading "make/turn/change into" so attributes attach to the same subject.
        edit = Regex.Replace(edit,
            @"^(make|turn|change|render|age)\s+",
            "",
            RegexOptions.IgnoreCase).Trim();

        // "age … 30 years" OR "30 years older"
        var ageMatch = Regex.Match(edit,
            @"\bage\b.{0,48}?\b(\d+)\s*years?\b|\b(\d+)\s*years?\s+older\b",
            RegexOptions.IgnoreCase);
        if (ageMatch.Success)
        {
            var yearsText = ageMatch.Groups[1].Success ? ageMatch.Groups[1].Value : ageMatch.Groups[2].Value;
            if (int.TryParse(yearsText, out var years) && years > 0)
            {
                edit =
                    $"aged {years} years, older face with wrinkles, hair may be greyer, {edit}";
            }
        }
        else if (Regex.IsMatch(edit, @"\bolder\b|\bage[sd]?\b", RegexOptions.IgnoreCase))
        {
            edit = $"age progression, older face with wrinkles, {edit}";
        }

        return
            $"photograph of the same person from the reference photo, {edit}, " +
            $"{ReferenceIdentityLock}, keep the original face structure and skin tone";
    }

    public static bool PromptImpliesPerson(string prompt) =>
        Regex.IsMatch(prompt,
            @"\b(person|people|human|man|woman|men|women|girl|boy|child|children|baby|" +
            @"someone|somebody|character|portrait|selfie|face|fisherman|sailor|captain|" +
            @"pilot|driver|rider|couple|family|crowd|guy|lady|gentleman|" +
            @"wizard|witch|knight|warrior|hero|heroine|princess|king|queen)\b",
            RegexOptions.IgnoreCase);

    // When the user asked for an object (e.g. "red boat"), Cartoon/Comic often invent a
    // fisherman or crew (live Cartoon seed-1: bearded man in a tiny rowboat).
    public static bool PromptImpliesWatercraft(string prompt) =>
        Regex.IsMatch(prompt,
            @"\b(boat|ship|yacht|canoe|kayak|sailboat|vessel|dinghy|ferry|submarine|rowboat|watercraft)\b",
            RegexOptions.IgnoreCase);

    public const string ObjectOnlyNegative =
        "person, people, man, woman, human, fisherman, sailor, captain, character, face, " +
        "portrait, crew, passenger, figure aboard, someone in the boat";

    // Black & White: "red boat" keeps painting red unless color words leave the prompt (live).
    public static string StripColorWords(string prompt)
    {
        var stripped = Regex.Replace(prompt,
            @"\b(red|blue|green|yellow|orange|purple|pink|violet|indigo|cyan|magenta|teal|turquoise|" +
            @"brown|beige|maroon|crimson|scarlet|gold|golden|silver|bronze|copper|auburn|" +
            @"blonde|blond|brunette|ginger|rainbow|multicolou?r(?:ed)?|colourful|colorful|" +
            @"vibrant|neon|pastel|azure|emerald|ruby|sapphire|amber)\b",
            "",
            RegexOptions.IgnoreCase);
        stripped = Regex.Replace(stripped, @"\s{2,}", " ").Trim();
        stripped = Regex.Replace(stripped, @"^[\s,]+|[\s,]+$", "");
        return string.IsNullOrWhiteSpace(stripped) ? prompt.Trim() : stripped;
    }

    // Exact templates from StyleSelectorXL / DreamStudio (sdxl_styles.json).
    // https://github.com/ahgsql/StyleSelectorXL/blob/main/sdxl_styles.json
    // Applied as-is: {prompt} is replaced with the user's text (no subject-first rewrite).
    private static readonly Dictionary<string, (string Template, string Negative)> StyleSelectorXlTemplates =
        new(StringComparer.Ordinal)
        {
            ["Line Art"] = (
                "line art drawing {prompt} . professional, sleek, modern, minimalist, graphic, line art, vector graphics",
                "anime, photorealistic, 35mm film, deformed, glitch, blurry, noisy, off-center, deformed, cross-eyed, closed eyes, bad anatomy, ugly, disfigured, mutated, realism, realistic, impressionism, expressionism, oil, acrylic"),
            // StyleSelectorXL base + anti-multi fence (live: sometimes two figures / split composition).
            ["Low Poly"] = (
                "low-poly style {prompt} . low-poly game art, polygon mesh, jagged, blocky, wireframe edges, " +
                "centered composition, single subject, one composition",
                "noisy, sloppy, messy, grainy, highly detailed, ultra textured, photo, " +
                "split screen, diptych, side by side, two panels, collage, multi-panel, " +
                "two subjects, multiple subjects, dual figure, twin, mirrored duplicate, comparison layout"),
            ["Origami"] = (
                "origami style {prompt} . paper art, pleated paper, folded, origami art, pleats, cut and fold, centered composition",
                "noisy, sloppy, messy, grainy, highly detailed, ultra textured, photo"),
            ["Cubist"] = (
                "Cubist artwork {prompt} . Geometric shapes, abstract, innovative, revolutionary",
                "anime, photorealistic, 35mm film, deformed, glitch, low contrast, noisy"),
            // StyleSelectorXL base + anti-swarm fence (live: many copies of the subject in one panel).
            // Dropped "characters and objects / game assets" — that phrase invites crowds and grids.
            ["Minecraft"] = (
                "Minecraft style {prompt} . Blocky, pixelated, vibrant colors, single subject, " +
                "one character, centered composition, one composition",
                "smooth, realistic, detailed, photorealistic, noise, blurry, deformed, " +
                "crowd, many copies, multiple subjects, duplicate characters, clone army, " +
                "grid of characters, sprite sheet, character select screen, wallpaper pattern, " +
                "tiled repeats, herd, flock, swarm, split screen, diptych, collage, multi-panel"),
            // StyleSelectorXL base + subject fence (live "jungle": seamless tribal wallpaper, no scene).
            ["Tribal"] = (
                "Tribal style {prompt} . Indigenous, ethnic, traditional patterns, bold, natural colors, " +
                "highly detailed, clear recognizable subject, subject as main focus, not a pattern fill",
                "modern, futuristic, minimalist, pastel, " +
                "abstract pattern only, seamless pattern, wallpaper, textile print, repeating motif, " +
                "no subject, missing subject, pure ornament, decorative border only"),
            ["Zentangle"] = (
                "Zentangle {prompt} . Intricate, abstract, monochrome, patterns, meditative, highly detailed",
                "colorful, representative, simplistic, large fields of color"),
            ["Kirigami"] = (
                "Kirigami representation of {prompt} . 3D, paper folding, paper cutting, Japanese, intricate, symmetrical, precision, clean lines",
                "painting, drawing, 2D, noisy, blurry, deformed"),
            ["Paper Quilling"] = (
                "Paper quilling art of {prompt} . Intricate, delicate, curling, rolling, shaping, coiling, loops, 3D, dimensional, ornamental",
                "photo, painting, drawing, 2D, flat, deformed, noisy, blurry"),
            ["Paper Cut Collage"] = (
                "Papercut collage of {prompt} . Mixed media, textured paper, overlapping, asymmetrical, abstract, vibrant",
                "photo, 3D, realistic, drawing, painting, high detail, disfigured"),
            ["Papercut Shadow Box"] = (
                "3D papercut shadow box of {prompt} . Layered, dimensional, depth, silhouette, shadow, papercut, handmade, high contrast",
                "painting, drawing, photo, 2D, flat, high detail, blurry, noisy, disfigured"),
            ["Stacked Papercut"] = (
                "Stacked papercut art of {prompt} . 3D, layered, dimensional, depth, precision cut, stacked layers, papercut, high contrast",
                "2D, flat, noisy, blurry, painting, drawing, photo, deformed"),
            ["Silhouette"] = (
                "Silhouette style {prompt} . High contrast, minimalistic, black and white, stark, dramatic",
                "ugly, deformed, noisy, blurry, low contrast, color, realism, photorealistic"),
            // New* trials — exact StyleSelectorXL (A/B on DreamShaper Lightning).
            ["Film Noir"] = (
                "Film noir style {prompt} . Monochrome, high contrast, dramatic shadows, 1940s style, mysterious, cinematic",
                "ugly, deformed, noisy, blurry, low contrast, realism, photorealistic, vibrant, colorful"),
            ["Impressionist"] = (
                "Impressionist painting {prompt} . Loose brushwork, vibrant color, light and shadow play, captures feeling over form",
                "anime, photorealistic, 35mm film, deformed, glitch, low contrast, noisy"),
            ["Pointillism"] = (
                "Pointillism style {prompt} . Composed entirely of small, distinct dots of color, vibrant, highly detailed",
                "line drawing, smooth shading, large color fields, simplistic"),
            ["Pop Art"] = (
                "Pop Art style {prompt} . Bright colors, bold outlines, popular culture themes, ironic or kitsch",
                "ugly, deformed, noisy, blurry, low contrast, realism, photorealistic, minimalist"),
            ["Steampunk"] = (
                "Steampunk style {prompt} . Antique, mechanical, brass and copper tones, gears, intricate, detailed",
                "deformed, glitch, noisy, low contrast, anime, photorealistic"),
        };

    public static (string Prompt, string ExtraNegative, float GuidanceScaleMultiplier) BuildPrompt(string prompt, string style)
    {
        // No style suffix, no extra negative terms, no guidance bump — just the user's own
        // prompt against the model's base negative prompt and default guidance scale.
        if (style == "None")
        {
            return (prompt, "", 1f);
        }

        // StyleSelectorXL trials: exact JSON templates, {prompt} = user text, CFG unchanged.
        if (StyleSelectorXlTemplates.TryGetValue(style, out var xl))
        {
            return (xl.Template.Replace("{prompt}", prompt), xl.Negative, 1f);
        }

        // The subject goes first: earlier tokens carry more weight in the text encoder, so
        // leading with style words buries the actual subject. Photorealistic also steers the
        // negative prompt away from the model's illustrative default — SDXL (and DreamShaper's
        // fine-tuning especially) leans painterly/stylized unless told otherwise, so "photo" in
        // the prompt alone doesn't reliably win on its own.
        var (styleSuffix, extraNegative, guidanceMultiplier) = style switch
        {
            // One centered glyph — landscape often splits into dual icons (live Icon batch).
            "Icon" => (
                "exactly one flat vector icon of the subject, single bold silhouette centered in frame, " +
                "minimal geometric design, solid limited color palette, logo-like simplicity, one icon only",
                "photo, photorealistic, realistic, 3d render, watercolor, oil painting, detailed illustration, " +
                "gradient shading, cartoon character, two icons, dual icon, twin icons, split screen, " +
                "side by side, diptych, collage, multi-panel, mirrored duplicate, icon pair, app icon grid, " +
                "two boats, multiple subjects, comparison layout, circular badge, emblem frame, " +
                "metallic ring, complex badge, sci-fi logo frame, ornate medallion",
                FlatStyleGuidanceMultiplier),

            // Guide / New Comic Book recipe — keep text/selective-color negatives from live failures.
            "Comic Book" => (
                "comic book art of the subject, graphic novel illustration, bold ink outlines, dramatic shadows, " +
                "halftone dots, high saturation, dynamic angle, vivid comic colors",
                "photo, photorealistic, realistic, soft, pastel, blurry, low contrast, anime, watercolor, oil painting, " +
                "3d render, smooth digital, " +
                "text, letters, words, speech bubble, dialogue balloon, caption, watermark, " +
                "silhouette poster, woodblock print, album cover, cinematic photo, " +
                "selective color, color splash, black and white, grayscale, monochrome, desaturated, " +
                "spot color, only red color, DSLR",
                FlatStyleGuidanceMultiplier),

            // Kept out of the UI (overlaps Cartoon/Anime; live Cel: stripes vs scenery). Fallback only.
            "Cel-Shaded" => (
                "cel-shaded 3d game render of the subject, Wind Waker style toon shading, " +
                "bold outlines, flat hard-edged color regions, consistent game environment",
                "photo, realistic, painterly, watercolor, oil painting, anime, manga, abstract stripes, " +
                "empty void, high contrast red and black poster",
                FlatStyleGuidanceMultiplier),

            // Guide / New Anime recipe — masterpiece quality tags, push off western comic & photo.
            "Anime" => (
                "anime illustration of the subject, manga style, clean line art, vibrant cel shading, " +
                "studio anime still, masterpiece, best quality, absurdres",
                "worst quality, low quality, realistic, photo, photorealistic, 3d render, western comic, " +
                "bad anatomy, extra fingers, blurry, oil painting, watercolor, " +
                "Ben-Day dots, American comic ink, pulp comic, thick pop-art outlines",
                FlatStyleGuidanceMultiplier),

            // Guide / New Watercolor recipe — wet-on-wet, paper texture, pastel.
            "Watercolor" => (
                "watercolor painting of the subject, soft edges, wet-on-wet technique, paint bleeds into white paper, " +
                "visible paper texture, delicate brushwork, pastel tones",
                "photo, realistic, digital, sharp edges, high contrast, dark, heavy saturation, 3d render, " +
                "oil painting, comic, vector art, flat colors, cartoon",
                1f),

            // Subject FIRST (live medium-first → palette-knife macros, no boat). Texture after.
            // Say "palette-knife ridges" not "palette knife" alone (tool shows up in frame).
            "Impasto" => (
                "impasto painting of the subject, thick oil paint, thick textured paint, visible brush strokes, " +
                "raised paint ridges on the subject, palette-knife ridged paint on the subject, " +
                "highly textured canvas, 3D paint surface texture, dramatic raking light revealing paint thickness, " +
                "rich pigments, high detail, clear recognizable subject as the main focus",
                "flat paint, smooth, airbrushed, low texture, plastic, " +
                "photograph, DSLR, photorealistic, photo, camera, thin paint, " +
                "palette knife in frame, painting knife in frame, paintbrush, paintbrushes, brush in paint, " +
                "paint jar, paint wells, dropper, art supplies, studio flat lay, paint tube, easel, " +
                "macro of paint only, abstract paint only, no subject, missing subject, only paint, " +
                "boat interior POV, split screen, diptych, two panels, side by side, " +
                "graphic poster, vector art, cartoon, watercolor, CGI, ornate frame",
                ImpastoGuidanceMultiplier),

            // Exact recipe that Impasto was landing live (user: "that's the style I like" under Impasto).
            "Graphic Oil" => (
                "thick textured oil painting of the subject on canvas, visible palette-knife and brush texture, " +
                "rich wet oil paint with painterly depth, clear recognizable subject, fine-art oil painting atmosphere",
                "3d render, photo, photorealistic, realistic, CGI, vector art, cartoon, smooth digital art, " +
                "watercolor, airbrushed, cel shading, comic outlines, comic book, bold black outlines, " +
                "thin paint, flat digital painting, paint tube, easel, abstract paint study, color swatches, " +
                "abstract only, no subject, ray tracing, extreme close-up interior, boat interior POV, " +
                "ornate gold frame, picture frame, museum frame in image, travel poster, typography, text, letters",
                GraphicPosterGuidanceMultiplier),

            // Soft atmospheric oil — blended light, gentle scene (former Oil Painting recipe).
            "Soft Oil" => (
                "classical oil painting of the subject on canvas, clearly visible brush strokes, canvas weave texture, " +
                "rich blended brush strokes, fine art museum quality, painterly atmosphere, natural color harmony",
                "3d render, photo, vector art, flat colors, cartoon, smooth digital art, comic book, cel shading, " +
                "high contrast red and black poster, graphic noir, thick impasto, heavy chunky paint",
                1f),

            // Guide / New Oil Painting recipe — thick strokes, canvas, museum oil look.
            // Keep strong painterly brushwork (dropping it collapsed to glossy photo); only fence
            // off the palette-knife / blocky-slab look that belongs to Graphic Oil & Impasto.
            "Oil Painting" => (
                "oil painting of the subject, oil on canvas, visible oil brushstrokes, painterly brushwork, " +
                "thick expressive brush strokes, visible canvas texture, rich color palette, " +
                "museum quality, classical fine art oil portrait",
                "photo, photorealistic, realistic, DSLR, glossy render, modern, digital, smooth airbrushed, " +
                "blurry, low quality, anime, cartoon, vector art, 3d render, CGI, " +
                "palette knife, palette-knife slabs, raised paint ridges, " +
                "blocky flat color patches, hard-edged paint blocks, geometric paint chips, graphic poster",
                1.6f),

            "Claymation" => (
                "claymation of the subject, stop-motion clay animation style, sculpted clay, simple miniature clay set background, " +
                "visible fingerprints and tool marks, handcrafted texture",
                "photo, photorealistic, realistic, flat colors, vector art, watercolor, oil painting, " +
                "2d illustration, high contrast red and black poster, empty void, collage, multi-panel, " +
                "hands, fingers, clay hands, reaching hands",
                FlatStyleGuidanceMultiplier),

            // Guide / New Fantasy Art recipe — epic cinematic digital painting (Dark Fantasy stays separate).
            "Fantasy Art" => (
                "epic fantasy art of the subject, highly detailed, dramatic lighting, digital painting, " +
                "sharp focus, mythical atmosphere, cinematic fantasy illustration",
                "photo, photorealistic, blurry, low quality, cartoon, anime, comic, pixel art, " +
                "flat colors, amateur, 3d render, vector art",
                1f),

            // Dark medieval fantasy — muted Frazetta mood (was the old Fantasy Art look).
            "Dark Fantasy" => (
                "dark fantasy oil illustration of the subject, medieval epic atmosphere, muted earth and twilight colors, " +
                "dramatic chiaroscuro, Frazetta-like mood, painted fantasy book cover, somber rich tones",
                "photo, photorealistic, watercolor, cartoon, comic, vector art, pixel art, neon CGI, 3d render, anime, " +
                "cel-shaded, bright primary red, high contrast red and black, pop art, cheerful flat colors",
                FlatStyleGuidanceMultiplier),

            // Guide / New Pixel Art recipe — 16-bit RPG, clean hard pixels (keep console/hardware negatives).
            // Live: object prompts pixelate fine, but people kept the smooth anime-portrait prior
            // (only the background looked pixelated), hence the anti-smooth-illustration negatives.
            "Pixel Art" => (
                "pixel art of the subject, 16-bit RPG style, limited color palette, retro game aesthetic, " +
                "large visible square pixels, hard pixel edges, aliased pixel steps, no anti-aliasing",
                "blurry, anti-aliasing, smooth, photorealistic, 3d render, gradient, high resolution, " +
                "oil painting, watercolor, photograph, photo, realistic, " +
                "smooth anime illustration, anime portrait, smooth cel shading, clean vector lineart, " +
                "smooth skin, detailed face, soft shading, airbrushed, digital painting, " +
                "game console, handheld console, controller, cartridge, CRT television, nintendo hardware, " +
                "empty black void, multi-panel, collage",
                FlatStyleGuidanceMultiplier),

            // Clean Pixar/Blender CGI - boats were beaching on sand/studio floors (live).
            "3D Render" => (
                "Pixar-quality 3d CGI render of the subject, Blender Cycles, smooth subsurface shading, " +
                "soft natural lighting, soft shadows, clean dimensional CG materials, natural accurate colors",
                "flat colors, vector art, 2d illustration, watercolor, oil painting, sketch, cartoon outlines, " +
                "comic book, painterly brush strokes, photoreal camera photo, " +
                "neon lighting, cyan light, glowing lights, laser beams, colored gel lighting, " +
                "weird mechanical attachments, surreal props, beached, on sand, on dry ground, " +
                "studio floor, pedestal, on pavement, grounded on land",
                1f),

            // Clean sci-fi chrome — deliberately NOT cyberpunk (neon rain / dystopia is Cyberpunk).
            "Futuristic" => (
                "futuristic sci-fi depiction of the subject, sleek advanced technology, polished chrome and white metal, " +
                "carbon fiber accents, clean minimal high-tech design, soft blue-white studio lighting, " +
                "subtle holographic HUD details, cinematic sci-fi concept art, bright controlled environment",
                "cyberpunk, neon rain, rainy night city, magenta neon, cyan neon glow, dystopian alley, " +
                "gritty, dirty, noir, wheels, tires, car, automobile, road, street, hangar with cars, sports car, " +
                "landing gear wheels, truck, motorcycle, medieval, rustic, vintage, watercolor, " +
                "oil painting, impasto, comic book, pixel art, claymation, pastoral, " +
                "flat vector icon, cute cartoon",
                FlatStyleGuidanceMultiplier),

            // SDXL Art cyberpunk tip language (e.g. #7/#12) — neon night city, not clean sci-fi.
            // https://stable-diffusion-art.com/sdxl-prompts/
            "Cyberpunk" => (
                "cyberpunk art of the subject, neon-lit night city, rain-slick streets, holographic signs, " +
                "high-tech dystopian atmosphere, magenta and cyan neon glow, dramatic rim lighting, " +
                "inspired by Victor Mosquera, cinematic cyberpunk illustration, dark rainy night",
                "daytime, sunny, pastoral, rural, medieval, rustic, watercolor, soft pastel, " +
                "clean white studio, bright daylight sky, polished chrome showroom, cozy, cottage, " +
                "natural landscape, soft blue-white studio lighting, oil painting, " +
                "flat vector icon, cute cartoon, claymation",
                FlatStyleGuidanceMultiplier),

            // Color photo — live Impasto lottery hit (sunset sailboat). Distinct from Black & White / Vintage.
            "Photorealistic" => (
                "photograph of the subject, photorealistic DSLR photo, real-world photography, " +
                "natural lighting, sharp focus, realistic materials, believable depth of field, " +
                "high resolution color photograph, candid real photo",
                "painting, oil painting, impasto, watercolor, illustration, drawing, digital art, " +
                "cartoon, anime, comic, vector art, flat colors, 3d render, CGI, Pixar, claymation, " +
                "pixel art, painterly, brush strokes, palette knife, canvas texture, artwork, " +
                "graphic poster, collage, split screen, diptych, abstract",
                PhotorealisticGuidanceMultiplier),

            // Sharp modern monochrome — distinct from Vintage (no sepia/grain/yellowing).
            // Live: cropped hull close-ups — push full subject in scene.
            "Black & White" => (
                "high resolution black and white photograph of the subject, wide shot of the full subject, " +
                "entire subject visible in frame, full scene composition, entirely grayscale, " +
                "no color anywhere, sharp monochrome photo, rich grayscale tones only, " +
                "detailed B&W photography, crisp contrast, pure black and white",
                "color, colorful, full color, chromatic, tinted, sepia, yellowed, warm tone, " +
                "red, blue, green, yellow, orange, purple, pink, brown, gold, cyan, magenta, " +
                "saturated color, RGB, hue, tint, selective color, color splash, " +
                "extreme close-up, macro, cropped, partial subject, cut off, fragment, detail only, " +
                "hull only, bow only, stern only, tight crop, zoomed in too close, " +
                "cartoon, clipart, illustration, drawing, painting, anime, flat colors, vector art, " +
                "heavy film grain, light leaks, scratches, vintage, old print, digital art, painterly",
                FlatStyleGuidanceMultiplier),

            // Yellowed aged film — not still-vivid red (live Vintage: bright red remains).
            "Vintage Photo" => (
                "aged vintage photograph of the subject, faded yellowed Kodak film, muted desaturated colors, " +
                "warm sepia cast, heavy film grain, dust and scratches on emulsion, light leaks, worn old print edges, " +
                "old print, nostalgic 1970s analog photo",
                "cartoon, clipart, illustration, drawing, painting, anime, flat colors, vector art, digital, modern, " +
                "vibrant saturated red, neon, HDR, high contrast",
                1f),

            // --- InsiderLLM guide recipes (New*) — A/B vs existing styles; DreamShaper XL Lightning, no LoRAs ---
            "New Photorealistic" => (
                "photograph of the subject, shot on Canon EOS R5 85mm lens, shallow depth of field, " +
                "natural lighting, golden hour or overcast diffused light, film grain, photorealistic, " +
                "8K UHD, RAW photo, real-world materials, sharp focus",
                "worst quality, low quality, blurry, cartoon, anime, illustration, painting, " +
                "oversaturated, plastic skin, CGI, 3d render, digital art, painterly",
                PhotorealisticGuidanceMultiplier),

            "Concept Art" => (
                "concept art of the subject, digital painting, artstation, epic composition, cinematic, " +
                "dramatic lighting, highly detailed, sharp focus, polished illustration",
                "photo, realistic, blurry, low quality, simple, flat colors, amateur, sketch, " +
                "cartoon, comic book, pixel art",
                1.8f),

            // SDXL Art tip #11 "Fish" look — push hard toward colorful vector (not painterly mixed-media).
            // https://stable-diffusion-art.com/sdxl-prompts/
            "Colorful Vector" => (
                "colorful vector illustration of the subject on a solid black background, " +
                "flat vector art, adobe illustrator style, clean hard edges, bold saturated colors, " +
                "vibrant neon color palette, epic full color mascot illustration, shutterstock vector, " +
                "by Jason Benjamin",
                "photo, photorealistic, realistic, DSLR, 3d render, CGI, painterly, digital painting, " +
                "oil painting, watercolor, soft brush strokes, muted colors, pastel, grayscale, monochrome, " +
                "desaturated, soft gradients, airbrushed, white background, plain white void, " +
                "complex photographic lighting, film grain",
                2.0f),

            "Minimalist" => (
                "minimalist vector illustration of the subject, flat design, clean lines, " +
                "limited color palette of 5 colors, geometric shapes, simple composition, no gradients, " +
                "adobe illustrator style",
                "photorealistic, photo, 3d render, shadow, gradient, texture, complex, busy, noisy, " +
                "detailed, organic shapes, painterly, watercolor, oil painting",
                2.0f),

            // Ink Wash is not in StyleSelectorXL — local recipe (SD Art Chinese-painting tip).
            "Ink Wash" => (
                "traditional Chinese ink wash painting of the subject, sumi-e, flowing black ink brushstrokes, " +
                "soft ink washes, rice paper texture, elegant negative space, classical East Asian painting, " +
                "graceful calligraphic lines, muted ink tones with subtle color accents",
                "photo, photorealistic, DSLR, oil painting, thick impasto, Western watercolor blooms, " +
                "comic book, anime, vector art, neon, cyberpunk, 3d render, CGI, heavy saturation, " +
                "pop art, graffiti, photoreal skin, glossy digital art",
                1.6f),

            // Full scenic cartoon - kill abstract geometric junk (live: panel 1 cubist tower).
            // Other "New *" StyleSelectorXL trials are handled above via StyleSelectorXlTemplates.
            _ => (
                "cartoon illustration of the subject, bold clean outlines, bright cheerful colors, " +
                "playful exaggerated proportions, full coherent cartoon scene with sky and background",
                "photo, photorealistic, realistic, 3d render, flat vector icon, " +
                "empty white background, plain white void, isolated on white, sticker art, blank backdrop, " +
                "abstract geometric shapes, cubist, fragmented polygons, unrelated abstract structure, " +
                "collage of random shapes",
                FlatStyleGuidanceMultiplier), // Cartoon
        };

        // Object prompts: keep people out (live Cartoon: fisherman invented for "red boat").
        var subjectLead = prompt;
        if (style == "Black & White")
            subjectLead = StripColorWords(prompt);

        if (!PromptImpliesPerson(prompt))
        {
            subjectLead = $"{subjectLead}, single subject only, no people";
            extraNegative = string.IsNullOrWhiteSpace(extraNegative)
                ? ObjectOnlyNegative
                : $"{extraNegative}, {ObjectOnlyNegative}";
        }

        // Watercraft in 3D / photo / futuristic / cyberpunk — keep afloat (live Futuristic: boats with wheels).
        if ((style == "3D Render" || style == "Photorealistic" || style == "New Photorealistic"
                || style == "Futuristic" || style == "Cyberpunk")
            && PromptImpliesWatercraft(prompt))
        {
            subjectLead = style switch
            {
                "Futuristic" => $"{subjectLead}, a sleek sci-fi boat on calm water, chrome watercraft hull, no wheels, floating on water",
                "Cyberpunk" => $"{subjectLead}, a cyberpunk boat on neon-lit rainy water, watercraft hull, no wheels, floating on water",
                _ => $"{subjectLead}, floating on water, in the water",
            };
            var waterNeg = style is "Futuristic" or "Cyberpunk"
                ? "wheels, tires, car, automobile, road, street, hangar, sports car, beached, on dry land"
                : "beached, on sand, on dry land, studio floor, pedestal, dry dock";
            extraNegative = string.IsNullOrWhiteSpace(extraNegative)
                ? waterNeg
                : $"{extraNegative}, {waterNeg}";
        }

        // Black & White + boat: live cropped to hull fragments — full boat in a scene.
        if (style == "Black & White" && PromptImpliesWatercraft(prompt))
        {
            subjectLead =
                $"{subjectLead}, entire boat visible, full boat in the water, wide scenic shot, " +
                "whole watercraft in frame";
            var cropNeg =
                "extreme close-up, macro, cropped boat, partial boat, hull only, bow only, " +
                "stern only, cut off, zoomed in too close, boat fragment";
            extraNegative = string.IsNullOrWhiteSpace(extraNegative)
                ? cropNeg
                : $"{extraNegative}, {cropNeg}";
        }

        // Impasto + boat: medium/tool words steal the subject (live: palette knife macros).
        if (style == "Impasto" && PromptImpliesWatercraft(prompt))
        {
            subjectLead = $"{subjectLead}, a boat on the water, the boat is the main subject";
            var boatNeg =
                "palette knife in frame, painting knife, paintbrush, art supplies, " +
                "macro of paint only, no boat, missing boat";
            extraNegative = string.IsNullOrWhiteSpace(extraNegative)
                ? boatNeg
                : $"{extraNegative}, {boatNeg}";
        }

        // Equal weight: user subject and style medium both early.
        // Impasto: SUBJECT first — medium-first ate the boat (live palette-knife macros).
        if (style == "Impasto")
        {
            return (
                $"{subjectLead}, {styleSuffix}, raised paint ridges on the subject, one composition, " +
                $"clear consistent {style} look",
                extraNegative,
                guidanceMultiplier);
        }

        // Same wrap Impasto used when it was accidentally landing this look (live).
        if (style == "Graphic Oil")
        {
            return (
                $"{subjectLead}, {styleSuffix}, thick paint texture throughout, clear consistent Graphic Oil look",
                extraNegative,
                guidanceMultiplier);
        }

        if (style == "Minimalist")
        {
            return (
                $"{subjectLead}, {styleSuffix}, flat clean shapes throughout, clear consistent Minimalist look",
                extraNegative,
                guidanceMultiplier);
        }

        if (style == "Colorful Vector")
        {
            return (
                $"{subjectLead}, {styleSuffix}, vivid saturated vector colors throughout, " +
                "flat vector shapes, solid black background, clear consistent Colorful Vector look",
                extraNegative,
                guidanceMultiplier);
        }

        // Pixel Art + people: the face/skin stayed smooth while only the scenery pixelated (live).
        // Naming the subject's own pixels is what pulls the portrait prior into the medium.
        if (style == "Pixel Art")
        {
            return (
                $"{subjectLead}, {styleSuffix}, the entire image is built from chunky square pixels, " +
                "the subject itself is pixelated with visible blocky pixel steps, low resolution sprite art, " +
                "clear consistent Pixel Art look",
                extraNegative,
                guidanceMultiplier);
        }

        // Lead with monochrome; full subject in scene (live B&W: hull fragments only).
        if (style == "Black & White")
        {
            return (
                $"black and white photograph, entirely grayscale with no color, wide shot, " +
                $"{subjectLead}, {styleSuffix}, full subject in frame, pure monochrome throughout, " +
                "clear consistent Black & White look",
                extraNegative,
                guidanceMultiplier);
        }

        return (
            $"{subjectLead}, {styleSuffix}, clear consistent {style} look, subject and style equally important",
            extraNegative,
            guidanceMultiplier);
    }

    // Inference knobs for reference+style — keep in sync with MainWindow GenerateAsync.
    public static (float Strength, float CfgFloor, double StepMul, string ExtraNeg)
        GetReferenceStyleInference(string style)
    {
        if (SoftPaintStyles.Contains(style))
        {
            if (style is "Oil Painting" or "Soft Oil")
                return (0.88f, 5.0f, 1.75, PaintMediumNegative(style));
            return (0.86f, 5.5f, 1.75, PaintMediumNegative(style));
        }
        if (FlatSimplifyStyles.Contains(style))
        {
            if (style == "Pixel Art")
                return (0.97f, 8.0f, 2.0, SimplifyMediumNegative(style));
            return (0.95f, 7.0f, 2.0, SimplifyMediumNegative(style));
        }
        if (GraphicInkStyles.Contains(style))
            return (0.92f, 6.5f, 2.0, $"{InkMediumNegative(style)}, studio background, plain backdrop, different background");
        if (style is "Fantasy Art" or "Dark Fantasy")
            // High denoise so photo pixels don't dilute the txt2img Fantasy look; anti-cast
            // negatives only (prompt already matches BuildPrompt).
            return (0.95f, 6.5f, 2.0, FantasyMediumNegative);
        return (0.86f, 5.0f, 1.5, "studio background, plain backdrop, solid color background, different background, empty background");
    }
}
