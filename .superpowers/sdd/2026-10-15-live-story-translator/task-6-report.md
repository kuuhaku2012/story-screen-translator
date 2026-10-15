# Task 6 implementation report

## Changes

- Added `src/StoryTranslator.App`, a WPF app targeting `net10.0-windows`.
- Added a shell with a manual region-selection action, selected-region dimensions, explicit OFF state, status text, and Exit action.
- Added a drag-to-select overlay on the primary display. Escape cancels selection. The selector returns the chosen rectangle; it does not capture pixels.
- Story Mode remains OFF at startup. Its control is disabled and the status explains that capture/OCR is not wired yet. Starting the app performs no capture or translation.
- No game/process detection, hooks, injection, input simulation, OCR, capture, settings, translation, or overlay was added.
- Added the app project to `StoryTranslator.sln`.

## Verification

Command:

```powershell
$env:DOTNET_CLI_HOME='E:\tool_dich_man_hinh\.tools\cli-home'
$env:NUGET_PACKAGES='E:\tool_dich_man_hinh\.tools\nuget-packages'
& 'E:\tool_dich_man_hinh\.tools\dotnet\dotnet.exe' build 'E:\tool_dich_man_hinh\StoryTranslator.sln'
```

Result:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

The first build attempts exposed ordinary compile issues (ambiguous `Point`, missing `Canvas` namespace, and an edit escaping typo); these were corrected before the successful build above.

## Scope limitation

This task provides the UI shell and manual rectangle selection only. Selection is currently limited to the primary display. Story Mode is intentionally unavailable until capture/OCR lifecycle wiring is implemented in Task 7. No live-game test was performed.
