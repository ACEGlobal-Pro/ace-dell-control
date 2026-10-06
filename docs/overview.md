# ACE Dell Control — overview

Stable knowledge only; status lives in `docs/STATUS.md`.

## Layout

| Path | Role |
|---|---|
| `src/*.cs`, `src/app.manifest` | The ACE GUI (C# WinForms, .NET Framework 4.7.2+): entry point, main window, safety logic, sensors |
| `installer/ACE-Dell-Control-Preview.nsi` | NSIS 3 script producing the one customer-facing installer EXE |
| `third-party/tools/` | Upstream binaries shipped in the installer (DellFanCmd 2.1.1 + bzh SMM driver, DellSetThermalSetting, LibreHardwareMonitorLib 0.9.6 + .NET support DLLs) |
| `third-party/upstream-source/` | Source snapshots of the GPL/LGPL/MPL components, as received |
| `third-party/licenses/`, `THIRD-PARTY-NOTICES.txt`, `DEPENDENCY-MANIFEST.txt` | Licence texts and inventory as received (not legal clearance) |
| `installer/BUILDING.txt` | The original build note (GUI compile command) |

## How it works (as received from the preview bundle)

The GUI shells out to `DellFanCmd.exe` (EC fan control through the bzh Dell
SMM driver) and `DellSetThermalSetting.exe` (Dell thermal modes), reads CPU
temperature through LibreHardwareMonitorLib, and sets the Windows "maximum
processor state" to 99%/100% for its "Turbo" control. Manual fan modes
disable EC automatic control and send keep-alives; exit, fan test and
uninstall try to restore it — best-effort, not fail-safe.

## Reference hashes (verified 2026-10-06)

- Preview installer `ACE-Dell-Control-Preview-Setup.exe`: `cdb11fd11c924d4ee9cb5fbce0b8495deff0925f100dda561a730140e368af33`
- Source bundle zip: `73b3894953fc8e9948e80a057a43b003e921e52ae89ed8ef46d21e7db9774c43`
