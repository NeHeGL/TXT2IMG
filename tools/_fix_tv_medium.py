from pathlib import Path

p = Path(r"D:\My Windows Files\Desktop\Code\Published\TXT2IMG+\MainWindow.xaml.cs")
t = p.read_text(encoding="utf-8")
t2 = t.replace('"animated TV cartoon"', '"saturday-morning cartoon animation"')
t2 = t2.replace("classic Saturday-morning TV cartoon animation still", "classic Saturday-morning cartoon animation still")
t2 = t2.replace("hand-drawn TV animation", "hand-drawn animated look")
needle = "photoreal, pulp comic inking, real skin, product photography, newsprint\","
repl = "photoreal, pulp comic inking, real skin, product photography, newsprint, television set, TV screen, CRT monitor, color bars\","
if "television set, TV screen" not in t2:
    t2 = t2.replace(needle, repl, 1)
p.write_text(t2, encoding="utf-8")
print("changed", t != t2)
