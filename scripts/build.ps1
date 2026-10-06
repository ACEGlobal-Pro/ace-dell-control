#requires -Version 5.1
<#
  Repeatable build for ACE Dell Control (technical preview).
  Recreates the staging layout in build/staging from repo files, compiles the GUI
  with the .NET Framework 4.x csc, builds the NSIS installer, writes SHA256SUMS.txt.
  Fails loudly on any missing input. Does not modify src/, installer/ or third-party/.
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root    = Split-Path -Parent $PSScriptRoot
$build   = Join-Path $root 'build'
$staging = Join-Path $build 'staging'
$out     = Join-Path $build 'out'

function Fail([string]$m) { Write-Host "BUILD FAILED: $m" -ForegroundColor Red; exit 1 }
function Need([string]$p) { if (-not (Test-Path -LiteralPath $p)) { Fail "missing required input: $p" }; $p }

# --- inputs -----------------------------------------------------------------
$srcFiles = @('ACE.Dell.Control.cs','MainForm.cs','Safety.cs','Sensors.cs')
$cs       = foreach ($f in $srcFiles) { Need (Join-Path $root "src\$f") }
$manifest = Need (Join-Path $root 'src\app.manifest')
$nsi      = Need (Join-Path $root 'installer\ACE-Dell-Control-Preview.nsi')
$reviewN  = Need (Join-Path $root 'installer\REVIEW-NOTICE.txt')
$readmeI  = Need (Join-Path $root 'installer\README-INSTALLER.txt')
$building = Need (Join-Path $root 'installer\BUILDING.txt')
$tpn      = Need (Join-Path $root 'third-party\THIRD-PARTY-NOTICES.txt')
$depm     = Need (Join-Path $root 'third-party\DEPENDENCY-MANIFEST.txt')
$tp       = Join-Path $root 'third-party'
foreach ($d in 'tools\DellFanCmd','tools\DellSetThermalSetting','tools\LibreHardwareMonitor','licenses','upstream-source') {
  Need (Join-Path $tp $d) | Out-Null
}
$zips = 'DellFanManagement-2.1.1-source.zip','LibreHardwareMonitor-v0.9.6-source.zip',
        'bzh-windrv-dell-smm-io-67786c69-source.zip','PawnIO.Modules-0.2.2-source.zip'
foreach ($z in $zips) { Need (Join-Path $tp "upstream-source\$z") | Out-Null }
foreach ($f in 'tools\DellFanCmd\DellFanCmd.exe','tools\DellSetThermalSetting\DellSetThermalSetting.exe',
               'tools\LibreHardwareMonitor\LibreHardwareMonitorLib.dll') { Need (Join-Path $tp $f) | Out-Null }

# --- tools ------------------------------------------------------------------
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
Need $csc | Out-Null
$makensis = $null
$cmd = Get-Command makensis.exe -ErrorAction SilentlyContinue
if ($cmd) { $makensis = $cmd.Source }
else {
  foreach ($c in "$env:ProgramFiles\NSIS\makensis.exe", "${env:ProgramFiles(x86)}\NSIS\makensis.exe") {
    if (Test-Path -LiteralPath $c) { $makensis = $c; break }
  }
}
if (-not $makensis) { Fail 'makensis.exe not found (install NSIS 3)' }
Write-Host "csc:       $csc"
Write-Host "makensis:  $makensis"
& $makensis /VERSION

# --- staging ----------------------------------------------------------------
if (Test-Path -LiteralPath $build) { Remove-Item -LiteralPath $build -Recurse -Force }
New-Item -ItemType Directory -Force -Path $staging, $out, "$staging\tools", "$staging\source" | Out-Null

Copy-Item -Path (@($cs) + @($manifest, $tpn, $depm, $readmeI, $reviewN)) -Destination $staging
Copy-Item (Join-Path $tp 'licenses') -Destination "$staging\licenses" -Recurse
foreach ($t in 'DellFanCmd','DellSetThermalSetting','LibreHardwareMonitor') {
  Copy-Item (Join-Path $tp "tools\$t") -Destination "$staging\tools\$t" -Recurse
}
foreach ($z in $zips) { Copy-Item (Join-Path $tp "upstream-source\$z") -Destination "$staging\source" }
Copy-Item $building -Destination "$staging\source\BUILDING.txt"
Copy-Item $nsi      -Destination "$staging\source\ACE-Dell-Control-Preview.nsi"

# --- compile GUI (BUILDING.txt command, now with the four src/*.cs files) ---
Push-Location $staging
try {
  $exe = 'ACE Dell Control.exe'
  & $csc /nologo /target:winexe /platform:x64 /optimize+ "/out:$exe" /win32manifest:app.manifest `
    /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll `
    /reference:System.Management.dll /reference:System.Windows.Forms.dll @srcFiles
  if ($LASTEXITCODE -ne 0) { Fail "csc exited with $LASTEXITCODE" }
} finally { Pop-Location }
Need (Join-Path $staging 'ACE Dell Control.exe') | Out-Null

# --- installer --------------------------------------------------------------
# The .nsi uses paths relative to its own folder (staging\..., REVIEW-NOTICE.txt).
# Copy it (unedited) into build/; the installer is moved to build/out afterwards.
Copy-Item $nsi -Destination $build
Copy-Item $reviewN -Destination $build
$setup = Join-Path $out 'ACE-Dell-Control-Setup.exe'
$nsiCopy = Join-Path $build 'ACE-Dell-Control-Preview.nsi'
& $makensis /V2 $nsiCopy
if ($LASTEXITCODE -ne 0) { Fail "makensis exited with $LASTEXITCODE" }
# The script's own OutFile writes next to the .nsi copy; move it to the output name.
$built = Need (Join-Path $build 'ACE-Dell-Control-Preview-Setup.exe')
Move-Item -LiteralPath $built -Destination $setup
Need $setup | Out-Null

# --- outputs ----------------------------------------------------------------
Copy-Item (Join-Path $staging 'ACE Dell Control.exe') -Destination (Join-Path $out 'ACE Dell Control.exe')
$lines = foreach ($f in Get-ChildItem $out -File | Where-Object Name -ne 'SHA256SUMS.txt' | Sort-Object Name) {
  '{0} *{1}' -f (Get-FileHash $f.FullName -Algorithm SHA256).Hash.ToLower(), $f.Name
}
Set-Content -Path (Join-Path $out 'SHA256SUMS.txt') -Value $lines -Encoding ascii
Write-Host '--- SHA256SUMS.txt'; $lines | ForEach-Object { Write-Host $_ }
Write-Host 'BUILD OK'
