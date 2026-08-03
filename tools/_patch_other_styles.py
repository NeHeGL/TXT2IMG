from pathlib import Path

p = Path(r"D:\My Windows Files\Desktop\Code\Published\TXT2IMG+\MainWindow.xaml.cs")
t = p.read_text(encoding="utf-8")

# --- Strengthen Icon through Cartoon (non-comic) style arms ---
# Replace from Icon through Cartoon default, keep Comic as-is

# Icon
t = t.replace(
'''            "Icon" => (
                "flat vector app icon",
                "single flat app-icon glyph only, centered on a rounded-square badge, 2 flat solid colors max, huge empty margin, plain solid fill, logo-mark silhouette — not a portrait, not a scene",
                $"{AntiPhotoNegative}, portrait, beauty face close-up, fashion photo, soft shading, detailed skin, 3d render, watercolor, oil painting, claymation, comic book, landscape, scenery, glossy, embossed, illustration scene, realistic clothing",
                false),''',
'''            "Icon" => (
                "flat mobile app icon",
                "iOS/Android home-screen icon only: simple flat glyph centered in a rounded square, 2 flat solid colors, huge empty margin, plain fill — reads as an app icon asset at a glance, not a portrait illustration",
                $"{AntiPhotoNegative}, portrait painting, beauty face, detailed face, soft shading, skin pores, 3d render, watercolor, oil painting, claymation, comic book, Ben-Day, anime, landscape, scenery, glossy glass, embossed, full scene illustration, realistic clothing folds, photoreal",
                false),'''
)

# Cel - keep Wind Waker but don't steal Link costume / lose outfit
t = t.replace(
'''            "Cel-Shaded" => (
                "Wind Waker cel shading",
                "Nintendo GameCube Wind Waker look, soft rounded toon forms, toy-like proportions, thin clean outlines, flat cheerful colors, soft-edged shadow bands only, bright sunny palette — cute GameCube cartoon, not comic ink",
                $"{AntiPhotoNegative}, comic book, comic ink, tipple, Ben-Day, crosshatching, deep blacks, high contrast pulp, Borderlands, gritty, PBR gloss, realistic proportions, watercolor, oil painting, claymation fingerprints, pixel art",
                false),'''.replace("tipple", "halftone"),
'''            "Cel-Shaded" => (
                "Wind Waker cel shading",
                "Nintendo Wind Waker GameCube cel look on the described subject, soft rounded toon forms, toy-like proportions, thin clean outlines, flat cheerful colors, soft shadow bands, bright sunny palette — keep the described outfit and prop, not a different character costume",
                $"{AntiPhotoNegative}, comic book, comic ink, tipple, Ben-Day, crosshatching, deep blacks, high contrast pulp, Borderlands, gritty, PBR gloss, realistic proportions, watercolor, oil painting, claymation fingerprints, pixel art, Link tunic, elf ears unless asked, Master Sword, Hyrule",
                false),'''.replace("tipple", "halftone")
)

# Watercolor - kill photo leaks
t = t.replace(
'''            "Watercolor" => (
                "watercolor painting on paper",
                "wet translucent watercolor washes, pigment blooms and bleeds, visible cold-press paper tooth, soft edges, delicate brush — clearly watercolor on paper, not a photo",
                $"{AntiPhotoNegative}, 3d render, vector art, flat digital colors, thick oil impasto, comic ink, Ben-Day, claymation, high contrast, deep blacks, neon glow, photoreal, DSLR",
                false),''',
'''            "Watercolor" => (
                "watercolor painting on paper",
                "wet translucent watercolor washes on cold-press paper, pigment blooms and bleeds, visible paper tooth, soft watery edges, delicate brush — unmistakably watercolor, never a photograph",
                $"{AntiPhotoNegative}, 3d render, vector art, flat digital colors, thick oil impasto, comic ink, Ben-Day, claymation, high contrast, deep blacks, neon glow, photoreal, DSLR, studio photo, real skin pores, beauty campaign",
                false),'''
)

# Impasto - keep the person as thick paint
t = t.replace(
'''            "Impasto" => (
                "thick impasto oil portrait",
                "heavy sculpted paint ridges on the figure, palette-knife strokes, chunky visible paint loads, tactile painted person — thick paint depicting the subject, not abstract paint blobs alone",
                $"{AntiPhotoNegative}, smooth digital, airbrushed, flat vector, comic ink, claymation, watercolor wash, sharp photo, 3d render, abstract paint still life without person",
                false),''',
'''            "Impasto" => (
                "thick impasto oil portrait of the subject",
                "heavy sculpted paint ridges forming the described person and prop, palette-knife strokes, chunky visible paint loads — a thick-paint portrait of the scene, not a close-up of paint texture alone",
                $"{AntiPhotoNegative}, smooth digital, airbrushed, flat vector, comic ink, claymation, watercolor wash, sharp photo, 3d render, abstract paint still life, paint blob only, palette close-up without figure",
                false),'''
)

# Pixel - no photo leaks
t = t.replace(
'''            "Pixel Art" => (
                "retro pixel art sprite",
                "chunky 8-bit pixels on every surface, limited NES palette, hard pixel edges, sprite-sheet clarity, no anti-aliasing — entire frame is pixel art",
                $"{AntiPhotoNegative}, smooth, anti-aliased, blurry, 3d render, watercolor, oil painting, claymation, comic tipple, photoreal detail, soft gradients, DSLR",
                false),'''.replace("tipple", "halftone"),
'''            "Pixel Art" => (
                "retro pixel art sprite",
                "chunky 8-bit pixels covering every surface edge to edge, limited NES palette, hard pixel edges, sprite clarity, zero anti-aliasing — if it is not pixelated it is wrong",
                $"{AntiPhotoNegative}, smooth, anti-aliased, blurry, 3d render, watercolor, oil painting, claymation, comic tipple, photoreal detail, soft gradients, DSLR, studio photo, real skin, beauty campaign",
                false),'''.replace("tipple", "halftone")
)

# Fantasy
t = t.replace(
'''            "Fantasy Art" => (
                "fantasy concept painting",
                "epic fantasy illustration, painterly digital concept art, magical atmosphere, dramatic illustrated light, storybook fantasy painting — not a photo",
                $"{AntiPhotoNegative}, 3d render, vector flat, comic tipple, cartoon outlines, anime eyes, claymation, pixel art, photograph, modern interior photo, product photo",
                false),'''.replace("tipple", "halftone"),
'''            "Fantasy Art" => (
                "fantasy concept painting",
                "epic fantasy concept-art painting, painterly brushwork, magical atmosphere, dramatic illustrated light, storybook fantasy — clearly painted fantasy art, not a photo of a room or product",
                $"{AntiPhotoNegative}, 3d render, vector flat, comic tipple, cartoon outlines, anime eyes, claymation, pixel art, photograph, modern interior photo, product photo, DSLR",
                false),'''.replace("tipple", "halftone")
)

# Cartoon
t = t.replace(
'''            _ => (
                "animated TV cartoon",
                "2D Saturday-morning TV cartoon still, bold clean cartoon outlines, flat bright cel colors, simple cartoon shapes, painted cartoon BG, hand-drawn animation on television — smooth cartoon color, NO Ben-Day dots, NO comic print",
                $"{AntiPhotoNegative}, 3d render, pixar, soft painterly shading, oil paint, watercolor, claymation, comic book, comic print, tipple dots, ben-day dots, screentone, manga, anime eyes, photoreal, pulp comic inking, real skin, product photography",
                false), // Cartoon
        };'''.replace("tipple", "halftone"),
'''            _ => (
                "animated TV cartoon",
                "classic Saturday-morning TV cartoon animation still, bold clean outlines, flat bright cel colors, simple cartoon shapes, painted cartoon background, hand-drawn TV animation — smooth cartoon color fills, NO Ben-Day dots, NO printed comic look",
                $"{AntiPhotoNegative}, 3d render, pixar, soft painterly shading, oil paint, watercolor, claymation, comic book, comic print, tipple dots, ben-day dots, screentone, manga, anime eyes, photoreal, pulp comic inking, real skin, product photography, newsprint",
                false), // Cartoon
        };'''.replace("tipple", "halftone")
)

# --- Prompt assembly: scene-first for Icon + Impasto + Pixel (subject/style fights) ---
old_asm = '''            if (style == "Comic Book")
            {
                // Scene FIRST — style-first always collapsed to Lichtenstein face crops.
                fullPrompt =
                    $"Single comic illustration (one frame only, no multi-panel page): {subjectPhrase}.{propEcho} {sceneLock} " +
                    $"Casual modern clothing as described — not a superhero costume. Plain solid background as described — no city, no speech bubbles, no text, no letters. " +
                    $"Human tan olive skin (clothing may be neon green; skin is not green). " +
                    $"Printed as {medium}: {styleSuffix}. " +
                    $"Obvious cyan-magenta-yellow Ben-Day print dots across the image, India ink outlines, newsprint — one scene, no word balloons, no jumbled text.";
            }
            else
            {
                fullPrompt =
                    $"{medium}. {styleSuffix}. " +
                    $"SCENE (required): {subjectPhrase}.{propEcho} {sceneLock} " +
                    $"Render that full scene as {medium} with the same look. {contentLock} " +
                    $"Mandatory: {medium} throughout — not a photograph, not face-only, not product photography.";
            }'''

new_asm = '''            if (style == "Comic Book")
            {
                // Scene FIRST — style-first always collapsed to Lichtenstein face crops.
                fullPrompt =
                    $"Single comic illustration (one frame only, no multi-panel page): {subjectPhrase}.{propEcho} {sceneLock} " +
                    $"Casual modern clothing as described — not a superhero costume. Plain solid background as described — no city, no speech bubbles, no text, no letters. " +
                    $"Human tan olive skin (clothing may be neon green; skin is not green). " +
                    $"Printed as {medium}: {styleSuffix}. " +
                    $"Obvious cyan-magenta-yellow Ben-Day print dots across the image, India ink outlines, newsprint — one scene, no word balloons, no jumbled text.";
            }
            else if (style == "Icon")
            {
                fullPrompt =
                    $"{medium} of the subject: {subjectPhrase}.{propEcho} {sceneLock} " +
                    $"{styleSuffix}. Flat app-icon glyph only — rounded-square badge, not a painted portrait, not a photo.";
            }
            else if (style == "Impasto")
            {
                fullPrompt =
                    $"Thick impasto painting of this scene: {subjectPhrase}.{propEcho} {sceneLock} " +
                    $"{styleSuffix}. The figure and prop must be visible as sculpted paint — not abstract paint texture alone.";
            }
            else if (style == "Pixel Art")
            {
                fullPrompt =
                    $"{medium} of: {subjectPhrase}.{propEcho} {sceneLock} " +
                    $"{styleSuffix}. Every pixel of the frame is chunky 8-bit — no smooth photoreal patches.";
            }
            else if (style == "Cel-Shaded")
            {
                fullPrompt =
                    $"Wind Waker cel-shaded version of: {subjectPhrase}.{propEcho} {sceneLock} " +
                    $"{styleSuffix}. Keep the described outfit and prop — do not replace with Link or another character.";
            }
            else
            {
                fullPrompt =
                    $"{medium}. {styleSuffix}. " +
                    $"SCENE (required): {subjectPhrase}.{propEcho} {sceneLock} " +
                    $"Render that full scene as {medium} with the same look. {contentLock} " +
                    $"Mandatory: {medium} throughout — not a photograph, not face-only, not product photography.";
            }'''

if old_asm not in t:
    raise SystemExit("assembly block not found")
t = t.replace(old_asm, new_asm, 1)

p.write_text(t, encoding="utf-8")
print("styles hardened")
