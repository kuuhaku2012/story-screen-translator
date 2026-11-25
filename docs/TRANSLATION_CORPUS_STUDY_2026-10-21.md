# Controlled 60-case development study

Status: all16 requests complete, 120 outputs structurally audited and manually reviewed; not accepted for production.

## Results

Source SHA256 matches the saved study plan. All60 IDs appeared once per condition with nonempty text and finish reason stop. No429 status observed. No new request was made during assessment.

Single-reviewer, non-blinded development grading: original41/60 usable (score2–3), normalized49/60. Normalized improves12 cases, regresses3, equal score45. These are explicit subjective annotations in `semantic-review.json`/`.tsv`, not an automatic semantic accuracy measure or held-out result. Both fail the planned95% usable threshold; normalized retains material errors.

Core findings: normalization fixes repeated hearing→feeling errors and some malformed manga text, restores indifference, but romantic idiom remains literal and the dying-behind-my-back sentence remains wrong. Context-dependent `I'll stay dry` remains literal. Intelligence network remains a network of intellect instead of information. Organization/place names Northern Alliance, Glass Harbor and Ashen Tower are translated in both conditions. This helper preserved source names but did not use the app's glossary placeholder protection; it is not evidence that the production glossary was tested or failed.

Regressions include we→I in the beer request, loss of `than I ever could` in a subtitle, and omission of waiting in a choice sentence. Some equal-score sentences still change style; equal score is not identical output.

API batch median: original915.5ms, normalized923ms; nearest-rank p95 on only8 batches is1195ms and2765ms respectively. Group sizes are4–8, so this is not per-subtitle latency and excludes limiter waits, OCR, capture and rendering. No speed benefit established.

Decision: retain normalized text as an experimental candidate only. Next independent gate is to test existing glossary placeholder protection with organization/place names and causal same-scene context on ambiguous sentences, keeping source and meaning checks explicit. Do not introduce global lowercasing into production or tune hardcoded translations to these cases. A positive follow-up would still require unseen data and end-to-end measurement. Main app/default/package unchanged by this study.

Command: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/test-qwen-expanded.ps1 -CorpusStudy`.

Same GPT-OSS 120B low reasoning, JSON output, temperature0.1, maximum completion2048 per request, same instruction across both conditions. Sixty original clean transcripts versus their lowercase versions with explicit glossary names restored. Standalone uppercase Roman numeral tokens (including I) and allowlisted acronyms API/OCR/MSIX/HTTP/NATO are preserved. Unknown all-capital names/acronyms cannot be inferred reliably; this transformation remains experimental. Exact case-insensitive text invariance is asserted. No Vietnamese answer, rubric or expected correction is sent to provider.

Eight groups of up to eight items; both conditions for each group, original-first for odd groups and normalized-first for even groups. Maximum16 POST attempts, no retries; rerunning skips attempted groups. Same grouping/order within each pair. The extra instruction says adjacent items may be unrelated and must be translated independently. Thus this is not a causal subtitle context study. Some original lowercase items differ little or not at all and are controls; do not claim all60 items test uppercase conversion equally.

Before network, validate60 unique IDs per condition, preservation sample, casing-only change, no images, reset-duration parser. At execution store study plan, source SHA256 and group IDs. Each response must have exactly the expected IDs and nonempty translations. Save response and allowlisted limit headers without credentials. HTTP errors stop; budget-aware waiting can take minutes across the experiment and is not provider latency. First request cannot know current account budget and 429 cannot be guaranteed absent. Each completion cap is2048; maximum allowed completion budget over16 requests is32768 tokens, not expected usage or price.

Outputs: `artifacts/translation-corpus-study`. Manual semantic scoring uses the existing0–3 rubric, critical meaning/name flags; compare both conditions and count regressions as well as improvements. Report incomplete batches and unscored items. Keep all60 cases development-only (42 inspected supplied-fixture transcripts,18 synthetic). No held-out or blind acceptance claim. Passing this experiment supports broader unseen testing, not immediate app integration. No main application/source/model default/package is changed by the study helper.
