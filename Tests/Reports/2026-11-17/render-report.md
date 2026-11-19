# Companion acceptance run

Functional assertions passed **72/72**. Cold start 2.88 s.

CPU 13th Gen Intel(R) Core(TM) i9-13900KS. RAM 32516 MB. Windows 11  (10.0.26200).

Times start when a complete PCM buffer is submitted or typed input is dispatched.
Audio fixtures are synthetic. Conversation checks confirm preserved gameplay state and a reply, with prose retained for manual review.

| Group | Correct | ≤1 s | Median | p95 | Max |
|---|---:|---:|---:|---:|---:|
| boundaries | 4/4 | 4/4 | 0.000 | 0.001 | 0.001 |
| cancellation | 2/2 | 1/1 | 0.416 | 0.416 | 0.416 |
| commands | 11/11 | 11/11 | 0.226 | 0.260 | 0.260 |
| conversation | 7/7 | 4/7 | 0.991 | 1.359 | 1.359 |
| doors | 8/8 | 8/8 | 0.001 | 0.527 | 0.527 |
| escort | 3/3 | 3/3 | 0.000 | 0.001 | 0.001 |
| invalid | 3/3 | — | — | — | — |
| memory-FastLlm-Нина | 9/9 | 9/9 | 0.000 | 0.000 | 0.000 |
| non-speech | 2/2 | — | — | — | — |
| voice-clean | 23/23 | 21/23 | 0.233 | 1.025 | 1.441 |

## Failures and deadline misses

- **conversation-FastLlm-1** correct=True, reply=1.017 s, action=-1.000 s
  - Input А у тебя как настроение?. Heard А у тебя как настроение?. Reply Пока что нормально..
  - waiting → waiting, doors=0, route=fast-chat.
- **conversation-FastLlm-4** correct=True, reply=1.359 s, action=-1.000 s
  - Input Можешь рассказать короткую шутку?. Heard Можешь рассказать короткую шутку?. Reply Почему так думаешь?.
  - waiting → waiting, doors=0, route=fast-chat.
- **conversation-FastLlm-5** correct=True, reply=1.166 s, action=-1.000 s
  - Input В чём шутка?. Heard В чём шутка?. Reply Шутка в том, что я всегда на твоей стороне!.
  - waiting → waiting, doors=0, route=fast-chat.
- **ru-thanks-clean-0** correct=True, reply=1.025 s, action=-1.000 s
  - Input Спасибо, что ты рядом.. Heard Спасибо. Что ты рядом?. Reply Я рядом, держусь за твою руку.
  - following the curator → following the curator, doors=0, route=fast-chat.
- **en-social-clean-0** correct=True, reply=1.441 s, action=-1.000 s
  - Input Hey, how are you doing?. Heard Hey. How are you doing?. Reply I'm doing well, thanks! How about you?.
  - following the curator → following the curator, doors=0, route=fast-chat.
