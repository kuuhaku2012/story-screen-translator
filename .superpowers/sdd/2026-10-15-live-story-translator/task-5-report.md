# Task 5 report — newest-line translation queue and bounded cache

## Scope
Added a Core-only text translation abstraction, a latest-line coordinator, and a bounded LRU in-memory cache. No provider, HTTP/network code, image input, game integration, or UI was added. The cache defaults to 256 source-text/target-language entries and can be replaced through `ITranslationCache` by the persistent JSON cache in Task 8.

## TDD evidence

RED command:
```powershell
$env:DOTNET_CLI_HOME='E:\tool_dich_man_hinh\.tools\cli-home'
$env:NUGET_PACKAGES='E:\tool_dich_man_hinh\.tools\nuget-packages'
& 'E:\tool_dich_man_hinh\.tools\dotnet\dotnet.exe' test tests/StoryTranslator.Core.Tests/StoryTranslator.Core.Tests.csproj --no-restore --filter FullyQualifiedName~TranslationCoordinatorTests
```

RED result: compile failed as expected with `CS0246` because `ITextTranslator` had not yet been implemented.

GREEN command: same command with `--logger 'console;verbosity=normal'`.

GREEN result: `Test Run Successful. Total tests: 5, Passed: 5, Failed: 0, Skipped: 0`.

Coverage: repeated in-flight requests coalesce; a completed result is cached; a different newer line cancels older work; `CancelPending()` cancels OFF work; cancellation-ignoring stale completions do not populate cache; and the in-memory cache evicts its least-recently-used item at capacity.

## Commit and working tree
Implementation commit: `476c0fa5d9107fc7af2839251a0c53428ec6a8cf` (`feat: add bounded newest-line translation coordinator`).
Task 3 report was not modified or staged. The implementation commit had a clean working tree after commit; this report is committed separately as a Task 5 report-only commit.
