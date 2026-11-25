# Manga OCR resolution gate

## Scope

Executed `scripts/evaluate-manga-resolution.py` on five stored development pages: page23, page172, manga001/002/005 at730px. 55 annotated text regions. Same existing PP-OCRv6 detector/recognizer/classifier paths,2 CPU threads, no new model, API call, screenshot or game. Whole pages go into OCR; manual coordinates are scoring-only. Each configuration ran three passes sequentially; first omitted from warmed timing.

## Results

| Configuration | Matched region transcript exact | CER including unmatched references | Warm page processing range |
|---|---:|---:|---:|
| Native image, detector max960 |32/55 (58.18%)|8.41%|0.84–2.19s|
| Image2x, detector max960 |28/55 (50.91%)|10.95%|0.84–1.71s|
| Image2x, detector max1440 |30/55 (54.55%)|10.87%|1.33–2.29s|

These are **combined detection/grouping/recognition proxy scores**, not pure recognizer accuracy: one-to-one greedy geometric association requires IoU>=0.3 against approximate manual rectangles. A recognized small text box inside a loose reference can be unmatched and scored empty. Inspection found this in some manga001 cases, so do not describe these numbers as the percentage of text the recognizer literally missed. This gate does not replace tighter polygon annotation or held-out validation. It is not directly comparable to previous center-membership scores or hand-cropped OCR results.

Concrete transcription failures on page23 remain dry→dhuy, DON'T→DOOT, WHAT DID SHE→garbled words, blood-supply phrase omissions, handwritten Hi→i. Source references and every auto group/transcript are saved per page for audit. None of the three configurations is established as meeting the planned combined product gates. Larger processing resolution does not reliably improve these fixtures, so do not blindly ship2x as a fix.

Time includes resize, OCR, grouping and reconstruction of group text, excludes model load, capture, stability debounce, translation, quota waits and rendering. Background machine load not controlled; only10 warmed timings per configuration, no production p95 or live scrolling claim.

## Decision and next implementation boundary

No app OCR backend, UI default, package, user glossary or API setting changed. Plan Task3 requires passing the OCR quality/resource gate before making a new backend default; this work does not meet that gate.

Before more whole-page resolution variants, improve the evaluator's reference polygons and separate recognition-only from region-recall/IoU metrics. Then evaluate selective rerecognition of automatically detected groups using local crops, with a fixed trigger based only on confidence/geometry (not gold text), reporting additional latency and regressions. References must never choose crops or replacement words at runtime. If no fixed strategy improves held-out accuracy within resource targets, reject this engine configuration rather than adding page-specific patches. Existing failed translation gate also remains independent.
