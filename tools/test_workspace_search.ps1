$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskApp = (Get-Content -LiteralPath (Join-Path $taskRoot 'dist/latest.txt')).Trim()
$taskBridge = (Get-Content -LiteralPath (Join-Path $taskRoot 'dist/latest-mcp.txt')).Trim()
$taskFixture = Join-Path $taskRoot ('dist/search-mcp-fixture-' + [guid]::NewGuid().ToString('N'))
$taskAlpha = Join-Path $taskFixture 'Project Alpha'
$taskBeta = Join-Path $taskFixture 'Project Beta'
New-Item -ItemType Directory -Path $taskAlpha,$taskBeta | Out-Null
[IO.File]::WriteAllText((Join-Path $taskAlpha 'report.txt'),'alpha')
[IO.File]::WriteAllText((Join-Path $taskAlpha 'private.txt'),'private')
[IO.File]::WriteAllText((Join-Path $taskAlpha '.gitignore'),'private.txt')
[IO.File]::WriteAllText((Join-Path $taskBeta 'report.txt'),'beta')
[IO.File]::WriteAllText((Join-Path $taskBeta '中文报告.txt'),'unicode')
[IO.File]::WriteAllText((Join-Path $taskFixture 'state.json'),'{"Version":4,"McpEnabled":true,"UseEverything":false,"Groups":[],"Rules":[]}')
$taskPipe = 'QiDesk-Search-Test-' + [guid]::NewGuid().ToString('N')
$taskHost = Start-Process -FilePath $taskApp -ArgumentList @('--mcp-fixture',('"'+$taskFixture+'"'),'--pipe',$taskPipe) -WindowStyle Hidden -PassThru
$taskInfo = New-Object Diagnostics.ProcessStartInfo
$taskInfo.FileName=$taskBridge
$taskInfo.Arguments='--mcp --pipe '+$taskPipe
$taskInfo.UseShellExecute=$false
$taskInfo.CreateNoWindow=$true
$taskInfo.RedirectStandardInput=$true
$taskInfo.RedirectStandardOutput=$true
$taskInfo.RedirectStandardError=$true
$taskInfo.StandardOutputEncoding=[Text.Encoding]::UTF8
$taskInfo.StandardInputEncoding=[Text.UTF8Encoding]::new($false)
$taskClient = [Diagnostics.Process]::Start($taskInfo)
$taskResults = [Collections.Generic.List[string]]::new()
$script:taskSequence = 0
function Invoke-TaskRpc($method,$parameters) {
    $script:taskSequence++
    $taskClient.StandardInput.WriteLine((@{jsonrpc='2.0';id=$script:taskSequence;method=$method;params=$parameters} | ConvertTo-Json -Compress -Depth 15))
    $taskRead = $taskClient.StandardOutput.ReadLineAsync()
    if (!$taskRead.Wait(15000)) { throw 'MCP response timed out' }
    $taskResponse = $taskRead.Result | ConvertFrom-Json
    if ($taskResponse.error) { throw ($taskResponse.error | ConvertTo-Json -Compress) }
    return $taskResponse.result
}
function Invoke-TaskTool($name,$arguments) {
    $taskResult = Invoke-TaskRpc 'tools/call' @{name=$name;arguments=$arguments}
    if ($taskResult.isError) { throw $taskResult.content[0].text }
    return ($taskResult.content[0].text | ConvertFrom-Json)
}
function Assert-Task($condition,$description) {
    if (!$condition) { throw ('FAILED: '+$description) }
    $taskResults.Add('PASS: '+$description)
}
try {
    $taskInit=Invoke-TaskRpc 'initialize' @{protocolVersion='2024-11-05';capabilities=@{};clientInfo=@{name='SearchIntegrationTest';version='1'}}
    $taskClient.StandardInput.WriteLine('{"jsonrpc":"2.0","method":"notifications/initialized"}')
    $taskTools=Invoke-TaskRpc 'tools/list' @{}
    Assert-Task (@($taskTools.tools.name | Sort-Object -Unique).Count -eq @($taskTools.tools).Count) 'MCP tools have unique names'
    Assert-Task (@($taskTools.tools | Where-Object name -eq 'qidesk_show_workspace').Count -eq 1) 'stdio exposes the floating workspace launcher tool'
    $taskArrangement=Invoke-TaskTool 'qidesk_arrange_groups' @{scope='current'}
    Assert-Task ($taskArrangement.arranged -eq 0 -and $taskArrangement.changed -eq 0) 'MCP arrangement tool handles an empty page without changing workspace configuration'
    Invoke-TaskTool 'qidesk_connect_workspace' @{path=$taskAlpha;source='test'} | Out-Null
    Invoke-TaskTool 'qidesk_connect_workspace' @{path=$taskBeta;source='test'} | Out-Null
    $taskCurrent=Invoke-TaskTool 'qidesk_get_current_workspace' @{}
    Assert-Task ($taskCurrent.path -eq $taskBeta) 'last MCP-connected project becomes current'
    Invoke-TaskTool 'qidesk_set_current_workspace' @{workspaceId='0'} | Out-Null
    $taskFound=Invoke-TaskTool 'qidesk_search_files' @{query='report'}
    Assert-Task (@($taskFound.Items).Count -eq 1 -and $taskFound.Items[0].Path -eq (Join-Path $taskAlpha 'report.txt')) 'MCP search only returns the selected project'
    $taskFound=Invoke-TaskTool 'qidesk_search_files' @{query='private'}
    Assert-Task (@($taskFound.Items).Count -eq 0) 'MCP search respects automatically imported project ignores'
    $taskFound=Invoke-TaskTool 'qidesk_search_files' @{query='report';workspaceId='1'}
    Assert-Task (@($taskFound.Items).Count -eq 1 -and $taskFound.Items[0].Path -eq (Join-Path $taskBeta 'report.txt')) 'explicit search scope does not change the current project'
    $taskCurrent=Invoke-TaskTool 'qidesk_get_current_workspace' @{}
    Assert-Task ($taskCurrent.path -eq $taskAlpha) 'read-only search leaves workspace selection unchanged'
    $taskBad=Invoke-TaskRpc 'tools/call' @{name='qidesk_set_current_workspace';arguments=@{workspaceId='999'}}
    Assert-Task $taskBad.isError 'invalid workspace selection is rejected'
    $taskSaved=Get-Content -LiteralPath (Join-Path $taskFixture 'state.json') -Raw | ConvertFrom-Json
    Assert-Task ($taskSaved.CurrentWorkspace -eq $taskAlpha) 'current workspace persists after invalid calls'
    Invoke-TaskTool 'qidesk_update_settings' @{settings=@{UseEverything=$true}} | Out-Null
    Invoke-TaskTool 'qidesk_connect_workspace' @{path=$taskRoot;source='test index'} | Out-Null
    $taskFound=Invoke-TaskTool 'qidesk_search_files' @{query='README.md'}
    Assert-Task ($taskFound.Provider -eq 'Everything' -and @($taskFound.Items | Where-Object Path -eq (Join-Path $taskRoot 'README.md')).Count -eq 1) 'real Everything backend finds the project README using positional search syntax'
    $taskFound=Invoke-TaskTool 'qidesk_search_files' @{query='中文报告';workspaceId='1'}
    Assert-Task ($taskFound.Provider -eq 'Everything' -and @($taskFound.Items).Count -eq 1 -and $taskFound.Items[0].Path -eq (Join-Path $taskBeta '中文报告.txt')) 'real Everything results preserve Chinese filenames through UTF-8 export'
    Invoke-TaskTool 'qidesk_search_files' @{query='-exit'} | Out-Null
    $taskFound=Invoke-TaskTool 'qidesk_search_files' @{query='README.md'}
    Assert-Task ($taskFound.Provider -eq 'Everything' -and @($taskFound.Items).Count -gt 0) 'filename search resembling a CLI command does not exit Everything'
} finally {
    $taskClient.StandardInput.Close()
    if (!$taskClient.WaitForExit(3000)) { $taskClient.Kill() }
    if (!$taskHost.HasExited) { $taskHost.Kill();$taskHost.WaitForExit() }
    $taskResults | Set-Content -LiteralPath (Join-Path $taskRoot 'dist/workspace-search-mcp-results.txt') -Encoding utf8
}
$taskResults
