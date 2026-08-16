"""Broader: find all label prints across extractors, multiline aware."""
import re, glob, os

ROOT = r'C:\Users\erlan\Documents\AI\GPT-Register-Tool'
keywords = ['akhir', 'otorisasi', 'pengalihan', 'halaman pembayaran', 'pindai']
for f in glob.glob(os.path.join(ROOT, 'services', '**', '*.py'), recursive=True):
    src = open(f, encoding='utf-8', errors='ignore').read()
    rel = os.path.relpath(f, ROOT)
    lines = src.splitlines()
    for i, ln in enumerate(lines, 1):
        if any(k in ln for k in keywords) and ('print' in ln or 'label' in ln.lower() or '=' in ln):
            print(f'{rel}:{i}: {ln.strip()[:140]}')
