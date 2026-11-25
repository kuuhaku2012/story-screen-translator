$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$layout=Join-Path $projectRoot 'artifacts\ocr-evaluation-package\layout'
$manifestPath=Join-Path $layout 'AppxManifest.xml'
$name='KuuHaku.StoryTranslator.Evaluation'
if (Get-AppxPackage -Name $name) { throw 'Evaluation registration already exists; no registration was changed.' }
[xml]$manifest=Get-Content -LiteralPath $manifestPath -Raw
$manifest.Package.Identity.Name=$name
$manifest.Save($manifestPath)
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class StoryOcrEvalActivation {
 [ComImport, Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")] private class Manager {}
 [ComImport, Guid("2e941141-7f97-4756-ba1d-9decde894a3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
 private interface IManager {
  [PreserveSig] int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string id,
   [MarshalAs(UnmanagedType.LPWStr)] string args,uint options,out uint pid);
 }
 public static int Activate(string id,string args,out uint pid) {
  var manager=(IManager)new Manager();
  try { return manager.ActivateApplication(id,args,0,out pid); }
  finally { Marshal.ReleaseComObject(manager); }
 }
}
'@
$package=$null
[uint32]$appProcessId=0
$result=Join-Path $projectRoot 'artifacts\multimodal-evaluation\windows-ocr.json'
try {
 Add-AppxPackage -Register -Path $manifestPath
 $package=Get-AppxPackage -Name $name
 if (-not $package) { throw 'Evaluation registration failed.' }
 if (Test-Path -LiteralPath $result) { Remove-Item -LiteralPath $result }
 $inputManifest=Join-Path $projectRoot 'artifacts\multimodal-evaluation\manifest.json'
 $aumid=$package.PackageFamilyName+'!'+[string]$manifest.Package.Applications.Application.Id
 $hr=[StoryOcrEvalActivation]::Activate($aumid,('--evaluate-ocr "'+$inputManifest+'"'),[ref]$appProcessId)
 if ($hr -lt 0) { [Runtime.InteropServices.Marshal]::ThrowExceptionForHR($hr) }
 $deadline=(Get-Date).AddMinutes(4)
 while ((Get-Date) -lt $deadline -and -not (Test-Path -LiteralPath $result)) { Start-Sleep -Milliseconds 250 }
 if (-not (Test-Path -LiteralPath $result)) { throw 'Windows OCR evaluation timed out.' }
 $report=Get-Content -LiteralPath $result -Raw | ConvertFrom-Json
 if ($report.Status -ne 'completed') { throw "OCR evaluation failed: $($report.Error)" }
 Write-Output "Windows OCR completed: $($report.Reports.Count) mode/input pairs."
} finally {
 if ($appProcessId) {
  $app=Get-Process -Id $appProcessId -ErrorAction SilentlyContinue
  if ($app -and $app.Path -eq (Join-Path $layout 'StoryTranslator.App.exe')) { Stop-Process -Id $appProcessId -Force }
 }
 if ($package) { Remove-AppxPackage -Package $package.PackageFullName }
}
