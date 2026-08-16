"""Restore internal contract strings to original Chinese, line-granular.

Contract lines = baseline lines containing either:
  (a) a Chinese string literal used as keyword matcher ('"X" in reason' style), or
  (b) a _RESULT_URL_RE label fragment (producer/matcher of result-URL labels), or
  (c) any literal containing such a phrase (the producers).

Uses difflib alignment between baseline worktree and current working tree;
overwrites current lines 1:1-aligned with qualifying baseline lines.
"""
import difflib, glob, os, re

ROOT = r'C:\Users\erlan\Documents\AI\GPT-Register-Tool'
BASE = r'C:\Users\erlan\Documents\AI\GPT-Register-Tool-baseline'
zh = re.compile(r'[\u4e00-\u9fff]')

# 1. collect contract phrases from baseline
matcher_re = re.compile(r'"((?:[^"\\]|\\.)*?)"\s+(?:not\s+)?in\s+')
phrases = set()
labels = set()

def read(p):
    return open(p, encoding='utf-8', errors='ignore').read()

for f in glob.glob(os.path.join(BASE, '**', '*.py'), recursive=True):
    if '__pycache__' in f:
        continue
    src = read(f)
    for m in matcher_re.finditer(src):
        if zh.search(m.group(1)):
            phrases.add(m.group(1))

# label fragments from _RESULT_URL_RE in baseline payment_link_manager
plm = read(os.path.join(BASE, 'sms_tool', 'payment_link_manager.py'))
m = re.search(r'_RESULT_URL_RE = re\.compile\((.*?)\)\n', plm, re.S)
if m:
    lits = re.findall(r'r?"([^"]*)"', m.group(1))
    joined = ''.join(lits)
    alt = re.search(r'\(\?:(.*?)\):', joined)
    if alt:
        for part in alt.group(1).split('|'):
            if part:
                labels.add(part)
print(f'matcher phrases: {len(phrases)}, labels: {len(labels)}')
for lb in sorted(labels):
    print('  label:', lb)

contract_keys = phrases | labels

def qualifies(line):
    return any(k in line for k in contract_keys)

# 2. walk files, align, restore
changed_files = 0
restored_lines = 0
for bf in glob.glob(os.path.join(BASE, '**', '*.py'), recursive=True):
    if '__pycache__' in bf:
        continue
    rel = os.path.relpath(bf, BASE)
    cf = os.path.join(ROOT, rel)
    if not os.path.exists(cf):
        continue
    blines = read(bf).splitlines(keepends=True)
    clines = read(cf).splitlines(keepends=True)
    qual = [i for i, ln in enumerate(blines) if qualifies(ln)]
    if not qual:
        continue
    sm = difflib.SequenceMatcher(None, blines, clines, autojunk=False)
    edits = {}
    for tag, i1, i2, j1, j2 in sm.get_opcodes():
        if tag == 'equal':
            continue
        if tag == 'replace' and (i2 - i1) == (j2 - j1):
            for k in range(i2 - i1):
                if qualifies(blines[i1 + k]) and blines[i1 + k] != clines[j1 + k]:
                    edits[j1 + k] = blines[i1 + k]
    if edits:
        for idx, newln in edits.items():
            clines[idx] = newln
        open(cf, 'w', encoding='utf-8', newline='').write(''.join(clines))
        changed_files += 1
        restored_lines += len(edits)
        print(f'  {rel}: restored {len(edits)} lines')

print(f'DONE. files={changed_files} lines={restored_lines}')
