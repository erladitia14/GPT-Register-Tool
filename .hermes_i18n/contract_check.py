"""Contract checker: every '<phrase>' in <reason/error/text> matcher must have a producer.

Scans sms_tool/ + services/ for keyword-match sites, then verifies the exact
phrase also appears somewhere as produced text (f-string / plain string),
excluding the matcher line itself.
"""
import re, glob, os, collections

ROOT = r'C:\Users\erlan\Documents\AI\GPT-Register-Tool'
matcher_re = re.compile(r'"((?:[^"\\]|\\.)*?)"\s+(?:not\s+)?in\s+(?:reason|error|text|str\(|output|message|content)', re.M)

sites = []
for f in glob.glob(os.path.join(ROOT, '**', '*.py'), recursive=True):
    if any(seg in f for seg in ('\\obj\\', '\\bin\\', '\\dist\\', '\\.git\\', '.hermes_i18n', '__pycache__')):
        continue
    src = open(f, encoding='utf-8', errors='ignore').read()
    for m in matcher_re.finditer(src):
        phrase = m.group(1)
        if len(phrase) < 3:
            continue
        sites.append((os.path.relpath(f, ROOT), src[:m.start()].count('\n') + 1, phrase))

print(f'matcher sites: {len(sites)}')
# build corpus of all file contents
corpus = {}
for f in glob.glob(os.path.join(ROOT, '**', '*.py'), recursive=True):
    if any(seg in f for seg in ('\\obj\\', '\\bin\\', '\\dist\\', '\\.git\\', '.hermes_i18n', '__pycache__')):
        continue
    corpus[os.path.relpath(f, ROOT)] = open(f, encoding='utf-8', errors='ignore').read()

broken = []
for rel, line_no, phrase in sites:
    # count occurrences of the phrase across corpus
    count = sum(c.count(phrase) for c in corpus.values())
    # matcher itself accounts for >=1 occurrence; need a producer elsewhere
    if count <= 1:
        broken.append((rel, line_no, phrase))
    else:
        # check if all occurrences are matchers (rare); simple heuristic: count `in`-matcher occurrences
        matcher_count = 0
        for c in corpus.values():
            matcher_count += len(re.findall(re.escape(f'"{phrase}"') + r'\s+(?:not\s+)?in\s+', c))
        if matcher_count >= count:
            broken.append((rel, line_no, phrase + '  [only matchers]'))

print(f'broken contracts: {len(broken)}')
for rel, ln, ph in broken:
    print(f'  {rel}:{ln}: {ph[:100]!r}')
