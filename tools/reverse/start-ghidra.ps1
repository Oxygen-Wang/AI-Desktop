param([string]$Binary='C:\Program Files (x86)\Stardock\Fences\DesktopDock64.dll',[int]$Port=8081,[string]$ProjectName='DesktopDock64')
$ErrorActionPreference='Stop'
$taskRoot=Join-Path $env:USERPROFILE '.codex\tools\fences-re-tools'
$taskGhidra=(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'ghidra') -Directory | Where-Object {$_.Name -like 'ghidra_*'} | Select-Object -First 1).FullName
$taskExtension=Join-Path $taskRoot 'ghidra-mcp-release\GhidraMCP-release-2-0\extension\GhidraMCP\lib\GhidraMCP.jar'
if(!(Test-Path -LiteralPath $Binary)){throw "Binary not found: $Binary"}
if(Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue){throw "Port $Port is already in use"}
$env:JAVA_HOME=Join-Path $taskRoot 'jdk-21'
$env:GHIDRA_INSTALL_DIR=$taskGhidra
$taskProject=Join-Path $PSScriptRoot '..\..\research\ghidra-projects'
$taskProject=[System.IO.Path]::GetFullPath($taskProject)
New-Item -ItemType Directory -Force -Path $taskProject | Out-Null
$taskArguments=@('-m','ghidra_mcp','--headless','--binary',('"'+(Resolve-Path -LiteralPath $Binary).Path+'"'),'--project',('"'+$taskProject+'"'),'--project-name',$ProjectName,'--ghidra-install-dir',('"'+$taskGhidra+'"'),'--extension-jar',('"'+$taskExtension+'"'),'--transport','streamable-http','--mcp-host','127.0.0.1','--mcp-port',[string]$Port,'--ready-timeout','45')
$taskPython=Join-Path $taskRoot 'python\Scripts\python.exe'
$taskProcess=Start-Process -FilePath $taskPython -ArgumentList $taskArguments -Environment @{JAVA_HOME=$env:JAVA_HOME;GHIDRA_INSTALL_DIR=$taskGhidra} -WorkingDirectory $taskRoot -WindowStyle Hidden -RedirectStandardOutput (Join-Path $taskRoot "$ProjectName.out.log") -RedirectStandardError (Join-Path $taskRoot "$ProjectName.err.log") -PassThru
$taskProcess.Id | Set-Content -LiteralPath (Join-Path $taskRoot "$ProjectName.pid")
Write-Output "GhidraMCP pid=$($taskProcess.Id), endpoint=http://127.0.0.1:$Port/mcp, binary=$Binary"
