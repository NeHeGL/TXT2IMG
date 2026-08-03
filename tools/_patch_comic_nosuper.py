from pathlib import Path

p = Path(r"D:\My Windows Files\Desktop\Code\Published\TXT2IMG+\MainWindow.xaml.cs")
t = p.read_text(encoding="utf-8")

old_neg = "yellow orange radial background, Lichtenstein\","
new_neg = "yellow orange radial background, Lichtenstein, superhero costume, spandex, cape, city skyline, speech bubble, sound effect text, action pose fighting, gibberish text bubble\","
if old_neg not in t:
    raise SystemExit("neg not found")
t = t.replace(old_neg, new_neg, 1)

old_prompt = '''                fullPrompt =
                    $"Full comic panel scene (required): {subjectPhrase}.{propEcho} {sceneLock} " +
                    $"Drawn as {medium}: {styleSuffix}. Visible Ben-Day dots in the shadows. " +
                    $"Show the full described subject and setting as a comic panel with Ben-Day dots and India ink — not a face-only pop-art crop.";'''

new_prompt = '''                fullPrompt =
                    $"Full comic panel scene (required): {subjectPhrase}.{propEcho} {sceneLock} " +
                    $"Casual modern clothing as described — not a superhero costume. Plain background color as described — no city skyline, no speech bubbles. " +
                    $"Drawn as {medium}: {styleSuffix}. Visible Ben-Day dots in the shadows. " +
                    $"Show the full described subject and setting as a comic panel with Ben-Day dots and India ink — not a face-only crop, not a superhero cover.";'''

if old_prompt not in t:
    raise SystemExit("prompt not found")
t = t.replace(old_prompt, new_prompt, 1)

p.write_text(t, encoding="utf-8")
print("ok")
