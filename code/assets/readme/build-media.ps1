param([string]$TestBinary, [string]$Python = 'python', [string]$Ffmpeg)
$ErrorActionPreference = 'Stop'
$taskRepo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
if (!$TestBinary) {
    $TestBinary = (Get-Content -LiteralPath (Join-Path $taskRepo 'dist\latest-tests.txt')).Trim()
}
if (!(Test-Path -LiteralPath $TestBinary)) { throw 'Build the test binary first.' }
$taskWork = Join-Path $taskRepo ('experimental\readme-media-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $taskWork | Out-Null
$taskAssembly = Join-Path $taskWork ([IO.Path]::GetFileName($TestBinary))
Copy-Item -LiteralPath $TestBinary -Destination $taskAssembly
$taskFramework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$taskRenderer = Join-Path $taskWork 'ReadmeMedia.exe'
$taskArgs = @('/nologo','/target:exe','/platform:x64','/optimize+',('/out:'+$taskRenderer),('/reference:'+$taskAssembly))
foreach ($taskReference in @('System.dll','System.Core.dll','System.Xml.dll','System.Web.Extensions.dll',
    'System.Xaml.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll')) {
    $taskArgs += '/reference:' + (Join-Path $taskFramework $taskReference)
}
$taskArgs += Join-Path $PSScriptRoot 'media-render.cs'
& (Join-Path $taskFramework 'csc.exe') @taskArgs
if ($LASTEXITCODE -ne 0) { throw 'Documentation renderer compilation failed.' }
$taskFrames = Join-Path $taskWork 'frames'
$taskProcessInfo = New-Object Diagnostics.ProcessStartInfo
$taskProcessInfo.FileName = $taskRenderer
$taskProcessInfo.Arguments = '"'+$taskFrames+'" "'+(Join-Path $PSScriptRoot 'media-copy.json')+'"'
$taskProcessInfo.UseShellExecute = $false
$taskProcessInfo.CreateNoWindow = $true
$taskProcessInfo.RedirectStandardError = $true
$taskProcessInfo.RedirectStandardOutput = $true
$taskProcess = [Diagnostics.Process]::Start($taskProcessInfo)
if (!$taskProcess.WaitForExit(60000)) { $taskProcess.Kill(); throw 'Documentation render timed out.' }
if ($taskProcess.ExitCode -ne 0) {
    Write-Output $taskProcess.StandardError.ReadToEnd()
    Write-Output $taskProcess.StandardOutput.ReadToEnd()
    throw 'Documentation render failed.'
}
foreach ($taskImage in @('partition-documents.png','partition-pictures.png','search-all.png')) {
    Copy-Item -LiteralPath (Join-Path $taskFrames $taskImage) -Destination $PSScriptRoot -Force
}
& $Python (Join-Path $PSScriptRoot 'encode-media.py') $taskFrames $PSScriptRoot
if ($LASTEXITCODE -ne 0) { throw 'GIF encoding failed. Documentation tooling requires Pillow.' }
if ($Ffmpeg) {
    & (Join-Path $PSScriptRoot 'encode-video.ps1') -Frames (Join-Path $taskFrames 'overview') -OutputFile (Join-Path $PSScriptRoot 'feature-tour.mp4') -Ffmpeg $Ffmpeg
}
Write-Output ('Media rendered from '+[IO.Path]::GetFileName($TestBinary))
Write-Output ('Scratch files: '+$taskWork)
