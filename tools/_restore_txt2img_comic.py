from pathlib import Path
import re

p = Path(r"D:\My Windows Files\Desktop\Code\Published\TXT2IMG+\MainWindow.xaml.cs")
t = p.read_text(encoding="utf-8")

start = t.index("            // Classic printed comic look")
if start < 0:
    start = t.index('            "Comic Book" => (')
    # find comment before it
    start = t.rfind("\n", 0, start) 
    # better: find unique
start = t.index('            "Comic Book" => (')
# include preceding comment block
comment_start = t.rfind("            //", 0, start)
if comment_start > 0 and start - comment_start < 400:
    start = comment_start

end = t.index('            "Cel-Shaded" => (')

new = '''            // Restored from original TXT2IMG: subject-first + classic "bold ink / tipple /
            // dynamic pop art" suffix (the look users want). Scene locks still run via
            // AmplifySubjectFacts / BuildSceneLock before assembly.
            "Comic Book" => (
                "comic book illustration",
                "bold ink outlines, tipple dot shading, dynamic pop art coloring, comic panel art style",
                "photo, photorealistic, realistic, 3d render, watercolor, oil painting, speech bubble, word balloon, gibberish text, letters, writing, multi-panel page",
                false),
'''.replace("tipple", "halftone")

t = t[:start] + new + t[end:]

# Comic assembly: match old TXT2IMG — subject first, then style suffix (not medium-first essay)
old_prompt = '''            if (style == "Comic Book")
            {
                // Scene FIRST — style-first always collapsed to Lichtenstein face crops.
                fullPrompt =
                    $"Printed comic book art of: {subjectPhrase}.{propEcho} {sceneLock} " +
                    $"{medium}. {styleSuffix}. " +
                    $"One frame only — bold ink, visible Ben-Day dots, printed on paper. No speech bubbles, no text, no multi-panel page.";
            }'''

# try current block variants
import re as _re
m = _re.search(r'            if \(style == "Comic Book"\)\s*\{.*?\n            \}', t, _re.S)
if not m:
    raise SystemExit("comic if block not found")

new_prompt = '''            if (style == "Comic Book")
            {
                // Original TXT2IMG assembly: "{subject}, {styleSuffix}" — that produced the
                // classic tipple/pop-art comic look. Keep sceneLock lightly so outfit/BG survive.
                fullPrompt =
                    $"{subjectPhrase}, {styleSuffix}, tipple dots, bold black ink{propEcho} {sceneLock}".Replace("tipple", "halftone");
            }'''

# Fix - can't use Replace in C# string like that in the generated code wrongly
new_prompt = '''            if (style == "Comic Book")
            {
                // Original TXT2IMG assembly: "{subject}, {styleSuffix}" — that produced the
                // classic tipple/pop-art comic look. Keep sceneLock lightly so outfit/BG survive.
                fullPrompt =
                    $"{subjectPhrase}, {styleSuffix}, tipple dots, bold black ink{propEcho} {sceneLock}";
            }'''.replace("tipple", "halftone")

t = t[:m.start()] + new_prompt + t[m.end():]

t, n = _re.subn(
    r'("Comic Book" => ")[^"]+(")',
    r'\1Bold ink outlines, tipple shading, pop art comic style\2'.replace("tipple", "halftone"),
    t,
    count=1,
)

p.write_text(t, encoding="utf-8")
print("ok", n)
# verify
assert "halftone dot shading, dynamic pop art coloring" in p.read_text(encoding="utf-8")
print("verified classic suffix")
