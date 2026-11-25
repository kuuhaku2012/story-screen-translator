# Focused GPT-OSS 120B experiment

Status: all four requests completed and were reviewed. No HTTP 429 in this small run. Not accepted for production.

## Actual results

All nine expected IDs returned once, with nonempty text and finish reason `stop`.

| Request | API time | Manual semantic findings |
|---|---:|---|
| Six clean manga sentences | 1428 ms | Kabura remains the addressee, but several other difficult sentences fail. |
| video-01-roi | 749 ms | Awkward/ungrammatical `rung rắc băng nặng`; umbrella ribs becomes generic frame. |
| video-06-roi | 730 ms | Broad meaning preserved; acceptable on this sample. |
| video-07-roi | 724 ms | `than I ever could` becomes `hơn bao giờ hết`, dropping comparison with the speaker's ability. |

Material manga errors include:

- `m5-loses`: `NGƯỜI NGUỘT MẠNH NHẤT LUÔN THẤU THUỐC` is meaningless Vietnamese and loses the romantic idiom.
- `m5-back`: adds `TRƯỚC MẶT TÔI` (in front of me), contrary to the original `behind my back`; the whole sentence is unnatural.
- `m5-crap`: `TÔI KHÔNG THỂ BỎ QUÊN ĐI ĐIỀU ĐÓ` changes indifference into inability to forget.
- `m5-gripes`: `PHẢN ÁNH` and repeated `MỌI LẦN` do not express repeated complaints naturally.
- `m5-haru`: `TÍNH TỰ YÊU` is awkward and `boils down to` is weakened to `begins with`.

This is manual review, not an automatic accuracy percentage. Haru/Kabura strings were retained case-insensitively; these few occurrences do not establish general proper-name preservation. Only text was sent. No further requests were made during review.

The three subtitle API times range from 724 to 749 ms. They exclude the helper's minimum two-second inter-request waits and all capture/OCR/stability/overlay stages. This is not evidence of sustained 0.73-second live subtitle throughput. Four successful requests show this run did not hit 429; they do not prove rate-limit handling under sustained load or validate the exhausted-budget waiting branch.

Decision: keep the main app/model/package unchanged. GPT-OSS 120B low reasoning in this configuration also fails the quality gate. Do not represent another blind model swap as a completed solution. The evidence now supports separating translation-provider evaluation from OCR/layout work: improve local grouping against saved references independently, and evaluate any future provider on clean difficult sentences before spending time integrating it. No game/anti-cheat safety conclusions follow from this test.

Run `scripts/test-qwen-expanded.ps1 -Focused120B`. Reuses existing helper rather than adding another API client. Maximum four POST attempts, no retry: six difficult clean manga sentences in one request, then each of three saved clean subtitle samples independently. Uses GPT-OSS 120B, low reasoning, JSON object mode, temperature 0.1, max output 2048 tokens. No images, live capture, game or new dependency.

This compares a candidate strategy, not an isolated model-only ablation: grouping, prompt and sample size differ from the previous Qwen run. Manga samples deliberately contain prior failures and are not held-out evaluation. Some idioms need scene context not available in this text-only request; assess ambiguity honestly. Subtitle requests receive no future text and no dialogue history. They are saved independent samples, not continuous playback. API timing excludes cooldown, OCR, stability, queueing and rendering.

Before subsequent requests, inspect allowlisted remaining/reset headers. Reserve a conservative UTF-8-byte input estimate plus maximum completion budget; this is not exact tokenization. Missing token headers cause a conservative 60-second wait. Exhausted request/token budgets cause waiting at least 60 seconds and the applicable reset plus margin; reset beyond two minutes stops. Header snapshots contain only limit/remaining/reset/retry-after values, no authentication. HTTP errors including 429 stop without automatic retries. First request cannot know an account's current remaining budget; this does not guarantee no 429. Marker files prevent repeated attempts after failures/timeouts.

Outputs: `artifacts/focused-120b/<case>/` and `comparison.html`. Main application is unchanged. Review all results before adopting a model. Success criteria: preserve addressee and names, correct core meaning without invented content, natural Vietnamese, complete IDs. Report per-subtitle latency separately from manga batch latency and account for cooldown when discussing practical throughput.

Official references checked: https://console.groq.com/docs/rate-limits and https://console.groq.com/docs/model/openai/gpt-oss-120b .
