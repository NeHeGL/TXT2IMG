from pathlib import Path
import re

p = Path(r"D:\My Windows Files\Desktop\Code\Published\TXT2IMG+\MainWindow.xaml.cs")
t = p.read_text(encoding="utf-8")
m = re.search(r'            if \(style == "Comic Book"\)\s*\{.*?\n            \}', t, re.S)
if not m:
    raise SystemExit("not found")
new = r'''            if (style == "Comic Book")
            {
                // Short prompts: exact original TXT2IMG "{subject}, {styleSuffix}" (looks amazing).
                // Long briefs: style must LEAD or beauty/fashion wording wins photoreal (live failure
                // even after subject compact + suffix echo).
                var comicSubject = longOrPhotoBrief
                    ? CompactSubjectForGraphicStyle(subjectPhrase)
                    : subjectPhrase;
                if (longOrPhotoBrief)
                {
                    fullPrompt =
                        $"{styleSuffix}, comic book illustration of {comicSubject}, " +
                        $"bold ink outlines, tipple dot shading, dynamic pop art coloring, " +
                        $"printed comic art not a photo, {styleSuffix}";
                    styleNegative =
                        $"{AntiPhotoNegative}, {styleNegative}, fashion photography, beauty campaign, real skin pores";
                    guidance = ClassicFlatStyleGuidance + 1.5f; // ~5.5 — still below Hard 7
                }
                else
                {
                    fullPrompt = $"{comicSubject}, {styleSuffix}";
                }
            }'''.replace("tipple", "halftone")
t = t[:m.start()] + new + t[m.end():]
p.write_text(t, encoding="utf-8")
print("ok")
