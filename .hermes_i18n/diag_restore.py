import io, re, tokenize

files = [
    r'C:\Users\erlan\Documents\AI\GPT-Register-Tool\sms_tool\registration.py',
    r'C:\Users\erlan\Documents\AI\GPT-Register-Tool\sms_tool\wallet_provider.py',
    r'C:\Users\erlan\Documents\AI\GPT-Register-Tool\sms_tool\gen_pp_link.py',
]
zh = re.compile(r'[\u4e00-\u9fff]')
for f in files:
    src = open(f, encoding='utf-8', errors='ignore').read()
    hits = len(zh.findall(src))
    # compile check
    try:
        compile(src, f, 'exec')
        comp = 'OK'
    except SyntaxError as e:
        comp = f'SYNTAX ERROR line {e.lineno}: {e.msg}'
    print(f'{f.split(chr(92))[-1]}: chinese_chars={hits} compile={comp}')
    if hits:
        for i, ln in enumerate(src.splitlines(), 1):
            if zh.search(ln):
                print(f'   first hit line {i}: {ln.strip()[:100]}')
                break
