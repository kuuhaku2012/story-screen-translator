# Glossary validation integration

## Change

Added `GlossaryProtector.RestoreValidated(string, ProtectedText)` and used it in StoryTranslationPipeline and SpatialTranslationPipeline. Counts exact reserved tokens in the protected source versus provider result before canonical-name restoration. Missing/duplicated/foreign/changed tokens fail with a Vietnamese error. Literal reserved tokens already present in source remain valid; deterministic token collision handling still works. Repeated occurrences of the same name are preserved independently.

Spatial batches validate every response before writing any restored translation to cache or state. One invalid response cannot cause a partial batch to be committed/published. Existing generic `Restore` remains available for legacy standalone use, but both production presentation pipelines use the validated operation.

## Evidence

New glossary tests initially failed compilation because the validated API did not exist. After implementation: 122 Core tests passed. Added pipeline regression for invalid second batch result, asserting no presentation and no partial cached result after re-enable. Full suite: **123 passed, 0 failed**.

Cases include organization/place names, changed/missing/duplicate/foreign tokens, literal source token collision, repeated name occurrences, and the observed context-only token appearing in a target with no expected placeholder. No API request or new image capture in this task.

## Limits / state

This is known-name structural protection, not semantic validation, arbitrary name recognition or proof that translated names keep their speaker roles. It does not detect leaked context text when it contains no reserved token. Glossary must contain canonical names/aliases for protection to apply. No synthetic evaluation names were added to the user's product glossary. Model defaults, input casing and full-page context behavior were not changed. No new MSIX package was produced in this task; existing launcher package remains the earlier build. Manga OCR/layout and translation quality gates remain unpassed, so do not invite another full product acceptance trial based on this change alone.
