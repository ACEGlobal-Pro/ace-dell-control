# ACE Dell Control: status

Technical preview. Not a stable release.

## What has been done

- Independent audit (`docs/audit/`): third-party binaries match upstream releases; the PawnIO source snapshot is version 0.2.2; a Costura.Fody notice was added. Safety fixes were made for every Critical and High finding; C1, H2 and H5 are only partly fixed (see the fix status table in `docs/audit/safety-review.md`). The persistent-manual option was removed.
- Repeatable local Windows build from repository files (`scripts/build.ps1`). Reference build hashes: installer `1cebce4e94714447bde9f1b3a3a2a5f70d0d8d10922e9a62d6be64981355c7ac`, GUI `623e1bab839154f6d46b0639a803c10b2c6a20ba7e1520637025688567ac8078`.
- Clean-Windows smoke test in Windows Sandbox passed (55 checks, 0 failures): install, launch, model gate CLOSED on virtual hardware, no fan tool run, clean close, uninstall with no leftovers.
- A supervised hardware test on a Dell Latitude 7400 stopped at step 1: Windows Smart App Control blocked the unsigned app. No fan, thermal or CPU setting was changed, and Dell automatic fan control was confirmed restored (`docs/test-runs/2026-10-06-latitude7400-hardware.md`).

## What is not tested

- Fan modes, thermal modes, Fan Test, the processor-state setting, close-during-manual restore and crash recovery have not run on real hardware with this build.
- No other Dell model has been tested. The Dell controls are disabled on any other model.
- Windows 10 and other Windows 11 configurations have had only the sandbox smoke test.

## Known limitations

- Unsigned (the SignPath Foundation declined the October 2026 application for now: the project is too new). Windows Smart App Control blocks the app, and some antivirus products may too. Do not turn off Windows security features to run it. Signing plan: `docs/CODE-SIGNING-POLICY.md`.
- The bundled fan driver loads on machines with Memory Integrity enabled only because a Windows code-integrity policy is in audit mode. If Microsoft enforces that policy, fan control will stop working on those machines.
- Restoring Dell automatic fan control is best effort. After a crash or power loss, restart the laptop.
- Open review items: guard process and boot task (H2), driver access-rights check (H5), SKU and BIOS gate (M5), restore before upgrade (M7), Turbo setting (M9).
- Hardware compatibility: Dell Latitude 7400, Windows 11 x64 only for any first release.
