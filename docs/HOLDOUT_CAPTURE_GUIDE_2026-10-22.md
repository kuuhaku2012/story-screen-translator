# Held-out OCR sample intake — 2026-10-22

## Why these samples are needed

All manga and video images currently in `artifacts/multimodal-evaluation/inputs` have appeared in earlier evaluations. They are useful for regression checks, but cannot show whether a new page or cutscene will work. The next OCR quality check must use material not used in prompt/model tuning or prior scoring.

## Add files

Place one full manga-page screenshot under `data/evaluation/holdout-inputs/manga/` and five full YouTube player screenshots under `data/evaluation/holdout-inputs/video/`:

- Three frames where an English subtitle is visible.
- Two frames where no subtitle is visible, including at least one with scene movement or player controls.
- Use visibly new moments from the video; the saved frames `video-01/02/03/04/06/07/08` have already been used. Previously sampled subtitle moments were approximately 00:21, 06:56, and 09:13. Do not include translated overlay in the input.
- For manga, use a full page not present in `artifacts/multimodal-evaluation/inputs/` and not the previously tested page23/page172 fixtures. Capture the full page at original size; do not crop individual dialogue bubbles.

Suggested names: `manga/page-new.png`, `video/caption-01.png` … `caption-03.png`, `video/blank-01.png`, `video/blank-02.png`.

A full screenshot is preferable to a subtitle-only crop: the evaluation will create and record the video-player crop locally, then compare it with full-frame OCR. No Groq/API key, game client, or game account is needed.

## Evaluation boundary

Run PP-OCRv6 medium through DirectML locally, record cold-start and warmed latency separately, inspect detection boxes and OCR text, and report false positives, misses, and manga line fragmentation. This intake does not authorize sending images or OCR text to a translation service and does not test overlay timing or game compatibility.
