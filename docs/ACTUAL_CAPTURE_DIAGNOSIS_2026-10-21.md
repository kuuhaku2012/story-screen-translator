# Actual capture diagnosis

Source: user-initiated diagnostic folder `artifacts/ocr-diagnostics/20261021-152634-339b4861a2dd43db83293e1f7b83eb0b`. Selected capture662x919 physical pixels at desktop origin647,95. Comic mode enabled. PNG inspected: clear manga page, no visible control window or translation cards contaminating capture. This supports correct input isolation for this sample only.

## Findings

1. Windows OCR reports180.18ms but corrupts and omits text: I'M SERIOUS→sszoug; DON'T PLAY DUMB WITH ME loses ME and changes DUMB; many other balloons incomplete. Result has16 groups including page number; group count is not detected-balloon recall.
2. Layout predicts15/16 cards at minimum width90px. MY source19x10 becomes90x32 (~15.2 times area), and DON'T block40x52 grows90x78.2. This is actual layout expansion from code, not evidence of a global coordinate offset.
3. Source boxes broadly sit around visible source text. Diagnostic does not measure live WPF window/DPI rendering; no definitive claim that DPI is correct or faulty.

## Same-pixel Rapid replay

`scripts/replay-ocr-diagnostic.py` used exact stored PNG and existing local PP-OCRv6 models,2CPU threads. No additional screen capture, API/image upload or model download. Full OCR+grouping times2224.97,1360.54,1295.24ms; first run includes lazy initialization. Windows timing excludes panel layout, so compare scope carefully.

Rapid recovers DON'T PLAY DUMB WITH ME, I'M SERIOUS, WANNA GO FOR A DRINK and the detective sentence. It still corrupts the homework opening, Azami/town, GUESS WHAT, WHAT DID SHE WANT, blood supply and NO CLUE, and emits Chinese characters for small handwritten text.18 groups are not18 correct balloons. No accuracy percentage is claimed without a new aligned transcription audit.

Comparison at `rapid-replay/comparison.html`, raw records at `rapid-replay/assessment.json`. IDs are local to each engine, not aligned equivalents. UI file opening was queued by Codex; the report can be opened manually.

## Decision

Two independently evidenced failures: recognition and oversized card geometry. Translation model failures remain independent from earlier clean-text experiments. Swapping to this Rapid configuration alone is insufficient and materially slower; do not present it as a fix. Do not patch coordinates by a guessed global offset. Next implementation may address card fitting independently, then evaluate another recognizer configuration on this exact source plus earlier/unseen pages with the same protocol. App source/package unchanged in this review.
