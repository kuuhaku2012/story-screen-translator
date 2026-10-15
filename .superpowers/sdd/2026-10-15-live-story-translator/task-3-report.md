# Task 3 report — OCR text stability and duplicate suppression

## Scope
Implemented deterministic Core-only text normalization and stability filtering. No capture, OCR engine, game process access, network, or UI code was added.

## Tests-first evidence (RED)
Command:
`$env:DOTNET_CLI_HOME='E:\\tool_dich_man_hinh\\.tools\\cli-home'; $env:NUGET_PACKAGES='E:\\tool_dich_man_hinh\\.tools\\nuget-packages'; & 'E:\\tool_dich_man_hinh\\.tools\\dotnet\\dotnet.exe' test tests/StoryTranslator.Core.Tests/StoryTranslator.Core.Tests.csproj --no-restore --filter FullyQualifiedName~TextStabilityFilterTests`

Initial result: expected compile failure because `TextStabilityFilter` did not exist (`CS0246`, six references). An initial run without elevated project write access failed before compilation while MSBuild attempted to create `bin/obj`; rerunning with the authorized project-directory access produced the expected RED result.

## Implementation
Added `src/StoryTranslator.Core/TextStabilityFilter.cs` and focused tests in `tests/StoryTranslator.Core.Tests/TextStabilityFilterTests.cs`.
- Trims outer whitespace and collapses repeated ASCII spaces only; punctuation and letters remain unchanged.
- Requires two consecutive identical normalized non-empty observations.
- Null/whitespace breaks the pending candidate sequence.
- Suppresses repeated emission of the last stable line; a changed stable line can emit, and a previously emitted line can emit again after another line became stable.

## GREEN evidence
Ran the same focused command after implementation.
Result: `Passed! - Failed: 0, Passed: 8, Skipped: 0, Total: 8`.

## Commit and working tree
Commit: `6d88a8d` (`feat: stabilize and deduplicate OCR text`).
Working tree status: clean after staging this report.

