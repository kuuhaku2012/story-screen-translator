# MSIX package identity and offline Windows OCR plan

## Goal

Package the existing WPF application as an x64 MSIX so Windows.Media.Ocr can run with package identity. Add a synthetic-image OCR smoke mode. The translation provider remains unconfigured by the user's choice, and normal Story Mode stays disabled.

## Constraints

- Keep all source, package layouts, artifacts, logs, and documentation under E:\tool_dich_man_hinh.
- Do not launch Wuthering Waves or use any game account. No account test can establish anti-cheat safety.
- Do not add a translation endpoint, key, network request, or dialogue logging.
- Do not import/trust a certificate or change Developer Mode. The user explicitly approved one temporary local MSIX registration for a synthetic OCR test; always unregister and verify cleanup. Package generation must not alter system trust or developer settings.
- Use only a synthetic image for real Windows OCR smoke testing; report language-pack or package-identity blocks honestly.
- Prefer the existing Windows SDK MakeAppx.exe; do not install Visual Studio workloads merely for packaging.

## Decisions

- Use an x64 full-trust desktop MSIX layout with a stable package identity, runFullTrust capability, and the minimum Windows version consistent with the app manifest.
- Use framework-dependent x64 as the primary package, as selected by the user: it is 6.93 MiB and requires .NET 10 Desktop Runtime on the target PC. Keep self-contained x64 as an optional `-SelfContained` package; keep single-file/trimming disabled for WPF compatibility.
- Produce an unsigned MSIX by default. scripts/package.ps1 may accept an already-present signing certificate thumbprint but must never create/export a private key or install trust automatically.
- Add a --ocr-smoke-test <result-path> path that creates an in-memory synthetic bitmap, calls the production WindowsOcrEngine, checks expected sample text, writes only the synthetic result, then exits. Normal launch behavior is unchanged.
- Runtime floor needs follow-up: manifest MinVersion and SupportedOSPlatformVersion are 10.0.17763.0, the TFM API contract is `net10.0-windows10.0.19041.0`, and WindowsSdkPackageVersion is 10.0.26100.87. Do not treat a successful current-machine build as proof of compatibility with 17763; audit API use and test an actual minimum-version OS before claiming it.

## Work steps

1. Update the implementation plan, project spec, and user guide for the selected MSIX direction and deferred provider.
2. Add a valid Package.appxmanifest, required image assets, and scripts/package.ps1 to publish x64 output, assemble the layout, create the MSIX with MakeAppx, and optionally sign using a user-supplied certificate thumbprint.
3. Add the offline synthetic OCR smoke mode and focused tests for argument handling, success/failure reporting, and fail-closed behavior without package identity.
4. Run the existing solution tests and Release build. Run the package script; inspect package output, signature state, and size.
5. The user approved a temporary local MSIX registration for an offline synthetic OCR smoke test. Activate only the app's smoke-test command, then unregister it and verify no app process or registration remains. Do not install a certificate, alter Developer Mode, or launch the game.
6. Record exact test/build/MakeAppx results, package size/signature state, limitations and unresolved OS prerequisites. Keep Task 9 deferred and Task 10 partial until provider selection and a usable signed/registered package workflow are resolved.

## Acceptance checks

- MSIX manifest validates and MakeAppx creates the x64 package.
- Package is unsigned by default; no certificate/private key is created or trusted automatically.
- Story Mode remains OFF/disabled without a real translator provider.
- Synthetic OCR smoke passes only when run with package identity and the English OCR language pack; otherwise it reports the exact prerequisite.
- dotnet test StoryTranslator.sln and dotnet build StoryTranslator.sln -c Release pass.
- No game, account, or network test occurs.

## Recorded result (2026-10-15)

- `dotnet test StoryTranslator.sln`: 74 passed, 0 failed, 0 skipped.
- `dotnet build StoryTranslator.sln -c Release`: 0 warnings, 0 errors.
- The framework-dependent package is the primary artifact: `E:\tool_dich_man_hinh\artifacts\msix\StoryTranslator-1.0.0.0-x64.msix`, 7,263,431 bytes (~6.93 MiB), SHA256 `BA579252803625F493BAE22E82BFEE39A5D967472C4B331F5A7316EFF684C330`, unsigned. It requires `Microsoft.NETCore.App` 10.0 and `Microsoft.WindowsDesktop.App` 10.0; this machine has only machine-wide runtimes 3.1.32 and 8.0.23. The script's optional `-SelfContained` switch builds the larger alternative.
- Real Windows OCR recognition passed on the synthetic text `STORY TEXT 123` in the self-contained MSIX layout using the same source and manifest. The framework-dependent activation was also attempted but did not produce a result; this machine lacks the required .NET 10 runtime, so that package's runtime behavior remains unverified here. No runtime was installed. Temporary package registration was removed and both package absence and process exit were verified. Developer Mode was already enabled before the test and was not changed. The English OCR language pack is available. .NET first-run generated an untrusted HTTPS development certificate; the exact certificate was removed by thumbprint and verified absent from personal and trusted-root stores.
- The unpackaged CLI diagnostic still correctly fails closed for missing package identity. Provider remains deferred; Story Mode disabled. No game/account/network test.
- Task 10 remains partial: provider integration, minimum-OS runtime compatibility (17763 vs TFM contract 19041 / SDK package 26100), and end-user signing/runtime/install guidance remain open.
