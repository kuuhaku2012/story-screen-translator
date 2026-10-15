# Task 7 report: region capture and Windows OCR adapters

## Implementation

- Added `CaptureWorker` with sequential sampling, default 500 ms interval (2 fps), no overlapping frame pipeline, cancellation on OFF, stale-result suppression, and deterministic frame disposal.
- Added injectable frame-source and text-recognizer interfaces so lifecycle tests use fakes and do not capture the desktop.
- Added `WindowsRegionCapture`, which validates a positive rectangle within the virtual desktop and copies only that selected rectangle with `Graphics.CopyFromScreen`. The temporary bitmap bytes stay in memory and are zeroed after conversion to an uncompressed `SoftwareBitmap`.
- Added `WindowsOcrEngine` for local `en-US` OCR. It returns recognized text only; unavailable language packs and missing package identity produce explicit initialization errors. Pipeline diagnostics contain an error category and exception type only, never OCR text.
- Targeted Windows SDK 10.0.26100.87 through `WindowsSdkPackageVersion` so .NET 10 can compile the Windows Runtime OCR APIs. The app now references Core, and the tests target the Windows TFM to exercise the App worker with platform-independent fakes.

## TDD evidence

- RED: after restoring the Windows test target, the focused test command failed to compile because `IFrameTextRecognizer`, `ICapturedFrame`, and `IRegionFrameSource` did not exist.
- GREEN: focused `CaptureLifecycleTests` passed 5/5, including empty-region rejection; OFF preventing subsequent work; frame disposal following success and recognition failure; and frame disposal plus late-result discard when OFF occurs during OCR.
- Full verification: `dotnet test StoryTranslator.sln` passed 31/31 tests.
- Build verification: `dotnet build StoryTranslator.sln -c Release` succeeded with 0 warnings and 0 errors.
- Tests use deterministic fakes. No desktop image, game client, or account was used.

## Runtime prerequisite and boundary

Microsoft documents `Windows.Media.Ocr` as supported for desktop apps only when they have package identity (MSIX). The current WPF shell is unpackaged, so this build does **not** establish that Windows OCR can run from the current app distribution. `WindowsOcrEngine.CreateEnglish()` checks for package identity and refuses to initialize without it; it also refuses when the installed English OCR language pack is unavailable. Keep Story Mode disabled until distribution provides MSIX package identity and OCR initialization has been verified on an offline synthetic image. The current task did not run the OCR adapter against a desktop/game image.

Official API references:

- [Graphics.CopyFromScreen](https://learn.microsoft.com/en-us/dotnet/api/system.drawing.graphics.copyfromscreen)
- [Windows.Media.Ocr namespace and package identity requirement](https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr?view=winrt-26100)
- [OcrEngine.RecognizeAsync](https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr.ocrengine.recognizeasync?view=winrt-26100)
- [OcrEngine.TryCreateFromLanguage](https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr.ocrengine.trycreatefromlanguage?view=winrt-26100)
