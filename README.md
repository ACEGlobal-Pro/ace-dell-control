# ACE Dell Control

A small Windows utility to read temperatures and fan speed, and to set fan and
thermal modes, on **one laptop model: the Dell Latitude 7400**. It is a
**technical preview**: unsigned, tested only lightly, and not ready for general use.

Copyright (C) 2026 ACE Global Pro. Licensed under GPL-3.0-only (see `LICENSE`
and `docs/LICENSING.md`). Not affiliated with or endorsed by Dell Inc.

## Status

- Windows 11 x64 only. Dell Latitude 7400 only: the app checks the model name
  and keeps the Dell controls disabled on anything else.
- Unsigned. Windows Smart App Control (and some antivirus products) will block
  it. See `docs/CODE-SIGNING-POLICY.md`. Do not turn off Windows security
  features to run it.
- Hardware testing so far is limited; see `docs/test-runs/` and `docs/STATUS.md`.

## Safety warnings

- It controls the laptop's fans through the embedded controller using a kernel
  driver. A wrong setting or a crash can leave the fans off or too slow and the
  machine can overheat. Use at your own risk.
- Manual fan modes switch off Dell's automatic fan control. Exit, fan test and
  uninstall try to switch it back on, but that is best effort, not guaranteed.
  If the fans seem wrong after a crash, restart the laptop.
- While the driver is loaded, other programs on the machine may be able to
  issue the same low-level calls. See `SECURITY.md`.
- The "Turbo" buttons set Windows' *maximum processor state* to 99% or 100%
  for the current power plan. 99% often, but not always, avoids Turbo Boost.
- The app runs as administrator and installs a driver. Read `docs/audit/safety-review.md`
  first.

## Building

Local build on Windows x64 with .NET Framework 4.x and NSIS 3. See
`docs/BUILD.md`. Releases are built locally too; nothing runs in the cloud.

## Third-party components

DellFanManagement 2.1.1 (GPL-3.0), the bzh Dell SMM driver (GPL-3.0),
LibreHardwareMonitor 0.9.6 (MPL-2.0), PawnIO.Modules 0.2.2 (LGPL-2.1),
Costura.Fody (MIT) and Microsoft .NET support libraries (MIT). Licence texts:
`third-party/licenses/`. Notices: `third-party/THIRD-PARTY-NOTICES.txt`.
Source for the GPL, LGPL and MPL components: `third-party/upstream-source/`.

## Privacy

This program will not transfer any information to other networked systems
unless specifically requested by the user or the person installing or operating
it. See `PRIVACY.md`.

## Code signing policy

Releases are currently **unsigned**. The project applied to the SignPath
Foundation's free open-source signing programme in October 2026 and was not
accepted yet because it is new; it may reapply once it is more established.
See `docs/CODE-SIGNING-POLICY.md`.

## No warranty

This program is distributed in the hope that it will be useful, but WITHOUT ANY
WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A
PARTICULAR PURPOSE, as set out in the GNU General Public License.
