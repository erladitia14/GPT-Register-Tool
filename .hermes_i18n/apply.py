"""Step 3: Apply translations back to source files.

Strategy: for every .xaml/.cs/.py file, replace each known source string
(inside double quotes for cs/xaml, inside double OR single quotes for py)
with its translation. Same source string always maps to same translation,
so logic comparisons (== "邮箱池") stay consistent between XAML body
<sys:String>全部</sys:String> and C# comparisons.

Also translates XAML element-body Chinese text (<sys:String>全部</sys:String>).
"""
import json, glob, os, re, sys

ROOT = r'C:\Users\erlan\Documents\AI\GPT-Register-Tool'
I18N = os.path.join(ROOT, '.hermes_i18n')

with open(os.path.join(I18N, 'translations.json'), encoding='utf-8') as fh:
    trans = json.load(fh)

# remove identity mappings (failed batches kept original)
trans = {k: v for k, v in trans.items() if v != k}
print(f'translations to apply: {len(trans)}')

# Sort by length desc so longer strings replace first (avoid partial overlap)
ordered = sorted(trans.items(), key=lambda kv: -len(kv[0]))

DRY = '--dry-run' in sys.argv

def escape_for_regex(s):
    return re.escape(s)

# Build one big regex with alternation for double-quoted strings
dq_map = {}
for src, tgt in ordered:
    dq_map[src] = tgt

def build_pattern(quote):
    alts = []
    idx_map = {}
    for i, (src, tgt) in enumerate(ordered):
        # pattern: quote + content + quote, content must match src exactly
        alts.append(re.escape(quote + src + quote))
        idx_map[i] = (src, tgt)
    pat = re.compile('|'.join(alts))
    return pat, idx_map

stats = {'files_changed': 0, 'replacements': 0}

def apply_to_file(path):
    try:
        src_text = open(path, encoding='utf-8').read()
    except UnicodeDecodeError:
        src_text = open(path, encoding='utf-8', errors='ignore').read()
    if not re.search(r'[\u4e00-\u9fff]', src_text):
        return
    original = src_text
    n_local = 0

    # replace quoted strings (double quotes) — longest first via ordered loop
    for src_s, tgt_s in ordered:
        dq = f'"{src_s}"'
        cnt = src_text.count(dq)
        if cnt:
            src_text = src_text.replace(dq, f'"{tgt_s}"')
            n_local += cnt
    # single quotes (python)
    if path.endswith('.py'):
        for src_s, tgt_s in ordered:
            sq = f"'{src_s}'"
            if sq in src_text:
                src_text = src_text.replace(sq, f"'{tgt_s}'")
    # XAML body text: <sys:String>中文</sys:String>
    if path.endswith('.xaml'):
        def repl_body(m):
            body = m.group(1)
            body = body.strip()
            return f'<sys:String>{trans.get(body, m.group(1))}</sys:String>'
        src_text = re.sub(r'<sys:String>([^<]*[\u4e00-\u9fff][^<]*)</sys:String>', repl_body, src_text)
        # ToolTip/Content element text like >中文<
        def repl_el(m):
            txt = m.group(1).strip()
            return '>' + trans.get(txt, txt) + '<' if txt in trans else m.group(0)
        src_text = re.sub(r'>([^<>]*[\u4e00-\u9fff][^<>]*)<', repl_el, src_text)

    if src_text != original:
        stats['files_changed'] += 1
        stats['replacements'] += sum(1 for s, _ in ordered if f'"{s}"' in original or (path.endswith('.py') and f"'{s}'" in original))
        if not DRY:
            open(path, 'w', encoding='utf-8', newline='').write(src_text)
        print(f'  changed: {os.path.relpath(path, ROOT)}')

targets = []
for ext in ('*.xaml', '*.cs', '*.py'):
    for f in glob.glob(os.path.join(ROOT, '**', ext), recursive=True):
        if any(seg in f for seg in ('\\obj\\', '\\bin\\', '\\dist\\', '\\.git\\', '.hermes_i18n')):
            continue
        targets.append(f)

for t in targets:
    apply_to_file(t)

print(f"files_changed={stats['files_changed']}")
