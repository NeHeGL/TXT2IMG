from pathlib import Path
import re

p = Path(r"D:\My Windows Files\Desktop\Code\Published\TXT2IMG+\MainWindow.xaml.cs")
t = p.read_text(encoding="utf-8")

start = t.index("            // Printed pulp look = limited CMYK")
end = t.index('            "Cel-Shaded" => (')

new = '''            // Classic printed comic look (ink + Ben-Day) — the earlier unique look users preferred.
            // Keep scene-first assembly below so outfit/BG/prop survive; avoid overcooked
            // "silver-age newsstand / misregistered CMYK" language that changed the vibe.
            "Comic Book" => (
                "printed comic book art",
                "American comic book print illustration, bold black ink outlines, visible tipple Ben-Day dots, saturated CMYK print colors, pulp comic page inking, looks like printed on paper — not TV animation",
                $"{AntiPhotoNegative}, soft airbrush, watercolor, oil painting, claymation, pixel art, wind waker, TV cartoon animation, saturday morning cartoon, smooth CG, pastel, anime, face only, extreme close-up face, headshot only, speech bubble, word balloon, gibberish text, letters, writing, multi-panel page, comic page layout",
                false),
'''.replace("tipple", "halftone")

t = t[:start] + new + t[end:]

old_prompt = '''                fullPrompt =
                    $"Single comic illustration (one frame only, no multi-panel page): {subjectPhrase}.{propEcho} {sceneLock} " +
                    $"Casual modern clothing as described — not a superhero costume. Plain solid background as described — no city, no speech bubbles, no text, no letters. " +
                    $"Human tan olive skin (clothing may be neon green; skin is not green). " +
                    $"Printed as {medium}: {styleSuffix}. " +
                    $"Obvious cyan-magenta-yellow Ben-Day print dots across the image, India ink outlines, newsprint — one scene, blank areas only, absolutely no letters, no logos, no title text, no word balloons.";'''

new_prompt = '''                fullPrompt =
                    $"Printed comic book art of: {subjectPhrase}.{propEcho} {sceneLock} " +
                    $"{medium}. {styleSuffix}. " +
                    $"One frame only — bold ink, visible Ben-Day dots, printed on paper. No speech bubbles, no text, no multi-panel page.";'''

if old_prompt not in t:
    raise SystemExit("prompt block not found")
t = t.replace(old_prompt, new_prompt, 1)

t, n = re.subn(
    r'("Comic Book" => ")[^"]+(")',
    r'\1Forced printed comic art — ink outlines, tipple/Ben-Day dots (not TV animation)\2'.replace("tipple", "halftone"),
    t,
    count=1,
)

p.write_text(t, encoding="utf-8")
print("restored classic comic", "desc", n)
