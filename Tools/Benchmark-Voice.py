"""Compare previous auto-language recognition with single-pass speech on local WAV fixtures.
Usage: python Tools/Benchmark-Voice.py .utmp/voice-latency --language polish
Uses only loopback; does not capture a microphone or download audio.
"""
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

def transcribe(url, audio, language, fast):
    fields = {'language':language, 'response_format':'json' if fast else 'verbose_json',
              'temperature':'0', 'temperature_inc':'0' if fast else '0.2',
              'best_of':'1' if fast else '2', 'beam_size':'1' if fast else '-1',
              'no_timestamps':'true' if fast else 'false', 'token_timestamps':'false' if fast else 'true'}
    boundary = uuid.uuid4().hex
    body = bytearray()
    for field, value in fields.items():
        body.extend(f'--{boundary}\r\nContent-Disposition: form-data; name="{field}"\r\n\r\n{value}\r\n'.encode())
    body.extend(f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="phrase.wav"\r\nContent-Type: audio/wav\r\n\r\n'.encode())
    body.extend(audio); body.extend(f'\r\n--{boundary}--\r\n'.encode())
    req = urllib.request.Request(url+'/inference',data=body,headers={'Content-Type':f'multipart/form-data; boundary={boundary}'})
    with urllib.request.urlopen(req,timeout=90) as response: return json.load(response)

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('fixtures', type=pathlib.Path)
    parser.add_argument('--language', default='polish', choices=['polish', 'english'])
    args = parser.parse_args()
    results = []
    with runtime.server('whisper', runtime.free_port(), uuid.uuid4().hex) as url:
        for path in sorted(args.fixtures.glob('*.wav')):
            for fast in [False, True]:
                started=time.perf_counter(); requests=1
                result=transcribe(url,path.read_bytes(),args.language if fast else 'auto',fast)
                if not fast and result.get('text','').strip() and result.get('language') not in ['polish','english']:
                    result=transcribe(url,path.read_bytes(),'polish',False); requests+=1
                row={'sample':path.name,'mode':'fixed-fast' if fast else 'previous-auto','seconds':round(time.perf_counter()-started,3),'requests':requests,'text':result.get('text','').strip()}
                results.append(row); print(json.dumps(row,ensure_ascii=False),flush=True)
    (ROOT/'.utmp/voice-latency-results.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8')

if __name__ == '__main__': main()
