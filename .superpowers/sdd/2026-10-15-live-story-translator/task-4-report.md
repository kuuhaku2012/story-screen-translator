# Task 4 report — protect proper names through translation

## Scope
Added a Core-only glossary model and per-sentence name protector, six focused tests, and a small illustrative JSON glossary. No network/provider calls, game interaction, OCR, capture, or UI integration was added.

## Tests-first evidence (RED)
Command:
`$env:DOTNET_CLI_HOME='E:\\tool_dich_man_hinh\\.tools\\cli-home'; $env:NUGET_PACKAGES='E:\\tool_dich_man_hinh\\.tools\\nuget-packages'; & 'E:\\tool_dich_man_hinh\\.tools\\dotnet\\dotnet.exe' test tests/StoryTranslator.Core.Tests/StoryTranslator.Core.Tests.csproj --no-restore --filter FullyQualifiedName~GlossaryProtectorTests`

Initial result: expected compile failure, exit code 1, because `GlossaryProtector` and `GlossaryEntry` were absent (`CS0246` / `CS0103`).

## Implementation
- `GlossaryEntry` stores canonical name, aliases, and category.
- The protector also recognizes each canonical spelling, compares case-insensitively, checks word boundaries, and prefers longer aliases at the same position. Equal-length conflicts resolve by glossary entry order.
- Each match gets an opaque, per-call unique token. The returned map is limited to that sentence; restore replaces only those exact tokens with exact canonical names. Other text is appended unchanged.
- `config/glossary.example.json` contains a few illustrative entries and explicitly says it is not exhaustive.

## GREEN evidence
Ran the same focused command after implementation.
Result: `Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6` (net10.0).

## Commit and working tree
Commit: `092031c` (`feat: preserve glossary names through translation`).
The parent-owned modification to `task-3-report.md` was left unstaged and untouched. Task 4 files and this report were staged/committed explicitly. Final `git status --short` shows only the parent-owned modified `task-3-report.md`; no Task 4 changes remain uncommitted.

