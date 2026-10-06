# ACE Dell Control: fan-control safety review

Reviewed 2026-10-06 · Scope: `src/ACE.Dell.Control.cs`,
`src/app.manifest`, `installer/ACE-Dell-Control-Preview.nsi`, and the preview bundle's safety
notes. I checked them against the upstream DellFanManagement 2.1.1 source and the bzh
driver source in `third-party/upstream-source/`. This was a read-only review: no code
was changed and nothing was run on hardware.

## Summary (plain English)

1. The app cannot currently tell whether "restore Dell Auto" worked. The fan tool it
   calls reports success even when its driver fails to load or the BIOS call fails.
   So every "restored" message, and the uninstaller's check, can be wrong.
2. The app starts a fresh copy of the fan tool every 5 to 6 seconds, plus one for each
   button press. Each copy installs and then removes a kernel driver. Nothing stops
   two copies running at once, and one copy can pull the driver out from under the
   restore command.
3. Closing the app, a crash, sleep, a forced kill or a mid-command exit can leave
   Dell's automatic fan control off. Nothing on disk records that, nothing recovers it
   at the next start, and nothing watches for it. The "Safe Fan Test" can even end
   with the fan stopped.
4. The app runs as administrator but finds `powercfg.exe` and its sensor DLLs by
   searching its own folder, and the installer lets the user pick any folder. Installed
   outside Program Files, or run portably, it becomes a way for ordinary programs to
   gain administrator rights.
5. Not ready for hardware testing beyond a supervised Latitude 7400 session. Fix the
   Critical and High items first. Several behaviours (EC timeout, sleep and reboot
   effects, driver races) can only be proven on the real laptop.

Line references are to this branch (`src/ACE.Dell.Control.cs` unless stated).
Upstream references are to `DellFanManagement-2.1.1/…` and
`bzh-windrv-dell-smm-io-67786c69…/…` inside the zipped sources.

## Fix status (2026-10-06)

Code `c8917a7`, installer `575293d`, smoke test `f996cc5`. Compiled on the Mac with
Mono `csc` at C# 5 against the .NET Framework 4.7.2 reference assemblies (no errors, no
warnings); the output parser and the restore routine were exercised with a fake
DellFanCmd (success, driver-load failure exiting 0, hang); `makensis` 3.13 compiles the
installer script. **Nothing has run on Windows or on the Latitude 7400 yet.**

| Finding | Status | Commit | What changed / what remains |
|---|---|---|---|
| C1 success detection | Partly | c8917a7, 575293d | Success now needs exit 0, the driver-load line and the command's own line each followed by ` ...Success.`, and no failure on stderr. "Restored" is shown only when confirmed; otherwise "restore UNCONFIRMED — restart the laptop", the fan is held at Max and the marker kept. The uninstaller uses the app's `--restore` mode. **Remains:** an SMM-level failure is still invisible (upstream 32/64-bit sentinel bug); the fix is in-process `DellFanLib` with correct signatures (GPL licence question) or an RPM plausibility check proven on hardware. |
| C2 overlapping processes | Fixed | c8917a7 | One serialized executor for every tool process (DellFanCmd, thermal tool, powercfg, sc), timeouts on all, restore pre-empts queued work; all command buttons disabled while one runs; keep-alive and background RPM run only when idle. Still one driver load per command (see C1 remainder). |
| H1 close during ec-disable | Fixed | c8917a7 | `ManualActive` and the marker are set before `ec-disable`/test; closing waits for the in-flight command, then restores; tool processes are in a kill-on-close job object. |
| H2 kill / hang / crash | Partly | c8917a7 | Marker `%ProgramData%\ACE Dell Control\manual-mode.marker` (admin-only ACL, junctions refused); next start restores and tells the user; unhandled exceptions restore. **Not done:** guard process with heartbeat, boot-time scheduled task. A kill, power loss or BSOD still runs no code until the next start. |
| H3 fan test | Fixed | c8917a7 | ACE sequence Medium → Max → verified restore; never level 0; 60 s cap; failed restore holds Max and says so; button renamed "Fan Test". |
| H4 elevation / search path | Fixed | c8917a7, 575293d | powercfg and sc by System32 path; app refuses to run unless under Program Files and not writable by Users/Authenticated Users/Everyone; sensor DLLs only from the install folder with a named allowlist; tool working directory set; installer has no directory page and forces `$PROGRAMFILES64`. **Later:** Authenticode checks on the tools. |
| H5 driver exposure | Partly (residual) | c8917a7 | Driver no longer loaded in Dell Auto mode (RPM on demand only; background RPM only in manual mode); leftover service stopped and deleted after a killed DellFanCmd and at uninstall. **Residual risk:** while the bzh driver is loaded (manual mode, keep-alive, fan test) any local process may be able to issue SMM calls if its device ACL is open. Verify with `accesschk`; a secured driver fork is a project-owner decision. |
| M1 keep-alive | Fixed | c8917a7 | Through the executor, skipped when busy, verified, logged; three failures in a row → restore. |
| M2 sleep/resume | Fixed | c8917a7 | Restore on suspend; on resume, retry if still recorded, and tell the user manual mode was cleared. Unsubscribed on close. |
| M3 session end | Fixed | c8917a7 | `SessionEnding` and shutdown-close run a bounded (8 s) restore with `ShutdownBlockReasonCreate`, never a modal; marker left for next start if unconfirmed. |
| M4 persistent manual | Fixed | c8917a7 | Option removed (project owner decision): exit always restores Dell Auto. |
| M5 model gate | Partly | c8917a7, 575293d | Exact `Dell Inc.` + `Latitude 7400` in app and uninstaller (2-in-1 no longer admitted); uninstaller defers to the app's gate. **Not done:** SKU and BIOS-version allowlist (needs values from the validated unit). |
| M6 single instance | Fixed | c8917a7 | `Global\ACE.DellControl` mutex; a second launch says so and exits. |
| M7 installer ordering | Partly | 575293d | Install and uninstall refuse while the mutex exists; verified, bounded restore before removal; driver service cleanup; registry keys removed last. **Not done:** restore before overwriting on upgrade (old builds have no `--restore`). |
| M8 RPM misreport | Fixed | c8917a7 | RPM parsed from ` Result: N`; otherwise "unavailable". |
| M9 Turbo label | Partly | c8917a7 | Buttons now say "Max state 99% / 100%"; no Turbo claim. **Not done:** `PERFBOOSTMODE`, numeric query, revert on uninstall. |
| M10 logging | Fixed | c8917a7 | `%ProgramData%\ACE Dell Control\logs\activity-YYYYMMDD.log` mirrors the on-screen log (ISO time, every command with exit code and output tail, restore verdicts, triggers, startup `Model gate: PASSED/CLOSED` line). Daily files, no size rotation; kept on uninstall as an audit trail. |
| L1 elevation loop | Fixed | c8917a7 | One elevation attempt (`--elevated`). |
| L2 WMI on UI thread | Fixed | c8917a7 | Thermal read-back, counters and model detection off the UI thread. |
| L3 timeouts / kill cleanup | Fixed | c8917a7 | One restore routine (3 attempts within a budget); `sc stop/delete` after any DellFanCmd timeout. |
| L4 sensor driver | Not fixed | — | Still to identify LibreHardwareMonitor's ring-0 component. |
| L5 hard-coded RPM labels | Fixed | c8917a7 | Labels show measured RPM only. |
| L6 disposed controls | Fixed | c8917a7 | UI updates guarded by `IsDisposed`; flows abort when a restore ran under them. |

Additional hardware checks created by these fixes: confirm the real DellFanCmd output
matches the parsed lines (a mismatch fails safe as "UNCONFIRMED"); time a restore on
suspend against Windows' sleep deadline; confirm the install folder passes the ACL check
on a clean Windows install.

---

## Critical

### C1. Success detection is unsound for every DellFanCmd command, including `ec-enable`

- **Where:** GUI `:94` (`RestoreDellAuto`: `ExitCode == 0`), `:353`, `:371`, `:374`,
  `:404`, `:412`, `:429-431`; installer `ACE-Dell-Control-Preview.nsi:100-101`.
  Root cause upstream:
  - `DellFanCmd/DellFanCmd.cs:19, 146`: `returnCode = 0`. When `LoadDriver()` fails,
    the code falls through and **exits 0**, having done nothing.
  - `DellFanCmd/DellFanCmd.cs:155, 194, 209`: failure is tested as
    `result == ulong.MaxValue`. But the native functions return a 32-bit
    `unsigned long` (`DellFanLib/DellFanLib.h`; `DellFanLib.cpp:323, 346` return
    `ULONG_MAX` = `0xFFFFFFFF`). The interop declares `ulong`
    (`DellFanLibInterop/DellFanLib.cs:31-56`), so the failure value arrives as
    `4294967295`, never as `ulong.MaxValue`. **The `Failed.` branches for
    ec-disable, ec-enable and fan-level can never run.** The tool prints
    ` ...Success.` and exits 0 whether or not the SMM call worked. Only
    `rpm-fan*` fails visibly, and only by accident: `int.Parse("4294967295")`
    throws, so it returns -1. The fan test's own fan-2 check (`:262`) uses
    `uint.MaxValue`, which confirms the author saw the 32-bit value there.
- **Failure scenario:** The user presses Dell Auto, or closes the app, while the
  driver cannot load. Causes include the race in C2, the driver blocklist or HVCI, a
  service marked for deletion, or a missing `.sys`. DellFanCmd exits 0. The GUI then:
  - sets `ManualActive = false` (`:96` / `:355`);
  - stops keep-alive and logs "Dell automatic fan control restored";
  - lets the window close.

  The EC is still disabled, with the fan pinned at Medium or Max and no keep-alive,
  until reboot. The uninstaller makes the same false check and removes the tools,
  which also removes the in-app way to recover.
- **Fix:**
  1. Stop trusting exit code 0. Short term, with no upstream edits: success needs
     exit code 0 **and** stdout containing the `Loading SMM I/O driver...` line
     followed by ` ...Success.`, **and** the command's own `Attempting to …` line
     followed by ` ...Success.`, **and** no `Failed.` on stderr. Anything else is
     "restore NOT confirmed".
  2. Because of the 32-bit/64-bit sentinel bug, even that cannot detect an SMM-level
     failure. The durable fix is to stop shelling out: load `DellFanLib.dll` in-process
     (or in the guard process from H2) with **correct** signatures (`uint` returns,
     failure = `0xFFFFFFFF`), call `Initialize()` once, and check every return value.
     This is a GPL-3.0 component, so the licence work in to-do §3 covers it either way.
     Note that `GetDriverPath()` resolves the `.sys` next to the **host EXE**.
  3. After `ec-enable`, add a plausibility check: read the RPM twice, a few seconds
     apart, and confirm it moves away from the fixed manual level. Report "not
     confirmed" rather than "restored" when it does not.
  4. In the installer, parse the output the same way, or call the guard EXE's
     `--restore` mode, which returns a trustworthy code.

### C2. Overlapping DellFanCmd processes race on install, start, stop and delete of the kernel driver

- **Where:** monitor timer `:168-170` → `:491` (an `rpm-fan1` process every ~6 s,
  **even in Dell Auto mode**); keep-alive timer `:171-173` → `:391` (a fan-level
  process every 5 s); every button (`:209-213`); close-path restore `:89`. There is no
  lock or queue anywhere. `ActionButton` (`:646-651`) disables only the clicked
  button, so Dell Auto, Medium, Max, Read RPM and Fan Test can all run at once.
  Upstream behaviour:
  - `DellFanLib.cpp:55-112`: each process calls `Initialize()`. If the device cannot
    be opened, it runs `InstallDriver()`, which first calls `RemoveDriver()`, which
    stops and deletes the service.
  - Every process calls `Shutdown()` → `RemoveDriver()` on exit (`DellFanCmd.cs:334`),
    which stops and deletes the driver service **even while another process has the
    device open**.
- **Failure scenario:** Keep-alive (5 s) and RPM polling (6 s) line up every ~30 s by
  construction, so overlap is routine, not rare. Example: a Dell Auto click (or the
  exit restore) starts `ec-enable` while a keep-alive `fan1-level1` process is
  exiting.
  1. The keep-alive process's `Shutdown()` stops the driver and deletes the service,
     which is marked for deletion while handles remain.
  2. `ec-enable` cannot open `\\.\BZHDELLSMMIO`, tries `CreateService`, and fails
     with `ERROR_SERVICE_MARKED_FOR_DELETE`.
  3. `LoadDriver()` returns false, and by C1 the process **exits 0**. The GUI
     reports Dell Auto restored while the EC stays disabled.

  Other interleavings in the same family: Fan Test plus Max at the same time; Dell
  Auto finishing between `ec-disable` and the level command (`:370-381`), leaving the
  UI showing "Manual" and keep-alive running.
- **Fix:**
  - One serialized command executor: a `SemaphoreSlim(1,1)` or single-consumer queue
    that every fan or thermal operation goes through, including timers and exit
    restore.
  - Keep the driver loaded once for the session (in-process or guard, see C1), not
    per command.
  - Disable the whole fan group while any fan command is in flight.
  - Never start keep-alive or RPM work while a mode change is pending.
  - Give restore priority: cancel queued work, wait for the in-flight command, then
    run `ec-enable`.
  - Drop RPM polling to on-demand, or no faster than 30 s, when not in manual mode.

---

## High

### H1. Closing or crashing during an in-flight `ec-disable` leaves EC disabled with no restore

- **Where:** `:370-372`. `Safety.ManualActive = true` is set only **after**
  `ec-disable` returns. `OnClosing` `:515` and `RestoreDellAuto` `:83` skip restore
  when `ManualActive` is false. The child process is not tied to the GUI's lifetime.
- **Failure scenario:** The user clicks Force Max and closes the window, or an
  exception reaches `ThreadException`, before `ec-disable` returns. No restore runs,
  the form closes, and the orphaned DellFanCmd finishes `ec-disable` (fans at max).
  The EC stays off, with no keep-alive and no app, until reboot. The same applies to
  the window between `ec-disable` and `manualLevel` being set.
- **Fix:** Use a pessimistic flag. Set `ManualActive = true`, and write the on-disk
  marker from H2, **before** starting `ec-disable` or `test`. Clear both only after a
  confirmed restore. With C2's executor, closing waits for the in-flight command
  (bounded) and then restores. Put child processes in a Job Object so they cannot
  outlive the app unsupervised. Restore must still run after the job kills them.

### H2. Process kill, hang or crash has no recovery path, and the app always starts claiming "Dell automatic control"

- **Where:** `:52`, an `AppDomain.UnhandledException` best-effort restore only.
  There is no watchdog and no persisted state. `:202` hard-codes
  "Status: Dell automatic control" at every start. DellFanCmd has no query for EC
  state.
- **What cannot be made safe:** a TerminateProcess ("End task", or killing the
  process tree), a power loss or a BSOD runs no code in the app. No in-process
  handler can cover these.
- **What can be made safe enough:**
  1. **Marker file:** `%ProgramData%\ACE\DellControl\ec-manual.marker`, admin-only
     ACL, written before any EC disable and deleted after a confirmed restore. At
     startup, if the marker exists: warn and run restore automatically (C1-grade
     verification).
  2. **Guard process:** the GUI starts `ACE Dell Control.exe --guard <pid>`, elevated
     and in a separate job. The guard waits on the GUI's process handle and, when the
     GUI exits for any reason with the marker present, runs `ec-enable`, verifies it,
     and logs. This covers Task Manager "End task" on the GUI, hangs (add a heartbeat
     lease: no renewal for 15 s → restore), and crashes. It does **not** cover killing
     both processes.
  3. **Boot/logon recovery:** a scheduled task (SYSTEM, at startup) that restores if
     the marker exists, for the power-loss case. This only helps if the BIOS does not
     already reset the EC on POST, which must be verified on hardware.
  4. **For a commercial build,** consider a small Windows service that owns the
     driver and handles lease, power and preshutdown events. That is more work but
     the only robust design. It is right-size only if the product goes commercial.
  5. **Relying on an EC timeout is not established.** Nothing in upstream source
     shows that the Dell EC re-takes control by itself. The existence of keep-alive
     suggests some BIOSes revert, but behaviour is unknown for the 7400. This is a
     hardware question.

### H3. "Safe Fan Test" can end with the fan OFF and EC disabled, and claims safety from a meaningless exit code

- **Where:** GUI `:394-423`, upstream `DellFanCmd.cs:248-307`.
  - Upstream ignores every return value in `test`.
  - It runs fan 1 at **level 0 (off)** for 7.5 s.
  - At the end it sets **fan 1 back to level 0** (`:284`) before measuring fan 2
    (another 15 s with fan 1 off if fan 2 is falsely detected) and before its final
    `EnableEcFanControl` (`:305`, result ignored).
  - The GUI's follow-up `ec-enable` is good, but it is judged by exit code (C1).
  - On failure (`:404-409`) the GUI only shows a message. The fan may be left **off**
    with EC disabled: keep-alive is stopped and `manualLevel` is null, with no
    fallback.
  - The 45 s timeout (`:402`) is close to the worst case: driver load, plus 22.5 s
    (one fan), plus 15 s (fan 2 falsely detected). A timeout kill mid-test leaves the
    driver loaded and the fan at whatever level it was.
  - Log text says "completed safely" and the button says "Safe".
- **Fix:**
  - Replace upstream `test` with an ACE-controlled sequence: Medium → Max → restore.
    **Never level 0.** If an off measurement is ever wanted, check temperature first
    and abort above a threshold.
  - Verify each step, with a 60 s hard cap.
  - On any restore failure, command **fan1-level2 (max)** as the fail-safe, keep the
    marker, retry restore with backoff, block close, and show a persistent red banner.
  - Rename the button "Fan Test" and only say "Dell Auto restored (verified)" when C1
    verification passes.

### H4. Elevated app resolves executables and DLLs from its own, user-choosable, directory (privilege escalation)

- **Where:**
  - `:447-449, :464`: `Run("powercfg.exe", …)` with `UseShellExecute=false`. .NET
    Framework calls `CreateProcess(NULL, "powercfg.exe …")`, whose search order is
    **the application directory first**, then the current directory, then System32.
  - `:70-73`: all tools resolve from `AppDomain.BaseDirectory`.
  - `:710-715`: `Assembly.LoadFrom` on `tools\LibreHardwareMonitor`, plus a global
    `AssemblyResolve` hook that loads **any** requested assembly name from that
    folder.
  - Installer `:18` (`MUI_PAGE_DIRECTORY`) lets the user install anywhere. A folder
    created under `C:\` inherits Authenticated Users: Modify.
  - The preview bundle also mentions a portable copy, which runs from a user-writable folder.
- **Failure scenario:** Installed to `C:\ACE Dell Control Preview` or run from
  Downloads, a standard-user process drops `powercfg.exe` next to the app, or
  replaces `DellFanCmd.exe` or `LibreHardwareMonitorLib.dll`. The next elevated launch
  runs it as administrator. DellFanCmd also loads a kernel driver from that folder.
- **Fix:**
  - Use `Path.Combine(Environment.SystemDirectory, "powercfg.exe")`.
  - At startup, refuse to run fan/driver features unless `BaseDirectory` is under
    `%ProgramFiles%` and its ACL denies write to non-admins. Check with `GetAccessControl`.
  - Remove the directory page, or force `$PROGRAMFILES64`.
  - Narrow `AssemblyResolve` to the known LHM dependency names.
  - Set `WorkingDirectory` on child processes to the tool's directory.
  - Later: verify Authenticode on DellFanCmd, DellFanLib and the `.sys` before use.
  - No unquoted-path issue was found: .NET quotes `FileName`, NSIS quotes `ExecWait`,
    `UninstallString` and the shortcuts.

### H5. The bzh driver exposes raw Dell SMM calls, likely to any local user, and the app keeps loading it

- **Where:** `bzh_dell_smm_io/bzh_dell_smm_io.cpp:39-45`:
  - `IoCreateDevice(..., 0, FALSE, …)`: no security descriptor, no
    `IoCreateDeviceSecure` or SDDL, no `FILE_DEVICE_SECURE_OPEN`;
  - IOCTL defined with `FILE_ANY_ACCESS` (`DellFanLib.cpp:28`);
  - the dispatch passes a caller-chosen SMM command and data.

  The GUI loads the driver every 5-6 s (C2), and a killed or timed-out DellFanCmd
  (`:554`, `:99`) leaves it loaded.
- **Failure scenario:** While the driver is loaded, any non-admin process opens
  `\\.\BZHDELLSMMIO` and issues arbitrary SMM calls: EC disable, fan off, or other
  BIOS SMM functions. This is a "vulnerable driver" class of exposure. It is
  probable, not proven: the default device ACL must be checked with
  `accesschk -o \Device\BZHDELLSMMIO` on Windows. That check can be done in a VM if
  the driver loads there.
- **Fix:**
  - Verify the ACL first. If it is open, this is a commercial-release blocker. Record
    it in the threat model (to-do §2).
  - Minimize load time. In C2's single-session design, load only while manual mode or
    a test is active, and **unload on return to Dell Auto**. Never poll RPM through
    the driver in Auto mode.
  - Long-term: an ACE-built driver fork using `IoCreateDeviceSecure` with an
    admin/SYSTEM-only SDDL and a command allowlist. That needs EV/attestation driver
    signing, which is a project-owner decision.
  - Also check Microsoft's vulnerable-driver blocklist and HVCI behaviour (to-do §1).

---

## Medium

### M1. Keep-alive is not fenced from restore, overlaps itself, and fails silently
- **Where:** `:388-392`, `:357`, `:381`, `:399`.
- **Problems:**
  - The timer is stopped only **after** `ec-enable` returns, so a tick during restore
    launches a concurrent fan-level process (feeds C2).
  - Async timer ticks re-enter: a 5 s timeout on a 5 s period.
  - The result is discarded and never logged.
  - `:381` restarts keep-alive even if Dell Auto completed meanwhile, so the UI says
    "Manual" while the EC is in auto.
- **Fix:**
  - Stop the timer and drain any in-flight tick *before* restore.
  - Run keep-alive through the serialized executor with a busy flag.
  - Log failures. After N consecutive failures, escalate: set max, then restore, then
    alert.
  - Re-check `ManualActive` and `manualLevel` after each await.

### M2. Sleep/resume is not handled
- **Where:** no `SystemEvents.PowerModeChanged` handler anywhere.
- **Failure scenario:** Manual Max or Medium, then the lid is closed (S3 or Modern
  Standby). The EC state through suspend is unknown. On resume the keep-alive blindly
  re-asserts a level whether or not the EC reverted, and the UI may lie either way.
  In Modern Standby, a pinned fan in a bag drains the battery.
- **Fix:**
  - On `Suspend`: restore Dell Auto, verified, and remember the requested mode.
  - On `Resume`: stay in Dell Auto and tell the user the manual mode was cleared.
    Re-applying it should be a deliberate click.
  - Unsubscribe on close: it is a static event, and a leak keeps the form alive.

### M3. Session end restore is synchronous and fragile; hibernate and fast startup are not covered
- **Where:** `:510-533`. WinForms raises `FormClosing` (`CloseReason.WindowsShutDown`)
  on `WM_QUERYENDSESSION`, so restore does run.
- **Problems:**
  - It can take up to 2×5 s on the UI thread without `ShutdownBlockReasonCreate`.
    Windows may kill the app first.
  - On failure, `e.Cancel = true` plus a modal `MessageBox` blocks shutdown behind a
    dialog. Forced or update restarts kill the app regardless.
  - Hibernate and fast-startup "shutdown" arrive as suspend (M2).
- **Fix:**
  - Handle `SystemEvents.SessionEnding` and `WM_QUERYENDSESSION` explicitly.
  - Call `ShutdownBlockReasonCreate("Restoring Dell automatic fan control")`, run the
    verified restore with a hard 8 s cap, then allow shutdown and leave the marker
    for boot recovery (H2) instead of a modal.

### M4. Persistent-manual option: false "restored" message and no state after relaunch
- **Where:** `:83` returns `true` when `PersistentManual`, so the `ThreadException`
  handler (`:46-48`) tells the user "Dell automatic fan control was restored" when
  nothing was done. The option is not persisted. After relaunch the app shows "Dell
  automatic control" (`:202`) while the EC may still be disabled with no keep-alive.
  The UI caption "Default safety: closing the app restores Dell Auto" (`:221`)
  overstates the guarantee (see C1, H1, H2).
- **Fix:** **Remove the option for any release.** It defeats every exit safeguard and
  depends on unproven BIOS behaviour without keep-alive. If the project owner wants it kept
  for internal use:
  - make `RestoreDellAuto` return a tri-state (`Restored`, `SkippedByUser`,
    `NotConfirmed`) and word messages accordingly;
  - keep the marker (H2) so the next launch shows "Manual (persisted)" with a
    one-click restore;
  - never let the uninstaller skip restore.

### M5. Model gate is a substring match and broader than the evidence
- **Where:** `:270-272`. `Manufacturer + " " + Model`, then `IndexOf("Dell")` and
  `IndexOf("Latitude 7400")`. This admits "Latitude 7400 2-in-1" and any future
  "Latitude 7400*", which to-do §1 says is unconfirmed. There is no SKU or BIOS check.
  The uninstaller (`nsi:95-97`) uses a different source and exact matching, so the
  two gates can disagree.
- **Note:** The WMI strings come from SMBIOS and are not forgeable without admin, so
  spoofing is not the concern; breadth is.
- **Fix:**
  - Exact match: `Manufacturer == "Dell Inc."` and `Model` in an explicit allowlist.
  - Add `SystemSKUNumber` (Win32_ComputerSystem) and a BIOS-version allowlist
    (Win32_BIOS.SMBIOSBIOSVersion) recorded from the validated unit.
  - Unknown BIOS → monitoring only.
  - Share one gate table between app and installer, for example by having the
    installer call `--restore`, which gates itself.

### M6. No single-instance guard
- **Where:** `Main` `:24-55`.
- **Failure scenario:** Two instances run, from the desktop and Start menu, or two
  users. Each has its own keep-alive and `ManualActive`. Closing one restores Dell
  Auto while the other keeps re-asserting a manual level. Two pollers double the race
  in C2.
- **Fix:** A named mutex `Global\ACE.DellControl`. A second launch activates the first
  window and exits. The installer can use the same mutex (M7).

### M7. Installer and uninstaller ordering gaps
- **Install or upgrade over a running app** (`nsi:42-85`): there is no running or
  manual-mode check. It overwrites `DellFanCmd.exe`, `DellFanLib.dll` and the `.sys`
  while the app may be mid-keep-alive. A locked file gives an Abort/Retry/Ignore
  prompt, and "Ignore" leaves mixed versions.
  - **Fix:** in the install section, refuse while the mutex (M6) exists. On gated
    models, run a verified restore before copying files.
- **Uninstall detection** (`nsi:90`): `FindWindow "" "ACE Dell Control"` sees only
  the current session's desktop. Another user's or session's running instance is
  missed, its tools are deleted, and that instance can never restore: `RestoreDellAuto`
  returns false at `:84`, and close is blocked forever.
  - **Fix:** check the global mutex.
- **`ExecWait` ec-enable** (`nsi:100`): judged by exit code (C1), with no timeout,
  so a hung tool hangs the uninstaller.
  - **Fix:** verified restore with a timeout.
- **Driver service cleanup:** a lingering `BZHDELLSMMIO` service or loaded driver
  (from a killed DellFanCmd, H5) locks the `.sys`. `RMDir /r` then silently leaves
  files, and the service entry can outlive the uninstall.
  - **Fix:** after restore, `sc stop BZHDELLSMMIO` and `sc delete BZHDELLSMMIO`,
    ignoring "not found", and check the directory is gone.
- Registry keys are deleted before files (`:114-115`). This is minor: delete them
  last, so a failed removal can still be retried from Apps & Features.

### M8. RPM reading misreports tool failure as "0 RPM"
- **Where:** `:429-431`. A driver-load failure exits 0 (C1) and is displayed as
  "0 RPM". That looks like a stopped fan, or hides one: the user cannot tell "fan
  stopped" from "tool failed".
- **Fix:** Require the ` Result: N` line in stdout and parse it. Show "unavailable"
  otherwise.

### M9. "Turbo OFF • 99%" label overclaims; the setting outlives uninstall
- **Where:** `:231-233`, `:441-475`.
  - `PROCTHROTTLEMAX = 99` commonly suppresses Intel Turbo, but it does not reliably
    disable it on Speed Shift (HWP) systems, and to-do §1 asks for exactly this
    validation.
  - The direct control is `PERFBOOSTMODE` (Processor performance boost mode,
    `be337238-0d82-4146-a960-4f3749d470c7`) = 0.
  - Only `SCHEME_CURRENT` changes: switching plans undoes it, and other plans keep
    their own values.
  - "Effective" (`:472`) shows the plan value, not the effective frequency.
  - The regex parses English `powercfg` output only (`:465-466`).
  - Uninstall does not revert the change.
- **Fix:**
  - Label it "Max processor state 99% / 100%" until hardware evidence supports
    "Turbo off", or switch to `PERFBOOSTMODE` and validate it.
  - Use `powercfg /getacvalueindex` and `/getdcvalueindex`, which give numeric output.
  - Record the original value and restore it on uninstall.

### M10. Logging is not adequate for a hardware-control tool
- **Where:** `:583-588`. The log is an in-memory 46 px textbox with times but no
  dates. It is lost on exit. Successful commands, their exit codes and stdout are not
  logged, and keep-alive results are never logged.
- **Fix:** an append-only log at `%ProgramData%\ACE\DellControl\logs\`, admin-write
  ACL, size-rotated. Each EC transition gets one line: ISO timestamp, command, exit
  code, parsed verdict, last 500 chars of output, `ManualActive` before and after,
  and the trigger (button, timer, close, power event, guard). This is what a
  supervised test and any later incident need.

---

## Low

- **L1. Self-elevation loop.** `:26-41` plus `requireAdministrator` in
  `app.manifest`. With UAC disabled and a standard user, `runas` starts another
  non-admin instance, which relaunches again, and so on. Fix: pass a `--elevated`
  marker and stop after one attempt.
- **L2. Blocking WMI call on the UI thread.** `:336` calls
  `DellThermalReader.GetCurrent()` (WMI) on the UI thread. Fix: move it into the
  `Task.Run`.
- **L3. Inconsistent timeouts; kills skip upstream cleanup.** Restore uses 2×5 s
  (`:94`) but Dell Auto uses 10 s (`:352`). On timeout, `Kill()` (`:99`, `:554`)
  skips upstream `Shutdown()`, leaving the driver loaded (H5). Fix: one restore
  routine with one timeout. Run a `sc stop/delete` cleanup after any kill.
- **L4. Sensor-library kernel driver not identified.** LibreHardwareMonitor 0.9.6
  loads its own ring-0 component into the elevated process. Confirm which one
  (PawnIO or WinRing0) and its blocklist status. WinRing0 has known CVEs and is
  blocked by Defender.
- **L5. UI labels outrun verified behaviour.** "Medium (~3,425 RPM)" and
  "Max (~7,034 RPM)" (`:210-211`) are from one unit. Fix: show measured RPM, not
  hard-coded values.
- **L6. `Fail()` can touch disposed controls.** `Fail()` and status updates after
  `await` can run after the form is disposed (close during a command), throwing into
  `ThreadException`. Fix: guard with `IsDisposed`, or cancel through a
  `CancellationToken` on close.

---

## Recommended fix list (priority order)

1. **Serialized fan executor (C2, M1).** One queue/semaphore for all fan and driver
   work. Disable the fan group while busy. Restore pre-empts queued work. No RPM
   polling through the driver in Dell Auto mode.
2. **Trustworthy success detection (C1, M8).** Parse stdout/stderr now. Then move to
   in-process `DellFanLib` with correct 32-bit sentinels and a single driver session,
   plus a post-restore RPM plausibility check. Report "not confirmed", never
   "restored", without verification.
3. **Pessimistic state and marker (H1, H2).** Set `ManualActive` and the ProgramData
   marker *before* any EC disable or test. Clear them only after verified restore.
   Restore at startup if the marker exists.
4. **Fan-test rewrite (H3).** No level 0. ACE-controlled steps with a hard cap. Max
   as the fail-safe on restore failure. Drop "Safe" wording until verified.
5. **Guard process (H2).** `--guard <pid>` with a heartbeat lease restores on GUI
   death or hang. Add a boot-time scheduled task for the power-loss case.
6. **Elevation hygiene (H4, L1).** Absolute System32 paths. Refuse to run from a
   non-admin-writable folder. Pin the install directory to Program Files. Narrow
   `AssemblyResolve`. Single-elevation attempt.
7. **Power and session events (M2, M3).** Restore on suspend. Bounded restore with
   `ShutdownBlockReasonCreate` on session end. Marker-based boot recovery.
8. **Remove the persistent-manual option (M4).** At minimum fix the false "restored"
   message.
9. **Single-instance mutex (M6), then installer fixes (M7).** Refuse install while
   running. Verified restore before overwrite and before removal. Global-mutex check.
   Driver service cleanup. Timeouts.
10. **Tighter model gate (M5).** Exact model, SKU and BIOS allowlist, shared with the
    installer.
11. **Persistent audit log (M10).**
12. **Driver exposure (H5).** Verify the device ACL. Minimize load time. Escalate a
    secured-driver fork to the project owner if commercial.
13. **Turbo label and setting (M9), then the Low items.**

## What only a supervised hardware test can prove (Latitude 7400, project owner's explicit approval required)

Run each item with RPM and temperature logged before and after, and Dell Auto
restore verified at the end.

1. **EC timeout and keep-alive need.** After `ec-disable` plus a level, with no
   keep-alive, does the EC or BIOS take control back by itself, and after how long?
   Does the commanded level persist? This decides whether keep-alive or a guard is
   load-bearing.
2. **Effect of a failed or no-op restore.** Does `ec-enable` measurably return
   control? Look for the RPM falling at idle from Max, and rising under load from
   Medium. Confirm the plausibility check in fix 2 can tell the difference.
3. **Driver race (C2).** Run concurrent DellFanCmd processes (keep-alive plus
   `ec-enable`) and confirm whether `ec-enable` exits 0 without effect, and whether
   in-use IOCTLs fail after another process's `Shutdown()`.
4. **Power transitions with EC disabled.** EC state after S3 or Modern Standby,
   hibernate, fast-startup shutdown, warm reboot and cold boot. Does POST re-enable
   EC auto? This decides whether boot recovery (H2 #3) is needed.
5. **Fan test.** Real duration, fan-2 detection result on the single-fan 7400, and
   temperatures during any off/low phase under load.
6. **Turbo.** 99% versus 100% versus `PERFBOOSTMODE=0` effect on actual clocks, on
   AC and on battery (M9).
7. **Uninstall in manual mode,** and installer upgrade over a running instance (M7).
8. **Can be done on a Windows VM instead of the laptop:**
   - the device ACL of `\\.\BZHDELLSMMIO` (H5);
   - driver blocklist and HVCI behaviour;
   - install-directory ACL (H4);
   - the single-instance and installer guard checks.
