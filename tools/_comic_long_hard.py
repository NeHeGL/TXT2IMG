from pathlib import Path
import re

p = Path(r"D:\My Windows Files\Desktop\Code\Published\TXT2IMG+\MainWindow.xaml.cs")
t = p.read_text(encoding="utf-8")
m = re.search(r'            if \(style == "Comic Book"\)\s*\{.*?\n            \}', t, re.S)
if not m:
    raise SystemExit("not found")

new = r'''            if (style == "Comic Book")
            {
                // Short: exact original TXT2IMG (looks great).
                // Long: beauty/fashion prompts overpower classic suffix unless style dominates
                // hard — compact subject, style-lead, Hard CFG, strong anti-photo (live: still
                // photoreal at ~5.5 CFG with style-lead).
                if (longOrPhotoBrief)
                {
                    var comicSubject = CompactSubjectForGraphicStyle(subjectPhrase);
                    fullPrompt =
                        $"comic book illustration, bold ink outlines, tipple dot shading, dynamic pop art coloring, comic panel art style, " +
                        $"{comicSubject}, " +
                        $"comic book illustration, bold ink outlines, tipple dot shading, dynamic pop art coloring, comic panel art style, " +
                        $"printed comic art, tipple dots, not a photograph";
                    styleNegative =
                        $"{AntiPhotoNegative}, fashion photography, beauty campaign, real skin pores, DSLR, " +
                        $"speech bubble, word balloon, gibberish text, letters, writing, multi-panel page";
                    guidance = HardStyleGuidance;
                }
                else
                {
                    fullPrompt = $"{subjectPhrase}, {styleSuffix}";
                }
            }'''.replace("tipple", "halftone")

t = t[:m.start()] + new + t[m.end():]
p.write_text(t, encoding="utf-8")
print("ok")
