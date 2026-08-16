"""Find quote-doubling corruption: patterns like = ""Text or (""Text inside .cs files."""
import re, glob, os

ROOT = r'C:\Users\erlan\Documents\AI\GPT-Register-Tool'
# suspicious: a double-quote immediately followed by a non-quote char inside an assignment
bad = re.compile(r'""[^",)\]\s;]')
for f in glob.glob(os.path.join(ROOT, '**', '*.cs'), recursive=True):
    if any(seg in f for seg in ('\\obj\\', '\\bin\\', '\\dist\\')):
        continue
    src = open(f, encoding='utf-8', errors='ignore').read()
    for m in bad.finditer(src):
        line_no = src[:m.start()].count('\n') + 1
        print(f'{os.path.relpath(f, ROOT)}:{line_no}: {src.splitlines()[line_no-1].strip()[:130]}')
