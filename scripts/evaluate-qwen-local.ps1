param(
    [ValidateSet('0.5b', '1.5b')]
    [string]$ModelSize = '0.5b'
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$binary = Join-Path $root '.tools\llama-cpp-b11669-win-cpu-x64\bin\llama-server.exe'
$modelFile = if ($ModelSize -eq '0.5b') { 'qwen2.5-0.5b-instruct-q4_k_m.gguf' } else { 'qwen2.5-1.5b-instruct-q4_k_m.gguf' }
$modelHash = if ($ModelSize -eq '0.5b') {
    '74a4da8c9fdbcd15bd1f6d01d621410d31c6fc00986f5eb687824e7b93d7a9db'
} else {
    '6a1a2eb6d15622bf3c96857206351ba97e1af16c30d7a74ee38970e434e9407e'
}
$model = Join-Path $root "models\local-chat\$modelFile"
$outputDir = Join-Path $root 'artifacts\local-llm-evaluation'
$outputStem = "qwen2.5-$ModelSize-cpu"
$outputPath = Join-Path $outputDir "$outputStem.json"
$stdoutPath = Join-Path $outputDir "$outputStem-server.stdout.log"
$stderrPath = Join-Path $outputDir "$outputStem-server.stderr.log"
$threadCount = 2
$inputs = @(
    @{ id='manga-gripes'; source='THEY REHASH THE SAME GRIPES OVER AND OVER AGAIN.' },
    @{ id='manga-loses'; source='I KNOW, I KNOW. THE ONE WHO FALLS THE HARDEST ALWAYS LOSES.' },
    @{ id='manga-back'; source='BUT I CANNOT BELIEVE SHE UP AND DIED ON ME BEHIND MY BACK!' },
    @{ id='manga-haru'; source='IT ALL BOILS DOWN TO HARU''S NARCISSISTIC TENDENCIES.' },
    @{ id='manga-crap'; source='I COULD NOT GIVE LESS OF A CRAP.' },
    @{ id='manga-dorks'; source='WHY THE HELL DO I HANG OUT WITH THESE DORKS EVERY NIGHT?' },
    @{ id='video-wind'; source="The wind shakes laden frost; the umbrella's ribs groan." },
    @{ id='video-feeling'; source='I knew you were born of feeling. Your capacity for joy and sorrow far exceeds my own.' },
    @{ id='video-walk'; source='Go. Walk forward, one step at a time. Walk farther and better than I ever could.' },
    @{ id='short-here'; source='Here you go!' }
)

if (-not (Test-Path -LiteralPath $binary)) { throw "Missing runtime: $binary" }
if (-not (Test-Path -LiteralPath $model)) { throw "Missing model: $model" }
if ((Get-FileHash -Algorithm SHA256 -LiteralPath $model).Hash.ToLowerInvariant() -ne $modelHash) { throw 'Model SHA-256 does not match the pinned official artifact.' }
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
$listener.Stop()
$serverProcess = $null
$records = [System.Collections.Generic.List[object]]::new()
$serverStarted = [System.Diagnostics.Stopwatch]::StartNew()

try {
    $arguments = @(
        '--model', $model,
        '--host', '127.0.0.1',
        '--port', "$port",
        '--threads', "$threadCount",
        '--threads-batch', "$threadCount",
        '--prio', '-1',
        '--ctx-size', '2048',
        '--predict', '96',
        '--parallel', '1',
        '--n-gpu-layers', '0',
        '--no-webui'
    )
    $serverProcess = Start-Process -FilePath $binary -ArgumentList $arguments -WorkingDirectory (Split-Path $binary) `
        -WindowStyle Hidden -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -PassThru
    $baseUrl = "http://127.0.0.1:$port"
    $ready = $false
    for ($i = 0; $i -lt 120; $i++) {
        if ($serverProcess.HasExited) { throw "llama-server exited early. See $stderrPath" }
        try {
            $null = Invoke-RestMethod -Uri "$baseUrl/health" -TimeoutSec 2
            $ready = $true
            break
        } catch {
            Start-Sleep -Milliseconds 500
        }
    }
    if (-not $ready) { throw "llama-server did not become ready. See $stderrPath" }
    $loadMilliseconds = $serverStarted.Elapsed.TotalMilliseconds
    $process = Get-Process -Id $serverProcess.Id
    $cpuBefore = $process.TotalProcessorTime.TotalMilliseconds
    $wallStart = [System.Diagnostics.Stopwatch]::StartNew()

    foreach ($item in $inputs) {
        $body = @{
            model = 'qwen-local'
            messages = @(
                @{ role='system'; content='Translate English dialogue into natural Vietnamese. Preserve character names exactly: Haru, Nazuna, Kabura. Keep the original meaning and conversational tone. Return only the Vietnamese translation, with no explanation.' },
                @{ role='user'; content="Translate: $($item.source)" }
            )
            temperature = 0
            max_tokens = 96
            stream = $false
        } | ConvertTo-Json -Depth 8 -Compress
        $started = [System.Diagnostics.Stopwatch]::StartNew()
        $response = Invoke-RestMethod -Method Post -Uri "$baseUrl/v1/chat/completions" `
            -ContentType 'application/json' -Body $body -TimeoutSec 60
        $started.Stop()
        $records.Add(@{
            Id = $item.id
            Source = $item.source
            Translation = [string]$response.choices[0].message.content
            Milliseconds = $started.Elapsed.TotalMilliseconds
            PromptTokens = $response.usage.prompt_tokens
            CompletionTokens = $response.usage.completion_tokens
        })
        Write-Output ("{0}: {1:N0} ms, {2} tokens" -f $item.id, $started.Elapsed.TotalMilliseconds, $response.usage.completion_tokens)
    }

    $wallStart.Stop()
    $process.Refresh()
    $cpuAfter = $process.TotalProcessorTime.TotalMilliseconds
    $report = @{
        Status = 'completed'
        Model = "Qwen/Qwen2.5-$ModelSize-Instruct-GGUF Q4_K_M"
        ModelSha256 = $modelHash
        Runtime = 'llama.cpp b11669 Windows x64 CPU'
        Threads = $threadCount
        GpuLayers = 0
        LoopbackOnly = $true
        ServerStartupMilliseconds = $loadMilliseconds
        TotalWallMilliseconds = $wallStart.Elapsed.TotalMilliseconds
        ProcessCpuMilliseconds = ($cpuAfter - $cpuBefore)
        PeakWorkingSetMiB = ($process.PeakWorkingSet64 / 1MB)
        P50RequestMilliseconds = (($records | ForEach-Object Milliseconds | Sort-Object)[[math]::Floor(($records.Count-1)/2)])
        P95RequestMilliseconds = (($records | ForEach-Object Milliseconds | Sort-Object)[[math]::Ceiling(($records.Count-1)*0.95)])
        Inputs = @($records)
    }
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $outputPath -Encoding utf8
    Write-Output "Report: $outputPath"
} finally {
    if ($serverProcess -and -not $serverProcess.HasExited) {
        $ownedProcess = Get-CimInstance Win32_Process -Filter "ProcessId = $($serverProcess.Id)" -ErrorAction SilentlyContinue
        if ($ownedProcess -and [System.IO.Path]::GetFullPath($ownedProcess.ExecutablePath) -eq [System.IO.Path]::GetFullPath($binary)) {
            Stop-Process -Id $serverProcess.Id -Force
        }
    }
}
