"""Developer smoke test. Run Unity LocalAiChecks.Validate first to export client requests.
Python standard library only; players never run this script. No network beyond loopback.
"""
import argparse
import contextlib
import io
import json
import pathlib
import socket
import subprocess
import time
import urllib.error
import urllib.request
import uuid
import wave

ROOT = pathlib.Path(__file__).resolve().parent.parent
BUNDLE = ROOT / 'Runtime/AI'

def free_port():
    with socket.socket() as sock:
        sock.bind(('127.0.0.1', 0))
        return sock.getsockname()[1]

@contextlib.contextmanager
def server(kind, port, key):
    model_dir = BUNDLE / 'models'
    executable = BUNDLE / 'windows-x64' / kind / (('llama' if kind == 'llama' else 'whisper') + '-server.exe')
    args = [str(executable), '--host', '127.0.0.1', '--port', str(port), '-t', '4']
    if kind == 'llama':
        args += ['-m', str(model_dir/'Qwen_Qwen3-1.7B-Q4_K_M.gguf'), '-ngl', '0', '-c', '4096', '-np', '1', '-tb', '4', '--jinja', '--reasoning-budget', '0', '--api-key', key]
    else:
        args += ['-m', str(model_dir/'ggml-base-q5_1.bin'), '-ng', '--vad', '-vm', str(model_dir/'ggml-silero-v5.1.2.bin'), '-vp', '300', '-nlp']
    with (ROOT/f'.utmp/{kind}-smoke.log').open('wb') as log:
        proc = subprocess.Popen(args, cwd=executable.parent, stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW)
        try:
            deadline = time.monotonic() + 120
            url = f'http://127.0.0.1:{port}'
            while True:
                if proc.poll() is not None: raise RuntimeError(f'{kind} exited with {proc.returncode}; see log')
                try:
                    req = urllib.request.Request(url + ('/health' if kind == 'llama' else '/'), headers={'Authorization':'Bearer '+key})
                    with urllib.request.urlopen(req, timeout=2): break
                except (urllib.error.URLError, TimeoutError):
                    if time.monotonic() > deadline: raise RuntimeError(f'{kind} startup timed out')
                    time.sleep(.2)
            yield url
        finally:
            proc.terminate()
            try: proc.wait(timeout=5)
            except subprocess.TimeoutExpired: proc.kill(); proc.wait()

def transcribe(url, audio):
    boundary = uuid.uuid4().hex
    body = bytearray()
    for field, value in [('language','english'),('response_format','json'),('temperature','0'),('temperature_inc','0'),
                         ('best_of','1'),('beam_size','1'),('no_timestamps','true'),('token_timestamps','false')]:
        body.extend(f'--{boundary}\r\nContent-Disposition: form-data; name="{field}"\r\n\r\n{value}\r\n'.encode())
    body.extend(f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="phrase.wav"\r\nContent-Type: audio/wav\r\n\r\n'.encode())
    body.extend(audio)
    body.extend(f'\r\n--{boundary}--\r\n'.encode())
    req = urllib.request.Request(url+'/inference', data=body, headers={'Content-Type':f'multipart/form-data; boundary={boundary}'})
    with urllib.request.urlopen(req, timeout=90) as response: return json.load(response)

def run():
    parser = argparse.ArgumentParser()
    parser.add_argument('--audio', type=pathlib.Path, help='Optional JFK sample from whisper.cpp, not downloaded by this test')
    args = parser.parse_args()
    fixtures = json.loads((ROOT/'.utmp/ai-fixtures.json').read_text(encoding='utf-8-sig'))
    results = []
    key = uuid.uuid4().hex
    with server('llama', free_port(), key) as url:
        for case in fixtures:
            if case.get('quick_action'):
                result = {'phrase':case['phrase'],'ok':case['quick_action']==case['expected'],'route':'exact command (Unity parser checked)','action':case['quick_action']}
                results.append(result); print(json.dumps(result,ensure_ascii=False),flush=True)
                continue
            start = time.monotonic()
            req = urllib.request.Request(url+'/v1/chat/completions', data=json.dumps(case['request']).encode(), headers={'Content-Type':'application/json','Authorization':'Bearer '+key})
            with urllib.request.urlopen(req, timeout=90) as response: data = json.load(response)
            choice = data['choices'][0]
            plan = json.loads(choice['message']['content'])
            names = ','.join(action['name'] for action in plan['actions'])
            if case.get('quick_followup'): names += ',' + case['quick_followup']
            ok = names == case['expected'] and choice['finish_reason'] != 'length'
            if case['target']: ok = ok and plan['actions'][0]['arguments'].get('target') == case['target']
            result = {'phrase':case['phrase'],'ok':ok,'seconds':round(time.monotonic()-start,2),'plan':plan}
            if case.get('quick_followup'): result['then_exact_command'] = case['quick_followup']
            results.append(result)
            print(json.dumps(result,ensure_ascii=False),flush=True)
    with server('whisper', free_port(), key) as url:
        buffer = io.BytesIO()
        with wave.open(buffer,'wb') as wav:
            wav.setnchannels(1); wav.setsampwidth(2); wav.setframerate(16000); wav.writeframes(bytes(32000))
        data = transcribe(url,buffer.getvalue())
        result = {'test':'speech silence','ok':not data.get('text','').strip(),'response':data}
        results.append(result); print(json.dumps(result,ensure_ascii=False),flush=True)
        if args.audio:
            start = time.monotonic(); data = transcribe(url,args.audio.read_bytes())
            result = {'test':'speech JFK','ok':'country' in data.get('text','').lower(),'seconds':round(time.monotonic()-start,2),'text':data.get('text')}
            results.append(result); print(json.dumps(result,ensure_ascii=False),flush=True)
    (ROOT/'.utmp/ai-smoke-results.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8')
    return 0 if all(result['ok'] for result in results) else 1

if __name__ == '__main__':
    raise SystemExit(run())
