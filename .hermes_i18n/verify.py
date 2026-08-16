"""Step 4: Verify - count remaining Chinese strings after apply + check logic-key consistency."""
import re, glob, os, json

ROOT = r'C:\Users\erlan\Documents\AI\GPT-Register-Tool'
I18N = os.path.join(ROOT, '.hermes_i18n')
zh = re.compile(r'[\u4e00-\u9fff]')

trans = {}
tp = os.path.join(I18N, 'translations.json')
if os.path.exists(tp):
    trans = json.load(open(tp, encoding='utf-8'))

remaining_files = {}
for ext in ('*.xaml', '*.cs', '*.py'):
    for f in glob.glob(os.path.join(ROOT, '**', ext), recursive=True):
        if any(seg in f for seg in ('\\obj\\', '\\bin\\', '\\dist\\', '\\.git\\', '.hermes_i18n')):
            continue
        try:
            src = open(f, encoding='utf-8', errors='ignore').read()
        except Exception:
            continue
        hits = zh.findall(src)
        if hits:
            remaining_files[os.path.relpath(f, ROOT)] = len(hits)

total_remaining = sum(remaining_files.values())
print(f'files with remaining chinese: {len(remaining_files)}, total chars: {total_remaining}')
for f, n in sorted(remaining_files.items(), key=lambda x: -x[1])[:15]:
    print(f'  {f}: {n}')

# logic-key consistency: strings used in == comparisons must have SAME translation everywhere
identity = [k for k, v in trans.items() if k == v]
print(f'\nidentity mappings (untranslated): {len(identity)}')
for s in identity[:20]:
    print('  ', repr(s[:80]))
