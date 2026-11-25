# Comic grouping integration

## Implemented

Ported the reciprocal nearest-line algorithm and elongated-heading guard from the Python prototype into `SpatialTextLayout.Group` when `comicMode` is enabled. Preserves source text, joins overlapping centered lines, keeps separate columns, and attaches a contained orphan word to a multiline group. Non-comic grouping remains on its existing path. Windows OCR and translation model/provider were not replaced.

## Verification

- Before implementation, new Core test `ComicOverlappingCenteredLinesStayTogether` failed against the previous implementation (1 failed, 5 passed in the filtered suite).
- After implementation, full Core suite: **115 passed, 0 failed**.
- Ran the real Core grouping via `scripts/OcrLayoutEvaluation` on 84 mode/input combinations. Saved separately to `artifacts/line-grouping/integrated-production.json` to preserve historical baseline.
- Scored three manga pages at two sizes: 44 reference observations of 22 distinct regions; integrated Core and Python each had 0 split references, 0 missing references, 0 cross-reference regions. Baseline Core had 36 split observations. Scoring uses reference-center membership and does not validate exact bubble boundaries, missed text, or unannotated regions.
- Important: those replay measurements use cached Rapid v6 line boxes. The actual app still uses Windows OCR; its poorer manga recognition can still affect grouping and translation.
- Both unsigned MSIX packages built successfully. Framework-dependent: 7,319,954 bytes, SHA256 `8F68B4DBB71339F81FF82BE0E0E3FDEB23A4BC3CB363BD27169AB1CA424F443F`. Self-contained trial: 71,458,547 bytes, SHA256 `8CDAB83320D75D52A21C7EDBF35F1AC9A22E0E88E2922C3F67DF2221A7499891`.

## Live display verification pending

Update: launcher recovery now checks that an existing registration is a development package at the exact project trial layout and that no StoryTranslator.App process is running before removing a leftover registration. Other installed packages/locations and active app sessions are refused. Retried `-SpatialSmokeTest`: PASS, recognized the synthetic text and removed its temporary registration successfully. This verifies the synthetic Windows OCR/overlay smoke path, not real manga translation quality.

Attempted `scripts/run-screen-trial.ps1 -SpatialSmokeTest`; it stopped before activation because the machine already has this application's package registration. Existing registration was not removed or changed. No claim of successful live display verification is made. Close the prior trial application and its owning trial console so its normal cleanup can run; then rerun the smoke test or launch `Thu-dich-man-hinh.cmd` and enable comic mode for manga. No API request/key access, game run, new certificate or Developer Mode change was performed in this integration.

If the registration persists after normal cleanup, inspect its ownership/location before deciding any removal; do not remove an unrelated user's installed package merely to run the smoke test.
