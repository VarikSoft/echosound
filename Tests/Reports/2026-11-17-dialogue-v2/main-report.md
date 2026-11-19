# Companion acceptance run

Functional assertions passed **332/337**. Cold start 3.79 s.

CPU 13th Gen Intel(R) Core(TM) i9-13900KS. RAM 32516 MB. Windows 11  (10.0.26200).

Times start when a complete PCM buffer is submitted or typed input is dispatched.
Audio fixtures are synthetic. Conversation checks confirm preserved gameplay state and a reply, with prose retained for manual review.
Authored dialogue and generated conversation are reported separately. First streamed text is not a complete reply.

| Group | Correct | ≤1 s | Median | p95 | Max |
|---|---:|---:|---:|---:|---:|
| boundaries | 36/36 | 36/36 | 0.000 | 0.841 | 0.891 |
| cancellation | 3/3 | 2/2 | 0.458 | 0.458 | 0.458 |
| commands | 11/11 | 11/11 | 0.242 | 0.276 | 0.276 |
| conversation | 14/14 | 14/14 | 0.000 | 0.000 | 0.000 |
| conversation-authored | 22/22 | 22/22 | 0.000 | 0.000 | 0.000 |
| conversation-llm | 12/12 | 8/12 | 0.949 | 1.767 | 1.767 |
| doors | 8/8 | 8/8 | 0.001 | 0.525 | 0.525 |
| escort | 3/3 | 3/3 | 0.000 | 0.001 | 0.001 |
| invalid | 3/3 | — | — | — | — |
| memory-FastLlm-Нина | 9/9 | 9/9 | 0.000 | 0.001 | 0.001 |
| memory-FastLlm-Саша | 9/9 | 9/9 | 0.000 | 0.000 | 0.000 |
| memory-FreeDialogue-Нина | 9/9 | 9/9 | 0.000 | 0.000 | 0.000 |
| memory-FreeDialogue-Саша | 9/9 | 9/9 | 0.000 | 0.000 | 0.000 |
| non-speech | 2/2 | — | — | — | — |
| voice-clean | 46/46 | 46/46 | 0.266 | 0.417 | 0.895 |
| voice-doors | 2/3 | 2/3 | 0.282 | 0.557 | 0.557 |
| voice-noise20 | 44/46 | 46/46 | 0.266 | 0.283 | 0.423 |
| voice-padded | 46/46 | 46/46 | 0.273 | 0.298 | 0.484 |
| voice-quiet | 44/46 | 46/46 | 0.266 | 0.282 | 0.417 |

## Failures and deadline misses

- **llm-FastLlm-2** correct=True, reply=1.198 s, action=-1.000 s
  - Input А если хочется чего-нибудь весёлого?. Heard А если хочется чего-нибудь весёлого?. Reply Предложу "Танец в лесу" – веселый и мистический фильм..
  - waiting → waiting, doors=0, route=fast-chat.
- **llm-FastLlm-4** correct=True, reply=1.085 s, action=-1.000 s
  - Input Придумай название для космического корабля.. Heard Придумай название для космического корабля.. Reply Космический корабль называется "Спящий в звёздном море"..
  - waiting → waiting, doors=0, route=fast-chat.
- **llm-FreeDialogue-0** correct=True, reply=1.134 s, action=-1.000 s
  - Input Какой фильм ты бы выбрал на вечер?. Heard Какой фильм ты бы выбрал на вечер?. Reply Я бы выбрал "Танк" для вечеринки..
  - waiting → waiting, doors=0, route=model-chat.
- **llm-FreeDialogue-1** correct=True, reply=1.767 s, action=-1.000 s
  - Input Почему именно такой?. Heard Почему именно такой?. Reply Потому что он напрямую связан с темой ужаса и мрачного настроения, как это часто происходит в кооперативных играх..
  - waiting → waiting, doors=0, route=model-chat.
- **ru-name-quiet-0** correct=False, reply=0.266 s, action=-1.000 s
  - Input Меня зовут Лёня.. Heard Меня зовут Люня.. Reply Понял, Люня..
  - following the curator → following the curator, doors=0, route=game-chat.
- **ru-name-quiet-1** correct=False, reply=0.266 s, action=-1.000 s
  - Input Меня зовут Лёня.. Heard Меня зовут Люня.. Reply Понял, Люня..
  - following the curator → following the curator, doors=0, route=game-chat.
- **ru-name-noise20-0** correct=False, reply=0.257 s, action=-1.000 s
  - Input Меня зовут Лёня.. Heard Меня зовут Люня.. Reply Понял, Люня..
  - following the curator → following the curator, doors=0, route=game-chat.
- **ru-name-noise20-1** correct=False, reply=0.266 s, action=-1.000 s
  - Input Меня зовут Лёня.. Heard Меня зовут Люня.. Reply Понял, Люня..
  - following the curator → following the curator, doors=0, route=game-chat.
- **ru-left** correct=False, reply=0.557 s, action=-1.000 s
  - Input ru-left. Heard Открой дверь слива.. Reply Уточни прямой командой: куда идти или какую дверь открыть/закрыть..
  - waiting → waiting, doors=0, route=fast-llm.

## Actual streamed first text

{"count": 21, "median": 0.2058658, "p95": 0.5926068999999999, "maximum": 0.6191137, "within_one_second": 21}
