"""Checks the actual C# fast-mode requests exported by LocalAiChecks.Validate on bundled CPU inference."""
import argparse
import importlib.util
import json
import pathlib
import re
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
    parser.add_argument('--model', type=pathlib.Path, help='Optional candidate GGUF; leaves the game bundle unchanged')
    args = parser.parse_args()
    cases = json.loads(args.fixtures.read_text(encoding='utf-8-sig'))
    results = []
    key = uuid.uuid4().hex
    with runtime.server('llama', runtime.free_port(), key, args.model.resolve() if args.model else None) as url:
        for case in [dict(cases[0], text='Warm up', expected=None)] + cases:
            start = time.monotonic()
            data = {}
            finish_reason = 'stop'
            if case['guarded']:
                actual = ['clarify']
            else:
                request = dict(case['request'])
                if case['expected'] is None:
                    request['messages'] = [request['messages'][0], {'role':'user','content':'Warm up'}]
                req = urllib.request.Request(url+'/v1/chat/completions', data=json.dumps(request).encode(),
                    headers={'Content-Type':'application/json','Authorization':'Bearer '+key})
                with urllib.request.urlopen(req, timeout=40) as response:
                    if request.get('stream'):
                        reply=''
                        finish_reason='length'
                        for line in response:
                            if not line.startswith(b'data:'): continue
                            payload=line[5:].strip()
                            if payload==b'[DONE]': break
                            chunk=json.loads(payload)
                            if not chunk.get('choices'): continue
                            choice=chunk['choices'][0]
                            reply += choice.get('delta',{}).get('content') or ''
                            sentence=re.search(r'[.!?]',reply)
                            if sentence:
                                reply=reply[:sentence.end()]
                                finish_reason='stop'
                                break
                            if choice.get('finish_reason'): finish_reason=choice['finish_reason']
                        actual={'reply':reply} if finish_reason=='stop' else []
                    else:
                        data=json.load(response)
                        choice=data['choices'][0]
                        finish_reason=choice['finish_reason']
                        actual=json.loads(choice['message']['content']) if finish_reason=='stop' else []
            if isinstance(actual, dict):
                reply = actual.get('reply')
                ok = case['expected'] == ['reply'] and isinstance(reply, str) and 0 < len(reply.strip()) <= 180
                if ok:
                    ok = bool(re.search(r'[\u0400-\u04ff]', reply)) == bool(re.search(r'[\u0400-\u04ff]', case['text']))
            else:
                ok = actual == case['expected'] if case['guarded'] else ','.join(actual) in case['accepted']
            row = {'text':case['text'], 'expected':case['expected'], 'actual':actual, 'ok':case['expected'] is None or ok,
                   'finish_reason':finish_reason,
                   'seconds':round(time.monotonic()-start,3),'route':'guard' if case['guarded'] else 'llm','usage':data.get('usage')}
            results.append(row)
            print(json.dumps(row,ensure_ascii=True),flush=True)
    (ROOT/'.utmp/fast-llm-results.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf8')
    passed = sum(row['ok'] for row in results[1:])
    times = sorted(row['seconds'] for row in results[1:] if row['route']=='llm')
    print(f'{passed}/{len(cases)} passed; warm inference median={times[len(times)//2]:.3f}s max={max(times):.3f}s')
    return 0 if passed==len(cases) else 1

if __name__ == '__main__': raise SystemExit(main())
