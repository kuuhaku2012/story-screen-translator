# Translation provider gate: controlled casing experiment

Status: both API attempts completed and reviewed. Lowercase is a promising development candidate, not accepted for production.

## Observed results

Both requests returned six valid IDs, finish reason `stop`, without HTTP 429. Uppercase batch: 1273 ms. Lowercase with preserved names: 1897 ms. These are single API observations, excluding waiting/OCR/rendering; no speed improvement is demonstrated.

| Case | Uppercase input | Lowercase input |
|---|---|---|
| Kabura addressee | Meaning retained | Meaning retained, but untranslated `Heyyy` remains |
| Repeated gripes | Awkward `phản ánh` and repeated phrasing | Correct complaints, more natural |
| Falls the hardest | Meaningless invented Vietnamese | Grammatical literal `người ngã mạnh nhất`, still misses romantic context |
| Died behind my back | Reverses location to `trước mặt tôi` | No reversal, but literal `chết đột ngột sau lưng tôi` remains unnatural/ambiguous |
| Narcissistic tendencies | Meaningless `tính các tâm` | `xu hướng tự yêu bản thân` closer; still less precise than narcissistic/self-absorbed traits |
| Couldn't give less of a crap | Wrong inability to forget | Correct indifference: `Tôi không quan tâm chút nào` |

Case-insensitive name identity was preserved for Kabura/Haru in both responses. Exact casing is restored only in the lowercase payload; this does not validate arbitrary name recognition or a complete glossary pipeline.

Interpretation: in this pair, lowering the source case materially improved several failures. There is one trial per condition in a fixed order, so model variability and order/time effects are not excluded. Do not assert all previous translation failures were caused by uppercase input. Do not apply a blanket lowercase rule to the app based on six known cases: acronyms, Roman numerals, names and context still require validation.

Next checkpoint: validate the candidate on the prepared 60-case development corpus with balanced input order, consistent name handling and a bounded call budget, then unseen material if the development gate is passed. Use causal scene context only after testing casing as its own variable. Production integration remains gated on semantic quality, latency and quota; no main-app source/default/package changed in this review. No new API calls were made during assessment.

Hypothesis: ALL CAPS manga text may contribute to the observed translation failures. This is unproven and cannot justify changing production input normalization yet.

Run `scripts/test-qwen-expanded.ps1 -CasingStudy`. Uses the same six difficult saved sentences, same GPT-OSS 120B low-reasoning configuration and exactly the same instruction for both requests. First retains uppercase. Second lowercases letters and restores known proper names. No other text edits, punctuation repair, gold translations or scene context are introduced. A casing-invariance assertion validates this before any request. This is not automatic proper-name detection; future production normalization would need glossary-backed behavior validated separately.

Maximum two POST attempts, marker before send, no automatic retry, stop on HTTP failure; wait according to existing conservative limit-header logic. Credentials entered manually in hidden prompt, RAM only. No image, new capture, game, new provider or dependency. Outputs under `artifacts/translation-casing-study`; prior results remain unchanged.

Review: addressee in Kabura question; gripes as repeated complaints; romantic idiom ambiguity; behind-my-back wording; narcissism versus self-interest; indifference versus inability to forget. Mark unclear context as ambiguous rather than assuming missing information. Both outputs are judged, including failures. A six-item positive result is only a hypothesis lead, not 60-case/held-out acceptance. If it does not improve materially, reject this normalization hypothesis and do not spend repeated calls tuning against these exact six answers.

The new development corpus generator produces 60 cases and a blank score sheet. It does not send all 60 in this two-request study. New unseen samples and causal subtitle session measurements remain required by the implementation plan.
