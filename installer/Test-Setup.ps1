param([string]$Setup)
$ErrorActionPreference = 'Stop'
$sourceRoot = Split-Path $PSScriptRoot -Parent
$workspace = Split-Path $sourceRoot -Parent
if (-not $Setup) { $Setup = Join-Path $workspace 'release/WeatherBeacon-1.0.0.0-x64-Setup.exe' }
$testRoot = Join-Path $sourceRoot ('.tools/setup-smoke-' + [Guid]::NewGuid().ToString('N'))
$installDir = [IO.Path]::GetFullPath((Join-Path $testRoot 'app'))
$allowedRoot = [IO.Path]::GetFullPath((Join-Path $sourceRoot '.tools')) + [IO.Path]::DirectorySeparatorChar
if (-not $installDir.StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe test target.' }
$uninstallKey = 'HKCU:/Software/Microsoft/Windows/CurrentVersion/Uninstall/{C96A9E53-AB05-4283-BA69-2507461ABDA7}_is1'
if (Test-Path -LiteralPath $uninstallKey) { throw 'A real installer registration already exists. Test stopped to preserve it.' }
New-Item -ItemType Directory -Path $testRoot | Out-Null
$shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'WeatherBeacon.lnk'
$shortcutBackup = Join-Path $testRoot 'original-shortcut.lnk'
$hadShortcut = Test-Path -LiteralPath $shortcut
if ($hadShortcut) { Copy-Item -LiteralPath $shortcut -Destination $shortcutBackup }
function Hash-OrMissing([string]$Path) {
    if (Test-Path -LiteralPath $Path) { return (Get-FileHash -LiteralPath $Path).Hash }
    return 'missing'
}
$config = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'WeatherBeacon'
$settingsHash = Hash-OrMissing (Join-Path $config 'settings.json')
$ledgerHash = Hash-OrMissing (Join-Path $config 'alert-delivery.json')
$runPath = 'HKCU:/Software/Microsoft/Windows/CurrentVersion/Run'
$runBefore = (Get-ItemProperty -LiteralPath $runPath -ErrorAction SilentlyContinue).WeatherBeacon
$ownedProcesses = @(Get-Process WeatherBeacon -ErrorAction SilentlyContinue | Where-Object {
    $_.Path -and $_.Path.StartsWith((Join-Path $workspace 'release'), [StringComparison]::OrdinalIgnoreCase)
})
$restartPaths = @($ownedProcesses | ForEach-Object Path | Sort-Object -Unique)
function Invoke-Uninstall {
    $uninstaller = Join-Path $installDir 'unins000.exe'
    if ((Test-Path -LiteralPath $uninstaller) -and (Test-Path -LiteralPath $uninstallKey)) {
        $arguments = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG="{0}"' -f (Join-Path $testRoot 'uninstall.log')
        $process = Start-Process -FilePath $uninstaller -ArgumentList $arguments -WindowStyle Hidden -PassThru
        if (-not $process.WaitForExit(30000)) { throw 'Uninstaller did not finish within 30 seconds.' }
        if ($process.ExitCode -ne 0) { throw "Uninstaller failed: $($process.ExitCode)" }
    }
}
try {
    foreach ($process in $ownedProcesses) { Stop-Process -Id $process.Id }
    $arguments = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /LANG=chinesesimplified /DIR="{0}" /LOG="{1}"' -f $installDir,(Join-Path $testRoot 'install.log')
    $process = Start-Process -FilePath $Setup -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(30000)) { throw 'Installer did not finish within 30 seconds.' }
    if ($process.ExitCode -ne 0) { throw "Installer failed: $($process.ExitCode)" }
    $registration = Get-ItemProperty -LiteralPath $uninstallKey
    if ($registration.DisplayVersion -ne '1.0.0.0') { throw 'Unexpected uninstall display version.' }
    $publish = Join-Path $workspace 'release/notification-fix-win-x64'
    $checked = 0
    foreach ($file in Get-ChildItem -LiteralPath $publish -Recurse -File) {
        $relative = $file.FullName.Substring($publish.Length + 1)
        if ($relative -match '^logs[\\/]' -or $file.Name -like '*.log') { continue }
        if ((Hash-OrMissing (Join-Path $installDir $relative)) -ne (Get-FileHash -LiteralPath $file.FullName).Hash) { throw "Payload mismatch: $relative" }
        $checked++
    }
    $shell = New-Object -ComObject WScript.Shell
    if ($shell.CreateShortcut($shortcut).TargetPath -ne (Join-Path $installDir 'WeatherBeacon.exe')) { throw 'Shortcut target mismatch.' }
    Invoke-Uninstall
    if (Test-Path -LiteralPath $uninstallKey) { throw 'Uninstall registration remained.' }
    if (Test-Path -LiteralPath (Join-Path $installDir 'WeatherBeacon.exe')) { throw 'Product executable remained.' }
    if ((Hash-OrMissing (Join-Path $config 'settings.json')) -ne $settingsHash) { throw 'Settings changed during packaging test.' }
    if ((Hash-OrMissing (Join-Path $config 'alert-delivery.json')) -ne $ledgerHash) { throw 'Alert ledger changed during packaging test.' }
    $runAfter = (Get-ItemProperty -LiteralPath $runPath -ErrorAction SilentlyContinue).WeatherBeacon
    if ($runBefore -ne $runAfter) { throw 'Existing portable startup entry changed.' }
    "PASS: installed and uninstalled; $checked payload hashes verified; shortcut, version, preserved settings/ledger/startup verified."
}
finally {
    Invoke-Uninstall
    if ($hadShortcut) { Copy-Item -LiteralPath $shortcutBackup -Destination $shortcut -Force }
    foreach ($path in $restartPaths) { Start-Process -FilePath $path -WindowStyle Hidden }
}
