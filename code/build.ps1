$taskStamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$taskOutputName = 'QiDesk-'+$taskStamp+'.exe'
$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$taskDist = [IO.Path]::GetFullPath((Join-Path $taskRoot '..\dist'))
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.x compiler not found.' }
New-Item -ItemType Directory -Force -Path $taskDist | Out-Null
$taskOutputPath=Join-Path $taskDist $taskOutputName
$references = @('System.dll','System.Core.dll','System.Xml.dll','System.Web.Extensions.dll','System.Net.Http.dll','System.Drawing.dll','System.Windows.Forms.dll','Accessibility.dll','WPF\WindowsBase.dll','System.Xaml.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll')
$taskBase = @('/nologo','/platform:x64','/optimize+','/utf8output')
foreach ($reference in $references) { $taskBase += '/reference:' + (Join-Path $framework $reference) }
$taskFluentAssets = Get-ChildItem -LiteralPath (Join-Path $taskRoot 'assets\fluent') -Filter '*.svg'
foreach ($asset in $taskFluentAssets) { $taskBase += '/resource:' + $asset.FullName + ',QiDesk.Fluent.' + $asset.Name }
# Shipping binaries never include the test harness. Tests live in src\tests and are
# compiled only into the separate Tests-*.exe with /define:QIDESK_TESTS.
$taskAppSources = @(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
$taskTestSources = @(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'src\tests') -Filter '*.cs' | ForEach-Object { $_.FullName })
$arguments = $taskBase + @('/target:winexe',('/out:' + $taskOutputPath)) + $taskAppSources
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
$taskIconPath=Join-Path $taskRoot 'assets\qidesk.ico'
$taskIconSource=Join-Path $taskRoot 'assets\qidesk.svg'
if ((Test-Path -LiteralPath $taskIconSource) -and (!(Test-Path -LiteralPath $taskIconPath) -or (Get-Item -LiteralPath $taskIconSource).LastWriteTime -gt (Get-Item -LiteralPath $taskIconPath).LastWriteTime)) {
    $taskIconProcess=Start-Process -FilePath $taskOutputPath -ArgumentList @('--build-icon',$taskIconSource,$taskIconPath) -WindowStyle Hidden -PassThru -Wait
    if ($taskIconProcess.ExitCode -ne 0) { throw 'Icon generation failed.' }
}
if (Test-Path -LiteralPath $taskIconPath) { $arguments += '/win32icon:'+$taskIconPath; $arguments += '/resource:'+(Join-Path $taskRoot 'assets\qidesk.png')+',QiDesk.Logo'; & $compiler @arguments; if ($LASTEXITCODE -ne 0) { throw 'Icon compilation failed.' } }
$taskMcpOutputPath=Join-Path $taskDist ('Mcp-'+$taskOutputName)
$taskMcpArguments=$arguments | ForEach-Object { if ($_ -eq '/target:winexe') { '/target:exe' } elseif ($_ -like '/out:*') { '/out:'+$taskMcpOutputPath } else { $_ } }
& $compiler @taskMcpArguments
if ($LASTEXITCODE -ne 0) { throw 'MCP compilation failed.' }
# AI sidecar binary: the same sources compiled as a console exe. The app calls it
# as a child process with --ai-serve. Boundary and protocol: research/AI-SIDECAR.md.
$taskAiOutputPath=Join-Path $taskDist ('Ai-'+$taskOutputName)
$taskAiArguments=$arguments | ForEach-Object { if ($_ -eq '/target:winexe') { '/target:exe' } elseif ($_ -like '/out:*') { '/out:'+$taskAiOutputPath } else { $_ } }
& $compiler @taskAiArguments
if ($LASTEXITCODE -ne 0) { throw 'AI sidecar compilation failed.' }
$taskTestOutputPath=Join-Path $taskDist ('Tests-'+$taskOutputName)
$taskTestArguments=$taskBase + @('/target:winexe','/define:QIDESK_TESTS',('/out:'+$taskTestOutputPath)) + $taskAppSources + $taskTestSources
& $compiler @taskTestArguments
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
$taskMcpOutputPath | Set-Content -LiteralPath (Join-Path $taskDist 'latest-mcp.txt')
$taskAiOutputPath | Set-Content -LiteralPath (Join-Path $taskDist 'latest-ai.txt')
$taskTestOutputPath | Set-Content -LiteralPath (Join-Path $taskDist 'latest-tests.txt')
$taskOutputPath | Set-Content -LiteralPath (Join-Path $taskDist 'latest.txt')
Copy-Item -LiteralPath (Join-Path $taskRoot 'assets\fluent\LICENSE') -Destination (Join-Path $taskDist 'Fluent-Icons-LICENSE.txt') -Force
# Ship only runtime PNG packs, provenance and licenses. Original SVG sources and
# development conversion scripts remain in the repository.
$taskSkinSource=Join-Path $taskRoot 'assets\icon-skins'
if(Test-Path -LiteralPath $taskSkinSource){
    foreach($taskSkin in Get-ChildItem -LiteralPath $taskSkinSource -Directory){
        $taskSkinTarget=Join-Path (Join-Path $taskDist 'icon-skins') $taskSkin.Name
        New-Item -ItemType Directory -Force -Path $taskSkinTarget | Out-Null
        Get-ChildItem -LiteralPath $taskSkin.FullName -File | Where-Object {$_.Extension -in @('.png','.json','.txt','.md')} | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $taskSkinTarget -Force}
    }
}
# Cat skins (from references/PathCat-main) are copied as-is next to the exe.
# Keep this file ASCII-only: Windows PowerShell 5.1 reads it with the ANSI code page.
$taskCatSource = Join-Path $taskRoot 'assets\cat'
$taskCatTarget = Join-Path $taskDist 'cat'
if (Test-Path -LiteralPath $taskCatSource) {
    if (Test-Path -LiteralPath $taskCatTarget) {
        # Mirror instead of delete-and-copy: robocopy keeps the target in sync and
        # does not depend on the recycle bin, so it also works in restricted shells.
        & robocopy $taskCatSource $taskCatTarget /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
        if ($LASTEXITCODE -ge 8) { throw ('Cat skin sync failed, robocopy exit ' + $LASTEXITCODE) }
    } else {
        New-Item -ItemType Directory -Force -Path $taskCatTarget | Out-Null
        Get-ChildItem -LiteralPath $taskCatSource -Force | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $taskCatTarget -Recurse -Force }
    }
}
# Keep dist to the current build only. Locked files (a running instance) are skipped.
$taskKeep = @($taskOutputPath,$taskMcpOutputPath,$taskTestOutputPath,$taskAiOutputPath) | ForEach-Object { [IO.Path]::GetFullPath($_) }
Get-ChildItem -LiteralPath $taskDist -Filter '*.exe' | Where-Object { $taskKeep -notcontains $_.FullName } | ForEach-Object { $stale=$_; try { Remove-Item -LiteralPath $stale.FullName -Force -ErrorAction Stop } catch { Write-Host ('Kept '+$stale.Name+' ('+$_.Exception.Message+')') } }
Write-Host ('Built '+$taskOutputPath)
Write-Host ('Built '+$taskAiOutputPath)
Write-Host ('Built '+$taskTestOutputPath)
# The code guard runs after the binaries exist so a build still leaves something
# usable when only a rule is violated. It never blocks the build: a broken rule must
# not stop someone from testing the app they just wrapped. It does stop a commit;
# see check-code.ps1 and CODE-RULES.md for how that is wired up.
try {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $taskRoot 'check-code.ps1') | ForEach-Object { Write-Host $_ }
    if ($LASTEXITCODE -ne 0) { Write-Host 'Code guard reported new violations; the build itself succeeded.' }
} catch {
    Write-Host ('Code guard could not run: '+$_.Exception.Message)
}
