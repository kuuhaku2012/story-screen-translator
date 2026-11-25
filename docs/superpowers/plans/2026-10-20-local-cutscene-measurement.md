# Local cutscene translation — measurement first

Goal: evaluate fully local EN→VI translation before integrating it into the app.

User decision: local model; downloading models and measuring resources authorized. CPU first on i5-11400H, GPU kept available to game. No game/client/process access, no provider key access.

1. Download official Argos en_vi 1.9 package from its public index. Install only CTranslate2, SentencePiece, NumPy and psutil into project-owned Python venv. No global runtime/package changes. Record model hash/source and package versions.
2. Benchmark resident INT8 CPU translator, beam 1 and 2, 2 threads. Measure cold load, warm per-sentence wall time (p50/p95), process CPU time, peak sampled RSS and process peak working set. Use a fixed, public dialogue set based on supplied manga and one visible video subtitle. Output translations alongside source; human quality comparison is required. No live game/FPS claim.
3. Gate: do not switch app to this engine unless latency and translation quality are acceptable. Aiming for warm p95 <= 1 second for a single short subtitle is an evaluation target, not a promise. Translation timing excludes capture/OCR/overlay.
4. OCR remains separate: compare a lightweight English PP-OCR model against Windows OCR on the supplied examples; a faster translator cannot repair missing OCR. Do not treat whole-page manga success as equivalent to game subtitles.
5. After model selection, implement a persistent local worker over stdin/stdout, no listening network port; add subtitle ROI mode with whole-sentence grouping, short stability window, latest-only pending sentence, stale-result rejection and retained overlay. Do not send OCR to cloud in local mode. Protect known proper names via glossary and verify output preservation.
6. Measure full pipeline on the supplied YouTube cutscene at normal playback before any game evaluation. Proposed acceptance: 95% of chosen subtitle sentences displayed within 1.5 seconds, no stale subtitle after replacement, no redraw flicker on unchanged text. Record missed sentences and actual resource use; if unmet, report failure and revise.

Sources: https://github.com/argosopentech/argospm-index ; https://opennmt.net/CTranslate2/performance.html ; https://github.com/RapidAI/RapidOCR .

Measurement update (2026-10-20): Argos, official Helsinki OPUS-MT, Meta NLLB-600M INT8, Qwen2.5 0.5B/1.5B CPU and Qwen3-4B GPU were evaluated for local EN→VI. OPUS is fast but fails idiom/context checks; larger Qwen3-4B still fails difficult dialogue and occupies 2.76 GiB of the RTX 3050's 4 GiB. See docs/LOCAL_TRANSLATION_EVALUATION.md. The comic-text-detector candidate finds most sampled text regions, but takes about 8.2 seconds per page and is unsuitable for scrolling. See docs/EVALUATION_MANGA_CUTSCENE_2026-10-20.md. Integration gate remains closed; no local provider or detector was enabled in the main app. The next product decision is whether an optional online translation provider is acceptable; keep OCR/capture/overlay local and limit translation to stable, new text with caching, bounded batching and cooldown for 429 responses.
