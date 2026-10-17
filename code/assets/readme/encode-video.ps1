param(
    [Parameter(Mandatory=$true)][string]$Frames,
    [Parameter(Mandatory=$true)][string]$OutputFile,
    [string]$Ffmpeg = 'ffmpeg'
)
$ErrorActionPreference = 'Stop'
$taskPattern = Join-Path ([IO.Path]::GetFullPath($Frames)) '%03d.png'
$taskOutput = [IO.Path]::GetFullPath($OutputFile)
$taskWork = Join-Path (Split-Path -Parent ([IO.Path]::GetFullPath($Frames))) ('video-'+[Guid]::NewGuid().ToString('N')+'.mp4')
& $Ffmpeg -hide_banner -loglevel error -y -framerate 8 -i $taskPattern -r 24 -c:v libx264 -preset medium -crf 20 -pix_fmt yuv420p -an -movflags +faststart $taskWork
if ($LASTEXITCODE -ne 0) { throw 'MP4 encoding failed.' }
Copy-Item -LiteralPath $taskWork -Destination $taskOutput -Force
Write-Output ('MP4 ready: '+[IO.Path]::GetFileName($taskOutput))
