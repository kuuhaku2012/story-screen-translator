# Task 2 implementation report: explicit Story Mode state

## Status contract

- A new `StoryModeController` starts OFF (`IsEnabled == false`).
- `Enable(region)` delegates the selected `Rectangle` to `IScreenCapture.Start` only while OFF, and becomes ON only after `Start` returns successfully.
- Repeated `Enable` while ON is a no-op.
- `Disable()` delegates `Stop` only while ON and becomes OFF after `Stop` returns. Calling it while OFF is a no-op.
- If `Start` throws, the controller remains OFF. If `Stop` throws, the assignment to OFF is not reached, so the controller remains ON.
- No process/game detection or real screen capture was added. All tests use an in-memory recording fake.

## Changes

- Added `src/StoryTranslator.Core/IScreenCapture.cs` with `Start(Rectangle)` and `Stop()`.
- Added `src/StoryTranslator.Core/StoryModeController.cs` with the explicit ON/OFF state and capture delegation.
- Added `tests/StoryTranslator.Core.Tests/StoryModeControllerTests.cs` covering initial OFF, selected-region capture start, repeated-enable no-op, stop/off transition, disable-while-off no-op, and failed-start state.
- Added the Core project reference to the test project so the controller contract can be compiled and exercised.

## TDD verification

Both commands used the repository-local SDK and local package/cache locations:

```powershell
$env:DOTNET_CLI_HOME = 'E:\tool_dich_man_hinh\.tools\dotnet-home'
$env:NUGET_PACKAGES = 'E:\tool_dich_man_hinh\.tools\nuget-packages'
& 'E:\tool_dich_man_hinh\.tools\dotnet\dotnet.exe' test tests\StoryTranslator.Core.Tests\StoryTranslator.Core.Tests.csproj --filter FullyQualifiedName~StoryModeControllerTests --verbosity minimal
```

### RED (before implementation)

Expected failure: the new fake cannot resolve `IScreenCapture` because the interface and controller had not been implemented.

```text
Determining projects to restore...
Restored E:\tool_dich_man_hinh\tests\StoryTranslator.Core.Tests\StoryTranslator.Core.Tests.csproj (in 264 ms).
1 of 2 projects are up-to-date for restore.
StoryTranslator.Core -> E:\tool_dich_man_hinh\src\StoryTranslator.Core\bin\Debug\net10.0\StoryTranslator.Core.dll
E:\tool_dich_man_hinh\tests\StoryTranslator.Core.Tests\StoryModeControllerTests.cs(78,51): error CS0246: The type or namespace name 'IScreenCapture' could not be found (are you missing a using directive or an assembly reference?) [E:\tool_dich_man_hinh\tests\StoryTranslator.Core.Tests\StoryTranslator.Core.Tests.csproj]
EXIT_CODE=1
```

### GREEN (after implementation)

```text
Determining projects to restore...
All projects are up-to-date for restore.
StoryTranslator.Core -> E:\tool_dich_man_hinh\src\StoryTranslator.Core\bin\Debug\net10.0\StoryTranslator.Core.dll
StoryTranslator.Core.Tests -> E:\tool_dich_man_hinh\tests\StoryTranslator.Core.Tests\bin\Debug\net10.0\StoryTranslator.Core.Tests.dll
Test run for E:\tool_dich_man_hinh\tests\StoryTranslator.Core.Tests\bin\Debug\net10.0\StoryTranslator.Core.Tests.dll (.NETCoreApp,Version=v10.0)
A total of 1 test files matched the specified pattern.
Passed!  - Failed:     0, Passed:     6, Skipped:     0, Total:     6, Duration: 135 ms - StoryTranslator.Core.Tests.dll (net10.0)
EXIT_CODE=0
```

## Concerns and limits

- This task defines state orchestration only. A production `IScreenCapture` implementation remains outside scope.
- The controller is intentionally synchronous and does not add locking; concurrent callers are not coordinated.
- Only the focused controller test filter was run, as requested.

## Commit

Committed on branch `work/live-story-translator` with message `feat: add explicit story mode controller`.
