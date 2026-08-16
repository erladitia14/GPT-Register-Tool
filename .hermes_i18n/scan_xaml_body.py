"""Step 1b: find Chinese text OUTSIDE quoted attributes in XAML (element body text)."""
import re, glob, os

root = r'C:\Users\erlan\Documents\AI\GPT-Register-Tool'
zh = re.compile(r'[\u4e00-\u9fff]+')
found = 0
for f in glob.glob(os.path.join(root, '**', '*.xaml'), recursive=True):
    if '\\obj\\' in f or '\\dist\\' in f or '\\bin\\' in f:
        continue
    src = open(f, encoding='utf-8', errors='ignore').read()
    stripped = re.sub(r'"[^"]*"', 'QQ', src)
    hits = list(zh.finditer(stripped))
    if hits:
        print(f)
        for m in hits[:8]:
            ctx = stripped[max(0, m.start() - 70):m.end() + 70].replace('\n', ' ').strip()
            print('    >>', ctx[:180])
        found += len(hits)
print('total outside-quote chinese runs:', found)
