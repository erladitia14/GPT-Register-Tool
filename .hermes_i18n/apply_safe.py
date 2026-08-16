"""Safe apply: rewrite only Python STRING tokens using tokenize positions.

For each string token containing Chinese, look up its content in the
translation map and rebuild the token preserving prefix (f/r/b/u) and
quote style, escaping as needed. Edits are applied bottom-up by position
so earlier offsets stay valid.
"""
import io, json, os, re, sys, tokenize

ROOT = r'C:\Users\erlan\Documents\AI\GPT-Register-Tool'
I18N = os.path.join(ROOT, '.hermes_i18n')
DRY = '--dry-run' in sys.argv

with open(os.path.join(I18N, 'translations.json'), encoding='utf-8') as fh:
    trans = json.load(fh)
trans = {k: v for k, v in trans.items() if v != k}
zh = re.compile(r'[\u4e00-\u9fff]')

def split_prefix(tokstr):
    i = 0
    while i < len(tokstr) and tokstr[i] in 'fFrRbBuU':
        i += 1
    return tokstr[:i], tokstr[i:]

def rebuild(prefix, body_token, translation):
    # translation text is already escaped like source (model preserved \n, \t, \" etc.)
    if body_token.startswith('"""') or body_token.startswith("'''"):
        q = body_token[:3]
        # guard: never let translation text contain the bare triple delimiter
        safe = translation.replace(q, '\\' + q)
        return prefix + q + safe + q
    elif body_token.startswith('"'):
        return prefix + '"' + translation + '"'
    else:  # single quote
        return prefix + "'" + translation + "'"

def apply_file(path):
    src = open(path, encoding='utf-8').read()
    if not zh.search(src):
        return 0
    lines = src.splitlines(keepends=True)
    edits = []  # (start_row, start_col, end_row, end_col, new_text)
    try:
        toks = list(tokenize.generate_tokens(io.StringIO(src).readline))
    except tokenize.TokenizeError as e:
        print(f'  SKIP (tokenize error): {path}: {e}')
        return 0
    n = 0
    for tok in toks:
        if tok.type != tokenize.STRING:
            continue
        tokstr = tok.string
        if not zh.search(tokstr):
            continue
        prefix, body = split_prefix(tokstr)
        # get inner content
        if body.startswith('"""') or body.startswith("'''"):
            inner = body[3:-3]
        elif body.startswith('"') or body.startswith("'"):
            inner = body[1:-1]
        else:
            continue
        # normalize escaped control for map lookup
        lookup = inner
        tgt = trans.get(lookup)
        if tgt is None:
            # try with real newlines mapped to escapes
            lookup2 = inner.replace('\n', '\\n').replace('\t', '\\t')
            tgt = trans.get(lookup2)
        if tgt is None:
            continue
        new_tok = rebuild(prefix, body, tgt)
        sr, sc = tok.start
        er, ec = tok.end
        edits.append((sr, sc, er, ec, new_tok))
        n += 1
    if not edits:
        return 0
    if DRY:
        print(f'  [dry] {os.path.relpath(path, ROOT)}: {n} tokens')
        return n
    # apply bottom-up
    for sr, sc, er, ec, new_tok in sorted(edits, reverse=True):
        start_line = lines[sr - 1]
        if sr == er:
            lines[sr - 1] = start_line[:sc] + new_tok + start_line[ec:]
        else:
            end_line = lines[er - 1]
            merged = start_line[:sc] + new_tok + end_line[ec:]
            lines[sr - 1:er] = [merged]
    open(path, 'w', encoding='utf-8', newline='').write(''.join(lines))
    print(f'  changed: {os.path.relpath(path, ROOT)}: {n} tokens')
    return n

targets = [
    'sms_tool/gen_pp_link.py',
    'sms_tool/pp_link_helpers.py',
    'sms_tool/registration.py',
    'sms_tool/wallet_provider.py',
    'services/protocol-payment/pix/pix_extract.py',
]
total = 0
for rel in targets:
    total += apply_file(os.path.join(ROOT, rel))
print(f'TOTAL tokens rewritten: {total}')
