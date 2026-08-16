"""Show the remaining Chinese context lines for manual handling."""
import re, glob, os

ROOT = r'C:\Users\erlan\Documents\AI\GPT-Register-Tool'
zh = re.compile(r'[\u4e00-\u9fff]')
for ext in ('*.xaml', '*.cs', '*.py'):
    for f in glob.glob(os.path.join(ROOT, '**', ext), recursive=True):
        if any(seg in f for seg in ('\\obj\\', '\\bin\\', '\\dist\\', '\\.git\\', '.hermes_i18n')):
            continue
        try:
            lines = open(f, encoding='utf-8', errors='ignore').read().splitlines()
        except Exception:
            continue
        hits = [(i + 1, ln) for i, ln in enumerate(lines) if zh.search(ln)]
        if hits:
            print(f'### {os.path.relpath(f, ROOT)} ({len(hits)} lines)')
            for no, ln in hits[:12]:
                print(f'  {no}: {ln.strip()[:150]}')
            print()
