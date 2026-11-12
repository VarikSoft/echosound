"""Checks the actual C# fast-mode requests exported by LocalAiChecks.Validate on bundled CPU inference."""
import argparse
import importlib.util
import json
import pathlib
import time
import urllib.request
import uuid

ROOT = pathlib.Path(__file__).resolve().parent.parent
spec = importlib.util.spec_from_file_location('local_ai_test', ROOT/'Tools/Test-LocalAI.py')
runtime = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runtime)

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--fixtures', type=pathlib.Path, default=ROOT/'.utmp/fast-llm-fixtures.json')
    args = parser.parse_args()
    cases = json.loads(args.fixtures.read_text(encoding='utf-8-sig'))
    results = []
    key = uuid.uuid4().hex
    with runtime.server('llama', runtime.free_port(), key) as url:
        for case in [dict(cases[0], text='Warm up', expected=None)] + cases:
            start = time.monotonic()
            data = {}
            if case['guarded']:
                actual = ['clarify']
            else:
                request = dict(case['request'])
                if case['expected'] is None:
                    request['messages'] = [request['messages'][0], {'role':'user','content':'Warm up'}]
                req = urllib.request.Request(url+'/v1/chat/completions', data=json.dumps(request).encode(),
                    headers={'Content-Type':'application/json','Authorization':'Bearer '+key})
                with urllib.request.urlopen(req, timeout=40) as response: data = json.load(response)
                choice = data['choices'][0]
                if choice['finish_reason'] != 'stop': raise RuntimeError('Incomplete plan: '+case['text'])
                raw = json.loads(choice['message']['content'])
                actual = [case['resolved'][code] for code in raw]
                if 'clarify' in actual: actual = ['clarify']
            row = {'text':case['text'], 'expected':case['expected'], 'actual':actual, 'ok':case['expected'] is None or actual==case['expected'],
                   'seconds':round(time.monotonic()-start,3),'route':'guard' if case['guarded'] else 'llm','usage':data.get('usage')}
            results.append(row)
            print(json.dumps(row,ensure_ascii=True),flush=True)
    (ROOT/'.utmp/fast-llm-results.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf8')
    passed = sum(row['ok'] for row in results[1:])
    times = sorted(row['seconds'] for row in results[1:] if row['route']=='llm')
    print(f'{passed}/{len(cases)} passed; warm inference median={times[len(times)//2]:.3f}s max={max(times):.3f}s')
    return 0 if passed==len(cases) else 1

if __name__ == '__main__': raise SystemExit(main())
