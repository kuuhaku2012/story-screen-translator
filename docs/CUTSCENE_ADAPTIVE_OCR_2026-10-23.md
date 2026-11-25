# Cutscene adaptive OCR trial — 2026-10-23

User live result on previous build: the long caption at 1:14 remains stable; the short “This is my cause” appears late or can be missed; the old translation's brief hold is acceptable. Saved fullscreen frames show native-size Windows OCR recognizes the short caption at 1:14 in roughly 123–158 ms after warmup, versus about 285–472 ms for unconditional 2× OCR on that frame. Native-size OCR misses the subtitle at 2:13, so an upscale fallback is still required.

Change: in cutscene mode, run native-size Windows OCR first and run 2× Windows OCR only when the selected subtitle zone is empty. Cutscene capture target interval is 250 ms instead of 500 ms, while a slow OCR pass can still set the effective interval. The manga OCR path is unchanged. Only screen pixels are read; only selected OCR text goes to Groq; no game process or account access.

Focused adaptive OCR tests: 2/2 passed. Complete suite: 137/137 passed. Self-contained MSIX packaging succeeded at `artifacts/cutscene-adaptive-ocr-trial-20261023/StoryTranslator-1.0.0.0-x64-self-contained.msix` (71,469,998 bytes). A temporary registration opened from that layout and the process path was verified.

User live result on this build: the short 1:14 caption is translated, but still late; the previously missed caption at 2:13 remains translated. Therefore the OCR fallback preserves coverage, but end-to-end speed is not yet sufficient for very short dialogue. The remaining delay has not been measured separately in this live session; prior Groq text calls took roughly 0.9–2.1 seconds on limited samples, which is a plausible bottleneck, not a measurement of this exact run. Do not claim a cutscene latency pass or guaranteed real-time translation. Next diagnostic should measure first observed subtitle → OCR → request wait/response → overlay timing without saving OCR text, screenshots, or credentials, then choose whether provider/model streaming or a faster translation path is justified.

## Timed live follow-up

User used default `openai/gpt-oss-20b`, replayed the requested segment, then closed the timing build. The timing-only JSON is at `data/cutscene-timing/cutscene-timing-20261022-200909-72ac7300c2be4709b65445549e7a8a37.json`; it contains UTC timestamps, stage labels, and milliseconds only. It has no key, OCR text, image, or translation. The session includes more than the six-second segment, so these are session aggregates, not an exact 1:14 trace.

| Stage | Samples | Median ms | Maximum ms |
| --- | ---: | ---: | ---: |
| Native Windows OCR | 181 | 55.0 | 151.5 |
| 2× OCR fallback | 40 | 190.6 | 384.2 |
| Groq response | 22 | 698.1 | 1662.5 |
| Subtitle observed → translation assigned | 19 | 1693.4 | 2102.3 |

A translation batch can assign several blocks at once, so 19 assigned-block samples are not 19 independent HTTP calls. The recorder's `groq-wait` measures the translator's internal wait after the pipeline's earlier rate-limit gate; its zero values do not prove there was no preceding queue or limiter wait. The gap between response and observed-to-translation time is consistent with sequential queueing and batch behavior but is not a per-caption attribution. OCR is no longer the dominant median cost. Further OCR tuning alone cannot make a subsecond caption reliably appear before the source disappears. A streaming first-token diagnostic with one existing OCR text sample would be the next bounded experiment before integrating a streaming overlay.

## Streaming decision

Groq's current Chat Completions documentation supports `stream: true` and incremental `delta.content` for plain text. It would allow an early partial overlay from one request, but the remaining problematic caption is a short sentence; the measured total response median is under 0.7 s for the whole request. Without a measured first-content-token gap, there is no evidence that streaming would save enough time to change this case, while partial overlays could reintroduce the flicker the user previously rejected. Do not switch the production path based on documentation alone. A bounded first-token experiment is optional; priority is accurate per-caption attribution and a translation path whose worst-case latency is shorter than the displayed subtitle.
