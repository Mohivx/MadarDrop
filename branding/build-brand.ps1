$ErrorActionPreference = 'Stop'
$taskBrandRoot = Split-Path $PSScriptRoot
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskFramework = Split-Path $taskCompiler
$taskBrandRefs = @('System.dll','System.Core.dll','System.Xaml.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll') | ForEach-Object { '/reference:' + (Join-Path $taskFramework $_) }
$taskBrandCompiler = Join-Path ([System.IO.Path]::GetTempPath()) ('MadarDrop-brand-' + [Guid]::NewGuid().ToString('N') + '.exe')
try {
    & $taskCompiler /nologo /target:exe /platform:x64 ('/out:' + $taskBrandCompiler) $taskBrandRefs (Join-Path $taskBrandRoot 'Brand.cs') (Join-Path $PSScriptRoot 'BrandBuilder.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Lemon artwork compilation failed.' }
    & $taskBrandCompiler (Join-Path $taskBrandRoot 'assets')
    if ($LASTEXITCODE -ne 0) { throw 'Lemon icon generation failed.' }
} finally { if (Test-Path -LiteralPath $taskBrandCompiler) { Remove-Item -LiteralPath $taskBrandCompiler } }
