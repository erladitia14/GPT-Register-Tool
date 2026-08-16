"""Debug: what does the model actually return for batch 0?"""
import json, os, urllib.request

ROOT = r'C:\Users\erlan\Documents\AI\GPT-Register-Tool'
I18N = os.path.join(ROOT, '.hermes_i18n')
API_URL = 'http://localhost:20128/v1/chat/completions'
API_KEY = open(r'C:\Users\erlan\Documents\AI\ApiCC.txt', encoding='utf-8').read().strip()

strings = list(json.load(open(os.path.join(I18N, 'strings.json'), encoding='utf-8')).keys())
batch = strings[:5]
payload = {
    'model': 'kr/deepseek-3.2',
    'messages': [{'role': 'user', 'content': 'Translate each Chinese string in this JSON array to Indonesian. Return ONLY a JSON array of translated strings, same length/order.\n' + json.dumps(batch, ensure_ascii=False)}],
    'temperature': 0.2,
    'max_tokens': 4000,
}
req = urllib.request.Request(API_URL, data=json.dumps(payload).encode('utf-8'),
    headers={'Content-Type': 'application/json', 'Authorization': f'Bearer {API_KEY}'})
with urllib.request.urlopen(req, timeout=120) as resp:
    print('HTTP status:', resp.status)
    print('Content-Type:', resp.headers.get('Content-Type'))
    raw = resp.read()
    print('RAW BODY (first 600 bytes):')
    print(raw[:600].decode('utf-8', errors='replace'))
    data = json.loads(raw.decode('utf-8'))
content = data['choices'][0]['message']['content']
print('RAW RESPONSE:')
print(repr(content[:800]))
try:
    out = json.loads(content.strip())
    print('PARSED OK:', out)
except Exception as e:
    print('PARSE FAIL:', e)
