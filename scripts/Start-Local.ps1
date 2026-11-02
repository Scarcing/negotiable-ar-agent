param(
    [string]$Python = "python",
    [string]$Ollama = "",
    [switch]$Lan,
    [string]$Token = ""
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if ($Python -eq 'python') {
    $bundledPython = Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
    if (Test-Path -LiteralPath $bundledPython) { $Python = $bundledPython }
}
if (-not $Ollama) {
    $portable = Join-Path $projectRoot '.tools\ollama\ollama.exe'
    if (Test-Path -LiteralPath $portable) { $Ollama = $portable } else { $Ollama = 'ollama' }
}
$env:OLLAMA_HOST = '127.0.0.1:11434'
$env:OLLAMA_MODELS = Join-Path $projectRoot '.tools\models'
try { Invoke-RestMethod 'http://127.0.0.1:11434/api/version' -TimeoutSec 2 | Out-Null }
catch {
    Start-Process -FilePath $Ollama -ArgumentList 'serve' -WindowStyle Hidden -RedirectStandardOutput (Join-Path $projectRoot 'ollama.log') -RedirectStandardError (Join-Path $projectRoot 'ollama-error.log') | Out-Null
    $ready = $false
    for ($attempt=0; $attempt -lt 20; $attempt++) {
        Start-Sleep -Milliseconds 500
        try { Invoke-RestMethod 'http://127.0.0.1:11434/api/version' -TimeoutSec 1 | Out-Null; $ready=$true; break } catch {}
    }
    if (-not $ready) { throw 'Ollama did not start. Inspect ollama-error.log.' }
}
if (-not $env:VLM_MODEL) { $env:VLM_MODEL = 'qwen3-vl:2b-instruct' }
& $Ollama pull $env:VLM_MODEL
if ($LASTEXITCODE -ne 0) { throw 'Could not download local VLM.' }
Write-Host 'Warming the local model before starting interactive requests...'
& $Python (Join-Path $projectRoot 'server\warmup.py')
if ($LASTEXITCODE -ne 0) { throw 'Local model warmup failed.' }
$env:AR_HOST = '127.0.0.1'
if ($Lan) {
    if (-not $Token) { throw 'LAN mode requires -Token; copy it into AgentSettings.accessToken.' }
    $env:AR_HOST = '0.0.0.0'; $env:AR_ACCESS_TOKEN = $Token
}
& $Python (Join-Path $projectRoot 'server\service.py')
