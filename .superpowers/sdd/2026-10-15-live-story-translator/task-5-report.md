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

## Review follow-up: stale work cancellation race

Added a deterministic regression test with an internal test-only pre-signal seam. It pauses replacement after the old work has been detached under the coordinator lock but before its cancellation token is signaled, then completes the old translator request. The test asserts the old Task is canceled and its late result is absent from cache.

RED: with the prior `catch (OperationCanceledException) when (work.Cancellation.IsCancellationRequested)` filter restored, the regression test failed at `Assert.True(stale.IsCanceled)`; the stale Task was faulted because the identity-mismatch `OperationCanceledException` carried an as-yet uncanceled token.

Fix: catch `OperationCanceledException` independently of CTS state and complete the coordinator Task as canceled. The cancellation callback/token is still signaled outside the lock.

GREEN command:
```powershell
$env:DOTNET_CLI_HOME='E:\tool_dich_man_hinh\.tools\cli-home'
$env:NUGET_PACKAGES='E:\tool_dich_man_hinh\.tools\nuget-packages'
& 'E:\tool_dich_man_hinh\.tools\dotnet\dotnet.exe' test tests/StoryTranslator.Core.Tests/StoryTranslator.Core.Tests.csproj --no-restore --filter FullyQualifiedName~TranslationCoordinatorTests --logger 'console;verbosity=normal'
```

GREEN result: `Test Run Successful. Total tests: 6, Passed: 6, Failed: 0, Skipped: 0`.

Follow-up implementation commit: `44a76f295637c01cf4f629a1e67ec12d550cf37b` (`fix: preserve canceled state for superseded translations`).
