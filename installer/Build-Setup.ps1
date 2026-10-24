param(
    [string]$PublishDir,
    [string]$OutputDir,
    [string]$Compiler
)
$ErrorActionPreference = 'Stop'
$sourceRoot = Split-Path $PSScriptRoot -Parent
if (-not $PublishDir) { $PublishDir = Join-Path (Split-Path $sourceRoot -Parent) 'release/notification-fix-win-x64' }
if (-not $OutputDir) { $OutputDir = Join-Path (Split-Path $sourceRoot -Parent) 'release' }
if (-not $Compiler) {
    $candidates = @(
        (Join-Path $sourceRoot '.tools/installer-toolchain/inno/ISCC.exe'),
        'C:/Program Files (x86)/Inno Setup 6/ISCC.exe',
        'C:/Program Files/Inno Setup 6/ISCC.exe'
    )
    $Compiler = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $Compiler) { throw 'Install Inno Setup 6.7.3 or later, or specify -Compiler.' }
$PublishDir = (Resolve-Path -LiteralPath $PublishDir).Path
$product = Join-Path $PublishDir 'WeatherBeacon.exe'
if (-not (Test-Path -LiteralPath $product)) { throw 'Publish WeatherBeacon.exe first.' }
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo($product)
if ($version.FileVersion -ne '1.0.0.0' -or $version.ProductVersion -ne '1.0.0.0') { throw 'The product version must be 1.0.0.0.' }
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$OutputDir = (Resolve-Path -LiteralPath $OutputDir).Path
& $Compiler "/DPublishDir=$PublishDir" "/DSetupOutputDir=$OutputDir" (Join-Path $PSScriptRoot 'WeatherBeacon.iss')
if ($LASTEXITCODE -ne 0) { throw "Installer compiler failed with exit code $LASTEXITCODE." }
Write-Output (Join-Path $OutputDir 'WeatherBeacon-1.0.0.0-x64-Setup.exe')
