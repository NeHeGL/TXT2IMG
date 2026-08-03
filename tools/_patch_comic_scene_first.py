from pathlib import Path

p = Path(r"D:\My Windows Files\Desktop\Code\Published\TXT2IMG+\MainWindow.xaml.cs")
t = p.read_text(encoding="utf-8")

# Replace Comic Book arm
start = t.index('            // Unique look = VISIBLE Ben-Day/screentone.')
end = t.index('            "Cel-Shaded" => (')
new_comic = '''            // Unique look = Ben-Day/ink, but NOT Lichtenstein face-crop. Scene must lead or
            // SDXL snaps to romance-comic close-ups (live: face-only, no tracksuit/lavender/prop).
            "Comic Book" => (
                "full-figure comic book illustration",
                "American comic ink and Ben-Day screentone print texture on the whole panel, thick black outlines, CMYK newsprint — comic print look while showing the full described scene",
                $"{AntiPhotoNegative}, smooth digital gradients, soft airbrush, painterly blending, watercolor, oil painting, claymation, pixel art, wind waker, TV cartoon, saturday morning cartoon, smooth CG, pastel, anime, clean vector, no tipple, photoreal skin, green skin, alien skin, face only, extreme close-up face, headshot only, cropped at neck, pop art portrait, romance comic cover, crying woman close-up, explosion burst background, radial speed lines background, yellow orange radial background, Lichtenstein",
                false),
'''
new_comic = new_comic.replace("tipple", "halftone")
t = t[:start] + new_comic + t[end:]

# Replace prompt assembly for non-reference to special-case Comic Book
old = '''        string fullPrompt;
        if (!hasReferencePhoto)
        {
            // Style once, then SCENE FACTS, then light style echo — so Ben-Day survives without
            // erasing outfit / prop / background (live comic face-only failure).
            fullPrompt =
                $"{medium}. {styleSuffix}. " +
                $"SCENE (required): {subjectPhrase}.{propEcho} {sceneLock} " +
                $"Render that full scene as {medium} with the same print look. {contentLock} " +
                $"Mandatory: {medium} throughout — not a photograph, not face-only, not product photography.";
        }'''

new = '''        string fullPrompt;
        if (!hasReferencePhoto)
        {
            if (style == "Comic Book")
            {
                // Scene FIRST — style-first always collapsed to Lichtenstein face crops.
                fullPrompt =
                    $"Full comic panel scene (required): {subjectPhrase}.{propEcho} {sceneLock} " +
                    $"Neon green tracksuit visible, purple-gold makeup tube in hand, solid lavender background. " +
                    $"Drawn as {medium}: {styleSuffix}. " +
                    $"Waist-up comic figure in the neon green tracksuit holding the makeup tube on lavender BG. " +
                    $"Ben-Day dots and India ink on that full scene — not a face-only pop-art crop.";
            }
            else
            {
                fullPrompt =
                    $"{medium}. {styleSuffix}. " +
                    $"SCENE (required): {subjectPhrase}.{propEcho} {sceneLock} " +
                    $"Render that full scene as {medium} with the same look. {contentLock} " +
                    $"Mandatory: {medium} throughout — not a photograph, not face-only, not product photography.";
            }
        }'''

if old not in t:
    raise SystemExit("assembly not found")
t = t.replace(old, new)

p.write_text(t, encoding="utf-8")
print("ok")
