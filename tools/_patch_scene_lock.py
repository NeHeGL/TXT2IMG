from pathlib import Path
import re

p = Path(r"D:\My Windows Files\Desktop\Code\Published\TXT2IMG+\MainWindow.xaml.cs")
t = p.read_text(encoding="utf-8")

# --- Comic Book style: keep print dots, drop Lichtenstein face-crop bias ---
old_comic = '''            // Unique look = VISIBLE Ben-Day/screentone. Without dots it collapses to smooth digital pop art.
            "Comic Book" => (
                "vintage printed comic book panel",
                "1960s American pulp comic printing, thick India-ink outlines, heavy black spotting, Ben-Day dots and screentone covering every shadow and skin tone, CMYK newsprint, cheap comic-book paper grain, Roy Lichtenstein tipple texture, crosshatched ink — the dots must be obvious",
                $"{AntiPhotoNegative}, smooth digital gradients, soft airbrush, painterly blending, watercolor, oil painting, claymation, pixel art, wind waker, TV cartoon, saturday morning cartoon, smooth CG, pastel, anime, clean vector, no tipple, photoreal skin, green skin, alien skin",
                false),'''
old_comic = old_comic.replace("tipple", "halftone")

new_comic = '''            // Unique look = VISIBLE Ben-Day/screentone. Avoid Lichtenstein face-crop bias
            // (live: comic look returned but only women's faces, no outfit/prop/lavender BG).
            "Comic Book" => (
                "vintage printed comic book panel",
                "American pulp comic printing, thick India-ink outlines, Ben-Day dots and screentone in shadows and skin, CMYK newsprint on cheap comic paper, visible print dots — full comic panel of the described scene, not a face-only pop-art crop",
                $"{AntiPhotoNegative}, smooth digital gradients, soft airbrush, painterly blending, watercolor, oil painting, claymation, pixel art, wind waker, TV cartoon, saturday morning cartoon, smooth CG, pastel, anime, clean vector, no tipple, photoreal skin, green skin, alien skin, face only, extreme close-up face, headshot only, cropped at neck, explosion burst background, radial speed lines background, Lichtenstein blonde cry",
                false),'''
new_comic = new_comic.replace("tipple", "halftone")

if old_comic not in t:
    raise SystemExit("comic block not found")
t = t.replace(old_comic, new_comic)

# --- Prompt assembly: scene lock between style and subject ---
old_asm = '''        string fullPrompt;
        if (!hasReferencePhoto)
        {
            fullPrompt =
                $"{medium}. {styleSuffix}. " +
                $"Depict: {subjectPhrase}.{propEcho} " +
                $"Again: {medium}. {styleSuffix}. {contentLock} " +
                $"Mandatory: entire frame is {medium} — not a photograph, not photoreal, not product photography.";
        }'''

new_asm = '''        var sceneLock = BuildSceneLock(subjectPhrase);

        string fullPrompt;
        if (!hasReferencePhoto)
        {
            // Style once, then SCENE FACTS, then light style echo — so Ben-Day survives without
            // erasing outfit / prop / background (live comic face-only failure).
            fullPrompt =
                $"{medium}. {styleSuffix}. " +
                $"SCENE (required): {subjectPhrase}.{propEcho} {sceneLock} " +
                $"Render that full scene as {medium} with the same print look. {contentLock} " +
                $"Mandatory: {medium} throughout — not a photograph, not face-only, not product photography.";
        }'''

if old_asm not in t:
    raise SystemExit("assembly block not found")
t = t.replace(old_asm, new_asm)

# --- AmplifySubjectFacts + BuildSceneLock ---
old_amp = '''    // Light touch: rename weak tokens only. Heavy "human skin / cosmetics" prose pushed product photos.
    private static string AmplifySubjectFacts(string subject)
    {
        var s = Regex.Replace(subject, @"\\b(?:waterproof\\s+)?concealer\\b",
            "makeup tube",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (PromptImpliesWoman(s) && !Regex.IsMatch(s, @"\\bfemale\\b", RegexOptions.IgnoreCase))
            s = $"woman, {s}";
        if (Regex.IsMatch(s, @"olive[- ]?skinned", RegexOptions.IgnoreCase))
            s = Regex.Replace(s, @"olive[- ]?skinned", "olive-skinned", RegexOptions.IgnoreCase);
        return s;
    }'''

# Use actual file content without double-escaping for search - read from file pattern differently
amp_start = t.index("    // Light touch: rename weak tokens only.")
amp_end = t.index("    private static string? TryExtractHeldProp")
new_amp = r'''    // Keep subject facts concrete. Expand weak tokens; preserve wardrobe/background/prop.
    private static string AmplifySubjectFacts(string subject)
    {
        var s = Regex.Replace(subject, @"\b(?:waterproof\s+)?concealer\b",
            "purple-and-gold makeup concealer tube",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        s = Regex.Replace(s, @"\blavender background\b",
            "solid flat lavender purple background",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        s = Regex.Replace(s, @"\bneon green tracksuit\b",
            "neon green tracksuit outfit (jacket and pants)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (PromptImpliesWoman(s) && !Regex.IsMatch(s, @"\bfemale\b", RegexOptions.IgnoreCase))
            s = $"woman, {s}";
        return s;
    }

    // Pull outfit / background / prop into an explicit scene checklist so style text cannot
    // collapse everything to a face crop (live comic failure).
    private static string BuildSceneLock(string subject)
    {
        var bits = new List<string>();
        var bg = Regex.Match(subject, @"\b(?:against|on)\s+((?:a|an|the)\s+)?([^,.\n]*\bbackground\b)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (bg.Success)
            bits.Add($"keep the {bg.Groups[2].Value.Trim()}");
        if (Regex.IsMatch(subject, @"\b(tracksuit|outfit|wearing|jacket|dress)\b", RegexOptions.IgnoreCase))
            bits.Add("waist-up or three-quarter figure showing the outfit, not face-only");
        if (Regex.IsMatch(subject, @"\bholds?\b|\bmakeup\b|\btube\b|\bconcealer\b", RegexOptions.IgnoreCase))
            bits.Add("beauty product clearly visible in hand");
        if (Regex.IsMatch(subject, @"olive|tan olive|complexion", RegexOptions.IgnoreCase))
            bits.Add("olive/tan skin tone on the person (not green)");
        return bits.Count == 0
            ? ""
            : "Must show: " + string.Join("; ", bits) + ".";
    }

'''
t = t[:amp_start] + new_amp + t[amp_end:]

# Need List<> - check if System.Collections.Generic is imported
if "using System.Collections.Generic;" not in t:
    t = t.replace("using System;", "using System;\nusing System.Collections.Generic;")

p.write_text(t, encoding="utf-8")
print("patched ok")
