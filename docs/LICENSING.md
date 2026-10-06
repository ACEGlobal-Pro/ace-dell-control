# Licensing

## ACE Dell Control's own code

The source in `src/` and the scripts, installer script and documentation written
for this project are copyright (C) 2026 ACE Global Pro and released under the
**GNU General Public License, version 3 only** (SPDX: `GPL-3.0-only`). The
licence text is in `LICENSE`.

### Why "only" and not "or later"

"Or later" would let ACE Dell Control be redistributed under a future GPL
version. That is only sound if everything it ships alongside permits it. The
licensing audit (`docs/audit/licensing-and-provenance.md`, section 3 and open
question 6) could not establish whether DellFanManagement or the bzh driver
allow "or later": both ship a plain GPL-3.0 `LICENSE` file with no per-file
headers saying "or any later version". Without that grant, "only" is the
choice that cannot conflict with the bundled GPL components. It can be
relaxed to "or later" later; tightening it afterwards would not be possible
for code others have already received.

### Why GPL at all

The GUI starts the GPL tools as separate processes and loads the MPL-2.0
LibreHardwareMonitor library by reflection, so the GPL is not strictly forced
by linking (an inference in the audit, not legal advice). It was chosen anyway
by the project owner's decision to publish the project as open source.

## Third-party components

Shipped unmodified, each under its own licence. Full texts are in
`third-party/licenses/`; the inventory is in `third-party/THIRD-PARTY-NOTICES.txt`
and `third-party/DEPENDENCY-MANIFEST.txt`.

| Component | Version | Licence |
|---|---|---|
| DellFanManagement (DellFanCmd, DellFanLib, DellSetThermalSetting) | 2.1.1 | GPL-3.0 |
| bzh-windrv-dell-smm-io driver | 2015 build, source commit 67786c69 | GPL-3.0 |
| LibreHardwareMonitorLib | 0.9.6 | MPL-2.0 |
| PawnIO.Modules (embedded in LibreHardwareMonitorLib) | 0.2.2 | LGPL-2.1 |
| Costura.Fody (merged into DellFanCmd.exe) | 4.1.0 | MIT |
| System.Memory, System.Buffers, System.Numerics.Vectors, System.Runtime.CompilerServices.Unsafe | see notices | MIT |

## Corresponding source

Source for every GPL, LGPL and MPL component that is shipped as a binary is in
`third-party/upstream-source/` (DellFanManagement 2.1.1, LibreHardwareMonitor
0.9.6, PawnIO.Modules 0.2.2, bzh-windrv-dell-smm-io 67786c69). The driver
binary was not rebuilt from that source, so equivalence is likely but unproven.
ACE Dell Control's own source is `src/`. If you received a binary installer, you
may ask for this source through the repository's issue tracker.

## Trademarks

Dell and Latitude are trademarks of Dell Inc., used only to name the supported
laptop. This project is not affiliated with or endorsed by Dell Inc.
