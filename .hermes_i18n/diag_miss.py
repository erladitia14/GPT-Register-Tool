"""Diagnose why certain strings were missed by extract.py."""
import re, json, os

ROOT = r'C:\Users\erlan\Documents\AI\GPT-Register-Tool'
pat = re.compile(r'"((?:[^"\\]|\\.)*[\u4e00-\u9fff](?:[^"\\]|\\.)*)"')

targets = {
    'MainWindow.Payment.cs': [210, 342, 379],
    'ProtocolPaymentExecution.cs': [190, 239],
    'MainWindow.Tasks.cs': [9],
}
for fn, lines in targets.items():
    path = os.path.join(ROOT, 'SmsWorkbench', fn)
    src = open(path, encoding='utf-8').read()
    all_found = pat.findall(src)
    print(f'=== {fn}: regex found {len(all_found)} strings total')
    for n in lines:
        line = src.splitlines()[n - 1]
        m = pat.findall(line)
        print(f'  line {n}: regex on line -> {m}')
        print(f'    raw: {line.strip()[:110]}')
