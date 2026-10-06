#requires -Version 5.1
<#
  Clean-machine smoke test: silent install, verify, launch, confirm stable, close
  gracefully, silent uninstall, check for leftovers. Intended for Windows Sandbox (NOT a Dell),
  so the app's model gate must disable the hardware controls (asserted from its log file).
  Never runs DellFanCmd or DellSetThermalSetting directly.
  Transcript: build/out/smoke-test.log
#>
param([string]$Setup)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$out  = Join-Path $root 'build\out'
if (-not $Setup) { $Setup = Join-Path $out 'ACE-Dell-Control-Setup.exe' }
New-Item -ItemType Directory -Force -Path $out | Out-Null
$logFile = Join-Path $out 'smoke-test.log'
Set-Content -Path $logFile -Value '' -Encoding utf8

$script:fail = 0
function Say([string]$m) {
  $l = '[{0}] {1}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $m
  Write-Host $l; Add-Content -Path $logFile -Value $l -Encoding utf8
}
function Check([string]$name, [bool]$ok, [string]$detail = '') {
  if ($ok) { Say "PASS  $name $detail" } else { Say "FAIL  $name $detail"; $script:fail++ }
}

$inst     = Join-Path $env:ProgramFiles 'ACE Dell Control Preview'
$appExe   = Join-Path $inst 'ACE Dell Control.exe'
$uninst   = Join-Path $inst 'Uninstall.exe'
$regPath  = 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ACEDellControlPreview'
$regApp   = 'HKLM:\Software\ACE\DellControlPreview'
$startDir = Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\ACE Dell Control Preview'
$desktopLnk = Join-Path ([Environment]::GetFolderPath('CommonDesktopDirectory')) 'ACE Dell Control Preview.lnk'
$appData  = Join-Path $env:ProgramData 'ACE Dell Control'
$appLogs  = Join-Path $appData 'logs'
$marker   = Join-Path $appData 'manual-mode.marker'

Say "OS: $((Get-CimInstance Win32_OperatingSystem).Caption) $([Environment]::OSVersion.Version)"
$bios = Get-CimInstance Win32_ComputerSystem
Say "Machine: $($bios.Manufacturer) / $($bios.Model)  (expected NOT a Dell Latitude 7400)"
Check 'runner is not a supported Dell model' ($bios.Model -notmatch 'Latitude 7400')
if (-not (Test-Path -LiteralPath $Setup)) { Say "FAIL setup missing: $Setup"; exit 1 }
Say "Setup: $Setup  sha256=$((Get-FileHash $Setup -Algorithm SHA256).Hash.ToLower())"
Check 'preinstall: clean (no install dir)' (-not (Test-Path $inst))

# --- install ----------------------------------------------------------------
$p = Start-Process -FilePath $Setup -ArgumentList '/S' -Wait -PassThru
Check 'silent install exit code 0' ($p.ExitCode -eq 0) "(exit $($p.ExitCode))"

$expected = @(
  'ACE Dell Control.exe','ACE.Dell.Control.cs','MainForm.cs','Safety.cs','Sensors.cs','app.manifest','README-INSTALLER.txt','REVIEW-NOTICE.txt',
  'THIRD-PARTY-NOTICES.txt','DEPENDENCY-MANIFEST.txt','Uninstall.exe',
  'tools\DellFanCmd\DellFanCmd.exe','tools\DellFanCmd\DellFanLib.dll','tools\DellFanCmd\bzh_dell_smm_io_x64.sys',
  'tools\DellSetThermalSetting\DellSetThermalSetting.exe',
  'tools\LibreHardwareMonitor\LibreHardwareMonitorLib.dll',
  'source\DellFanManagement-2.1.1-source.zip','source\LibreHardwareMonitor-v0.9.6-source.zip',
  'source\bzh-windrv-dell-smm-io-67786c69-source.zip','source\PawnIO.Modules-0.2.2-source.zip',
  'source\ACE-Dell-Control-Preview.nsi','source\BUILDING.txt',
  'licenses\GPL-3.0.txt','licenses\MPL-2.0.txt','licenses\LGPL-2.1.txt','licenses\MIT.txt')
foreach ($e in $expected) { Check "file $e" (Test-Path -LiteralPath (Join-Path $inst $e)) }
Check 'shortcut: Start Menu app'       (Test-Path (Join-Path $startDir 'ACE Dell Control Preview.lnk'))
Check 'shortcut: Start Menu uninstall' (Test-Path (Join-Path $startDir 'Uninstall.lnk'))
Check 'shortcut: Desktop'              (Test-Path $desktopLnk)
$reg = Get-ItemProperty -Path $regPath -ErrorAction SilentlyContinue
Check 'uninstall registry key' ($null -ne $reg)
if ($reg) {
  Check 'registry DisplayName'     ($reg.DisplayName -eq 'ACE Dell Control Technical Preview') "($($reg.DisplayName))"
  Check 'registry DisplayVersion'  ([bool]$reg.DisplayVersion) "($($reg.DisplayVersion))"
  Check 'registry UninstallString' ($reg.UninstallString -like '*Uninstall.exe*') "($($reg.UninstallString))"
  Check 'registry InstallLocation' ($reg.InstallLocation -eq $inst) "($($reg.InstallLocation))"
}
Check 'registry app InstallDir' ((Get-ItemProperty $regApp -ErrorAction SilentlyContinue).InstallDir -eq $inst)

# --- launch -----------------------------------------------------------------
$proc = $null
if (Test-Path -LiteralPath $appExe) {
  $launchTime = Get-Date
  $proc = Start-Process -FilePath $appExe -WorkingDirectory $inst -PassThru
  Say "Launched pid $($proc.Id); waiting 15s"
  Start-Sleep -Seconds 15
  $proc.Refresh()
  Check 'GUI still running after 15s' (-not $proc.HasExited) $(if ($proc.HasExited) { "(exit $($proc.ExitCode))" } else { '' })
}

if ($proc -and -not $proc.HasExited) {
  Say "MainWindowTitle: '$($proc.MainWindowTitle)'  Responding: $($proc.Responding)"
  Check 'main window present' ($proc.MainWindowHandle -ne 0 -and $proc.MainWindowTitle -eq 'ACE Dell Control')
  # The app mirrors its on-screen activity log to %ProgramData%\ACE Dell Control\logs\activity-YYYYMMDD.log.
  $logText = $null
  $logs = @(Get-ChildItem -LiteralPath $appLogs -Filter 'activity-*.log' -File -ErrorAction SilentlyContinue |
    Where-Object { $_.LastWriteTime -ge $launchTime.AddMinutes(-1) } | Sort-Object Name)
  Check 'app log file written' ($logs.Count -gt 0) "($appLogs)"
  if ($logs.Count -gt 0) {
    $logText = ($logs | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw -Encoding utf8 }) -join "`n"
    Say "--- app activity log ($($logs[-1].FullName)) ---"; $logText.TrimEnd().Split("`n") | ForEach-Object { Say "  | $($_.TrimEnd())" }
    Check 'log: model gate CLOSED on non-Dell hardware' ($logText -match 'Model gate: CLOSED')
    Check 'log: model gate never PASSED here' ($logText -notmatch 'Model gate: PASSED')
    Check 'log: Dell controls disabled on unverified model' ($logText -match 'Dell-specific controls disabled on unverified model')
    Check 'log: no DellFanCmd was run' ($logText -notmatch 'Ran DellFanCmd')
  }
  Check 'no manual-mode marker' (-not (Test-Path -LiteralPath $marker))
  Say 'Closing gracefully (CloseMainWindow)'
  [void]$proc.CloseMainWindow()
  $exited = $proc.WaitForExit(15000)
  Check 'GUI exited after CloseMainWindow' $exited
  if (-not $exited) { Say 'Forcing kill'; Stop-Process -Id $proc.Id -Force; Start-Sleep 2 }
}
Check 'no ACE Dell Control process remains' (-not (Get-Process -Name 'ACE Dell Control' -ErrorAction SilentlyContinue))

# --- uninstall --------------------------------------------------------------
if (Test-Path -LiteralPath $uninst) {
  # NSIS uninstaller copies itself to temp unless _? is given; use _= to wait for completion.
  $u = Start-Process -FilePath $uninst -ArgumentList "/S _?=$inst" -Wait -PassThru
  Check 'silent uninstall exit code 0' ($u.ExitCode -eq 0) "(exit $($u.ExitCode))"
  # with _? the uninstaller cannot delete itself or its folder
  Remove-Item -LiteralPath $uninst -Force -ErrorAction SilentlyContinue
  Remove-Item -LiteralPath $inst -Force -ErrorAction SilentlyContinue
} else { Check 'uninstaller present' $false }

Check 'leftover: install dir gone'    (-not (Test-Path -LiteralPath $inst))
if (Test-Path -LiteralPath $inst) { Get-ChildItem $inst -Recurse -Force | ForEach-Object { Say "  leftover: $($_.FullName)" } }
Check 'leftover: uninstall reg key'   (-not (Test-Path $regPath))
Check 'leftover: app reg key'         (-not (Test-Path $regApp))
Check 'leftover: Start Menu folder'   (-not (Test-Path $startDir))
Check 'leftover: Desktop shortcut'    (-not (Test-Path $desktopLnk))
Check 'no manual-mode marker after uninstall' (-not (Test-Path -LiteralPath $marker))
Say "Kept by design (audit trail): $appLogs"

Say "RESULT: $(if ($script:fail -eq 0) { 'PASS' } else { "FAIL ($script:fail checks)" })"
exit $(if ($script:fail -eq 0) { 0 } else { 1 })
