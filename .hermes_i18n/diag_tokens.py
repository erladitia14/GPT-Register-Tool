"""Why do registration.py and wallet_provider.py find 0 matches? Compare tokenize inner vs map keys."""
import io, json, os, re, tokenize

ROOT = r'C:\Users\erlan\Documents\AI\GPT-Register-Tool'
trans = json.load(open(os.path.join(ROOT, '.hermes_i18n', 'translations.json'), encoding='utf-8'))
zh = re.compile(r'[\u4e00-\u9fff]')

for rel in ('sms_tool/registration.py', 'sms_tool/wallet_provider.py'):
    src = open(os.path.join(ROOT, rel), encoding='utf-8').read()
    toks = [t for t in tokenize.generate_tokens(io.StringIO(src).readline) if t.type == tokenize.STRING and zh.search(t.string)]
    print(f'=== {rel}: {len(toks)} chinese tokens')
    for t in toks[:4]:
        body = t.string
        while body and body[0] in 'fFrRbBuU':
            body = body[1:]
        inner = body[3:-3] if body.startswith(('"""', "'''")) else body[1:-1]
        direct = inner in trans
        norm = inner.replace('\n', '\\n').replace('\t', '\\t') in trans
        # fuzzy: any map key that starts with same first 40 chars
        fuzzy = [k for k in trans if k.replace('\n', '\\n')[:40] == inner.replace('\n', '\\n')[:40]][:1]
        print(f'  direct={direct} norm={norm} fuzzy={bool(fuzzy)} first40={inner[:40]!r}')
        if fuzzy:
            print(f'    map key first60: {fuzzy[0][:60]!r}')
