# PP-OCRv6 medium recognizer evaluation — 2026-10-21

## Setup

Compared PP-OCRv6 medium against the current direct-line PP-OCRv6 recognizer on the same five saved development pages. Detector, perspective line crops, crop order, and region association were held constant; the recognizer was the only model change. Source SHA256 values match page by page. Each path used three warmed observations per page. Scores use the existing approximate ROI-center association and are not held-out accuracy measurements.

GPU measurements used the PC's NVIDIA GeForce RTX 3050 Laptop GPU (4 GiB; driver 610.78) and ONNX Runtime DirectML 1.24.4 in an isolated evaluation virtual environment. Both detector and recognizer sessions report `DmlExecutionProvider` with CPU fallback; all 55 recognized transcript strings match the CPU medium run exactly. The DirectML wheel download was 25.1 MB and was not added to the app package. ONNX Runtime currently describes DirectML as sustained engineering and recommends WinML for new Windows deployments; treat this as an experiment path, not the final app integration choice ([DirectML provider guidance](https://onnxruntime.ai/docs/execution-providers/DirectML-ExecutionProvider.html), [Windows/WinML guidance](https://onnxruntime.ai/docs/get-started/with-windows.html)).

The model was downloaded from the official [PaddlePaddle PP-OCRv6 medium ONNX model card](https://huggingface.co/PaddlePaddle/PP-OCRv6_medium_rec_onnx/tree/main), pinned at revision `50c7eacafc52fa7bcf4194e8cd08e46f8558504b`. The ONNX file is 76,554,979 bytes; SHA256 `9c09abf0957f7968c7586464b7397b84ad2387a0497a351af40e9acc71b673ba`. The model card declares Apache-2.0. It is stored under `models/ocr-evaluation/ppocrv6-medium/`; it is not part of the app package.

## Results

“Exact” follows the evaluation script’s normalized transcript match. Timing is the median of the three warmed whole-page detector-plus-recognizer observations. It excludes capture/debounce, cached bubble geometry, translation, cooldown, and overlay rendering.

| Saved page | Direct v6 exact | Medium exact | Medium CPU median | Medium DirectML median | CPU→DML speedup |
|---|---:|---:|---:|---:|---:|
| actual-page23 | 11/17 | 14/17 | 4,371 ms | 579 ms | 7.55x |
| page172 | 8/16 | 9/16 | 5,406 ms | 658 ms | 8.22x |
| manga-001 | 5/6 | 6/6 | 2,517 ms | 387 ms | 6.51x |
| manga-002 | 4/6 | 5/6 | 3,148 ms | 464 ms | 6.79x |
| manga-005 | 7/10 | 8/10 | 5,283 ms | 724 ms | 7.30x |
| **Total** | **35/55** | **42/55** | — | — |

Character error rate fell from 4.05% to 2.86% when upgrading to medium; GPU execution preserves that same 42/55 result and those exact transcript strings. Eight region IDs moved from non-exact to exact, while one moved from exact to non-exact, for a net gain of seven exact transcripts. DirectML reduced the medium model's warmed OCR time to 0.39–0.72 seconds/page, 6.51–8.22x faster than CPU on these pages. It also ran about 2.2–2.6x faster than direct v6 on CPU here. These times still exclude capture, translation, and overlay rendering.

## Error review and decision

The recognition improvement is real on these saved fixtures but uneven: page23 gained three exact lines; page172 gained one net; each other page gained one. GPU acceleration changes speed, not quality: medium still misses or corrupts text, including parts of “I'll stay dry,” “Don't underestimate the intelligence…,” “I've seen them around a lot,” and “and laugh like idiots over nothing.” The page172 “laugh” result still contains spurious CJK characters. The references are approximate regions, so these counts do not measure detector recall, box placement, semantic translation, or visual overlay quality.

**The GPU medium model is a promising speed candidate, but it still fails the OCR quality gate and is not integrated.** Its OCR-only latency is promising for further manga/cutscene evaluation, but it does not yet show that the app can keep up end to end. Next, evaluate a truly held-out manga page and several stable frames from the user-selected YouTube cutscene, then measure OCR-to-overlay latency. Consider app integration only after that quality review; for a new Windows app path, evaluate WinML rather than shipping the experimental DirectML Python environment. No application source package, external translation API, or game screen was used in this experiment.

## Reproducible outputs

- `scripts/evaluate-direct-line-ocr.py --recognizer medium`
- `artifacts/direct-line-ocr-medium/summary.json` and `artifacts/direct-line-ocr-medium/preview.html` (CPU)
- `artifacts/direct-line-ocr-medium-dml/summary.json` and `artifacts/direct-line-ocr-medium-dml/preview.html` (DirectML)
- Per-page transcript, provider, detection, source-hash, and timing JSON files in both output folders

PaddleOCR's published recognition overview describes the available recognition model families and their intended tradeoffs: [official recognition documentation](https://github.com/PaddlePaddle/PaddleOCR/blob/main/docs/version3.x/module_usage/text_recognition.en.md).
