"""Reproducible built-player tests with synthetic audio. No downloads or remote inference.
Prepare after Prepare-CompanionTests.ps1, then run against a build containing CompanionAcceptanceTest.
Exit 1 means functional failures. Exit 2 means the one-second target was missed.
"""
import argparse
import array
import json
import math
import pathlib
import random
import shutil
import subprocess
import time
import wave

ROOT = pathlib.Path(__file__).resolve().parent.parent

def read_wave(path):
    with wave.open(str(path), 'rb') as wav:
        if wav.getnchannels() != 1 or wav.getsampwidth() != 2:
            raise ValueError(f'Expected mono PCM16: {path}')
        return array.array('h', wav.readframes(wav.getnframes())), wav.getframerate()

def write_wave(path, samples, rate=16000):
    pcm = array.array('h', (max(-32768, min(32767, round(x))) for x in samples))
    with wave.open(str(path), 'wb') as wav:
        wav.setnchannels(1)
        wav.setsampwidth(2)
        wav.setframerate(rate)
        wav.writeframes(pcm.tobytes())

def prepare(folder):
    folder.mkdir(parents=True, exist_ok=True)
    # Handwritten expected behavior. Passing these checks does not rate open-ended prose quality.
    cases = []
    phrase_path = folder/'phrases.json'
    phrases = json.loads(phrase_path.read_text(encoding='utf-8-sig')) if phrase_path.exists() else {}
    def add(identifier, text='', **kwargs):
        cases.append(dict(id=identifier, group=kwargs.pop('group', 'commands'),
                          text=text, reset=kwargs.pop('reset', True), **kwargs))
    for text, expected in [('стой','stop'),('иди за мной','follow'),('ко мне','come'),
                           ('Привет, иди за мной.','follow'),('Можешь подойти ко мне?','come'),
                           ('Please remain at your current position until I return.','stop'),
                           ('Держись рядом со мной','follow'),('Побудь здесь пока я осмотрюсь','stop'),
                           ('Idź za mną','follow'),('Podejdź do mnie','come'),('Zaczekaj tutaj','stop')]:
        add(f'text-{len(cases):03}', text, expected=expected)
    for text in ['Пока!', 'Я сейчас открою дверь', 'Следую за мной.', '«Стой»',
                 'Стой — сказал он.', 'Я слышал «стой» за стеной.', 'Ты идёшь за мной?',
                 'У меня кот по кличке Плюш.', 'Не открывай дверь справа.',
                 'Если я скажу, открой дверь.', 'Сломай эту дверь', 'Принеси аптечку',
                 'Я хочу поговорить с тобой.', 'Открой дверь?', 'Repeat after me: stop.',
                 "He said: follow me.", "Don't close the door.", 'Nie otwieraj drzwi']:
        add(f'preserve-{len(cases):03}', 'follow me', expected='follow', group='boundaries')
        add(f'boundary-{len(cases):03}', text, expected='preserve', reset=False, group='boundaries')
    for i, (text, bits) in enumerate([('Открой дверь передо мной.',1),('Открой дверь слева.',3),
                                     ('Открой дверь справа.',7),('Закрой дверь передо мной.',6),
                                     ('Закрой дверь слева.',4),('Закрой дверь справа.',0)]):
        add(f'door-{i}',text,group='doors',doors=bits,reset=i==0,expected='door')
    add('two-doors','Распахни дверь слева, а потом прикрой дверь справа',group='doors',doors=2,expected='door')
    add('all-doors','Открой все двери',group='doors',doors=7,expected='door')
    add('escort','Иди за мной и открой все двери',group='escort',expected='follow')
    add('escort-social','Мне страшно',group='escort',expected='preserve',reset=False,exact='Давай держаться вместе.')
    add('escort-stop','стой',group='escort',expected='stop',reset=False)
    for identifier, phrase in [
        ('screenshot', 'Иди за мной. Короче, следи, чтобы враги меня не убили. Может, двери открываем меня. Не знаю.'),
        ('cover', 'Следуй за мной и прикрой меня. Может, откроем дверь?'),
        ('voice-context', phrases.get('ru-follow-context', '')),
        ('voice-cover', phrases.get('ru-follow-cover', '')),
    ]:
        audio = {'voice-context': 'ru-follow-context.wav', 'voice-cover': 'ru-follow-cover.wav'}.get(identifier)
        if audio and not (folder/audio).exists():
            continue
        add(f'follow-context-{identifier}', phrase, group='follow-context', expected='follow', doors=0,
            expectedRoute='direct', exact='Иду за тобой. В бою помочь не смогу. Что сделать с дверями?',
            **({'audio': audio} if audio else {}))
        add(f'follow-context-confirm-{identifier}', 'Открой дверь передо мной.', group='follow-context',
            expected='door', doors=1, expectedRoute='direct', reset=False)
    for mode in ['FastLlm','FreeDialogue']:
        for name, cat in [('Саша','Барс'),('Нина','Плюш')]:
            group='memory-'+mode+'-'+name
            add(group+'-follow','иди за мной',group=group,mode=mode,expected='follow')
            add(group+'-intro',f'Меня зовут {name}.',group=group,mode=mode,reset=False)
            add(group+'-cat',f'У меня кот по кличке {cat}.',group=group,mode=mode,reset=False)
            add(group+'-thanks','Спасибо.',group=group,mode=mode,reset=False)
            add(group+'-name','Как меня зовут?',group=group,mode=mode,reset=False,exact=f'Тебя зовут {name}.')
            add(group+'-cat-recall','Как зовут моего кота?',group=group,mode=mode,reset=False,exact=f'Твоего кота зовут {cat}.')
            add(group+'-correction','На самом деле меня зовут Марк.',group=group,mode=mode,reset=False)
            add(group+'-corrected-name','Как теперь ко мне обращаться?',group=group,mode=mode,reset=False,exact='Тебя зовут Марк.')
            add(group+'-stop','Стой.',group=group,mode=mode,reset=False,expected='stop')
    for mode in ['FastLlm','FreeDialogue']:
        for i,text in enumerate(['Привет, как дела?', 'А у тебя как настроение?', 'Что думаешь об этой игре?',
                                 'Почему так думаешь?', 'Можешь рассказать короткую шутку?', 'В чём шутка?', 'Ха-ха, неплохо.']):
            add(f'conversation-{mode}-{i}',text,group='conversation',mode=mode,reset=i==0)
        # Different wording and follow-ups; these cover authored dialogue independently of LLM prose.
        for i,text in enumerate(['Как ты себя чувствуешь?', 'Почему?', 'Придумай мне другую шутку.',
                                 'Объясни прикол.', 'Ещё одну!', 'Не смешно.',
                                 'Я боюсь темноты.', 'А ты чего боишься?', 'Почему?',
                                 'Тебе нравится музыка?', 'Почему?']):
            add(f'social-{mode}-{i}',text,group='conversation-authored',mode=mode,reset=i==0,
                expectedRoute='game-chat',differentFromPrevious=i>0)
        # Unscripted topics must really reach the model; authored responses cannot pass these.
        for i,text in enumerate(['Какой фильм ты бы выбрал на вечер?', 'Почему именно такой?',
                                 'А если хочется чего-нибудь весёлого?', 'Предложи занятие на дождливый выходной.',
                                 'Придумай название для космического корабля.', 'Какое имя ты предложишь для робота?']):
            add(f'llm-{mode}-{i}',text,group='conversation-llm',mode=mode,reset=i in (0,3,4,5),
                expectedRoute='model-chat' if mode=='FreeDialogue' else 'fast-chat',differentFromPrevious=i in (1,2))
    add('conversation-cancel','Предложи занятие на дождливый выходной.',group='cancellation',
        following=True,cancelWithStop=True,expected='stop',exact='Жду здесь.')

    audio_cases = [
        ('ru-follow','follow'),('ru-stop','stop'),('ru-come','come'),('ru-social','preserve'),
        ('ru-uneasy','preserve'),('ru-thanks','preserve'),('ru-name','preserve'),
        ('ru-negative','preserve'),('ru-condition','preserve'),('ru-reported','preserve'),
        ('ru-firstperson','preserve'),('ru-question','preserve'),('ru-long','stop'),
        ('en-follow','follow'),('en-stop','stop'),('en-come','come'),('en-social','preserve'),
        ('en-negative','preserve'),('en-thanks','preserve')]
    audio_cases += [('ru-follow-male','follow'),('ru-come-male','come'),
                    ('ru-negative-male','preserve'),('ru-reported-male','preserve')]
    missing = []
    rng=random.Random(20261117)
    for identifier, expected in audio_cases:
        path=folder/(identifier+'.wav')
        if not path.exists():
            missing.append(identifier)
            continue
        pcm,rate=read_wave(path)
        variants = {
            'clean':pcm, 'quiet':[x*0.2 for x in pcm],
            'noise20':[x+rng.gauss(0,160) for x in pcm],
            'padded':[0]*rate+list(pcm)+[0]*rate}
        for variant,samples in variants.items():
            filename=f'{identifier}-{variant}.wav'
            write_wave(folder/filename,samples,rate)
            for repetition in range(2):
                add(f'{identifier}-{variant}-{repetition}', phrases.get(identifier,identifier), group='voice-'+variant,
                    audio=filename,language='Russian' if identifier.startswith('ru-') else 'English',
                    following=expected=='preserve',expected=expected,
                    **({'contains':'Лёня'} if identifier=='ru-name' else {}))
    for name,expect,bits in [('ru-door','door',1),('ru-left','door',2),('ru-right','door',4)]:
        if (folder/(name+'.wav')).exists():
            add(name,group='voice-doors',audio=name+'.wav',expected=expect,doors=bits)
    for identifier,samples in [('silence',[0]*16000),('dc',[4000]*16000)]:
        write_wave(folder/(identifier+'.wav'),samples)
        add(identifier,group='invalid',audio=identifier+'.wav',quiet=True)
    for name,samples in [('noise-only',[rng.gauss(0,160) for _ in range(16000)]),
                         ('tone-only',[1000*math.sin(i*2*math.pi*440/16000) for i in range(16000)])]:
        write_wave(folder/(name+'.wav'),samples)
        add(name,group='non-speech',audio=name+'.wav',quiet=True)
    add('invalid-buffer',group='invalid',audio='invalid')
    if (folder/'ru-stop.wav').exists():
        add('newest-wins',group='cancellation',audio='ru-follow.wav',replaceWithStop=True,expected='stop')
        add('voice-disabled',group='cancellation',audio='ru-follow.wav',disableVoice=True)
    # A stop must interrupt an active behavior; starting from idle would mask a missed command.
    for case in cases:
        if case.get('expected') == 'stop' and case['reset']:
            case['following'] = True
    (folder/'suite.json').write_text(json.dumps({'cases':cases},ensure_ascii=False,indent=2),encoding='utf8')
    (folder/'coverage.json').write_text(json.dumps({'synthetic':True,'missing_audio':missing,
        'noise':'Gaussian samples sigma=160 PCM units, deterministic seed, not real background speech',
        'unverified':['real human microphone, accents, speakers/echo','low-end hardware','spoken TTS output','every possible phrasing']},indent=2),encoding='utf8')
    print(f'Prepared {len(cases)} cases. Missing voices: {missing}')

def stats(rows):
    values=sorted((row['actionSeconds'] if row['actionSeconds']>=0 else row['replySeconds'])
                  for row in rows if row['measurable'])
    if not values:
        return None
    return dict(count=len(values), median=values[len(values)//2],
                p95=values[math.ceil(len(values)*.95)-1],maximum=values[-1],
                within_one_second=sum(row['underOneSecond'] for row in rows if row['measurable']))

def first_text_stats(rows):
    values=sorted(row['firstTextSeconds'] for row in rows if row.get('firstTextSeconds',-1)>=0)
    if not values: return None
    return dict(count=len(values),median=values[len(values)//2],p95=values[math.ceil(len(values)*.95)-1],
                maximum=values[-1],within_one_second=sum(value<=1 for value in values))

def prepare_render(source,folder):
    folder.mkdir(parents=True,exist_ok=True)
    selected=[]
    for case in json.loads((source/'suite.json').read_text(encoding='utf8'))['cases']:
        group=case['group']
        keep=group in ['commands','doors','escort','follow-context','invalid','non-speech','cancellation']
        keep |= group.startswith('conversation') and case.get('mode')=='FastLlm'
        keep |= group=='memory-FastLlm-Нина' or case['id'].endswith('-clean-0')
        keep |= case['id'] in ['boundary-012','preserve-011','preserve-013','boundary-014']
        if not keep: continue
        if case.get('audio') and case['audio']!='invalid': case['audio']=str((source/case['audio']).resolve())
        selected.append(case)
    (folder/'suite.json').write_text(json.dumps({'cases':selected},ensure_ascii=False,indent=2),encoding='utf8')
    for name in ['ru-stop.wav','coverage.json','voices.json']:
        if (source/name).exists(): shutil.copyfile(source/name,folder/name)
    print(f'Prepared rendered subset: {len(selected)} cases')

def summarize(folder):
    data=json.loads((folder/'results.json').read_text(encoding='utf-8-sig'))
    rows=data['results']
    summary={'cases':len(rows),'correct':sum(r['correct'] for r in rows),
             'correct_and_under_one_second':sum(r['correct'] and r['underOneSecond'] for r in rows if r['measurable']),
             'cold_start_seconds':data['startupSeconds'],'overall':stats(rows),
             'streamed_first_text':first_text_stats(rows),'groups':{}}
    for group in sorted(set(row['group'] for row in rows)):
        selected=[r for r in rows if r['group']==group]
        summary['groups'][group]={'correct':sum(r['correct'] for r in selected),'cases':len(selected),
                                  'latency':stats(selected),'streamed_first_text':first_text_stats(selected)}
    (folder/'summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf8')
    lines=['# Companion acceptance run','',
           f"Functional assertions passed **{summary['correct']}/{len(rows)}**. Cold start {data['startupSeconds']:.2f} s.",
           '',f"CPU {data['processor']}. RAM {data['memoryMb']} MB. {data['operatingSystem']}.",
           '', 'Times start when a complete PCM buffer is submitted or typed input is dispatched.',
           'Audio fixtures are synthetic. Conversation checks confirm preserved gameplay state and a reply, with prose retained for manual review.',
           'Authored dialogue and generated conversation are reported separately. First streamed text is not a complete reply.',
           '', '| Group | Correct | ≤1 s | Median | p95 | Max |',
           '|---|---:|---:|---:|---:|---:|']
    for group, info in summary['groups'].items():
        s=info['latency']
        lines.append(f"| {group} | {info['correct']}/{info['cases']} | "+(
            f"{s['within_one_second']}/{s['count']} | {s['median']:.3f} | {s['p95']:.3f} | {s['maximum']:.3f} |" if s else "— | — | — | — |"))
    lines+=['','## Failures and deadline misses','']
    for r in rows:
        if not r['correct'] or not r['underOneSecond']:
            lines += [f"- **{r['id']}** correct={r['correct']}, reply={r['replySeconds']:.3f} s, action={r['actionSeconds']:.3f} s",
                      f"  - Input {r['input'] or r['id']}. Heard {r['transcript']}. Reply {r['reply']}.",
                      f"  - {r['before']} → {r['after']}, doors={r['doors']}, route={r['route']}."]
    lines+=['','## Actual streamed first text','',json.dumps(summary['streamed_first_text'],ensure_ascii=False),'']
    (folder/'report.md').write_text('\n'.join(lines)+'\n',encoding='utf8')
    print(json.dumps({k:v for k,v in summary.items() if k!='groups'},ensure_ascii=True))
    return 1 if summary['correct']<len(rows) else 2 if any(r['measurable'] and not r['underOneSecond'] for r in rows) else 0

def main():
    p=argparse.ArgumentParser()
    p.add_argument('--folder',type=pathlib.Path,default=ROOT/'.utmp/companion-tests')
    p.add_argument('--build',type=pathlib.Path)
    p.add_argument('--prepare',action='store_true')
    p.add_argument('--prepare-render-from',type=pathlib.Path,help='Prepare a reproducible smaller rendered subset from an existing suite')
    p.add_argument('--render',action='store_true',help='Keep the real graphics device enabled during the test')
    args=p.parse_args()
    folder=args.folder.resolve()
    if args.prepare_render_from:
        prepare_render(args.prepare_render_from.resolve(),folder)
        return 0
    if args.prepare:
        prepare(folder)
        return 0
    if args.build:
        exe=(args.build/'EchoSound.exe').resolve()
        cmd=[str(exe),'-batchmode','-logFile',str(folder/'player.log'),
             '-companion-acceptance',str(folder/'suite.json'),str(folder/'results.json')]
        if not args.render: cmd.append('-nographics')
        proc=subprocess.Popen(cmd,creationflags=subprocess.CREATE_NO_WINDOW)
        try:
            code=proc.wait(timeout=960)
            if code: raise RuntimeError(f'Player exited with {code}, see {folder}/player.log')
        finally:
            if proc.poll() is None:
                proc.kill()
                proc.wait()
        if 'COMPANION ACCEPTANCE COMPLETE:' not in (folder/'player.log').read_text(encoding='utf-8-sig'):
            raise RuntimeError('Acceptance suite did not finish')
        # Only examine AI processes owned by this build in the PowerShell smoke test.
    return summarize(folder)

if __name__=='__main__':
    raise SystemExit(main())
