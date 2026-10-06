ACE Dell Control Technical Preview

1. Close ACE Dell Control before running setup or uninstalling it.
2. Run ACE-Dell-Control-Preview-Setup.exe and approve the Windows Administrator
   prompt only if you trust this copy.
3. Use the ACE Dell Control desktop or Start menu shortcut.
4. The application asks for Administrator access each launch. It does not
   disable User Account Control or install a background service.

Supported install environment: x64 Windows 10/11 with .NET Framework 4.7.2+.
Windows 10 is outside Microsoft's normal support after October 2025; its
inclusion here is technical compatibility, not an OS security endorsement.

Hardware safety: Dell-specific thermal/fan controls are enabled only on the
tested Dell Latitude 7400. Other models are monitoring/Windows-power only.
RPM and CPU temperature readings may show N/A when a sensor is unavailable.

Setup always installs to Program Files; the app refuses to run from any other
folder (it runs as Administrator and loads a kernel driver from its folder).

Closing the app, sleep, sign-out and Windows shutdown restore Dell automatic
fan control, and the app only says "restored" when the fan tool's output
confirms it. Otherwise it says the restore is UNCONFIRMED, holds the fan at Max
and asks you to restart the laptop. While manual mode is active a marker is
kept in %ProgramData%\ACE Dell Control; if the app is killed or crashes, the
next start finds it and restores Dell Auto. A hard power loss or forcibly
killing the process runs no code: restart the laptop before further testing.
The activity log is written to %ProgramData%\ACE Dell Control\logs.
On a Latitude 7400, uninstall runs a confirmed Dell Auto restore before
removing the fan tools and cancels removal if it cannot be confirmed.

This is an unsigned review build. A self-signed certificate would not make it
trusted by Windows SmartScreen. Do not install custom root certificates or
disable security settings to suppress a warning.

Bundled third-party license/source information is in the installed source
folder and THIRD-PARTY-NOTICES.txt. The ACE GUI is licensed under GPL-3.0-only; the bundled components keep their own licenses.
