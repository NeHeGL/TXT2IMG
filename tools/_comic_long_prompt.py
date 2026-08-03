from pathlib import Path
import re

p = Path(r"D:\My Windows Files\Desktop\Code\Published\TXT2IMG+\MainWindow.xaml.cs")
t = p.read_text(encoding="utf-8")

# Guidance: short comic = classic 4.0; long = slight bump only (not 7 — that cooks a different look)
t = t.replace(
'''            // Original TXT2IMG comic used ~4.0 CFG (2.5 × 1.6) — higher CFG cooks it into a different look.
            if (style == "Comic Book") return ClassicFlatStyleGuidance;''',
'''            // Original TXT2IMG comic used ~4.0 CFG. Long briefs need a little more style pull
            // without jumping to HardStyleGuidance (that changes the look entirely).
            if (style == "Comic Book")
                return longOrPhotoBrief ? ClassicFlatStyleGuidance + 0.8f : ClassicFlatStyleGuidance;'''
)

# Comic assembly: short = exact old; long = compact subject + repeated classic suffix
old = '''            if (style == "Comic Book")
            {
                // Exact original TXT2IMG pattern: "{prompt}, {styleSuffix}".
                fullPrompt = $"{subjectPhrase}, {styleSuffix}";
            }'''

new = '''            if (style == "Comic Book")
            {
                // Short prompts: exact original TXT2IMG "{subject}, {styleSuffix}" (looks amazing).
                // Long beauty briefs drown that suffix — compact the subject and echo the same
                // classic style tokens so the tipple/pop-art look survives.
                var comicSubject = longOrPhotoBrief
                    ? CompactSubjectForGraphicStyle(subjectPhrase)
                    : subjectPhrase;
                fullPrompt = longOrPhotoBrief
                    ? $"{comicSubject}, {styleSuffix}, {styleSuffix}"
                    : $"{comicSubject}, {styleSuffix}";
            }'''.replace("tipple", "halftone")

if old not in t:
    raise SystemExit("comic assembly not found")
t = t.replace(old, new, 1)

# Add CompactSubjectForGraphicStyle after AmplifySubjectFacts
marker = "    // Pull outfit / background / prop into an explicit scene checklist"
if "CompactSubjectForGraphicStyle" not in t:
    helper = r'''    // Long prompts bury style suffixes. Keep identity/outfit/prop/BG; drop essay wording
    // so classic comic (and similar graphic styles) still read as the short-prompt look.
    private static string CompactSubjectForGraphicStyle(string subject)
    {
        var bits = new List<string>();
        if (PromptImpliesWoman(subject)) bits.Add("woman");
        else if (PromptImpliesPerson(subject)) bits.Add("person");

        if (Regex.IsMatch(subject, @"olive|tan olive|complexion", RegexOptions.IgnoreCase))
            bits.Add("tan olive skin");
        var outfit = Regex.Match(subject, @"\bneon green tracksuit\b[^,]*|\btracksuit\b[^,]*",
            RegexOptions.IgnoreCase);
        if (outfit.Success) bits.Add(outfit.Value.Trim());
        else if (Regex.IsMatch(subject, @"\b(jacket|dress|outfit|hoodie)\b", RegexOptions.IgnoreCase))
            bits.Add("described outfit");

        if (Regex.IsMatch(subject, @"makeup|concealer|tube", RegexOptions.IgnoreCase))
            bits.Add("holding purple-gold makeup tube");
        else
        {
            var held = Regex.Match(subject, @"\bholds?\s+([^,.\n]+)", RegexOptions.IgnoreCase);
            if (held.Success) bits.Add("holding " + held.Groups[1].Value.Trim());
        }

        if (Regex.IsMatch(subject, @"light-purple background|lavender|lilac", RegexOptions.IgnoreCase))
            bits.Add("solid light-purple background");
        else
        {
            var bg = Regex.Match(subject, @"\b(?:against|on)\s+((?:a|an|the)\s+)?([^,.\n]*\bbackground\b)",
                RegexOptions.IgnoreCase);
            if (bg.Success) bits.Add(bg.Groups[2].Value.Trim());
        }

        // Fall back to a trimmed original if we extracted nothing useful.
        if (bits.Count < 2)
        {
            var words = subject.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return string.Join(' ', words.Take(Math.Min(words.Length, 16)));
        }
        return string.Join(", ", bits);
    }

'''
    if marker not in t:
        raise SystemExit("marker not found for helper")
    t = t.replace(marker, helper + marker, 1)

# Need System.Linq for Take - already has System.Linq

p.write_text(t, encoding="utf-8")
print("ok")
