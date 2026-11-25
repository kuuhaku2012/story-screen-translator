# Recognition inside fused regions

## Scope

`scripts/evaluate-fused-region-ocr.py` uses the five saved development pages, including latest actual Windows capture. Existing local PP-OCRv6 models,2CPUthreads. No API, image upload, new capture or model download. Baseline whole-page OCR versus automatic fused-region crops. Region detection uses independent bubble proposals plus fresh detector-only line boxes and final fusion helper. Crop padding2px; scale targets32px estimated line height, capped3. No manual labels choose regions/scale/output.

Recognizes every proposed crop with detector/classifier/recognizer, maps detected coordinates back to original source pixels, saves transcripts and timing. This intentionally simple path repeats detection within crops and is not the final efficient recognizer architecture. Explicitly set use_rec/use_cls/use_det each recognition call: initial harness hit RapidOCR flag persistence after a detector-only call; corrected before collecting reported results.

## Results

55 approximate reference regions, same center-associated transcript scoring for both paths; letter/digit normalization ignores punctuation/spacing/case. One warmed observation/page per path after excluded engine warmup. Reference boxes score outputs only.

| Metric | Full-page | Proposed-region OCR |
|---|---:|---:|
| Normalized exact region transcripts |35/55|39/55|
| Character error rate |4.05%|3.25%|

15 regions reduce character errors,10 regress,30 unchanged. Four net extra exact transcripts do not mean only four regions changed or no regressions. The latest source differs from earlier page23 fixture and scoring runs; do not compare CER values across those studies as identical conditions. No held-out or independent annotation review.

| Page | Exact full-page | Exact region crops | Detector+fusion+all-crop recognition |
|---|---:|---:|---:|
|Actual page23|11/17|12/17|2254.59ms|
|172|8/16|9/16|2042.72ms|
|manga001|5/6|6/6|1098.34ms|
|manga002|4/6|5/6|1346.56ms|
|manga005|7/10|7/10|2320.10ms|

Timing excludes cached contour-proposal computation (~milliseconds reported separately), model initialization, capture, stability/debounce, provider/cooldown and overlay. It is not live end-to-end latency or a p95. Recognition received empty detector output on some small crops; that is a genuine failure, not omitted from the transcripts.

## Failure review

Page23 recovers GUESS WHAT/WHAT DID SHE WANT/NO CLUE more completely, but I'M SERIOUS and HUH WHY regress; handwriting can disappear. Region scale/crop changes alter recognizer results. Manga005 adds garbage AWLN after an otherwise correct loses sentence: larger crop includes non-text that detector/recognizer interprets as extra text. Page172 some captions improve while others lose/change letters. Bounding more area is therefore not synonymous with better recognition.

## Decision

Keep fused geometry as a promising proposal generator; do not replace current OCR with this all-crop path as a solved solution.39/55 exact (~70.9%) is below90% goal, CER3.25% also above3%, and total preprocessing/recognition already consumes most or more than the intended end-to-end budget. No main-app/default/package change.

Next candidate: reuse original detector quadrilaterals and batch recognition of line crops, avoiding repeated detector calls and filtering recognition to established text support. Measure whether grouping preserves all lines and whether a fixed, bounded fallback improves difficult small/italic text without adding noise. Validate alternative recognition configuration only after holding detection/crop inputs constant. Do not select correct words by gold references or combine outputs using an oracle. Translation semantic gate remains unresolved.

Outputs: `artifacts/fused-region-ocr/preview.html`, per-page full unchanged transcripts/crops/scales/times and `summary.json`.
