param([string]$PayloadFolder = 'release-0.4.0')
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot
$taskPayload = Join-Path $taskRoot $PayloadFolder
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskFramework = Split-Path $taskCompiler
$taskReferences = @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll') | ForEach-Object { '/reference:' + (Join-Path $taskFramework $_) }
$taskReferences += '/win32icon:' + (Join-Path $taskRoot 'assets\MadarDrop.ico')
if (Test-Path -LiteralPath (Join-Path $taskPayload 'ffmpeg.exe')) { throw 'Public setup excludes FFmpeg. Build a clean payload without -IncludeMedia.' }
& $taskCompiler /nologo /target:winexe /platform:x64 /optimize+ /utf8output ('/out:' + (Join-Path $taskPayload 'MadarDrop.Uninstall.exe')) ('/win32manifest:' + (Join-Path $taskRoot 'app.manifest')) $taskReferences (Join-Path $PSScriptRoot 'UpdateCore.cs') (Join-Path $PSScriptRoot 'SetupSupport.cs') (Join-Path $PSScriptRoot 'Maintenance.cs')
if ($LASTEXITCODE -ne 0) { throw 'Uninstaller compilation failed.' }
Copy-Item -LiteralPath (Join-Path $taskRoot 'TangerineWindows.exe.config') -Destination (Join-Path $taskPayload 'MadarDrop.Uninstall.exe.config') -Force
$taskFiles = Get-ChildItem -LiteralPath $taskPayload -File -Recurse | ForEach-Object { [PSCustomObject]@{Path=$_.FullName.Substring($taskPayload.Length + 1).Replace('\','/'); Hash=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash; Length=$_.Length} }
$taskManifest = [PSCustomObject]@{Version='0.4.0';Files=@($taskFiles)}
$taskManifestPath = Join-Path $PSScriptRoot 'manifest-0.4.0.json'
[System.IO.File]::WriteAllText($taskManifestPath, ($taskManifest | ConvertTo-Json -Depth 5), (New-Object System.Text.UTF8Encoding($false)))
$taskZip = Join-Path $taskRoot 'MadarDrop-0.4.0-payload.zip'
if (Test-Path -LiteralPath $taskZip) { Remove-Item -LiteralPath $taskZip }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($taskPayload, $taskZip, [System.IO.Compression.CompressionLevel]::Optimal, $false)
$taskExe = Join-Path $taskRoot 'MadarDrop-Setup-0.4.0.exe'
& $taskCompiler /nologo /target:winexe /platform:x64 /optimize+ /utf8output ('/out:' + $taskExe) ('/win32manifest:' + (Join-Path $taskRoot 'app.manifest')) ('/resource:' + $taskZip + ',payload.zip') ('/resource:' + $taskManifestPath + ',manifest.json') $taskReferences (Join-Path $PSScriptRoot 'UpdateCore.cs') (Join-Path $PSScriptRoot 'SetupSupport.cs') (Join-Path $PSScriptRoot 'Setup.cs')
if ($LASTEXITCODE -ne 0) { throw 'Setup compilation failed.' }
Write-Output $taskExe
