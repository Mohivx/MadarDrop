$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$taskSource = Join-Path $taskRoot 'publish\MadarDrop'
New-Item -ItemType Directory -Path $taskSource -Force | Out-Null
$taskTop = @('README.md','README.ar.md','PUBLISHING.md','THIRD-PARTY.txt','LICENSE','requirements-build.txt','build.ps1','build_runtime.py','pdf_worker.py','TangerineWindows.exe.config','app.manifest','.gitignore','.gitattributes','prepare-source.ps1')
$taskTop += @(Get-ChildItem -LiteralPath $taskRoot -Filter '*.cs' -File | Select-Object -ExpandProperty Name)
foreach ($taskName in $taskTop) { Copy-Item -LiteralPath (Join-Path $taskRoot $taskName) -Destination $taskSource -Force }
foreach ($taskDirectory in @('updater','docs','assets','branding','.github\workflows')) { New-Item -ItemType Directory -Path (Join-Path $taskSource $taskDirectory) -Force | Out-Null }
foreach ($taskName in @('UpdateCore.cs','SetupSupport.cs','Setup.cs','Maintenance.cs','UpdateTests.cs','LegacyFixture.cs','build-setup.ps1')) { Copy-Item -LiteralPath (Join-Path $taskRoot ('updater\' + $taskName)) -Destination (Join-Path $taskSource 'updater') -Force }
foreach ($taskName in @('VALIDATION.md','REFERENCE.ar.md','VIDEO-REVIEW-2026-10-21.ar.md','EXPORTS-0.4.2.ar.md','VIDEO-TOOLS-0.4.3.ar.md','video-tools-ar-dark.png','split-en-light.png','trim-en-light.png','redact-en-light.png','redact-ar-dark.png','metadata-ar-dark.png','snapshots-en-light.png','snapshots-ar-dark.png','tools-ar-dark.png','crop-ar-dark.png','main-en-light.png','settings-ar-dark.png','main-ar-light.png','tools-ar-light.png','video-crop-en-light.png','compact-edit-en-light.png','floating-progress-ar-dark.png','floating-progress-en-light.png')) { Copy-Item -LiteralPath (Join-Path $taskRoot ('docs\' + $taskName)) -Destination (Join-Path $taskSource 'docs') -Force }
foreach ($taskName in @('lemon.svg','MadarDrop.ico','lemon-preview.png')) { Copy-Item -LiteralPath (Join-Path $taskRoot ('assets\' + $taskName)) -Destination (Join-Path $taskSource 'assets') -Force }
foreach ($taskName in @('BrandBuilder.cs','build-brand.ps1')) { Copy-Item -LiteralPath (Join-Path $taskRoot ('branding\' + $taskName)) -Destination (Join-Path $taskSource 'branding') -Force }
Copy-Item -LiteralPath (Join-Path $taskRoot '.github\workflows\build.yml') -Destination (Join-Path $taskSource '.github\workflows\build.yml') -Force
$taskArchive = Join-Path $taskRoot 'MadarDrop-Source-0.4.3.zip'
if (Test-Path -LiteralPath $taskArchive) { Remove-Item -LiteralPath $taskArchive }
Add-Type -AssemblyName System.IO.Compression
$taskZipStream = [System.IO.File]::Open($taskArchive, [System.IO.FileMode]::CreateNew)
$taskZip = [System.IO.Compression.ZipArchive]::new($taskZipStream, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($taskFile in (Get-ChildItem -LiteralPath $taskSource -Recurse -File -Force | Where-Object { $_.FullName -notmatch '[\\/]\.git[\\/]' })) {
        $taskRelative = $taskFile.FullName.Substring($taskSource.Length + 1).Replace('\','/')
        $taskEntry = $taskZip.CreateEntry(('MadarDrop/' + $taskRelative), [System.IO.Compression.CompressionLevel]::Optimal)
        $taskInput = [System.IO.File]::OpenRead($taskFile.FullName)
        $taskOutput = $taskEntry.Open()
        try { $taskInput.CopyTo($taskOutput) } finally { $taskInput.Dispose(); $taskOutput.Dispose() }
    }
} finally { $taskZip.Dispose(); $taskZipStream.Dispose() }
$taskAssets = @('MadarDrop-Setup-0.4.3.exe', 'MadarDrop-Source-0.4.3.zip')
$taskHashes = foreach ($taskName in $taskAssets) { (Get-FileHash -LiteralPath (Join-Path $taskRoot $taskName) -Algorithm SHA256).Hash.ToLower() + '  ' + $taskName }
[System.IO.File]::WriteAllLines((Join-Path $taskRoot 'SHA256SUMS.txt'), $taskHashes, [System.Text.UTF8Encoding]::new($false))
Write-Output $taskSource
Write-Output $taskArchive
