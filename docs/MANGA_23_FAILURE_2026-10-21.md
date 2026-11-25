# New-page failure audit

User's new page and live overlay photo demonstrate that the current application is not acceptable for manga. Previous cached Rapid-line geometry results did not validate the Windows OCR pipeline actually shipped in the app. The app should not have been presented as an established manga improvement based on that proxy alone.

## New offline reproduction

`scripts/evaluate-manga-23.py` copies only the supplied original page into `artifacts/manga-regression-23`. No new screen capture, API call or image upload. Runs the existing local Rapid v6 configuration, two CPU threads, on this page three times: 2103.78, 1363.31, 1263.24 ms; grouping 3.025 ms. Against 17 manually transcribed approximate text rectangles: 11 normalized exact transcripts, no missing reference, no split reference or cross-reference group by center-membership proxy. Annotated `grouped.png` was visually inspected. This is a different OCR backend from the app and not a live app success.

Several phrases remain damaged (`I'll stay dry`, `DON'T`, `GUESS WHAT`, `WHAT DID SHE WANT`, blood supply, handwritten Hi). Approximate scoring rectangles may omit edge text; normalized exact counts must not be described as exact semantic accuracy or complete page recall. Coordinates are used for evaluation only, not injected into detection/grouping. Page number and standalone question mark are outside the chosen transcription set.

## Code findings

- WindowsOcrEngine still feeds Windows word boxes into the integrated grouping. Missing or corrupt words cannot be recovered by grouping alone.
- SpatialPanelLayout grows cards to a minimum width of 90 capture pixels and larger heights, bounded partly by other source regions. This can obscure nearby art and does not detect speech-bubble boundaries.
- TranslationOverlay sets text trimming and clips the text/card after shrinking font to 11. Inadequate card space can therefore leave partial English/translated output visually truncated. This does not by itself prove a coordinate-transform error.
- Prior clean-text experiments already showed material translation errors independently of OCR. Changing recognition alone cannot establish acceptable Vietnamese translation.

## Decision

No new package was built or claimed fixed in this audit. No provider request was sent. Do not patch more sample-specific positions/words or reuse perfect scores on 22 known regions as product acceptance. Further implementation needs the actual app to use a measured manga recognizer, a distinct card-fitting/bubble strategy with visible overflow handling, and clean-text translation quality acceptance. Evaluate both new and previous pages on the same actual end-to-end path before another user trial. The measured full-page local OCR remains over one second before translation, so fast scrolling/cutscene readiness is not demonstrated.
