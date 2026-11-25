using System.Text.RegularExpressions;

namespace TXT2IMG;

// Shared style/subject prompt assembly — used by the app and headless matrix tests.
public static class StylePromptBuilder
{
    // Flat/vector styles (Cartoon, Icon) ask the model to suppress shading/lighting
    // entirely — a much bigger jump away from its photographic training default than a painterly
    // style like Watercolor/Oil Painting, which keeps that shading. Negative-prompt wording alone
    // couldn't fix this (tried and reverted); a guidance-scale bump for just these styles forces
    // stronger adherence to the style/negative prompt without affecting the styles that already
    // work reliably.
    public const float FlatStyleGuidanceMultiplier = 1.6f;

    // Graphic Oil CFG bump — keeps the flat hard-shadow oil look vs DreamShaper's painterly default.
    public const float GraphicPosterGuidanceMultiplier = 1.7f;

    public static bool PromptImpliesPerson(string prompt) =>
        Regex.IsMatch(prompt,
            @"\b(person|people|human|man|woman|men|women|girl|boy|child|children|baby|" +
            @"someone|somebody|character|portrait|selfie|face|fisherman|sailor|captain|" +
            @"pilot|driver|rider|couple|family|crowd|guy|lady|gentleman|" +
            @"wizard|witch|knight|warrior|hero|heroine|princess|king|queen|fairy|fae|elf|nymph)\b",
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
        // leading with style words buries the actual subject.
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
            // off the palette-knife / blocky-slab look that belongs to Graphic Oil.
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

            // Classic D&D / RPG handbook painted art — not ArtStation previs (that's Concept Art).
            "Fantasy Art" => (
                "classic Dungeons and Dragons fantasy book illustration of the subject, RPG handbook painted art, " +
                "rich saturated fantasy colors, heroic fantasy scene, detailed painted fantasy illustration, " +
                "Larry Elmore style fantasy art, adventurer in a fantasy world, painted D&D rulebook artwork",
                "photo, photorealistic, blurry, low quality, cartoon, anime, comic, comic book, pixel art, " +
                "flat colors, amateur, 3d render, vector art, slice of life photo, plain snapshot, " +
                "artstation keyframe, film previsualization, production design sheet, video game concept art, " +
                "cinematic previs, glossy digital concept painting, matte painting study",
                FlatStyleGuidanceMultiplier),

            // Dark medieval fantasy — muted Frazetta mood (Cyberpunk-style scene lock: environment wins over prompt).
            "Dark Fantasy" => (
                "dark fantasy oil illustration, medieval twilight wilderness, muted earth and charcoal tones, " +
                "dramatic chiaroscuro, Frazetta-like painted fantasy book cover, somber rich oil paint throughout",
                "photo, photorealistic, DSLR, watercolor, cartoon, comic, comic book, graphic novel, anime, manga, " +
                "cel-shaded, vector art, pixel art, neon CGI, cyberpunk, 3d render, bright primary colors, " +
                "high saturation, pop art, cheerful flat colors, playful cartoon, glossy digital illustration, " +
                "artstation polish, camping snapshot, modern casual photo, slice of life",
                1.8f),

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
            // Scene + palette lock — subject can be anything; environment and medium still read cyberpunk.
            "Cyberpunk" => (
                "cyberpunk art of the subject, neon-lit night city, rain-slick streets, holographic signs, " +
                "high-tech dystopian atmosphere, magenta and cyan neon glow, dramatic rim lighting, " +
                "inspired by Victor Mosquera, cinematic cyberpunk illustration, dark rainy night",
                "daytime, sunny, pastoral, rural, medieval, rustic, watercolor, soft pastel, " +
                "clean white studio, bright daylight sky, polished chrome showroom, cozy, cottage, " +
                "natural landscape, soft blue-white studio lighting, oil painting, " +
                "flat vector icon, cute cartoon, claymation",
                FlatStyleGuidanceMultiplier),

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
            "Concept Art" => (
                "professional video game concept art keyframe, cinematic digital painting, artstation trending, " +
                "epic production-design illustration, dramatic environmental lighting, atmospheric depth, " +
                "painterly brushwork, film previsualization mood, full color key art throughout",
                "photo, photorealistic, DSLR, snapshot, blurry, low quality, amateur, rough sketch, " +
                "cartoon, comic book, graphic novel, manga, anime, cel-shaded, bold ink outlines, " +
                "halftone, Ben-Day dots, screentone, vector logo, flat vector icon, pixel art, " +
                "watercolor, oil on canvas, impasto, cheerful flat colors, cute cartoon, slice of life, " +
                "playful exaggerated proportions, graphic novel panel, pop art, camping photo, " +
                "dungeons and dragons handbook, D&D rulebook illustration, Larry Elmore, RPG book art",
                2.0f),

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

        // Watercraft in 3D / futuristic / cyberpunk — keep afloat (live Futuristic: boats with wheels).
        if ((style == "3D Render" || style == "Futuristic" || style == "Cyberpunk")
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

        // Strong scene/medium locks (same idea as Cyberpunk) — style wins over cheerful or mismatched prompts.
        if (style == "Dark Fantasy")
        {
            return (
                $"{subjectLead}, {styleSuffix}, " +
                "somber medieval fantasy environment throughout, painted dark fantasy on subject and background, " +
                "muted twilight mood, clear consistent Dark Fantasy look, not cartoon, not comic",
                extraNegative,
                guidanceMultiplier);
        }

        if (style == "Fantasy Art")
        {
            return (
                $"{subjectLead}, {styleSuffix}, " +
                "Dungeons and Dragons rulebook illustration throughout, painted heroic fantasy on subject and scene, " +
                "classic RPG handbook art mood, clear consistent Fantasy Art look, not concept art keyframe, not artstation previs",
                extraNegative,
                guidanceMultiplier);
        }

        if (style == "Concept Art")
        {
            return (
                $"{subjectLead}, {styleSuffix}, " +
                "cinematic concept keyframe throughout, polished digital painting on subject and full environment, " +
                "production-design artstation mood, film previs lighting, no comic ink halftone or cartoon outlines, " +
                "clear consistent Concept Art look, not photograph, not graphic novel, not D&D rulebook illustration",
                extraNegative,
                guidanceMultiplier);
        }

        if (style == "Comic Book")
        {
            return (
                $"{subjectLead}, {styleSuffix}, bold ink outlines and halftone dots throughout, " +
                "graphic novel comic art on subject and background, clear consistent Comic Book look, " +
                "not glossy concept painting",
                extraNegative,
                guidanceMultiplier);
        }

        return (
            $"{subjectLead}, {styleSuffix}, clear consistent {style} look, subject and style equally important",
            extraNegative,
            guidanceMultiplier);
    }
}
