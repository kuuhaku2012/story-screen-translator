# Live Story Screen Translator Implementation Plan

> For agentic workers: use the executing-plans skill to implement task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Build a lightweight Windows utility that translates only user-selected dialogue while Story Mode is manually enabled.

**Architecture:** A WPF shell controls an OCR pipeline in a Core library: explicit mode state, low-rate region capture, text stabilization/deduplication, glossary protection, translation adapter/cache, and a separate overlay. No game process access or input automation.

**Tech Stack:** C#, .NET 10 Windows desktop, WPF, Windows OCR, bounded JSON cache, xUnit for Core behavior tests.

**Spec:** docs/PROJECT_SPEC.md

## Global Constraints

- Story Mode starts OFF; capture/OCR/translation stop when switched OFF.
- Capture only a user-selected rectangle; do not persist frames or dialogue text by default.
- No game process, memory, file, hook, injected code, anti-cheat state, or simulated input access.
- Do not run with Wuthering Waves unless Kuro gives written permission for this exact workflow.
- Use generated images, saved video, or a non-anti-cheat app for tests; no alt-account ban probing.
- Do not integrate a translation provider or API key until the user chooses one.
- Preserve glossary names exactly through translation.
- Use TDD: each behavior test must fail before production implementation.

---

### Task 1: Create solution skeleton and verify test harness

**Files:**
- Create: src/StoryTranslator.Core/StoryTranslator.Core.csproj
- Create: tests/StoryTranslator.Core.Tests/StoryTranslator.Core.Tests.csproj
- Create: StoryTranslator.sln

- [ ] Create the solution and projects: run dotnet new sln -n StoryTranslator; create a net10.0 class library and xUnit test project; add both projects to the solution.
- [ ] Run dotnet test on the test project. Expected: test host starts and reports zero tests without errors.
- [ ] Run dotnet --info and record SDK/runtime versions. If .NET 10 SDK is absent, stop before installing software and ask the user.

### Task 2: Implement explicit Story Mode state

**Files:**
- Create: src/StoryTranslator.Core/StoryModeController.cs
- Create: src/StoryTranslator.Core/IScreenCapture.cs
- Create: tests/StoryTranslator.Core.Tests/StoryModeControllerTests.cs

**Interfaces:**
- IScreenCapture.Start(Rectangle region) starts capture for one selected rectangle.
- IScreenCapture.Stop() stops frame production.
- StoryModeController.Enable(Rectangle region) starts capture once.
- StoryModeController.Disable() stops capture and reports OFF.

- [ ] Write tests proving a fresh controller is OFF, enabling starts once, repeated enable does not start another capture, and disabling stops capture. Use a recording fake so tests never capture a real desktop.
- [ ] Run focused tests. Expected: FAIL because controller/interface are absent.
- [ ] Implement only the state and start/stop delegation; do not add game detection.
- [ ] Re-run focused tests. Expected: all mode-state tests PASS.

### Task 3: Stabilize OCR text and suppress duplicates

**Files:**
- Create: src/StoryTranslator.Core/TextStabilityFilter.cs
- Create: tests/StoryTranslator.Core.Tests/TextStabilityFilterTests.cs

**Interface:** Observe(text) returns a normalized new line only after two consecutive equal non-empty observations. Once emitted, repeated observations of the same line return null until a different stable line appears.

- [ ] Write tests for null/whitespace, one unstable observation, two equal observations, duplicate suppression, and accepting a later changed line.
- [ ] Run focused tests. Expected: FAIL because TextStabilityFilter is absent.
- [ ] Implement trim/collapse of outer and repeated spaces; do not rewrite punctuation or letters.
- [ ] Re-run focused tests. Expected: all stability tests PASS.

### Task 4: Protect proper names through translation

**Files:**
- Create: src/StoryTranslator.Core/GlossaryEntry.cs
- Create: src/StoryTranslator.Core/GlossaryProtector.cs
- Create: tests/StoryTranslator.Core.Tests/GlossaryProtectorTests.cs
- Create: config/glossary.example.json

**Interfaces:** GlossaryEntry stores canonical name, aliases, and category. Protect(source) returns protected text plus a token map. Restore(translated, tokenMap) replaces only tokens from the current sentence with their exact canonical names.

- [ ] Write tests for exact restoration of one and multiple names, case-insensitive alias matching, longest overlapping alias, and unchanged unrelated text.
- [ ] Run focused tests. Expected: FAIL because glossary types are absent.
- [ ] Implement deterministic longest-match protection using unique opaque tokens.
- [ ] Re-run focused tests. Expected: all glossary tests PASS; no external translator is called.

### Task 5: Bound translation queue and cache repeated lines

**Files:**
- Create: src/StoryTranslator.Core/TranslationCoordinator.cs
- Create: src/StoryTranslator.Core/ITextTranslator.cs
- Create: tests/StoryTranslator.Core.Tests/TranslationCoordinatorTests.cs

**Interfaces:** ITextTranslator.TranslateAsync(source, target, cancellationToken) returns text. TranslationCoordinator.SubmitAsync(line, cancellationToken) returns cached text or translates once. A newer line cancels/replaces older pending work; OFF cancels all pending work.

- [ ] Write tests for one translator call per repeated input, cache hit, cancellation when a newer line arrives, and cancellation on OFF.
- [ ] Run focused tests. Expected: FAIL because coordinator types are absent.
- [ ] Implement newest-line behavior and in-memory cache behind an interface, without network calls.
- [ ] Re-run focused tests. Expected: all coordinator tests PASS.

### Task 6: Add WPF shell and manual region configuration

**Files:**
- Create: src/StoryTranslator.App/StoryTranslator.App.csproj
- Create: src/StoryTranslator.App/App.xaml
- Create: src/StoryTranslator.App/MainWindow.xaml
- Create: src/StoryTranslator.App/MainWindow.xaml.cs
- Create: src/StoryTranslator.App/RegionSelectorWindow.xaml
- Modify: StoryTranslator.sln

- [ ] Build an OFF-by-default shell with Story Mode toggle, region selection action, status text, and exit action.
- [ ] Build standard WPF controls. Starting the app must not start capture. Do not add auto-detection of story/combat/game.
- [ ] Run dotnet build StoryTranslator.sln. Expected: app and Core compile without warnings/errors.

### Task 7: Implement region capture and Windows OCR adapters

**Files:**
- Create: src/StoryTranslator.App/WindowsRegionCapture.cs
- Create: src/StoryTranslator.App/WindowsOcrEngine.cs
- Create: src/StoryTranslator.App/CaptureWorker.cs
- Create: tests/StoryTranslator.Core.Tests/CaptureLifecycleTests.cs

- [ ] Write lifecycle tests proving capture rejects an empty rectangle, stops after OFF, and disposes each frame after OCR.
- [ ] Run lifecycle tests. Expected: FAIL before lifecycle enforcement exists.
- [ ] Implement capture through documented Windows desktop APIs, only for the configured rectangle. Keep buffers in memory and dispose after OCR. Do not enumerate/open game processes.
- [ ] Implement Windows OCR using the installed language pack. Return recognized text; log errors/timings without dialogue content.
- [ ] Run lifecycle tests and build. Expected: tests PASS and OFF cancels the worker.

### Task 8: Add translation overlay and settings

**Files:**
- Create: src/StoryTranslator.App/TranslationOverlay.xaml
- Create: src/StoryTranslator.App/TranslationOverlay.xaml.cs
- Create: src/StoryTranslator.App/SettingsStore.cs
- Create: src/StoryTranslator.App/GlossaryStore.cs
- Create: src/StoryTranslator.Core/JsonTranslationCache.cs
- Create: tests/StoryTranslator.Core.Tests/JsonTranslationCacheTests.cs

- [ ] Show the newest translation in a separate topmost WPF window with hide/close actions. Do not draw into or attach to another process.
- [ ] Store region, sampling interval, language pair, and glossary in app data. Do not store secrets in JSON.
- [ ] Write tests proving translation cache entries survive a reload, the cache evicts the oldest entry at 1,000 entries, and malformed cache files recover to an empty cache without logging dialogue text.
- [ ] Run focused cache tests. Expected: FAIL because JsonTranslationCache is absent.
- [ ] Implement an atomic JSON cache file with a hard 1,000-entry limit and no dialogue logging.
- [ ] Re-run cache tests. Expected: all persistence and eviction tests PASS.
- [ ] Verify OFF hides overlay and cancels work; ON permits new stabilized lines.

### Task 9: Integrate a translation provider after user selection

**Files:**
- Create after provider selection: src/StoryTranslator.App/ConfiguredTextTranslator.cs
- Modify after provider selection: app settings and user guide

- [ ] Confirm provider, key, cost, and privacy model before adding an endpoint.
- [ ] Send only text and language codes. Add timeout, cancellation, caching, and secret redaction in logs.
- [ ] Test adapter against deterministic local responses and verify no image bytes are submitted.

### Task 10: Offline end-to-end verification and packaging

**Files:**
- Create: docs/TEST_REPORT.md
- Create: docs/USER_GUIDE.md
- Create: scripts/package.ps1

- [ ] Run dotnet test StoryTranslator.sln and dotnet build StoryTranslator.sln -c Release.
- [ ] Manually verify with synthetic dialogue frames or a saved local clip: live updates, protected names, duplicate suppression, OFF behavior, overlay, and errors.
- [ ] Measure CPU/RAM with mode OFF and ON, OCR-to-overlay latency, and translation request count. Report measured values only.
- [ ] Package a portable Windows artifact and removal guide; exclude keys and account data.
- [ ] Keep live game use gated on Kuro’s written confirmation. Offline success and alt-account use do not establish anti-cheat safety.


