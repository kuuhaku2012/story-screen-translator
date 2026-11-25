# Independent bubble-region proposals

## Scope

Implemented an offline prototype using OpenCV bright enclosed components, shape filters and enclosed dark-component statistics. No Windows/Rapid OCR box, manual rectangle, reference text or page-specific coordinate participates in detection. Existing five stored development images only, no model/dependency download, API/key, new capture or game. Three detector runs/page, includes preprocessing/contour filtering; not OCR/translation latency.

Outputs: `artifacts/bubble-proposals/preview.html`, annotated PNGs and raw `assessment.json`; script `scripts/evaluate-bubble-proposals.py`. This is separate from production source/packages.

## Observations

On actual page23,23 proposed regions in about8ms for the last run. Visual inspection confirms closed balloons for homework, DON'T PLAY DUMB, I'M SERIOUS, NAZUNA, detective and blood-supply dialogue are proposed independently of the missing/corrupt Windows words. However, artwork such as face, shirt, wall/seat regions is also proposed. The connected WANNA/HEY balloons are one proposal; connected OH/WHAT is also one proposal. Small handwriting and dialogue outside closed balloons remain gaps.

On other development pages runtime~12–18ms, but proposal count8–34 includes art. Manga002 detects several closed balloons visually, while floating dorks caption is absent. Page172 has many floating captions, a structural weakness for this approach. Solid/dark/open/connected balloons and background captions need a separate text-region path.

## Scoring limitations

Report includes coverage fraction of approximate reference rectangles and whether their centers fall in a proposal. Neither is bubble-detection precision/recall or an IoU benchmark. In manga002, no loose reference reaches80% area coverage despite multiple visibly correct closed-balloon proposals; the hand-labeled text rectangles extend outside the proposed boundary. Do not claim all those balloons were missed. Conversely a large background/art proposal may cover a reference and receive a coverage hit despite being wrong. `proposalsWithoutReferenceMatch` is not a false-positive count because only a subset of page content is annotated. More than one reference in one proposal can be an unwanted merged region.

## Decision

This fast geometric detector is a possible source of region proposals, not an accepted complete detector. Do not use raw proposals to paint over artwork or send crop images to a provider. It does not replace failed OCR/translation gates. Next candidate must combine independent bubble proposals with a text-presence/line-layout gate, split connected balloons where separate text clusters are supported, retain a text-only path for floating captions, and evaluate exact region labels on unseen pages. A positive result on this page alone is insufficient. No main-app/package change in this task.
