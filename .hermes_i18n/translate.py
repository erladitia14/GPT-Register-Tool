"""Step 2: Translate all unique Chinese strings to Indonesian via 9Router API.

Loads .hermes_i18n/strings.json, batches by char size, asks the model to
return a JSON array of translations (same order/length), validates, and
saves .hermes_i18n/translations.json incrementally (resumable).
"""
import json, os, sys, time, re, urllib.request

ROOT = r'C:\Users\erlan\Documents\AI\GPT-Register-Tool'
I18N = os.path.join(ROOT, '.hermes_i18n')
API_URL = 'http://localhost:20128/v1/chat/completions'
API_KEY = open(r'C:\Users\erlan\Documents\AI\ApiCC.txt', encoding='utf-8').read().strip()
MODEL = sys.argv[1] if len(sys.argv) > 1 else 'kr/deepseek-3.2'

PROMPT = (
    "You are a professional software localizer. Translate each string in the JSON array "
    "from Chinese to natural Indonesian (Bahasa Indonesia) for a desktop app UI/log messages. "
    "STRICT RULES:\n"
    "1. Return ONLY a JSON array of strings, same length and same order as the input.\n"
    "2. Preserve EXACTLY all placeholders like {0}, {1}, {name}, {email}, %s, %d, \\n, \\t and escaped quotes.\n"
    "3. Keep technical terms, emails, URLs, file paths, and code identifiers untranslated.\n"
    "4. Translate meaning, keep it concise (UI labels short).\n"
    "5. If a string is only punctuation/symbols, keep it unchanged.\n"
    "Input JSON array:"
)

# numbered braces + printf verbs are hard requirements
NUM_PH = re.compile(r'\{\d+\}|%[sd]')
# named braces are soft (warn only) — some may be false positives inside prose
NAMED_PH = re.compile(r'\{[a-zA-Z_][a-zA-Z0-9_]*\}')

def normalize_ctl(s):
    """Map real control chars to their escape-sequence form."""
    return s.replace('\n', '\\n').replace('\t', '\\t').replace('\r', '\\r')

with open(os.path.join(I18N, 'strings.json'), encoding='utf-8') as fh:
    strings = list(json.load(fh).keys())

trans_path = os.path.join(I18N, 'translations.json')
done = {}
if os.path.exists(trans_path):
    done = json.load(open(trans_path, encoding='utf-8'))

remaining = [s for s in strings if s not in done]
print(f'total={len(strings)} done={len(done)} remaining={len(remaining)} model={MODEL}', flush=True)

# batch by char budget
BUDGET = 3500
batches = []
cur, size = [], 0
for s in remaining:
    if len(s) > BUDGET:
        if cur:
            batches.append(cur)
            cur, size = [], 0
        batches.append([s])
        continue
    if size + len(s) > BUDGET and cur:
        batches.append(cur)
        cur, size = [], 0
    cur.append(s)
    size += len(s)
if cur:
    batches.append(cur)

print(f'batches: {len(batches)}', flush=True)

def call(batch):
    payload = {
        'model': MODEL,
        'messages': [
            {'role': 'user', 'content': PROMPT + '\n' + json.dumps(batch, ensure_ascii=False)}
        ],
        'temperature': 0.2,
        'max_tokens': 16000,
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
        # 9Router streams even when stream:false — accumulate delta content
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
            except (json.JSONDecodeError, KeyError, IndexError, TypeError):
                continue
        content = ''.join(parts)
    else:
        content = json.loads(raw)['choices'][0]['message']['content']
    content = content.strip()
    if content.startswith('```'):
        content = re.sub(r'^```[a-zA-Z]*\s*', '', content)
        content = re.sub(r'\s*```$', '', content)
    # strict=False tolerates raw control chars inside JSON strings
    return json.loads(content, strict=False)

fails = 0
for bi, batch in enumerate(batches):
    ok = False
    for attempt in range(3):
        try:
            out = call(batch)
            if not isinstance(out, list) or len(out) != len(batch):
                raise ValueError(f'length mismatch {len(out) if isinstance(out, list) else "?"} != {len(batch)}')
            results = []
            for src, tgt in zip(batch, out):
                if not isinstance(tgt, str):
                    raise ValueError(f'non-string translation for {src[:40]!r}')
                # when source has only literal escapes (no real control chars),
                # normalize translation so apply() can match exactly
                if not any(c in src for c in '\n\t\r'):
                    tgt = normalize_ctl(tgt)
                # hard check: numbered placeholders / printf verbs must survive
                if sorted(NUM_PH.findall(normalize_ctl(src))) != sorted(NUM_PH.findall(tgt)):
                    raise ValueError(f'placeholder mismatch: {src[:60]!r} -> {tgt[:60]!r}')
                missing = set(NAMED_PH.findall(src)) - set(NAMED_PH.findall(tgt))
                if missing:
                    print(f'  WARN dropped named placeholder {missing} in {src[:50]!r}', flush=True)
                results.append((src, tgt))
            for src, tgt in results:
                done[src] = tgt
            ok = True
            break
        except Exception as e:
            print(f'  batch {bi} attempt {attempt} failed: {e}', flush=True)
            time.sleep(3 * (attempt + 1))
    if not ok:
        fails += 1
        # fallback: keep original so nothing blocks apply
        for s in batch:
            done.setdefault(s, s)
        print(f'  batch {bi} FAILED permanently, kept original', flush=True)
    if (bi + 1) % 10 == 0 or bi == len(batches) - 1:
        json.dump(done, open(trans_path, 'w', encoding='utf-8'), ensure_ascii=False, indent=0)
        print(f'progress: {bi+1}/{len(batches)} batches, {len(done)} strings done', flush=True)

json.dump(done, open(trans_path, 'w', encoding='utf-8'), ensure_ascii=False, indent=0)
print(f'COMPLETE. translated={len(done)} failed_batches={fails}', flush=True)
