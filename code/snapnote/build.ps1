$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$root = $PSScriptRoot
$destDir = [IO.Path]::GetFullPath((Join-Path $root '..\..\output\SnapNote'))
New-Item -ItemType Directory -Force -Path $destDir | Out-Null
$dest = Join-Path $destDir 'SnapNote.exe'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /out:$dest /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:Microsoft.VisualBasic.dll (Join-Path $root 'SnapNote.cs')
if ($LASTEXITCODE -ne 0) { throw 'SnapNote compilation failed' }
Write-Host $dest
