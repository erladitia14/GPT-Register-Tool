"""Step 1c: risk scan - find Chinese strings used in comparisons/dict lookups (must NOT translate)."""
import re, glob, os

root = r'C:\Users\erlan\Documents\AI\GPT-Register-Tool'
zh_str = re.compile(r'"((?:[^"\\]|\\.)*[\u4e00-\u9fff](?:[^"\\]|\\.)*)"')
risky = set()

# Python: comparisons, dict keys, 'in [..]' lists used for logic
py_risk = re.compile(r'(?:==|!=|\bget\(|\bin\s*\[|\bkey\s*=|status\s*=|kind\s*=)\s*["\'][^"\']*[\u4e00-\u9fff][^"\']*["\']')
for f in glob.glob(os.path.join(root, '**', '*.py'), recursive=True):
    if '\\obj\\' in f or '\\dist\\' in f or '.hermes_i18n' in f:
        continue
    src = open(f, encoding='utf-8', errors='ignore').read()
    for m in py_risk.finditer(src):
        line_no = src[:m.start()].count('\n') + 1
        risky.add(('PY', os.path.relpath(f, root), line_no, m.group(0)[:120]))

# C#: switch cases, == comparisons with chinese
cs_risk = re.compile(r'(?:case\s+|==\s*|!=\s*)"[^"]*[\u4e00-\u9fff][^"]*"')
for f in glob.glob(os.path.join(root, '**', '*.cs'), recursive=True):
    if '\\obj\\' in f or '\\dist\\' in f or '\\bin\\' in f:
        continue
    src = open(f, encoding='utf-8', errors='ignore').read()
    for m in cs_risk.finditer(src):
        line_no = src[:m.start()].count('\n') + 1
        risky.add(('CS', os.path.relpath(f, root), line_no, m.group(0)[:120]))

print(f'risky sites: {len(risky)}')
for r in sorted(risky)[:40]:
    print(' ', r[0], r[1], r[2], '|', r[3])
