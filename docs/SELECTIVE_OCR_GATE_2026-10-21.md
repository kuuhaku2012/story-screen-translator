# Selective local rerecognition — development gate

## What ran

`scripts/evaluate-selective-ocr.py` on five stored pages,55 reference regions. Existing local PP-OCRv6 models, CPU2 threads, no API/key/image upload or new dependency. One warmed whole-page run per page, after an excluded warmup. Detection and grouping choose crops automatically before reference-based scoring.

Fixed policy before results: choose at most3 groups whose minimum line confidence is below0.90, lowest first. Pad by one quarter median line height (at least3px). Upscale crop to target line height40px, factor capped3. Rerun full local OCR on crop. Accept only if mean confidence improves by at least0.03 and normalized transcript length ratio stays0.65–1.5. No expected word/coordinates select or alter candidates. This policy is an experimental heuristic, not calibrated confidence or proof of correctness.

## Separate text and geometry scoring

Earlier resolution gate used an IoU>=0.3 association, causing some valid small text regions inside loose reference rectangles to score empty. This experiment removes that cut-off for **text scoring**, associating detected line centers with approximate reference rectangles and ordering their actual transcripts. Geometry is reported separately as split/cross-reference counts. This is still not pure recognition-only accuracy: missed detection, misplaced lines and loose reference edges can affect text scores. No polygon-level IoU, complete-page precision/recall, or corrected tight annotations are claimed. Old32/55 and new35/55 baseline are different scoring definitions, not an engine improvement.

| Metric | Baseline | Selective |
|---|---:|---:|
| Whole transcript exact after letter/digit normalization |35/55|39/55|
| Character error rate |3.57%|2.70%|
| Reference regions split across groups |2|2|
| Groups touching multiple references |1|1|
| Reference regions with no detected text |0|0|

Five regions reduced character errors; four became normalized exact. Zero annotated regions increased errors in this run. This excludes errors outside55 reference regions and does not establish absence of regressions generally. Normalization ignores spaces/punctuation/case and does not mean sentence translation is correct.

Examples: `THAS SHEN` recovered `THAT'S WHEN`; `YEAAH` recovered `YEAH`; missing letters in `AND THEY` restored; `WORTH IT` recovered. One page172 sentence still contains an extra letter. Page23 has no scored improvement despite3 automatic retries and2 accepted crop replacements, so the user's newest failure is **not resolved**.

## Time

| Page | Baseline ms | Added rerecognition ms | Total ms |
|---|---:|---:|---:|
|23|1264.60|457.51|1722.11|
|172|1548.41|379.97|1928.38|
|manga001|989.18|170.45|1159.63|
|manga002|991.29|312.99|1304.28|
|manga005|1458.29|188.26|1646.55|

Single warmed observation/page, not p95 or controlled latency benchmark. Excludes capture, stability, translation/network/cooldown, rendering and model startup. Extra crop work already puts total OCR+grouping around1.16–1.93s, so end-to-end manga target remains unproven and cutscene suitability cannot be inferred.

## Decision

CER is promising on this development sample, but only39/55 (70.9%) whole transcripts are exact, below the planned90% gate, with geometry errors remaining. No held-out set or independent label audit. Do not integrate as the default app recognizer or call this a product fix. No app/package/provider changes in this task.

Next implementation boundary: resolve scene/page23 recognition failures with a validated recognizer/crop strategy, calibrating confidence against errors rather than treating a confidence increase as ground truth. Tighten independent region labels before accepting geometry metrics. Compare failures and resource costs across all pages and unseen samples; do not keep increasing upscale or retry count to chase known words. Translation semantic gate remains independent and failed.
