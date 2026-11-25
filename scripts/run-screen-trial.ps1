[CmdletBinding()]
param([switch] $OcrSmokeTest, [switch] $OverlaySmokeTest, [switch] $LargeOcrSmokeTest, [switch] $SpatialSmokeTest, [string] $LayoutDirectory)

$ErrorActionPreference = 'Stop'
if ($OcrSmokeTest -and $OverlaySmokeTest) { throw 'Select only one smoke mode.' }
if ($LargeOcrSmokeTest -and ($OcrSmokeTest -or $OverlaySmokeTest)) { throw 'Select only one smoke mode.' }
if ($SpatialSmokeTest -and ($OcrSmokeTest -or $OverlaySmokeTest -or $LargeOcrSmokeTest)) { throw 'Select only one smoke mode.' }
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$layout = Join-Path $projectRoot 'artifacts\screen-trial\layout'
if ($LayoutDirectory) {
    $layout = [IO.Path]::GetFullPath($LayoutDirectory)
    $allowedRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts')).TrimEnd('\') + '\'
    if (-not $layout.StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Trial layout must stay inside project artifacts.' }
}
$manifestPath = Join-Path $layout 'AppxManifest.xml'
if (-not (Test-Path -LiteralPath $manifestPath)) { throw 'Build artifacts/screen-trial with package.ps1 -SelfContained first.' }
[xml]$manifest = Get-Content -LiteralPath $manifestPath -Raw
$name = [string]$manifest.Package.Identity.Name
$existing = Get-AppxPackage -Name $name
if ($existing) {
    . (Join-Path $PSScriptRoot 'trial-registration-policy.ps1')
    $action = Get-TrialRegistrationAction $projectRoot $layout $existing.InstallLocation ([bool]$existing.IsDevelopmentMode) ([bool](Get-Process -Name 'StoryTranslator.App' -ErrorAction SilentlyContinue))
    if ($action -eq 'Foreign') {
        throw 'An installed package or a trial from another location already exists. It was not changed.'
    }
    if ($action -eq 'Running') {
        throw 'Story Translator is still running. Close its application window, then launch again. Its registration was not changed.'
    }
    Write-Host 'Recovering leftover trial registration from this project...'
    Remove-AppxPackage -Package $existing.PackageFullName
    if (Get-AppxPackage -Name $name) { throw 'Leftover trial registration could not be removed.' }
}

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class StoryScreenTrialActivation {
    [ComImport, Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")]
    private class Manager {}
    [ComImport, Guid("2e941141-7f97-4756-ba1d-9decde894a3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IManager {
        [PreserveSig] int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string id,
            [MarshalAs(UnmanagedType.LPWStr)] string args, uint options, out uint pid);
    }
    public static int Activate(string id, string args, out uint pid) {
        var manager = (IManager)new Manager();
        try { return manager.ActivateApplication(id, args, 0, out pid); }
        finally { Marshal.ReleaseComObject(manager); }
    }
}
'@

$package = $null
[uint32]$appProcessId = 0
$result = Join-Path $projectRoot 'artifacts\screen-trial\ocr-result.json'
if ($OverlaySmokeTest) { $result = Join-Path $projectRoot 'artifacts\screen-trial\overlay-result.json' }
if ($LargeOcrSmokeTest) { $result = Join-Path $projectRoot 'artifacts\screen-trial\large-ocr-result.json' }
if ($SpatialSmokeTest) { $result = Join-Path $projectRoot 'artifacts\screen-trial\spatial-result.json' }
try {
    # Requires existing Developer Mode; does not change it or install any certificate.
    Add-AppxPackage -Register -Path $manifestPath
    $package = Get-AppxPackage -Name $name
    if (-not $package) { throw 'Temporary package registration could not be verified.' }
    $appId = [string]$manifest.Package.Applications.Application.Id
    $aumid = $package.PackageFamilyName + '!' + $appId
    $activationArguments = ''
    if ($OcrSmokeTest -or $OverlaySmokeTest -or $LargeOcrSmokeTest -or $SpatialSmokeTest) {
        if (Test-Path -LiteralPath $result) { Remove-Item -LiteralPath $result -Force }
        $activationArguments = '--ocr-smoke-test "' + $result + '"'
        if ($OverlaySmokeTest) { $activationArguments = '--overlay-smoke-test "' + $result + '"' }
        if ($LargeOcrSmokeTest) { $activationArguments = '--large-ocr-smoke-test "' + $result + '"' }
        if ($SpatialSmokeTest) { $activationArguments = '--spatial-smoke-test "' + $result + '"' }
    }
    $hr = [StoryScreenTrialActivation]::Activate($aumid, $activationArguments, [ref]$appProcessId)
    if ($hr -lt 0) { [Runtime.InteropServices.Marshal]::ThrowExceptionForHR($hr) }
    if ($OcrSmokeTest -or $OverlaySmokeTest -or $LargeOcrSmokeTest -or $SpatialSmokeTest) {
        $deadline = (Get-Date).AddSeconds(40)
        while ((Get-Date) -lt $deadline -and -not (Test-Path -LiteralPath $result)) { Start-Sleep -Milliseconds 250 }
        if (-not (Test-Path -LiteralPath $result)) { throw 'Synthetic OCR smoke timed out.' }
        $report = Get-Content -LiteralPath $result -Raw
        $report
        if (($report | ConvertFrom-Json).Status -ne 'passed') { throw 'Synthetic OCR smoke failed.' }
    } else {
        Write-Host 'App opened. Enter key only inside the app. Close the app to remove its temporary package registration.'
        $app = Get-Process -Id $appProcessId -ErrorAction SilentlyContinue
        if ($app) { Wait-Process -Id $appProcessId }
    }
} finally {
    if ($appProcessId) {
        $app = Get-Process -Id $appProcessId -ErrorAction SilentlyContinue
        $expectedExe = Join-Path $layout 'StoryTranslator.App.exe'
        if ($app -and $app.Path -eq $expectedExe) { Stop-Process -Id $appProcessId -Force }
    }
    if ($package) {
        $current = Get-AppxPackage -Name $name
        if ($current -and $current.PackageFullName -eq $package.PackageFullName -and
            [IO.Path]::GetFullPath($current.InstallLocation).TrimEnd('\').Equals([IO.Path]::GetFullPath($layout).TrimEnd('\'), [StringComparison]::OrdinalIgnoreCase)) {
            Remove-AppxPackage -Package $package.PackageFullName
        }
    }
    if (Get-AppxPackage -Name $name) { Write-Warning 'Package registration remains. Check before running the trial again.' }
    else { Write-Host 'Temporary package registration removed.' }
}
