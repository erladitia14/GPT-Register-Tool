"""Step 2b: Retry identity-mapped (failed) strings ONE AT A TIME via 9Router.

After translate.py finishes, any string whose translation == original was a
failed batch fallback. This script re-requests them individually, which is
far more reliable than batched calls.
"""
import json, os, re, time, urllib.request

ROOT = r'C:\Users\erlan\Documents\AI\GPT-Register-Tool'
I18N = os.path.join(ROOT, '.hermes_i18n')
API_URL = 'http://localhost:20128/v1/chat/completions'
API_KEY = open(r'C:\Users\erlan\Documents\AI\ApiCC.txt', encoding='utf-8').read().strip()
MODEL = 'kr/deepseek-3.2'

PROMPT = (
    "Translate the following Chinese text to natural Indonesian (Bahasa Indonesia) "
    "for a software app. Rules: preserve placeholders like {0}, {1}, %s, %d, \\n, \\t exactly; "
    "keep code identifiers, paths, URLs unchanged. Reply with ONLY the translated text, "
    "no explanations, no quotes.\n\nText:\n"
)

NUM_PH = re.compile(r'\{\d+\}|%[sd]')

def normalize_ctl(s):
    return s.replace('\n', '\\n').replace('\t', '\\t').replace('\r', '\\r')

def call(src):
    payload = {
        'model': MODEL,
        'messages': [{'role': 'user', 'content': PROMPT + src}],
        'temperature': 0.2,
        'max_tokens': 8000,
        'stream': False,
    }
    req = urllib.request.Request(
        API_URL,
        data=json.dumps(payload).encode('utf-8'),
        headers={'Content-Type': 'application/json', 'Authorization': f'Bearer {API_KEY}'},
    )
    with urllib.request.urlopen(req, timeout=240) as resp:
        ctype = resp.headers.get('Content-Type', '')
        raw = resp.read().decode('utf-8')
    if 'text/event-stream' in ctype or raw.lstrip().startswith('data:'):
        parts = []
        for line in raw.splitlines():
            line = line.strip()
            if not line.startswith('data:'):
                continue
            body = line[5:].strip()
            if body == '[DONE]':
                break
            try:
                chunk = json.loads(body)
                c = chunk['choices'][0].get('delta', {}).get('content') or ''
                if c:
                    parts.append(c)
            except Exception:
                continue
        return ''.join(parts)
    return json.loads(raw)['choices'][0]['message']['content']

trans_path = os.path.join(I18N, 'translations.json')
done = json.load(open(trans_path, encoding='utf-8'))
failed = [k for k, v in done.items() if k == v and re.search(r'[\u4e00-\u9fff]', k)]
print(f'failed strings to retry: {len(failed)}', flush=True)

fixed, still_failed = 0, 0
for i, src in enumerate(failed):
    ok = False
    for attempt in range(3):
        try:
            out = call(src).strip()
            # strip wrapping code fences
            if out.startswith('```'):
                out = re.sub(r'^```[a-zA-Z]*\s*', '', out)
                out = re.sub(r'\s*```$', '', out)
            if not any(c in src for c in '\n\t\r'):
                out = normalize_ctl(out)
            if sorted(NUM_PH.findall(normalize_ctl(src))) != sorted(NUM_PH.findall(out)):
                raise ValueError(f'placeholder mismatch: {src[:50]!r} -> {out[:50]!r}')
            if out == src:
                raise ValueError('model returned identical text')
            done[src] = out
            fixed += 1
            ok = True
            break
        except Exception as e:
            print(f'  [{i+1}] attempt {attempt} failed: {str(e)[:90]}', flush=True)
            time.sleep(3 * (attempt + 1))
    if not ok:
        still_failed += 1
        print(f'  [{i+1}] STILL FAILED: {src[:70]!r}', flush=True)
    if (i + 1) % 10 == 0 or i == len(failed) - 1:
        json.dump(done, open(trans_path, 'w', encoding='utf-8'), ensure_ascii=False, indent=0)
        print(f'progress {i+1}/{len(failed)}', flush=True)

json.dump(done, open(trans_path, 'w', encoding='utf-8'), ensure_ascii=False, indent=0)
print(f'DONE. fixed={fixed} still_failed={still_failed}', flush=True)
