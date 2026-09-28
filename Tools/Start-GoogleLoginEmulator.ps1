param(
    [string]$ApkPath = "$env:USERPROFILE/Documents/ArtArchives/20260928-google-play-iap/game20260920-emulator-complete-settings.apk",
    [string]$Avd = 'Medium_Phone_API_36.0',
    [string]$SdkRoot = "$env:LOCALAPPDATA/Android/Sdk",
    [string]$Serial = 'emulator-5554',
    [switch]$ObserveOnly
)

$ErrorActionPreference = 'Stop'
$adb = Join-Path $SdkRoot 'platform-tools/adb.exe'
if (!(Test-Path -LiteralPath $adb)) {
    $adb = 'C:/Program Files/Unity/Hub/Editor/6000.3.8f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe'
}
if (!(Test-Path -LiteralPath $adb)) { throw 'Android SDK platform-tools/adb.exe is required.' }

function Invoke-Adb {
    param([string[]]$Arguments)
    $result = & $adb -s $Serial @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw "adb command failed: $result" }
    return $result
}

if (!$ObserveOnly) {
    if (!(Test-Path -LiteralPath $ApkPath -PathType Leaf)) { throw "APK missing: $ApkPath" }
    $devices = & $adb devices
    if (!($devices -match ('^' + [regex]::Escape($Serial) + '\s+device$'))) {
        $emulator = Join-Path $SdkRoot 'emulator/emulator.exe'
        if (!(Test-Path -LiteralPath $emulator)) { throw 'Install Android Emulator with a Google Play AVD first.' }
        if ($Serial -notmatch '^emulator-(\d+)$') { throw 'Use an emulator serial such as emulator-5554.' }
        $port = $Matches[1]
        # The emulator is an interactive test window that the user needs to see.
        # Do not wipe AVD data: its Google account survives restarts.
        if (!($devices -match ('^' + [regex]::Escape($Serial) + '\s+'))) {
            Start-Process -FilePath $emulator -ArgumentList @('-avd', $Avd, '-port', $port,
                '-no-snapshot', '-gpu', 'software', '-no-audio', '-cores', '4', '-memory', '3072', '-no-metrics') | Out-Null
        }
    }
    $deadline = (Get-Date).AddMinutes(3)
    do {
        $devices = & $adb devices
        $ready = $devices -match ('^' + [regex]::Escape($Serial) + '\s+device$')
        if ($ready) {
            $boot = & $adb -s $Serial shell getprop sys.boot_completed 2>$null
            if ($boot -eq '1') { break }
        }
        if ((Get-Date) -gt $deadline) { throw 'Emulator boot timed out. Check its window before retrying.' }
        Start-Sleep -Seconds 2
    } while ($true)
    Invoke-Adb -Arguments @('install', '-r', (Resolve-Path -LiteralPath $ApkPath).Path)
    Invoke-Adb -Arguments @('shell', 'am', 'start', '-n',
        'com.semobobo.game20260920/com.unity3d.player.UnityPlayerActivity')
    Write-Host 'In the emulator, accept the game terms and select Sign in with Google.'
    Write-Host 'Enter Google credentials in the emulator only, never in this terminal or chat.'
}

# Print only the app-defined non-sensitive markers, never full SDK/auth logs.
$devices = & $adb devices
if (!($devices -match ('^' + [regex]::Escape($Serial) + '\s+device$'))) { throw 'Emulator is not connected.' }
$appProcess = (Invoke-Adb -Arguments @('shell', 'pidof', 'com.semobobo.game20260920') | Out-String).Trim()
if ($appProcess -notmatch '^\d+$') { throw 'The game is not running. Launch it before observing login.' }
$diagnostics = Invoke-Adb -Arguments @('logcat', '-d', "--pid=$appProcess", '-s', 'Unity:I')
$diagnostics | Select-String -Pattern '\[DoodleAuth\]' | ForEach-Object { $_.Line }
Write-Host 'Re-run with -ObserveOnly after signing in. Success requires a Google token, BACKND status 200/201, and game_scene_entered.'
