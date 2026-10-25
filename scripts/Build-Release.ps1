# Builds WeatherBeacon: runs the UI/feature checks, then publishes (self-contained) and packages x86, x64 and ARM64 installers.
# Usage: double-click scripts\Build-Release.cmd, or: powershell -ExecutionPolicy Bypass -File scripts\Build-Release.ps1 [-Name <label>] [-Arch x64,arm64]
# Output: ..\release\<Name>-win-x64, ..\release\<Name>-setup, ..\release\<Name>-build.log; check screenshots in .tools\<Name>-*
param(
    [string]$Name = ('build-' + (Get-Date -Format 'yyyyMMdd-HHmm')),
    [ValidateSet('x86','x64','arm64')][string[]]$Arch = @('x86','x64','arm64')
)
$ErrorActionPreference = 'Stop'
$source = Split-Path $PSScriptRoot -Parent
$release = Join-Path (Split-Path $source -Parent) 'release'
$log = Join-Path $release "$Name-build.log"
New-Item -ItemType Directory -Force -Path $release | Out-Null
Start-Transcript -Path $log -Force | Out-Null
try {
    $dotnet = Join-Path $source '.tools/dotnet/dotnet.exe'
    if (-not (Test-Path -LiteralPath $dotnet)) { $dotnet = 'dotnet' }
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_NOLOGO = '1'
    Set-Location $source

    $checks = @(
        @{ Title = 'Onboarding UI check'; Project = 'tests/WeatherBeacon.Onboarding/WeatherBeacon.Onboarding.csproj'; Out = "$Name-onboarding" },
        @{ Title = 'Feature check (logs, rain reminder, widget layout)'; Project = 'tests/WeatherBeacon.Features/WeatherBeacon.Features.csproj'; Out = "$Name-features" }
    )
    foreach ($check in $checks) {
        Write-Output "== $($check.Title)"
        & $dotnet run --project $check.Project -c Release -- (Join-Path $source ".tools/$($check.Out)")
        if ($LASTEXITCODE -ne 0) { throw "$($check.Title) failed ($LASTEXITCODE)." }
    }

    # One self-contained publish and one installer per architecture; all installers go to <Name>-setup.
    $setup = Join-Path $release "$Name-setup"
    foreach ($a in $Arch) {
        Write-Output "== Publish win-$a"
        $publish = Join-Path $release "$Name-win-$a"
        if (Test-Path -LiteralPath $publish) { Remove-Item -LiteralPath $publish -Recurse -Force }
        & $dotnet publish src/WeatherBeacon.App/WeatherBeacon.App.csproj -c Release -r "win-$a" --self-contained true -o $publish
        if ($LASTEXITCODE -ne 0) { throw "Publish win-$a failed ($LASTEXITCODE)." }
        Write-Output "== Setup $a"
        & (Join-Path $source 'installer/Build-Setup.ps1') -PublishDir $publish -OutputDir $setup -Arch $a
    }
    Write-Output 'BUILD-OK'
}
catch {
    Write-Output "BUILD-FAILED: $_"
}
finally {
    Stop-Transcript | Out-Null
}
