# Companion acceptance run

Functional assertions passed **90/90**. Cold start 3.54 s.

CPU 13th Gen Intel(R) Core(TM) i9-13900KS. RAM 32516 MB. Windows 11  (10.0.26200).

Times start when a complete PCM buffer is submitted or typed input is dispatched.
Audio fixtures are synthetic. Conversation checks confirm preserved gameplay state and a reply, with prose retained for manual review.
Authored dialogue and generated conversation are reported separately. First streamed text is not a complete reply.

| Group | Correct | ≤1 s | Median | p95 | Max |
|---|---:|---:|---:|---:|---:|
| boundaries | 4/4 | 4/4 | 0.000 | 0.001 | 0.001 |
| cancellation | 3/3 | 2/2 | 0.491 | 0.491 | 0.491 |
| commands | 11/11 | 11/11 | 0.277 | 0.301 | 0.301 |
| conversation | 7/7 | 7/7 | 0.000 | 0.001 | 0.001 |
| conversation-authored | 11/11 | 11/11 | 0.000 | 0.000 | 0.000 |
| conversation-llm | 6/6 | 3/6 | 1.189 | 1.909 | 1.909 |
| doors | 8/8 | 8/8 | 0.001 | 0.526 | 0.526 |
| escort | 3/3 | 3/3 | 0.000 | 0.001 | 0.001 |
| invalid | 3/3 | — | — | — | — |
| memory-FastLlm-Нина | 9/9 | 9/9 | 0.000 | 0.001 | 0.001 |
| non-speech | 2/2 | — | — | — | — |
| voice-clean | 23/23 | 23/23 | 0.274 | 0.408 | 0.933 |

## Failures and deadline misses

- **llm-FastLlm-0** correct=True, reply=1.909 s, action=-1.000 s
  - Input Какой фильм ты бы выбрал на вечер?. Heard Какой фильм ты бы выбрал на вечер?. Reply Скорее всего, я бы выбрал "Песня о тенях" для ночи в мрачном лабиринте..
  - waiting → waiting, doors=0, route=fast-chat.
- **llm-FastLlm-1** correct=True, reply=1.557 s, action=-1.000 s
  - Input Почему именно такой?. Heard Почему именно такой?. Reply Потому что он напрямую связан с темой ночи и тьмы, как это нужно в лабиринте..
  - waiting → waiting, doors=0, route=fast-chat.
- **llm-FastLlm-2** correct=True, reply=1.189 s, action=-1.000 s
  - Input А если хочется чего-нибудь весёлого?. Heard А если хочется чего-нибудь весёлого?. Reply Весёлый фильм — это не то, что нужно в лабиринте..
  - waiting → waiting, doors=0, route=fast-chat.

## Actual streamed first text

{"count": 7, "median": 0.34808279999999997, "p95": 0.643651, "maximum": 0.643651, "within_one_second": 7}
