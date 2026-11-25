# Glossary/context development experiment

Status: partial provider run reviewed; three responses saved, fourth condition not attempted. No app/default/package changes. No API retry during review.

## Results and stop diagnosis

- Plain:1338ms,8 valid IDs; organization/place names translated undesirably, semantic failures remain.
- Glossary-only:1167ms,8 valid IDs, target placeholders retained exactly; restored names include Northern Alliance, Glass Harbor, Ashen Tower and Riven. Literal falling, dying, staying dry and intelligence-network errors remain. This supports known-name protection only, not full translation acceptance.
- Glossary+context:3322ms,8 valid IDs, but m5-loses copies multiple context sentences and context-only `STG_0002` despite no expected target placeholder. The placeholder validator correctly rejected the condition before saving restored output. The generic catch message concealed this as possible network failure; API response actually completed successfully.
- Context-only: not attempted after rejection. No full four-condition comparison or general conclusion about all context strategies is justified.

Saved response audit: `scripts/assess-glossary-context.py` passes its expected-failure checks and writes `artifacts/translation-context-study/assessment.json`. Existing request marker remains intact; no hidden retry or result editing. Runner now records sanitized failure stage/item ID for future failures, without raw exceptions/key/error bodies. Do not treat that new error-reporting path as a new live-tested request.

Decision: glossary-only is useful for preserving explicitly known names. Current full-page context prompting fails containment and is not suitable for integration. Do not heuristically cut leaked context or blindly restore context names. A follow-up should narrow context to verified relevant dialogue and validate output containment, or change translation strategy; it still requires an independent quality gate. Main model, app and package remain unchanged.

Four requests maximum: plain normalized text; glossary-protected text; glossary-protected text with same-page context; plain normalized text with same-page context. Same eight targets, model, settings and instruction in every condition. Fixed order is explicit and not a repeatability guarantee. This separates glossary and context axes, but uses only known development failures, not held-out validation.

Targets: two synthetic organization/place-name cases; manga romantic idiom, died-behind-my-back, narcissistic traits, staying dry, intelligence network, and Kabura addressee. Context is exclusively saved English from the same supplied manga page, excluding the target. Synthetic name controls have empty context. Entire-page context may include later manga lines and is appropriate only to a currently visible manga page; this is not a causal subtitle experiment. It does not invent a relationship, romantic hint or Vietnamese target wording. Ambiguous statements may remain ambiguous.

`scripts/TranslationContextEvaluation` calls actual Core GlossaryProtector. Each protected target and its own context share a token map, target placed first. Provider must preserve exactly the target placeholders, not context-only placeholders. Runner validates unique ID coverage and exact placeholder counts before saving restored results. Missing/altered/duplicated tokens are failures; no blind replacements or retries. Raw provider result is saved before validation. Response restoration uses the exact same ordinal token replacement semantics as Core, after stricter experiment validation.

Run helper from project cwd to prepare `artifacts/translation-context-study/prepared.json`. Then use `Thu-glossary-ngu-canh.cmd`. Key RAM-only; maximum4 POST attempts with2048 completion tokens each, existing header waits/no-retry/stop-on-error rules. Text only; no screenshot or image upload. Reruns skip attempted conditions. Saves request/response/limit headers, and `restored.json` only after token validation. Table includes only structurally restored responses; absence of a condition means review its response/status, not assume success.

Acceptance for this checkpoint: expected known names restored exactly in protected conditions without moved/dropped tokens; independent manual semantic evaluation of all32 outputs including any regressions. Names cannot be evaluated merely by matching source casing. Context effectiveness is not assumed. Even success would need larger unseen data, actual app pipeline evaluation and live causal subtitle latency before production adoption.
