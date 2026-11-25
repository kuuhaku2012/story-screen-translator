# Bubble/text fusion development result

## Implemented

`scripts/bubble_text_regions.py` combines independent bright-region proposals with PP-OCRv6 detector-only line boxes. Assign each line to the smallest proposal covering at least80% of its rectangle; keep unassigned lines in a floating-text path. Remove bubble proposals without line support. Within each bucket, reciprocal line grouping separates spatial clusters; use full bubble rectangle only for a single cluster, otherwise keep separate cluster boxes. No source/reference text or manual reference coordinates feed the fusion. The detector supplies evidence of text-like shapes, not recognized words or guaranteed actual text.

Two offline unit checks pass: remove unsupported art; split two distant text clusters in a connected shape while preserving an uncontained line. Real-data script `scripts/evaluate-bubble-text-fusion.py` ran detector+fusion3 times on each of five stored development images, local models,2CPUthreads. No recognizer/provider/key/new image capture/download. Cached bubble-proposal preprocessing runtime is excluded; also excludes OCR recognition/translation/rendering.

| Page | Raw bubbles | Unsupported proposals removed | Final regions | Last detector+fusion run |
|---|---:|---:|---:|---:|
|Actual page23|23|10|19|334.42ms|
|172|8|3|20|386.79ms|
|manga001|34|25|11|342.91ms|
|manga002|25|18|11|339.02ms|
|manga005|23|8|17|369.39ms|

Numbers of removed proposals are not counts of correctly rejected art; if text detector misses real words, a real bubble can be removed. Region counts are not precision/recall. Timing is descriptive, not live pipeline latency.

## Visual review

Inspected actual-page23 and page172 annotated images. Actual-page23 removes previous face/seat false positives and separates WANNA/HEY into two clusters, OH/WHAT into two clusters, while retaining handwritten dry/Hi outside balloons. The page number and standalone question mark also remain text detections. No recognition/transcript quality is established by these boxes.

Page172 still has a large bright-background proposal classified as a single bubble, covering art; some floating captions remain split, and small background text creates unwanted regions. Text-detector support alone is insufficient to distinguish bright background from a true balloon. Applying the retained full rectangles as overlay masks would still be wrong. This new-page improvement does not justify a universal default.

## Output and decision

`artifacts/bubble-text-fusion/preview.html`, PNGs and `assessment.json`. Green=single cluster with bright-region boundary, blue=cluster separated within connected bright region, orange=floating text. Geometric proposals only, not completed translation.

No main-app/package change. Candidate remains experimental. Before integration, distinguish actual balloon contours from large art/background components and evaluate caption grouping on independently labeled/unseen pages. Then measure recognition of candidate crops plus geometry false positives and total latency, rather than adopting boxes merely because one page looks good. Previously failed OCR/translating quality gates remain in force.
