param([string]$UnityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.3.27f1\Editor\Unity.exe')
$ErrorActionPreference='Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$process = Start-Process -FilePath $UnityEditor -ArgumentList @('-batchmode','-quit','-projectPath',('"'+(Join-Path $projectRoot 'Unity')+'"'),'-buildTarget','Android','-executeMethod','NegotiableAR.Editor.ProjectSetup.BuildAndroid','-logFile',('"'+(Join-Path $projectRoot 'unity-build.log')+'"')) -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0) { throw 'Unity build failed. Inspect unity-build.log.' }
Write-Host (Join-Path $projectRoot 'Unity\Builds\NegotiableAR.apk')
