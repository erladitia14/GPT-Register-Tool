"""Find corrupted C# char literals: single-quoted multi-char sequences in .cs files."""
import re, glob, os

ROOT = r'C:\Users\erlan\Documents\AI\GPT-Register-Tool'
# a C# char literal must be 1 char (or an escape like '\n'). Find 'XX+'
bad = re.compile(r"'([^'\\\n]{2,})'")
for f in glob.glob(os.path.join(ROOT, '**', '*.cs'), recursive=True):
    if any(seg in f for seg in ('\\obj\\', '\\bin\\', '\\dist\\')):
        continue
    src = open(f, encoding='utf-8', errors='ignore').read()
    for m in bad.finditer(src):
        # skip if preceded by double-quote context (inside a string) — rough check
        line_no = src[:m.start()].count('\n') + 1
        line = src.splitlines()[line_no - 1]
        print(f'{os.path.relpath(f, ROOT)}:{line_no}: {line.strip()[:130]}')
