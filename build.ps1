param([string]$OutputName = 'release-0.3.0', [string]$PythonExe = 'python.exe', [switch]$IncludeMedia)
$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$taskOutput = Join-Path $taskRoot $OutputName
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskFramework = Split-Path $taskCompiler
$taskReferences = @('System.dll', 'System.Core.dll', 'System.Xaml.dll', 'System.Xml.dll', 'System.Xml.Linq.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll', 'System.Web.Extensions.dll', 'System.IO.Compression.dll', 'System.IO.Compression.FileSystem.dll', 'WPF\WindowsBase.dll', 'WPF\PresentationCore.dll', 'WPF\PresentationFramework.dll') | ForEach-Object { '/reference:' + (Join-Path $taskFramework $_) }
$taskSources = Get-ChildItem -LiteralPath $taskRoot -Filter '*.cs' | Select-Object -ExpandProperty FullName
& $taskCompiler /nologo /target:winexe /platform:x64 /optimize+ /utf8output ('/out:' + (Join-Path $taskOutput 'TangerineWindows.exe')) ('/win32manifest:' + (Join-Path $taskRoot 'app.manifest')) $taskReferences $taskSources
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
foreach ($taskTool in $(if ($IncludeMedia) { @('ffmpeg') } else { @() })) {
    $taskInstalled = Get-Command ($taskTool + '.exe') -ErrorAction SilentlyContinue
    if (-not $taskInstalled) { throw ($taskTool + ' was not found. Install FFmpeg, then rebuild.') }
    Copy-Item -LiteralPath $taskInstalled.Source -Destination (Join-Path $taskOutput ($taskTool + '.exe')) -Force
    $taskToolRoot = Split-Path (Split-Path $taskInstalled.Source)
    $taskLicense = Join-Path $taskToolRoot 'LICENSE'
    if (Test-Path -LiteralPath $taskLicense) { Copy-Item -LiteralPath $taskLicense -Destination (Join-Path $taskOutput 'FFmpeg-LICENSE.txt') -Force }
}
Copy-Item -LiteralPath (Join-Path $taskRoot 'README.ar.md') -Destination $taskOutput -Force
Copy-Item -LiteralPath (Join-Path $taskRoot 'THIRD-PARTY.txt') -Destination $taskOutput -Force
Copy-Item -LiteralPath (Join-Path $taskRoot 'pdf_worker.py') -Destination $taskOutput -Force
Copy-Item -LiteralPath (Join-Path $taskRoot 'TangerineWindows.exe.config') -Destination $taskOutput -Force
Copy-Item -LiteralPath (Join-Path $taskRoot 'LICENSE') -Destination $taskOutput -Force
& $PythonExe (Join-Path $taskRoot 'build_runtime.py') $taskOutput
if ($LASTEXITCODE -ne 0) { throw 'Packaging PDF dependencies failed.' }
Write-Output (Join-Path $taskOutput 'TangerineWindows.exe')
