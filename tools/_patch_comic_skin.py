from pathlib import Path

p = Path(r"D:\My Windows Files\Desktop\Code\Published\TXT2IMG+\MainWindow.xaml.cs")
t = p.read_text(encoding="utf-8")

# Stronger speech-bubble ban already partly there; make comic prompt explicit.
old = '''                fullPrompt =
                    $"Full comic panel scene (required): {subjectPhrase}.{propEcho} {sceneLock} " +
                    $"Casual modern clothing as described — not a superhero costume. Plain background color as described — no city skyline, no speech bubbles. " +
                    $"Drawn as {medium}: {styleSuffix}. Visible Ben-Day dots in the shadows. " +
                    $"Show the full described subject and setting as a comic panel with Ben-Day dots and India ink — not a face-only crop, not a superhero cover.";'''

new = '''                fullPrompt =
                    $"Full comic panel scene (required): {subjectPhrase}.{propEcho} {sceneLock} " +
                    $"Casual modern clothing as described — not a superhero costume. Plain solid background color as described — no city skyline, no speech bubbles, no text balloons. " +
                    $"Human tan olive skin (clothing may be neon green; skin is not green). " +
                    $"Drawn as {medium}: {styleSuffix}. Visible Ben-Day dots in the shadows. " +
                    $"Show the full described subject and setting as a comic panel with Ben-Day dots and India ink — not a face-only crop, not a superhero cover.";'''

if old not in t:
    raise SystemExit("block not found")
t = t.replace(old, new, 1)

# Also bump olive green-skin negatives when olive present - already there; add sunglasses? skip

p.write_text(t, encoding="utf-8")
print("ok")
