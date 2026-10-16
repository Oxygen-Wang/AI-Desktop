$ErrorActionPreference = 'Stop'
$taskDist = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\dist'))
$taskExe = Join-Path $taskDist 'QiDesk.exe'
if(Test-Path -LiteralPath (Join-Path $taskDist 'latest.txt')){$taskExe=(Get-Content -LiteralPath (Join-Path $taskDist 'latest.txt')).Trim()}
if (!(Test-Path -LiteralPath $taskExe)) { & (Join-Path $PSScriptRoot 'build.ps1');$taskExe=(Get-Content -LiteralPath (Join-Path $taskDist 'latest.txt')).Trim() }
Start-Process -FilePath $taskExe -WorkingDirectory $PSScriptRoot -WindowStyle Hidden
