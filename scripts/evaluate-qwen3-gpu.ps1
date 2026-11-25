$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$binary = Join-Path $root '.tools\llama-cpp-b11755-win-cuda12-x64\llama-server.exe'
$model = Join-Path $root 'models\local-chat\qwen3-4b-q4_k_m.gguf'
$modelHash = '7485fe6f11af29433bc51cab58009521f205840f5b4ae3a32fa7f92e8534fdf5'
$outputDir = Join-Path $root 'artifacts\local-llm-evaluation'
$outputPath = Join-Path $outputDir 'qwen3-4b-gpu.json'
$stdoutPath = Join-Path $outputDir 'qwen3-4b-gpu-server.stdout.log'
$stderrPath = Join-Path $outputDir 'qwen3-4b-gpu-server.stderr.log'
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

function Get-GpuMemoryUsedMiB {
    $rows = & nvidia-smi --query-gpu=memory.used --format=csv,noheader,nounits
    $exitCode = $LASTEXITCODE
    $value = ([string](@($rows)[0])).Trim()
    if ($exitCode -ne 0 -or $value -notmatch '^\d+$') { throw 'nvidia-smi did not return GPU memory usage.' }
    return [int]$value
}

if (-not (Test-Path -LiteralPath $binary)) { throw "Missing CUDA runtime: $binary" }
if (-not (Test-Path -LiteralPath $model)) { throw "Missing model: $model" }
if ((Get-FileHash -Algorithm SHA256 -LiteralPath $model).Hash.ToLowerInvariant() -ne $modelHash) { throw 'Model SHA-256 does not match the official pinned artifact.' }
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
$listener.Stop()
$serverProcess = $null
$records = [System.Collections.Generic.List[object]]::new()
$gpuBefore = Get-GpuMemoryUsedMiB
$serverStarted = [System.Diagnostics.Stopwatch]::StartNew()

try {
    $arguments = @(
        '--model', $model,
        '--host', '127.0.0.1',
        '--port', "$port",
        '--threads', '2',
        '--threads-batch', '2',
        '--ctx-size', '2048',
        '--predict', '96',
        '--parallel', '1',
        '--n-gpu-layers', '99',
        '--ubatch-size', '128',
        '--flash-attn', 'auto',
        '--no-webui'
    )
    $serverProcess = Start-Process -FilePath $binary -ArgumentList $arguments -WorkingDirectory (Split-Path $binary) `
        -WindowStyle Hidden -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -PassThru
    $baseUrl = "http://127.0.0.1:$port"
    $ready = $false
    for ($i = 0; $i -lt 180; $i++) {
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
    $startupMilliseconds = $serverStarted.Elapsed.TotalMilliseconds
    $gpuAfterLoad = Get-GpuMemoryUsedMiB
    $process = Get-Process -Id $serverProcess.Id
    $cpuBefore = $process.TotalProcessorTime.TotalMilliseconds
    $wallStart = [System.Diagnostics.Stopwatch]::StartNew()

    foreach ($item in $inputs) {
        $body = @{
            model = 'qwen3-local'
            messages = @(
                @{ role='system'; content='Translate English dialogue into natural Vietnamese. Preserve names exactly: Haru, Nazuna, Kabura. Keep the intended meaning and conversational tone. Return only the Vietnamese translation. /no_think' },
                @{ role='user'; content="Translate: $($item.source) /no_think" }
            )
            temperature = 0
            max_tokens = 96
            stream = $false
        } | ConvertTo-Json -Depth 8 -Compress
        $started = [System.Diagnostics.Stopwatch]::StartNew()
        $response = Invoke-RestMethod -Method Post -Uri "$baseUrl/v1/chat/completions" `
            -ContentType 'application/json' -Body $body -TimeoutSec 90
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
    $report = @{
        Status = 'completed'
        Model = 'Qwen/Qwen3-4B-GGUF Q4_K_M'
        ModelSha256 = $modelHash
        Runtime = 'llama.cpp b11755 Windows x64 CUDA 12.4'
        Gpu = 'NVIDIA GeForce RTX 3050 Laptop GPU'
        GpuTotalMiB = 4096
        GpuUsedBeforeMiB = $gpuBefore
        GpuUsedAfterLoadMiB = $gpuAfterLoad
        GpuModelDeltaMiB = ($gpuAfterLoad - $gpuBefore)
        GpuLayers = 99
        Threads = 2
        LoopbackOnly = $true
        ServerStartupMilliseconds = $startupMilliseconds
        TotalWallMilliseconds = $wallStart.Elapsed.TotalMilliseconds
        ProcessCpuMilliseconds = ($process.TotalProcessorTime.TotalMilliseconds - $cpuBefore)
        PeakWorkingSetMiB = ($process.PeakWorkingSet64 / 1MB)
        P50RequestMilliseconds = (($records | ForEach-Object Milliseconds | Sort-Object)[[math]::Floor(($records.Count-1)/2)])
        P95RequestMilliseconds = (($records | ForEach-Object Milliseconds | Sort-Object)[[math]::Ceiling(($records.Count-1)*0.95)])
        Inputs = @($records)
    }
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $outputPath -Encoding utf8
    Write-Output "Report: $outputPath"
} finally {
    if ($serverProcess -and -not $serverProcess.HasExited) {
        Stop-Process -Id $serverProcess.Id -Force
    }
}
