from pathlib import Path

p = Path(r"D:\My Windows Files\Desktop\Code\Published\TXT2IMG+\MainWindow.xaml.cs")
t = p.read_text(encoding="utf-8")

start = t.index("            // Unique look = Ben-Day/ink, but NOT Lichtenstein face-crop.")
end = t.index('            "Cel-Shaded" => (')

new = r'''            // Printed pulp look = limited CMYK / Ben-Day dots on newsprint — not smooth digital
            // "comic illustration". Scene still leads so outfit/BG/prop survive.
            "Comic Book" => (
                "vintage 4-color newsprint comic printing",
                "cheap pulp comic paper, limited CMYK process colors, cyan magenta yellow black Ben-Day dots and rosettes clearly visible in every shadow and flat, thick black India-ink outlines, misregistered print colors, classic American newsstand comic printing from the silver age — look printed, not drawn digitally",
                $"{AntiPhotoNegative}, smooth digital gradients, soft airbrush, painterly blending, watercolor, oil painting, claymation, pixel art, wind waker, TV cartoon, saturday morning cartoon, smooth CG, pastel, anime, clean vector art, no tipple, no dots, photoreal skin, green skin, alien skin, face only, extreme close-up face, headshot only, cropped at neck, pop art portrait, romance comic cover, crying woman close-up, explosion burst background, radial speed lines background, Lichtenstein face crop, superhero costume, spandex, cape, city skyline, speech bubble, word balloon, thought bubble, caption box, sound effect text, letters, writing, typography, gibberish text, action pose fighting, multi-panel page",
                false),
'''
new = new.replace("tipple", "halftone")
t = t[:start] + new + t[end:]

old_prompt = '''                fullPrompt =
                    $"Full comic panel scene (required): {subjectPhrase}.{propEcho} {sceneLock} " +
                    $"Casual modern clothing as described — not a superhero costume. Plain solid background color as described — no city skyline, no speech bubbles, no text balloons. " +
                    $"Human tan olive skin (clothing may be neon green; skin is not green). " +
                    $"Drawn as {medium}: {styleSuffix}. Visible Ben-Day dots in the shadows. " +
                    $"Show the full described subject and setting as a comic panel with Ben-Day dots and India ink — not a face-only crop, not a superhero cover.";'''

new_prompt = '''                fullPrompt =
                    $"Single comic illustration (one frame only, no multi-panel page): {subjectPhrase}.{propEcho} {sceneLock} " +
                    $"Casual modern clothing as described — not a superhero costume. Plain solid background as described — no city, no speech bubbles, no text, no letters. " +
                    $"Human tan olive skin (clothing may be neon green; skin is not green). " +
                    $"Printed as {medium}: {styleSuffix}. " +
                    $"Obvious cyan-magenta-yellow Ben-Day print dots across the image, India ink outlines, newsprint — one scene, no word balloons, no jumbled text.";'''

if old_prompt not in t:
    raise SystemExit("prompt not found")
t = t.replace(old_prompt, new_prompt, 1)

# UI description
import re
t, n = re.subn(
    r'("Comic Book" => ")[^"]+(")',
    r'\1Forced vintage newsprint comic — CMYK Ben-Day dots, India ink, no text balloons\2',
    t,
    count=1,
)

p.write_text(t, encoding="utf-8")
print("ok", "desc", n)
