# Building and smoke-testing ACE Dell Control (technical preview)

All building and testing is done locally. The only use of GitHub Actions is the release-only workflow (`.github/workflows/release.yml`), which runs on version tags to build and sign a release.

## Build

Requires Windows x64, .NET Framework 4.x (`csc.exe` under `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319`) and NSIS 3.

    pwsh ./scripts/build.ps1

`scripts/build.ps1` recreates the original staging layout in `build/staging` from repo files (`src/`, `third-party/`, `installer/`, `installer/BUILDING.txt`), compiles the GUI (the four `src/*.cs` files) with the command from `BUILDING.txt`, copies the unedited `.nsi` into `build/` so its relative paths work, runs `makensis`, and moves the installer to `build/out`. Outputs in `build/out/`: `ACE-Dell-Control-Setup.exe`, `ACE Dell Control.exe` (the compiled GUI), `SHA256SUMS.txt`. Any missing input stops the build with a non-zero exit.

## Smoke test

    pwsh ./scripts/smoke-test.ps1     # needs an elevated prompt; installs on the machine it runs on

Silent install, check expected files, shortcuts and uninstall registry entries, launch the installed GUI, confirm it stays up 15 s, read its activity log file (`%ProgramData%\ACE Dell Control\logs\activity-YYYYMMDD.log`, which mirrors the on-screen log) and assert `Model gate: CLOSED` and that no DellFanCmd ran, close with `CloseMainWindow`, silent uninstall, check for leftovers. Transcript: `build/out/smoke-test.log`. It never runs `DellFanCmd` or `DellSetThermalSetting`.

## Where it runs

Locally only, on a Windows x64 machine. The smoke test installs software, so run it inside **Windows Sandbox** (a throwaway clean Windows; see `scripts/sandbox/`), never on your everyday Windows. Windows Sandbox is virtual hardware, so the Dell model gate keeps the fan controls off there.

## What the smoke test proves

- The source compiles and the installer builds from repository files alone.
- The installer installs silently on a clean Windows machine, creates the expected files, shortcuts and uninstall entry, and the uninstaller removes them.
- The GUI launches elevated, stays up, and shuts down cleanly on a window close.
- On a non-Dell machine the model gate is CLOSED and the Dell controls are disabled (read from the app's log file).

## What it does NOT prove

- Anything about real hardware: no Dell Latitude 7400, no fan or thermal control, no EC or driver behaviour, no sensor readings.
- Real-hardware behaviour inside Windows Sandbox: it is a virtual machine on the Dell, so the Dell code paths do not run there.
- Signing, SmartScreen, or antivirus reputation: nothing is code-signed.
- The uninstaller's Dell-model path (restoring automatic fan control), which only runs on a Latitude 7400.

## Pre-check on the Mac (no Windows needed)

Syntax and API checks only, not a substitute for the Windows build: Mono's `csc`
with `-langversion:5` (the Windows Framework compiler is C# 5) against
`/opt/homebrew/lib/mono/4.7.2-api`, and `makensis` (run with `LC_ALL=en_US.UTF-8`;
it crashes in the default locale) against a dummy `staging/` tree.
