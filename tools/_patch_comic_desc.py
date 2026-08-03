from pathlib import Path

p = Path(r"D:\My Windows Files\Desktop\Code\Published\TXT2IMG+\MainWindow.xaml.cs")
t = p.read_text(encoding="utf-8")
old = '            "Comic Book" => "Forced printed comic art \u2014 ink outlines, tipple/Ben-Day dots (not TV animation)",'
# file has tipple not tipple
old = '            "Comic Book" => "Forced printed comic art \u2014 ink outlines, tipple/Ben-Day dots (not TV animation)",'
# Actually read exact line
for line in t.splitlines():
    if '"Comic Book" => "Forced' in line:
        print(repr(line))
        new = '            "Comic Book" => "Forced printed comic art \u2014 India ink + obvious Ben-Day/screentone dots (not smooth digital)",'
        t = t.replace(line, new)
        break
p.write_text(t, encoding="utf-8")
print("updated")
