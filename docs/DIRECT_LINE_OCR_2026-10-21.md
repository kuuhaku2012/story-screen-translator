# Direct-line recognition experiment

## Implementation

`scripts/evaluate-direct-line-ocr.py`: run detector once per page; perspective-crop original source quadrilaterals using installed RapidOCR's own crop method; batch orientation classification and text recognition; restore original line ordering; apply explicit text-score threshold; assign recognized lines to fused groups once to avoid duplicate emission. No crop-level redetection. Fusion still uses the geometric proposals independently of recognized vocabulary.

Inspected installed RapidOCR code for TextRecInput/list batching, ordering restoration, crop mapping and flag persistence. Calls internal package methods `crop_text_regions`, `cls_and_rotate`, `recognize_txt`; this is a version-specific experiment, not a supported app adapter or packaging commitment. Dataset/score definitions remain approximate center-associated55 development regions; scores are for recognized lines, while assembled group transcripts are also saved for manual inspection. No held-out/live acceptance.

## Results

Three warmed observations per page after excluded warmup,2CPUthreads. Local existing models only, no API/download/game/new capture. Detector settings/path and source images held constant when comparing recognition variants. Source SHA256 saved per page. No claim that every input was an independently unseen sample or that timing conditions/background load were controlled.

| Path | Exact normalized transcripts | CER |
|---|---:|---:|
| Prior whole-region crop/re-detect/recognize |39/55|3.25%|
| Detect once + direct v6 recognition |35/55|4.05%|
| Same detector settings + English v5 mobile recognizer |25/55|10.71%|

Direct v6 timing ranges across three warmed observations/page:

| Page | Direct v6 ms | Prior crop pipeline ms, one separate run |
|---|---:|---:|
|actual-page23|1288–1407|2255|
|172|1673–1732|2043|
|manga001|921–979|1098|
|manga002|1043–1126|1347|
|manga005|1548–1753|2320|

The English recognizer was not faster on these runs (~969–1920ms overall range) and had worse text quality. Excludes contour proposal preparation, capture/debounce, translation/cooldown and rendering. Prior crop timing was a separate single run; do not present a precise statistically established speedup or live p95.

## Interpretation and decision

Avoiding crop redetection is a useful architectural simplification and recovers the full-page baseline quality, but loses crop-resampling gains on some words. It does not solve low-resolution/stylized-font recognition. English-only existing model is rejected as a replacement on this sample. Do not promise that fewer detector calls alone improves semantic accuracy or satisfies cutscene latency.

Keep fused geometry and direct-line v6 as experimental baseline; both55-region quality gates still fail. Future fallback must be bounded and independently improve errors without oracle selection; prior confidence-only fallback and all-crop paths have known limitations. If the next candidate still fails, change recognizer/data approach instead of repeatedly tuning resize thresholds on these same pages. No source app/default/package changed in this task; no additional API call requested from the user.

Outputs: `artifacts/direct-line-ocr/preview.html`, `artifacts/direct-line-ocr-english/preview.html`, per-page transcripts/quad records/times/source hashes and summary JSONs.
