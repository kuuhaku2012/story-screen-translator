# Offline manga grouping result

## Scope and outcome

Replayed saved Rapid v6 line boxes on three manga pages at 1200px and 730px. This yields 44 reference observations of 22 distinct text regions, not 44 independent samples. No API calls, model downloads, new capture or game activity.

| Method | Split reference observations | Missing references | Regions touching multiple references |
|---|---:|---:|---:|
| Saved production comic grouping | 36/44 | 0 | 0 |
| Existing reciprocal-neighbor prototype | 2/44 | 0 | 2 |
| Prototype with heading guard | 0/44 | 0 | 0 |

These are center-membership geometry proxies against reference boxes. They do not measure correct OCR text, translation quality, exact bubble boundaries, or false positives outside annotated regions. References are used only in scoring; no vocabulary, page coordinates or gold text is supplied to the grouping function. Selected visual inspection found the wide chapter heading was chaining to short dialogue and covering neighboring bubbles. The new guard rejects a large width jump only when the wider line is also very elongated. A width-ratio-only guard incorrectly split a short `I` from `KNOW`; it was narrowed before accepting the prototype change.

Three synthetic checks first reproduced the heading failure; after the fix they passed. A fourth check protects short pronoun joining. Scope remains an experimental Python grouping helper; the WPF/Core production grouping and MSIX packages were not changed. Better geometry does not fix the clean-text translation failures documented in earlier experiments.

## Artifacts and reproducibility

- `scripts/manga_line_regions.py`: reciprocal-neighbor prototype, now rejects heading scale jumps.
- `scripts/test_manga_line_regions.py`: synthetic heading, overlap, columns and short-word checks.
- `scripts/evaluate-line-grouping.py`: offline scoring and annotated images.
- `artifacts/line-grouping/baseline/`: prior prototype behavior.
- `artifacts/line-grouping/width-guard/`: intermediate candidate; short-pronoun regression found visually.
- `artifacts/line-grouping/heading-guard/`: final candidate report and annotated images.

The evaluator uses cached OCR and saved production layout, not a freshly built app. Grouping alone takes only a few milliseconds on these pages in a single run; OCR, capture, translation and rendering are excluded and no warmed latency benchmark is asserted. This result supports porting and evaluating the candidate in the app, not claiming the app has already improved.
