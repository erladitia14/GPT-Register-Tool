"""Step 1: Extract all unique Chinese-containing double-quoted strings from the repo."""
import re, glob, os, json

root = r'C:\Users\erlan\Documents\AI\GPT-Register-Tool'
skip = ('\\obj\\', '\\bin\\', '\\dist\\', '\\.git\\', '.hermes_i18n')
pattern = re.compile(r'"((?:[^"\\]|\\.)*[\u4e00-\u9fff](?:[^"\\]|\\.)*)"')
# Also single-quoted python strings
pattern_py = re.compile(r"'((?:[^'\\]|\\.)*[\u4e00-\u9fff](?:[^'\\]|\\.)*)'")

unique = {}
for ext in ('*.xaml', '*.cs', '*.py'):
    for f in glob.glob(os.path.join(root, '**', ext), recursive=True):
        if any(seg in f for seg in skip):
            continue
        try:
            src = open(f, encoding='utf-8', errors='ignore').read()
        except Exception:
            continue
        pats = (pattern, pattern_py) if ext == '*.py' else (pattern,)
        for p in pats:
            for m in p.findall(src):
                # skip things that look like format specifiers only
                if len(m) > 3000:
                    continue
                unique[m] = unique.get(m, 0) + 1

out_dir = os.path.join(root, '.hermes_i18n')
os.makedirs(out_dir, exist_ok=True)
out = sorted(unique.items(), key=lambda x: (-x[1], x[0]))
with open(os.path.join(out_dir, 'strings.json'), 'w', encoding='utf-8') as fh:
    json.dump(dict(out), fh, ensure_ascii=False, indent=1)

print('unique strings:', len(unique))
print('total occurrences:', sum(unique.values()))
lens = [len(s) for s in unique]
print('avg len:', sum(lens) // len(lens), 'max:', max(lens))
# total chars to translate
print('total chars:', sum(lens))
