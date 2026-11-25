$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'trial-registration-policy.ps1')
$project='E:\tool_dich_man_hinh'
$target=Join-Path $project 'artifacts\panel-trial\layout'
$cases=@(
 @{path='E:\tool_dich_man_hinh\artifacts\diagnostic-trial\layout';dev=$true;running=$false;expected='Recover'},
 @{path='E:\tool_dich_man_hinh\artifacts\screen-trial\layout';dev=$true;running=$false;expected='Recover'},
 @{path=$target;dev=$true;running=$true;expected='Running'},
 @{path=$target;dev=$false;running=$false;expected='Foreign'},
 @{path='C:\Other\layout';dev=$true;running=$false;expected='Foreign'},
 @{path='E:\tool_dich_man_hinh\artifacts\diagnostic-trial\layout-other';dev=$true;running=$false;expected='Foreign'}
)
foreach ($case in $cases) {
 $action=Get-TrialRegistrationAction $project $target $case.path $case.dev $case.running
 if ($action -ne $case.expected) {throw "Policy mismatch: $action versus $($case.expected)"}
}
Write-Output 'PASS: six registration ownership/running policy cases; no package mutations.'
