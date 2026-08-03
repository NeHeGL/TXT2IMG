from pathlib import Path
import re

p = Path(r"D:\My Windows Files\Desktop\Code\Published\TXT2IMG+\MainWindow.xaml.cs")
t = p.read_text(encoding="utf-8")
m = re.search(r'            if \(style == "Comic Book"\)\s*\{.*?\n            \}', t, re.S)
if not m:
    raise SystemExit("missing comic block")
print("OLD:\n", m.group(0))
new = """            if (style == \"Comic Book\")
            {
                // Exact original TXT2IMG pattern: \"{prompt}, {styleSuffix}\".
                fullPrompt = $\"{subjectPhrase}, {styleSuffix}\";
            }"""
t = t[: m.start()] + new + t[m.end() :]
p.write_text(t, encoding="utf-8")
print("patched")
