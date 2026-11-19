# Companion acceptance run

Functional assertions passed **297/302**. Cold start 3.00 s.

CPU 13th Gen Intel(R) Core(TM) i9-13900KS. RAM 32516 MB. Windows 11  (10.0.26200).

Times start when a complete PCM buffer is submitted or typed input is dispatched.
Audio fixtures are synthetic. Conversation checks confirm preserved gameplay state and a reply, with prose retained for manual review.

| Group | Correct | ≤1 s | Median | p95 | Max |
|---|---:|---:|---:|---:|---:|
| boundaries | 36/36 | 35/36 | 0.000 | 0.770 | 1.188 |
| cancellation | 2/2 | 1/1 | 0.415 | 0.415 | 0.415 |
| commands | 11/11 | 11/11 | 0.226 | 0.251 | 0.251 |
| conversation | 14/14 | 5/14 | 1.349 | 4.132 | 4.132 |
| doors | 8/8 | 8/8 | 0.001 | 0.525 | 0.525 |
| escort | 3/3 | 3/3 | 0.000 | 0.001 | 0.001 |
| invalid | 3/3 | — | — | — | — |
| memory-FastLlm-Нина | 9/9 | 9/9 | 0.000 | 0.000 | 0.000 |
| memory-FastLlm-Саша | 9/9 | 9/9 | 0.000 | 0.000 | 0.000 |
| memory-FreeDialogue-Нина | 9/9 | 9/9 | 0.000 | 0.000 | 0.000 |
| memory-FreeDialogue-Саша | 9/9 | 9/9 | 0.000 | 0.000 | 0.000 |
| non-speech | 2/2 | — | — | — | — |
| voice-clean | 46/46 | 42/46 | 0.237 | 1.111 | 1.330 |
| voice-doors | 2/3 | 2/3 | 0.251 | 0.533 | 0.533 |
| voice-noise20 | 44/46 | 46/46 | 0.237 | 0.816 | 0.847 |
| voice-padded | 46/46 | 46/46 | 0.240 | 0.847 | 0.863 |
| voice-quiet | 44/46 | 46/46 | 0.233 | 0.805 | 0.865 |

## Failures and deadline misses

- **boundary-016** correct=True, reply=1.188 s, action=-1.000 s
  - Input Следую за мной.. Heard Следую за мной.. Reply Хорошо, я следую за тобой..
  - following the curator → following the curator, doors=0, route=fast-chat.
- **conversation-FastLlm-4** correct=True, reply=1.349 s, action=-1.000 s
  - Input Можешь рассказать короткую шутку?. Heard Можешь рассказать короткую шутку?. Reply Почему так думаешь?.
  - waiting → waiting, doors=0, route=fast-chat.
- **conversation-FastLlm-5** correct=True, reply=1.202 s, action=-1.000 s
  - Input В чём шутка?. Heard В чём шутка?. Reply Шутка в том, что я всегда на твоей стороне!.
  - waiting → waiting, doors=0, route=fast-chat.
- **conversation-FastLlm-6** correct=True, reply=1.100 s, action=-1.000 s
  - Input Ха-ха, неплохо.. Heard Ха-ха, неплохо.. Reply Пусть это будет наша шутка!.
  - waiting → waiting, doors=0, route=fast-chat.
- **conversation-FreeDialogue-1** correct=True, reply=4.132 s, action=-1.000 s
  - Input А у тебя как настроение?. Heard А у тебя как настроение?. Reply Нормально, всё в порядке..
  - waiting → waiting, doors=0, route=model-chat.
- **conversation-FreeDialogue-2** correct=True, reply=1.650 s, action=-1.000 s
  - Input Что думаешь об этой игре?. Heard Что думаешь об этой игре?. Reply Я думаю, что это интересная игра, где можно исследовать и находить решения..
  - waiting → waiting, doors=0, route=model-chat.
- **conversation-FreeDialogue-3** correct=True, reply=2.184 s, action=-1.000 s
  - Input Почему так думаешь?. Heard Почему так думаешь?. Reply Потому что игроки могут находить решения и исследовать мир, что делает игру интересной и увлекательной..
  - waiting → waiting, doors=0, route=model-chat.
- **conversation-FreeDialogue-4** correct=True, reply=2.419 s, action=-1.000 s
  - Input Можешь рассказать короткую шутку?. Heard Можешь рассказать короткую шутку?. Reply Короткая шутка: 'Почему в комнате тишина? Потому что все спят.'.
  - waiting → waiting, doors=0, route=model-chat.
- **conversation-FreeDialogue-5** correct=True, reply=2.162 s, action=-1.000 s
  - Input В чём шутка?. Heard В чём шутка?. Reply Шутка в том, что все в комнате спят, поэтому тишина..
  - waiting → waiting, doors=0, route=model-chat.
- **conversation-FreeDialogue-6** correct=True, reply=2.685 s, action=-1.000 s
  - Input Ха-ха, неплохо.. Heard Ха-ха, неплохо.. Reply Хорошо, шутка понравилась! Давай продолжим исследование..
  - waiting → waiting, doors=0, route=model-chat.
- **ru-social-clean-0** correct=True, reply=1.329 s, action=-1.000 s
  - Input А как у тебя дела?. Heard А как у тебя дела?. Reply Хорошо, я в порядке..
  - following the curator → following the curator, doors=0, route=fast-chat.
- **ru-thanks-clean-0** correct=True, reply=1.111 s, action=-1.000 s
  - Input Спасибо, что ты рядом.. Heard Спасибо. Что ты рядом?. Reply Я рядом, держусь за твою руку.
  - following the curator → following the curator, doors=0, route=fast-chat.
- **ru-thanks-clean-1** correct=True, reply=1.003 s, action=-1.000 s
  - Input Спасибо, что ты рядом.. Heard Спасибо. Что ты рядом?. Reply Я рядом, держусь за твою руку.
  - following the curator → following the curator, doors=0, route=fast-chat.
- **ru-name-quiet-0** correct=False, reply=0.229 s, action=-1.000 s
  - Input Меня зовут Лёня.. Heard Меня зовут Люня.. Reply Понял, Люня..
  - following the curator → following the curator, doors=0, route=game-chat.
- **ru-name-quiet-1** correct=False, reply=0.224 s, action=-1.000 s
  - Input Меня зовут Лёня.. Heard Меня зовут Люня.. Reply Понял, Люня..
  - following the curator → following the curator, doors=0, route=game-chat.
- **ru-name-noise20-0** correct=False, reply=0.239 s, action=-1.000 s
  - Input Меня зовут Лёня.. Heard Меня зовут Люня.. Reply Понял, Люня..
  - following the curator → following the curator, doors=0, route=game-chat.
- **ru-name-noise20-1** correct=False, reply=0.237 s, action=-1.000 s
  - Input Меня зовут Лёня.. Heard Меня зовут Люня.. Reply Понял, Люня..
  - following the curator → following the curator, doors=0, route=game-chat.
- **en-social-clean-0** correct=True, reply=1.330 s, action=-1.000 s
  - Input Hey, how are you doing?. Heard Hey. How are you doing?. Reply I'm doing well, thanks! How about you?.
  - following the curator → following the curator, doors=0, route=fast-chat.
- **ru-left** correct=False, reply=0.533 s, action=-1.000 s
  - Input ru-left. Heard Открой дверь слива.. Reply Уточни прямой командой: куда идти или какую дверь открыть/закрыть..
  - waiting → waiting, doors=0, route=fast-llm.
