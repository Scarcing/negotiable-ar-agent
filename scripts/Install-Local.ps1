param([string]$Version='0.40.13')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$tools=Join-Path $projectRoot '.tools'
New-Item -ItemType Directory -Force $tools | Out-Null
$archive=Join-Path $tools 'ollama.zip'
if (-not (Test-Path -LiteralPath (Join-Path $tools 'ollama\ollama.exe'))) {
    & curl.exe -L --fail --retry 3 --connect-timeout 20 --output $archive "https://github.com/ollama/ollama/releases/download/v$Version/ollama-windows-amd64.zip"
    if ($LASTEXITCODE -ne 0) { throw 'Ollama download failed.' }
    Expand-Archive -LiteralPath $archive -DestinationPath (Join-Path $tools 'ollama') -Force
}
Write-Host 'Portable Ollama installed. Run scripts\Start-Local.ps1 next.'
