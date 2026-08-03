from pathlib import Path
import re

p = Path(r"D:\My Windows Files\Desktop\Code\Published\TXT2IMG+\MainWindow.xaml.cs")
t = p.read_text(encoding="utf-8")

# Replace comic long block
m = re.search(r'            if \(style == "Comic Book"\)\s*\{.*?\n            \}', t, re.S)
if not m:
    raise SystemExit("comic block missing")

new_comic = r'''            if (style == "Comic Book")
            {
                // Short: exact original TXT2IMG. Long: style tokens + compact scene (CFG ~5.8 —
                // Hard 7 kept the look but blew lavender/prop and glitched a frame).
                if (longOrPhotoBrief)
                {
                    var comicSubject = CompactSubjectForGraphicStyle(subjectPhrase);
                    fullPrompt =
                        $"{styleSuffix}, {comicSubject}, " +
                        $"neon green tracksuit, holding purple-gold makeup tube, solid light-purple background, " +
                        $"{styleSuffix}, tipple dots, bold black ink, not a photograph";
                    styleNegative =
                        $"{AntiPhotoNegative}, fashion photography, beauty campaign, real skin pores, DSLR, " +
                        $"speech bubble, word balloon, gibberish text, letters, writing, multi-panel page, " +
                        $"starburst background, radial sunburst";
                    guidance = ClassicFlatStyleGuidance + 1.8f; // ~5.8
                }
                else
                {
                    fullPrompt = $"{subjectPhrase}, {styleSuffix}";
                }
            }'''.replace("tipple", "halftone")

t = t[:m.start()] + new_comic + t[m.end():]

# Watercolor: don't hardcode woman/tracksuit — use compact subject
t = t.replace(
'''            else if (style == "Watercolor")
            {
                // "lavender background" otherwise becomes lavender flowers (live failure).
                fullPrompt =
                    $"Watercolor painting of the described person and scene: {subjectPhrase}.{propEcho} {sceneLock} " +
                    $"{styleSuffix}. Paint the woman in the tracksuit holding the makeup tube — not flowers, not a botanical still life.";
            }''',
'''            else if (style == "Watercolor")
            {
                // "lavender background" otherwise becomes lavender flowers (live failure).
                var wcSubject = longOrPhotoBrief ? CompactSubjectForGraphicStyle(subjectPhrase) : subjectPhrase;
                fullPrompt =
                    $"watercolor painting of {wcSubject}, {styleSuffix}, " +
                    $"soft pigment washes on paper — not flowers, not a botanical still life, not a photo";
            }'''
)

# Cartoon / Anime / Pixel: long-prompt style-lead like comic (keep short path via else branch)
# Replace the generic else and add long handling before it for Cartoon by expanding else if chain

# Find Pixel Art / Cel / Watercolor / else - add Cartoon and Anime long paths before else
# Currently Cartoon falls in else. Add explicit Cartoon + Anime before else.

old_else = '''            else
            {
                fullPrompt =
                    $"{medium}. {styleSuffix}. " +
                    $"SCENE (required): {subjectPhrase}.{propEcho} {sceneLock} " +
                    $"Render that full scene as {medium} with the same look. {contentLock} " +
                    $"Mandatory: {medium} throughout — not a photograph, not face-only, not product photography.";
            }'''

new_else = '''            else if (style == "Cartoon" || style == "Anime" || style == "Pixel Art")
            {
                // Same disease as Comic: long beauty briefs wash out the short-prompt look.
                if (longOrPhotoBrief)
                {
                    var gSubject = CompactSubjectForGraphicStyle(subjectPhrase);
                    fullPrompt =
                        $"{styleSuffix}, {gSubject}, {styleSuffix}, not a photograph";
                    styleNegative = $"{AntiPhotoNegative}, {styleNegative}";
                    guidance = Math.Max(guidance, HardStyleGuidance - 0.5f);
                }
                else
                {
                    fullPrompt = $"{subjectPhrase}, {styleSuffix}";
                }
            }
            else
            {
                fullPrompt =
                    $"{medium}. {styleSuffix}. " +
                    $"SCENE (required): {subjectPhrase}.{propEcho} {sceneLock} " +
                    $"Render that full scene as {medium} with the same look. {contentLock} " +
                    $"Mandatory: {medium} throughout — not a photograph, not face-only, not product photography.";
            }'''

if old_else not in t:
    raise SystemExit("else block missing")
t = t.replace(old_else, new_else, 1)

# Strengthen compact subject: always include neon green if present, force makeup wording
old_compact_outfit = '''        var outfit = Regex.Match(subject, @"\\bneon green tracksuit\\b[^,]*|\\btracksuit outfit\\b[^,]*|\\btracksuit\\b",
            RegexOptions.IgnoreCase);
        if (outfit.Success) bits.Add(outfit.Value.Trim());'''

# already fine - improve makeup and bg
t = t.replace(
'''        if (Regex.IsMatch(subject, @"light-purple background|lavender|lilac", RegexOptions.IgnoreCase))
            bits.Add("solid light-purple background");''',
'''        if (Regex.IsMatch(subject, @"light-purple background|lavender|lilac", RegexOptions.IgnoreCase))
            bits.Add("plain solid light-purple background wall");'''
)

p.write_text(t, encoding="utf-8")
print("patched")
