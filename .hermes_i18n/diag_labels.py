"""Map every 'final URL' label producer vs _RESULT_URL_RE alternatives."""
import re, glob, os

ROOT = r'C:\Users\erlan\Documents\AI\GPT-Register-Tool'
mgr = open(os.path.join(ROOT, 'sms_tool', 'payment_link_manager.py'), encoding='utf-8').read()
i = mgr.find('_RESULT_URL_RE = re.compile(')
print('REGEX:')
print(mgr[i:i+300])
print()

# find prints containing 'akhir' or 'URL' label patterns in extractor services
pats = ['akhir', 'pindai', 'otorisasi', 'pengalihan', 'redirect', 'halaman pembayaran']
for f in glob.glob(os.path.join(ROOT, 'services', '**', '*.py'), recursive=True):
    src = open(f, encoding='utf-8', errors='ignore').read()
    rel = os.path.relpath(f, ROOT)
    for m in re.finditer(r'print\([^)]*\)', src):
        t = m.group(0)
        if any(p in t for p in pats) and ('http' not in t):
            print(f'{rel}: {t[:130]}')
