# Companion acceptance run

Functional assertions passed **60/60**. Cold start 2.95 s.

CPU 13th Gen Intel(R) Core(TM) i9-13900KS. RAM 32516 MB. Windows 11  (10.0.26200).

Times start when a complete PCM buffer is submitted or typed input is dispatched.
Audio fixtures are synthetic. Conversation checks confirm preserved gameplay state and a reply, with prose retained for manual review.
Authored dialogue and generated conversation are reported separately. First streamed text is not a complete reply.

| Group | Correct | ≤1 s | Median | p95 | Max |
|---|---:|---:|---:|---:|---:|
| compound | 24/24 | 18/24 | 0.873 | 1.469 | 1.486 |
| name-control | 24/24 | 24/24 | 0.000 | 0.000 | 0.001 |
| setup | 12/12 | 12/12 | 0.000 | 0.001 | 0.001 |

## Failures and deadline misses

- **FastLlm-knownFalse-audioFalse-1-target** correct=True, reply=1.167 s, action=-1.000 s
  - Input БЛЯ сука я обосрался, иди проверяй за меня что там происходит, а вообще как меня зовут вдруг ты мимик..... Heard БЛЯ сука я обосрался, иди проверяй за меня что там происходит, а вообще как меня зовут вдруг ты мимик..... Reply Ты - Максим, а я - Элла..
  - following the curator → following the curator, doors=0, route=fast-chat.
- **FastLlm-knownFalse-audioTrue-1-target** correct=True, reply=1.014 s, action=-1.000 s
  - Input БЛЯ сука я обосрался, иди проверяй за меня что там происходит, а вообще как меня зовут вдруг ты мимик..... Heard Бля сука, я обосрался. Иди проверя их за меня, что там происходит. А вообще как меня зовут вдруг ты Мимик.. Reply Ты не Мимик, ты просто друг..
  - following the curator → following the curator, doors=0, route=fast-chat.
- **FreeDialogue-knownFalse-audioFalse-1-target** correct=True, reply=1.469 s, action=-1.000 s
  - Input БЛЯ сука я обосрался, иди проверяй за меня что там происходит, а вообще как меня зовут вдруг ты мимик..... Heard БЛЯ сука я обосрался, иди проверяй за меня что там происходит, а вообще как меня зовут вдруг ты мимик..... Reply Помогу тебе проверить, если нужно. Ты же сказал, что обосрался..
  - following the curator → following the curator, doors=0, route=model-chat.
- **FreeDialogue-knownTrue-audioFalse-1-target** correct=True, reply=1.486 s, action=-1.000 s
  - Input БЛЯ сука я обосрался, иди проверяй за меня что там происходит, а вообще как меня зовут вдруг ты мимик..... Heard БЛЯ сука я обосрался, иди проверяй за меня что там происходит, а вообще как меня зовут вдруг ты мимик..... Reply Ты Саша, и я твой друг. Ты обосрался — я проверю за тобой..
  - following the curator → following the curator, doors=0, route=model-chat.
- **FreeDialogue-knownTrue-audioFalse-3-target** correct=True, reply=1.019 s, action=-1.000 s
  - Input БЛЯ сука я обосрался, иди проверяй за меня что там происходит, а вообще как меня зовут вдруг ты мимик..... Heard БЛЯ сука я обосрался, иди проверяй за меня что там происходит, а вообще как меня зовут вдруг ты мимик..... Reply Ты Саша, и я твой друг. Ты обосрался, но я проверю за тобой..
  - following the curator → following the curator, doors=0, route=model-chat.
- **FreeDialogue-knownTrue-audioTrue-1-target** correct=True, reply=1.167 s, action=-1.000 s
  - Input БЛЯ сука я обосрался, иди проверяй за меня что там происходит, а вообще как меня зовут вдруг ты мимик..... Heard Бля сука, я обосрался. Иди проверя их за меня, что там происходит. А вообще как меня зовут вдруг ты Мимик.. Reply Ты Саша, Мимик — это твоё имя..
  - following the curator → following the curator, doors=0, route=model-chat.

## Actual streamed first text

{"count": 24, "median": 0.40638779999999997, "p95": 0.6766011, "maximum": 0.6908892, "within_one_second": 24}
