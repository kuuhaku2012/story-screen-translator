# Qwen expanded evaluation

Status: partial run reviewed. Clean manga completed; OCR manga returned HTTP 429. Subtitle batches were not attempted. No new API calls during review.

## Results

- Clean manga: 22/22 unique expected IDs, finish reason `stop`, 2,279 ms for one batch. This is API batch time, not per-sentence or end-to-end latency.
- Actual OCR manga: HTTP 429, no translation response saved. The runner stopped as designed; no automatic retry.
- Clean/OCR subtitles: not attempted after the rate limit response. No cutscene translation result or latency can be inferred.

Manual review of the clean-input response found material errors independent of OCR:

| ID | Problem |
|---|---|
| m2-listening | `ARE YOU LISTENING, KABURA?` became a question asking whether someone is listening to Kabura. The addressee became the speaker. |
| m2-dorks | Adds an ungrammatical phrase `CHƠI VỚI ĐỨNG NÀY NHỮNG KẺ NGỐC`. |
| m2-haru | Repeats `MÌNH MÌNH MÌNH`; fails to express the accusation naturally. |
| m5-gripes | Outputs the meaningless `LỜI CAU BÈN` for complaints/gripes. |
| m5-loses | Literal `RƠI SÂU NHẤT` loses the romantic meaning of falling hardest in context. |
| m5-back | Literal `CHẾT SAU LƯNG TÔI` does not convey the complaint naturally. |
| m5-haru | `NARCISSISTIC TENDENCIES` became `SỰ TỰ LỢI`, changing narcissism/self-absorption to self-interest. |

The simple greetings and several straightforward sentences are usable. Expected character names remain present case-insensitively; this is not a general name-preservation guarantee. Frequent use of `mày`, `chúng nó`, `bà ta` adds a relationship/tone not established by the text-only request.

Decision: **not accepted for production**. The earlier provisional ranking on 14 items did not generalize to this expanded sample. Better OCR/grouping remains necessary, but cannot fix these clean-text translation errors. Do not spend additional API calls retrying the same failed model assessment merely to fill the table. A subsequent experiment should evaluate a different translation strategy and rate-limit scheduling with a small, causal subtitle workload before any main-app integration. No main app/model defaults/packages were changed.

The HTTP status alone cannot distinguish a request/token window limit from a longer account quota. This helper did not persist response limit headers, so no reset time is asserted.

## Scope

Reuse saved references and OCR records from `artifacts/multimodal-evaluation`, without downloading media or capturing new screens. Four calls maximum to `qwen/qwen3.8-27b`: 22 clean manga items, the same 22 actual Rapid v6 OCR items, 3 clean subtitle samples, the same 3 Windows OCR 2x samples. No images are sent. Credentials are entered through SecureString and not written to disk. Each batch has an attempt marker written before the POST; reruns skip attempted batches. HTTP failures stop the run without retries; raw error bodies are not saved.

## Interpretation

Compare manual reference, actual input and unchanged translation by ID. Review viewpoint, idioms, omissions and proper names manually. Valid JSON/IDs are structural checks, not semantic accuracy. No hardcoded corrections or expected Vietnamese translations are supplied to the model.

OCR text was associated with manually defined reference regions in the previous evaluation. This does not validate automatic speech-bubble grouping. Some manga dialogue overlaps earlier experiments, so this is not a wholly unseen test set. Three positive subtitle samples are insufficient to establish general cutscene quality. Batch context includes multiple scenes and differs from causal live subtitle context. API batch timing excludes capture, stability, OCR, scheduling and overlay, and cannot establish live latency.

## Run

`powershell.exe -NoProfile -ExecutionPolicy Bypass -File E:\tool_dich_man_hinh\scripts\test-qwen-expanded.ps1`

Outputs: `artifacts/qwen-expanded/<batch>/request-body.json`, `attempt.json`, `response.json`, `status.json`; combined `comparison.html`. Main app/provider/package are unchanged. Review the results before deciding any production model or grouping change.
