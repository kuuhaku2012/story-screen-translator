function Get-TrialRegistrationAction {
    param([string]$ProjectRoot,[string]$TargetLayout,[string]$RegisteredLayout,[bool]$Development,[bool]$AppRunning)
    if (-not $Development -or [string]::IsNullOrWhiteSpace($RegisteredLayout)) { return 'Foreign' }
    $registered=[IO.Path]::GetFullPath($RegisteredLayout).TrimEnd('\')
    $allowed=@($TargetLayout)+( @('screen-trial','diagnostic-trial','panel-trial') | ForEach-Object {Join-Path $ProjectRoot "artifacts\$_\layout"} )
    $owned=@($allowed | Where-Object {$registered.Equals([IO.Path]::GetFullPath($_).TrimEnd('\'),[StringComparison]::OrdinalIgnoreCase)}).Count -gt 0
    if (-not $owned) {return 'Foreign'}
    if ($AppRunning) {return 'Running'}
    return 'Recover'
}
