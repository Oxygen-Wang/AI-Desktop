param([string]$Workspace="")
$ErrorActionPreference = 'Stop'
$taskDist = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\dist'))
$taskMcpLatest = Join-Path $taskDist 'latest-mcp.txt'
if (!(Test-Path -LiteralPath $taskMcpLatest)) { throw 'Build QiDesk before starting MCP.' }
$taskMcpExe = (Get-Content -LiteralPath $taskMcpLatest -Raw).Trim()
if (!(Test-Path -LiteralPath $taskMcpExe)) { throw 'MCP executable missing. Rebuild QiDesk.' }
if ($Workspace) { & $taskMcpExe --mcp --workspace $Workspace } else { & $taskMcpExe --mcp }
exit $LASTEXITCODE
