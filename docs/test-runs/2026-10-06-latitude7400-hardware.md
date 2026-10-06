# Supervised hardware test: Dell Latitude 7400 test machine, 2026-10-06

Machine: a Dell Latitude 7400 test machine · Approved by the project maintainers
Build: installer SHA-256 `1cebce4e94714447bde9f1b3a3a2a5f70d0d8d10922e9a62d6be64981355c7ac`

## Result: STOPPED / FAIL (blocked by Smart App Control; no fan step ran)

Windows **Smart App Control is On (enforcing)** on this laptop. It blocked the unsigned,
locally compiled `ACE Dell Control.exe` when the test launched it ("An Application Control
policy has blocked this file", Code Integrity events 3033/3077, policy
`VerifiedAndReputableDesktop {0283ac0f-…}`). The brief says to stop and report if a Windows
security setting blocks the app, and not to work around it. The run stopped after step 1.
No manual fan mode, thermal mode, Fan Test or processor-state change was made.

EC automatic fan control was confirmed restored at the end, from DellFanCmd's output text.
The Dell was left clean.

## Baseline (read-only, 14:50)

| Item | Value |
|---|---|
| Model / SKU / BIOS | `Dell Inc.` / `Latitude 7400`, SKU 08E1, BIOS 1.43.0 (2025-09-08) |
| OS | Windows 11 Pro 10.0.26200, on AC power |
| Secure Boot | Off |
| Memory Integrity (HVCI) | On, running |
| Vulnerable driver blocklist | On (`VulnerableDriverBlocklistEnable = 1`) |
| Smart App Control | **On** (`VerifiedAndReputablePolicyState = 1`), found after the block |
| Prior ACE Dell Control / DellFanCmd | Not installed, not running; no marker; no `BZHDELLSMMIO` service |
| Max processor state | AC 99% / DC 99% (Balanced plan) |
| Fan 1 RPM (DellFanCmd `rpm-fan1`) | 4,801; 3,395–4,818 across the session in Dell Auto. Fan 2 not present (upstream returns 0xFFFFFFFF) |
| CPU temperature | Not read independently. The only ACPI zone reads a static 25.1 °C. The app's own reading was never reached. |

## Results

| Step | Expected | Observed | Result | Temps / RPM |
|---|---|---|---|---|
| 0 Driver under HVCI | bzh driver loads, or STOP if blocked | Loads. Code Integrity logs event 3076 each time: the driver fails the signing requirement, but the *audit-mode* policy `{784c4414-…}` lets it load | PASS (risk noted) | 4,801 RPM |
| 1 Silent install, launch, model gate | exit 0; log `Model gate: PASSED` with `Dell Inc. / Latitude 7400` | Install exit 0. Installed DellFanCmd SHA-256 matches the build tree. **Launch blocked by Smart App Control.** Run stopped. | FAIL (blocked) | 4,818 RPM before |
| 2 Read sensors | temp and RPM shown/logged | Not run | NOT RUN | — |
| 3 Thermal modes | each confirmed; end on baseline | Not run | NOT RUN | — |
| 4 Medium / Max / Dell Auto | RPM read; restore confirmed; marker gone | Not run | NOT RUN | — |
| 5 Fan Test | completes, restore confirmed | Not run | NOT RUN | — |
| 6 Max processor state | 99% verified AC+DC, back to baseline | Not run (stays at baseline 99/99) | NOT RUN | — |
| 7 Close during manual | close restores Dell Auto | Not run | NOT RUN | — |
| 8 Crash recovery | marker → relaunch restores and tells user | Not run | NOT RUN | — |
| 9 Uninstall | exit 0, no leftovers, EC automatic | `Uninstall.exe /S` exit 0. Its `--restore` step ran the app (no block event), logged `Model gate: PASSED`, and logged `Dell Auto restore CONFIRMED (installer, attempt 1)`. The folder holding `Uninstall.exe` remained, as expected with `_?=`, and was removed by hand. Shortcuts and registry keys were removed by the uninstaller. Left by design: `%ProgramData%\ACE Dell Control\logs` (audit trail). | PASS | — |
| Final independent ec-enable | DellFanCmd output proves `enable EC control … Success.` | Confirmed from output text after every run attempt (14:57:51, 14:58:51, 15:00:58). RPM 3,395 → 3,398 | PASS | 3,398 RPM |

A first attempt at 14:57 stopped on a script error (a PowerShell strict-mode bug on an empty
app log) before the app was launched. Its `finally` block still ran a confirmed ec-enable.
The bug was fixed and the run restarted at 14:58.

## Findings

1. **Smart App Control blocks the unsigned GUI** on a standard Windows 11 install. The
   Windows Sandbox smoke test could not show this, because Sandbox has no Smart App
   Control. Any user with Smart App Control on cannot start the app. Turning Smart App
   Control off cannot be undone without reinstalling Windows. The durable fix is code
   signing (see `docs/CODE-SIGNING-POLICY.md`). Running the test on this laptop would need
   either a signed build, or a decision to turn Smart App Control off.
2. **The block was inconsistent.** The same EXE ran when the NSIS uninstaller launched it
   (`--restore`, 15:00:50), and no Code Integrity event was logged. It was not exploited
   to continue the test. One consequence: the uninstaller's safety restore happened to
   work, but it cannot be relied on while the GUI is blocked.
3. **Uninstall robustness.** If Smart App Control had also blocked `--restore`, the
   uninstaller would refuse to remove the app on a Latitude 7400 (`unsafeToRemove`). The
   user could not remove it without a manual cleanup.
4. **The bzh driver loads only because a Code Integrity policy is in audit mode**
   (event 3076, policy `{784c4414-…}`). If Microsoft moves that policy to enforce, fan
   control stops working on HVCI machines. This adds to H5 and the driver-signing
   question.
5. The DellFanCmd output on real hardware matches the strings the app parses. Load, enable
   and RPM lines are exact (`Result: N` equals the exit code). The uninstaller-path restore
   was confirmed on hardware.
