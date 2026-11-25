[CmdletBinding()]
param(
    [string] $OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\msix'),
    [string] $CertificateThumbprint,
    [switch] $SelfContained
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools\dotnet-home'
$env:NUGET_PACKAGES = Join-Path $projectRoot '.tools\nuget-packages'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$outputPath = [IO.Path]::GetFullPath($OutputDirectory)
$rootPrefix = $projectRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $outputPath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputDirectory must be inside project root '$projectRoot'."
}

$dotnet = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet -PathType Leaf)) {
    throw "Project-local .NET SDK not found at '$dotnet'."
}
$manifest = Join-Path $projectRoot 'Package.appxmanifest'
$assets = Join-Path $projectRoot 'Assets'
if (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) { throw "Missing package manifest: $manifest" }

New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
$publishPath = Join-Path $outputPath 'publish-win-x64'
$layoutPath = Join-Path $outputPath 'layout'
foreach ($ownedPath in @($publishPath, $layoutPath)) {
    $fullOwnedPath = [IO.Path]::GetFullPath($ownedPath)
    if (-not $fullOwnedPath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean path outside project root: $fullOwnedPath"
    }
    if (Test-Path -LiteralPath $fullOwnedPath) {
        Remove-Item -LiteralPath $fullOwnedPath -Recurse -Force
    }
}
New-Item -ItemType Directory -Path $publishPath, $layoutPath -Force | Out-Null

$selfContainedValue = if ($SelfContained) { 'true' } else { 'false' }
& $dotnet publish (Join-Path $projectRoot 'src\StoryTranslator.App\StoryTranslator.App.csproj') `
    --configuration Release --runtime win-x64 --self-contained $selfContainedValue `
    -p:PublishSingleFile=false -p:PublishTrimmed=false `
    --output $publishPath
if ($LASTEXITCODE -ne 0) { throw "Self-contained win-x64 publish failed with exit code $LASTEXITCODE." }

Copy-Item -Path (Join-Path $publishPath '*') -Destination $layoutPath -Recurse -Force
Copy-Item -LiteralPath $manifest -Destination (Join-Path $layoutPath 'AppxManifest.xml') -Force
New-Item -ItemType Directory -Path (Join-Path $layoutPath 'Assets') -Force | Out-Null
Copy-Item -Path (Join-Path $assets '*') -Destination (Join-Path $layoutPath 'Assets') -Recurse -Force

$sdkRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
$makeAppx = Get-ChildItem -Path $sdkRoot -Filter makeappx.exe -File -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.Directory.Name -eq 'x64' } |
    Sort-Object { [version](Split-Path -Leaf (Split-Path -Parent (Split-Path -Parent $_.FullName))) } -Descending |
    Select-Object -First 1
if (-not $makeAppx) { throw "Windows SDK x64 makeappx.exe not found beneath '$sdkRoot'." }

$packageFileName = if ($SelfContained) {
    'StoryTranslator-1.0.0.0-x64-self-contained.msix'
} else {
    'StoryTranslator-1.0.0.0-x64.msix'
}
$packagePath = Join-Path $outputPath $packageFileName
if (Test-Path -LiteralPath $packagePath) { Remove-Item -LiteralPath $packagePath -Force }
& $makeAppx.FullName pack /d $layoutPath /p $packagePath /o
if ($LASTEXITCODE -ne 0) { throw "MakeAppx failed with exit code $LASTEXITCODE." }

if (-not [string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
    $normalizedThumbprint = ($CertificateThumbprint -replace '\s', '').ToUpperInvariant()
    $certificate = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Thumbprint -eq $normalizedThumbprint } | Select-Object -First 1
    if (-not $certificate) { throw "No existing CurrentUser\My certificate has thumbprint '$normalizedThumbprint'. No certificate was created or imported." }
    if (-not $certificate.HasPrivateKey) { throw 'The selected existing certificate has no private key.' }
    $publisher = ([xml](Get-Content -LiteralPath $manifest -Raw)).Package.Identity.Publisher
    if ($certificate.Subject -ne $publisher) { throw "Existing certificate subject '$($certificate.Subject)' does not match manifest Publisher '$publisher'." }
    $signtool = Get-ChildItem -Path $sdkRoot -Filter signtool.exe -File -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.Directory.Name -eq 'x64' } |
        Sort-Object { [version](Split-Path -Leaf (Split-Path -Parent (Split-Path -Parent $_.FullName))) } -Descending |
        Select-Object -First 1
    if (-not $signtool) { throw "Windows SDK x64 signtool.exe not found beneath '$sdkRoot'." }
    & $signtool.FullName sign /fd SHA256 /sha1 $normalizedThumbprint /s My $packagePath
    if ($LASTEXITCODE -ne 0) { throw "SignTool failed with exit code $LASTEXITCODE." }
}

$packageItem = Get-Item -LiteralPath $packagePath
[pscustomobject]@{
    PackagePath = $packageItem.FullName
    SizeBytes = $packageItem.Length
    Signed = -not [string]::IsNullOrWhiteSpace($CertificateThumbprint)
    SelfContained = [bool]$SelfContained
    MakeAppx = $makeAppx.FullName
} | Format-List
