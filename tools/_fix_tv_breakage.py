from pathlib import Path
import re

p = Path(r"D:\My Windows Files\Desktop\Code\Published\TXT2IMG+\MainWindow.xaml.cs")
t = p.read_text(encoding="utf-8")

# 1) Fix comic long: no hardcoded beauty wardrobe — compact subject only
m = re.search(r'            if \(style == "Comic Book"\)\s*\{.*?\n            \}', t, re.S)
if not m:
    raise SystemExit("comic missing")

new_comic = r'''            if (style == "Comic Book")
            {
                // Short: exact original TXT2IMG. Long: classic style tokens + compact subject
                // (no hardcoded beauty wardrobe — that poisoned other prompts / short batches).
                if (longOrPhotoBrief)
                {
                    var comicSubject = CompactSubjectForGraphicStyle(subjectPhrase);
                    fullPrompt =
                        $"{styleSuffix}, {comicSubject}, {styleSuffix}, " +
                        $"tipple dots, bold black ink, not a photograph";
                    styleNegative =
                        $"{AntiPhotoNegative}, fashion photography, beauty campaign, real skin pores, DSLR, " +
                        $"speech bubble, word balloon, gibberish text, letters, writing, multi-panel page";
                    guidance = ClassicFlatStyleGuidance + 1.8f; // ~5.8
                }
                else
                {
                    fullPrompt = $"{subjectPhrase}, {styleSuffix}";
                }
            }'''.replace("tipple", "halftone")
t = t[:m.start()] + new_comic + t[m.end():]

# 2) REMOVE broken Cartoon/Anime/Pixel branch — "TV cartoon" style-lead generated literal TVs.
# Pixel is already handled above; Cartoon/Anime fall through to medium+scene else.
bad = re.search(
    r'            else if \(style == "Cartoon" \|\| style == "Anime" \|\| style == "Pixel Art"\)\s*\{.*?\n            \}\n',
    t,
    re.S,
)
if not bad:
    raise SystemExit("bad cartoon branch missing")
t = t[:bad.start()] + t[bad.end():]

# 3) Cartoon + Anime get explicit safe long handling WITHOUT leading with "TV"
# Insert before the final else
old_else = '''            else
            {
                fullPrompt =
                    $"{medium}. {styleSuffix}. " +
                    $"SCENE (required): {subjectPhrase}.{propEcho} {sceneLock} " +
                    $"Render that full scene as {medium} with the same look. {contentLock} " +
                    $"Mandatory: {medium} throughout — not a photograph, not face-only, not product photography.";
            }'''

new_else = '''            else if (style == "Cartoon" || style == "Anime")
            {
                // Do NOT style-lead with "TV cartoon" — that generates literal televisions (live failure).
                var gSubject = longOrPhotoBrief ? CompactSubjectForGraphicStyle(subjectPhrase) : subjectPhrase;
                fullPrompt =
                    $"{medium} of {gSubject}. {styleSuffix}. {sceneLock} " +
                    $"Depict {gSubject} — not a photograph.";
                if (longOrPhotoBrief)
                {
                    styleNegative = $"{AntiPhotoNegative}, {styleNegative}, television set, TV screen, CRT, color bars";
                    guidance = Math.Max(guidance, HardStyleGuidance - 1.0f);
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
    raise SystemExit("else missing")
t = t.replace(old_else, new_else, 1)

# 4) Cartoon styleSuffix: soften "TV" literal — say "saturday morning animation" without TV noun spam
t = t.replace(
'''                "2D Saturday-morning TV cartoon still, bold clean cartoon outlines, flat bright cel colors, simple cartoon shapes, painted cartoon BG, hand-drawn animation on television — smooth cartoon color fills, NO Ben-Day dots, NO printed comic look",''',
'''                "2D Saturday-morning cartoon animation still, bold clean cartoon outlines, flat bright cel colors, simple cartoon shapes, painted cartoon background, hand-drawn animated look — smooth cartoon color fills, NO Ben-Day dots, NO printed comic look",'''
)
# also check other cartoon suffix variant
t = t.replace(
'''                "classic Saturday-morning TV cartoon animation still, bold clean outlines, flat bright cel colors, simple cartoon shapes, painted cartoon background, hand-drawn TV animation — smooth cartoon color fills, NO Ben-Day dots, NO printed comic look",''',
'''                "classic Saturday-morning cartoon animation still, bold clean outlines, flat bright cel colors, simple cartoon shapes, painted cartoon background, hand-drawn animated look — smooth cartoon color fills, NO Ben-Day dots, NO printed comic look",'''
)

p.write_text(t, encoding="utf-8")
print("fixed")
