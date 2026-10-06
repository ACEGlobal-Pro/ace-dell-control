# ACE Dell Control: licensing and provenance audit

Audit date: 2026-10-06 (all URLs accessed 2026-10-06). Not legal advice. "Fact" means checked directly in this audit; "Inference" means reasoning that needs qualified legal or Windows-side confirmation. Handoff claims were treated as unverified.

## Summary

1. Hashes: all 9 binaries in `third-party/tools/` are byte-identical to official upstream assets (GitHub releases for DellFanManagement 2.1.1 and LibreHardwareMonitor 0.9.6; NuGet packages for the four System.* DLLs). No mismatches.
2. Licences: each component's licence is confirmed from its upstream repo or package. The four licence texts shipped match the official or upstream texts.
3. **Source gap (blocking for distribution):** the manifest says LibreHardwareMonitorLib embeds PawnIO.Modules 0.1.6. That is wrong. All 12 embedded modules are byte-identical to the PawnIO.Modules **0.2.2** release, and only 2 of them equal 0.1.6. The shipped 0.1.6 source zip is not the corresponding source. Also missing: Costura.Fody (MIT, merged into DellFanCmd.exe) is not in the notices.
4. GPL: the ACE GUI starts the two GPL tools as separate processes and does not reference DellFanLib. It loads the MPL-2.0 LibreHardwareMonitorLib in-process by reflection. That pattern is generally treated as aggregation, so a GPL-3.0 licence for ACE's GUI is not forced by linking. This is inference. The installer still ships GPL binaries and owes full corresponding source.
5. SignPath Foundation: a priced GPL-3.0 utility is not excluded by the terms. The blockers are the "own binaries only" rule (the fan driver is a pre-built binary the ACE Global Pro maintainers does not maintain), the "verifiable reputation" test for a brand-new project, and the "circumvent security measures" clause, which a kernel EC fan-control tool may trip. Eligibility is SignPath's sole decision. Azure Artifact Signing does not cover India (organisations or individuals) per Microsoft's page today, so a paid OV certificate is the realistic paid path.

## 1. Component table

Hashes are SHA-256. "Official" means the file inside the upstream release asset or NuGet package downloaded during this audit.

| Component | Version | Licence confirmed | Hash match | Source complete | Notes |
|---|---|---|---|---|---|
| DellFanCmd.exe | 2.1.1 | GPL-3.0 (repo LICENSE, byte-identical to shipped `GPL-3.0.txt`) | MATCH `7b315043...9c38e` vs release asset `DellFanCmd-2.1.1.zip` | Yes for the tag. Zip file list equals the git tree of tag 2.1.1 (579c843c). Content not diffed per file | Costura.Fody 4.1.0 (MIT) merges DellFanLibInterop (and Costura) into the exe. No Costura MIT notice shipped. Unsigned (no Authenticode) |
| DellFanLib.dll | 2.1.1 | GPL-3.0 | MATCH `b769200b...7de9ef` (inside `DellFanCmd-2.1.1.zip`) | Yes (`DellFanLib/*.cpp`, `.h`, vcxproj in zip) | Native C++. The separate `DellFanLib-2.1.1.zip` asset holds a different build (`339c51a4...`) and is not what ships. Not referenced by the ACE GUI. Unsigned |
| DellSetThermalSetting.exe | 2.1.1 | GPL-3.0 | MATCH `8ca55cc4...f0b74` vs `DellSetThermalSetting-2.1.1.zip` | Yes | Unsigned |
| bzh_dell_smm_io_x64.sys | PE timestamp 2015-09-05 (no version) | GPL-3.0 (bzh repo LICENSE, added in commit 67786c69, 2016-05-28) | MATCH `c5244350...b2af` vs `DellFanCmd-2.1.1.zip` and `DellFanLib-2.1.1.zip` | Probably. Commit 67786c69 only adds LICENSE to the 2015 code (repo has 3 commits). **Not rebuilt**, so source-to-binary equivalence is unproven. The source tree includes a prebuilt `bzh_dell_smm_lowlevel.lib` plus `.asm` | Signed by "Aaron Kelley" (Sectigo RSA Code Signing CA, cert valid 2019-01-17 to 2020-01-17, Microsoft Code Verification Root cross-chain). Aaron Kelley re-signed another author's driver. Signature chain parsed with openssl only; trust, timestamp and revocation to verify on Windows. PDB path in binary: `D:\works\vs2015\bzh_dell_smm_io_[Final]\x64\Release\...` |
| LibreHardwareMonitorLib.dll | 0.9.6 | MPL-2.0 (repo `LICENSE`; NuGet nuspec `<license type="expression">MPL-2.0`) | MATCH `6ebc1943...66bf0` vs release asset `LibreHardwareMonitor.zip` and NuGet `runtimes/win-x64/lib/net472` | Yes for MPL (LHM v0.9.6 source zip, tag commit 3d331e33) | Unsigned. **NuGet declares dependencies not shipped:** DiskInfoToolkit, HidSharp, RAMSPDToolkit-NDD, System.Management, System.Threading.AccessControl. Only the 4 System.* DLLs are shipped. CPU-only use may work, but this is untested here (to verify on Windows) |
| PawnIO modules (embedded in LHMLib) | **0.2.2, not 0.1.6** | LGPL-2.1 (PawnIO.Modules `COPYING`, identical in tags 0.1.6 and 0.2.2 and to shipped `LGPL-2.1.txt`) | n/a. 12 of 12 embedded `.bin` files are identical to release 0.2.2. Only `LpcACPIEC.bin` and `LpcIO.bin` equal 0.1.6. `IsaBridgeEC.bin` does not exist in 0.1.6 | **No.** Shipped zip is tag 0.1.6. Source for 0.2.2 (tag object 95a194e6 → commit e12a858d) is needed. The 0.1.6 zip also lacks `IsaBridgeEC.p` | `.bin` modules are binary-only in the LHM source tree. LGPL "source" means the `.p` Pawn sources plus the compiler, which PawnIO.Modules vendors under `_pawn/` |
| System.Memory.dll | 4.6.3 | MIT (NuGet nuspec) | MATCH `d5e8e486...e913d` vs NuGet `lib/net462` (also equals LHM release copy) | n/a (permissive) | Microsoft-signed (CN=.NET, Microsoft Code Signing PCA 2011), parsed only |
| System.Buffers.dll | 4.6.1 | MIT | MATCH `2d78d770...12cac3` vs NuGet `lib/net462` | n/a | Microsoft-signed |
| System.Numerics.Vectors.dll | 4.6.1 | MIT | MATCH `20c2fa81...f4c9` vs NuGet `lib/net462` | n/a | Microsoft-signed |
| System.Runtime.CompilerServices.Unsafe.dll | 6.1.2 | MIT | MATCH `08cbd727...cdacf` vs NuGet `lib/net462` | n/a | Microsoft-signed |

Full hashes of shipped files (all matched):

```
8ca55cc4f91d9263741b5fb70469ea526f17af780261dddc36fda422136b0f74  DellSetThermalSetting/DellSetThermalSetting.exe
7b315043cff2b0080a64c3d424f7f676a3e3dda548ae8e7ea883ec5e2ab9c38e  DellFanCmd/DellFanCmd.exe
b769200b290609e44a530e0331fe338a0ea19de85bb87b58b522b5d2be7de9ef  DellFanCmd/DellFanLib.dll
c524435027b6c252464dab58e78cb01f7a09c820dad870677aa15b62b974b2af  DellFanCmd/bzh_dell_smm_io_x64.sys
6ebc194316536ba61af5be24508ad9fcbb2ecc685e716c12e787c79530f66bf0  LibreHardwareMonitor/LibreHardwareMonitorLib.dll
2d78d770c9cb997199154ae8c018b9f1d1efbc86729f7264dde6dbad2a12cac3  LibreHardwareMonitor/System.Buffers.dll
08cbd7278b66f1e68425a82d4b97181a4130d93e3dd91831407aba7212ccdacf  LibreHardwareMonitor/System.Runtime.CompilerServices.Unsafe.dll
20c2fa81b8c70d651099d762954f285fd4f942e63b2d7217c145dab8d4b2f4c9  LibreHardwareMonitor/System.Numerics.Vectors.dll
d5e8e4866f9cfa66f7765660f84b210198893e55335487afe5ebda342c0e913d  LibreHardwareMonitor/System.Memory.dll
```

Method: assets downloaded with `gh release download` and the NuGet flat-container API into the session scratchpad. GitHub exposes no hash for the DellFanManagement 2.1.1 assets, so those comparisons are my own SHA-256 of the downloaded official zips. LHM 0.9.6 release zip digest `086d9f1b...c001` and PawnIO 0.1.6 zip `c98af662...b248d` equal the GitHub-published digests.

Signatures: `osslsigncode` is not installed on this Mac (checked, not installed). I extracted the Authenticode PKCS#7 blob from each PE with a script and listed certificates with `openssl pkcs7`. This reads the certificate chain only; it does not validate trust, timestamps, or revocation. **Signature validity: to verify on Windows** (`Get-AuthenticodeSignature` / `signtool verify /pa /v`). Results: `DellFanCmd.exe`, `DellFanLib.dll`, `DellSetThermalSetting.exe`, `LibreHardwareMonitorLib.dll` carry no Authenticode signature. The driver and the four System.* DLLs do.

### Licence texts (`third-party/licenses/`)

| File | Result |
|---|---|
| `GPL-3.0.txt` | Byte-identical to DellFanManagement's own LICENSE (`53927bd0...`). Versus GitHub's SPDX copy it differs only in `http` vs `https` for fsf.org and `{...}` vs `<...>` placeholders, which is the gnu.org text form. Official gnu.org was unreachable from here, so I did not compare against it directly |
| `LGPL-2.1.txt` | Byte-identical to PawnIO.Modules `COPYING` (`dc626520...`). Differs from GitHub's SPDX copy only in line wrapping |
| `MPL-2.0.txt` | Identical (whitespace-normalised) to `mozilla.org/media/MPL/2.0/index.txt`. Matches LHM `LICENSE` |
| `MIT.txt` | Byte-identical to dotnet/runtime `LICENSE.TXT` (`cfc21f5e...`). NuGet packages declare plain `MIT`; this .NET Foundation wording is acceptable |

Missing notices: Costura.Fody 4.1.0 (MIT, inside DellFanCmd.exe); copyright holder lines for each component (the notices file names projects but not the copyright statements MIT, MPL and LGPL expect); the unshipped LHM NuGet dependencies if ever added (HidSharp is Apache-2.0, to verify).

Primary sources: https://github.com/AaronKelley/DellFanManagement/releases/tag/2.1.1 , https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/releases/tag/v0.9.6 , https://www.nuget.org/packages/LibreHardwareMonitorLib/0.9.6 , https://github.com/namazso/PawnIO.Modules/releases , https://github.com/424778940z/bzh-windrv-dell-smm-io , https://www.nuget.org/packages/System.Memory/4.6.3 , https://www.nuget.org/packages/System.Buffers/4.6.1 , https://www.nuget.org/packages/System.Numerics.Vectors/4.6.1 , https://www.nuget.org/packages/System.Runtime.CompilerServices.Unsafe/6.1.2

## 2. Source completeness

Facts:
- `DellFanManagement-2.1.1-source.zip` has the same 54-file list as git tag 2.1.1 and contains `DellFanCmd/`, `DellFanLib/`, `DellFanLibInterop/`, `DellSmmIoLib/`, `DellSetThermalSetting/`, `DellFanKeepAlive/` and the `.sln`. All three shipped Dell tools are covered. The Fody/Costura NuGet packages are restored at build time and are not in the zip. The zip also holds a prebuilt `OpenHardwareMonitorLib.dll` (used only by DellFanKeepAlive, not shipped).
- The driver source commit `67786c69a80f2dd3e0d948cfaba6ced6c2c090e3` is the head of the repo (2016-05-28). Its only change is adding LICENSE; the code is from the 2015-09-05 commits, matching the binary's 2015-09-05 PE timestamp. The installed driver is the prebuilt, re-signed binary from DellFanCmd 2.1.1. No bit-identical rebuild was attempted, so "corresponding source" is a reasonable inference, not verified.
- LHM `LibreHardwareMonitor-v0.9.6-source.zip` is the 0.9.6 tree (474 files, includes `LICENSE` and `Resources/PawnIo/*.bin`).
- PawnIO: LHM 0.9.6's DLL embeds 12 modules: AMDFamily0F/10/17, IntelMSR, IsaBridgeEC, LpcACPIEC, LpcCrOSEC, LpcIO, RyzenSMU, SmbusI801, SmbusNCT6793, SmbusPIIX4. Each is byte-identical to the same-named file in the PawnIO.Modules **0.2.2** release (tested against all 12 releases 0.2.0 to 0.2.11; 0.2.2 is the only full match). 0.1.6 does not ship `IsaBridgeEC`. Implication: the `PawnIO.Modules-0.1.6-source.zip` does not correspond to what ships. Replace with the 0.2.2 tag (annotated tag object `95a194e64ef7f044c8d441d857daabe21e44952f`, pointing at commit `e12a858d952461ee2e919897cacbff7f905fe370`), and fix `THIRD-PARTY-NOTICES.txt`, `DEPENDENCY-MANIFEST.txt`, `README-INSTALLER.txt`/`REVIEW-NOTICE.txt` and the NSIS `File` lines.
- PawnIO driver (the kernel driver LHM talks to) is not shipped. LHM looks up an installed "PawnIO" via the registry and `\\?\GLOBALROOT\Device\PawnIO`. Without it, CPU sensors from LHM likely read N/A (fact from LHM source; actual behaviour on a Latitude 7400 to verify on Windows). If ACE ever bundles or installs PawnIO, its licence and signing need their own audit.

Inference: LHM's `.bin` modules are LGPL-2.1 compiled bytecode inside an MPL-2.0 DLL. An LGPL "relink or replace" right is satisfied in spirit because the modules are separable resources and the Pawn sources and compiler are public, but this needs legal confirmation.

## 3. GPL-3.0 implications for the ACE GUI

Facts from `src/ACE.Dell.Control.cs`:
- GPL tools are run as separate processes: `Paths.Fan` (DellFanCmd.exe) and `Paths.Thermal` (DellSetThermalSetting.exe) via `ProcessStartInfo`/`Process`, passing command-line arguments and reading stdout/exit code (lines ~89, 333, 352-402, 540-545).
- The GUI does not reference `DellFanLib.dll` or `DellFanLibInterop` anywhere (grep found nothing).
- `LibreHardwareMonitorLib.dll` (MPL-2.0) is loaded in the same process: `Assembly.LoadFrom` plus reflection (`Activator.CreateInstance`) on `LibreHardwareMonitor.Hardware.Computer` (lines ~708-721), with an `AssemblyResolve` hook for the System.* DLLs.
- The NSIS installer copies the GUI, GPL tools, driver, MPL library and source zips into one installer.

Inference (not legal advice):
- Running separate GPL programs through command-line and process boundaries is commonly treated as "mere aggregation" (see the GNU GPL FAQ on pipes, sockets and command-line arguments). On that reading, ACE's GUI need not itself be GPL, and the GUI licence is an owner choice. The FSF's line depends on how intimate the communication is; this GUI is a thin wrapper that passes arguments and parses text, which sits at the less-risky end, but it is a wrapper whose entire function is the GPL tool's function. Legal review recommended before a proprietary licence.
- MPL-2.0 is file-level copyleft. Loading LHM dynamically does not impose MPL on ACE's own source. MPL files must stay available in source form (satisfied by the source zip) and their notices kept. MPL-2.0 section 3.3 allows combination with GPL-3.0 code via the secondary-licence provision, but there is no such combination here.
- Shipping GPL binaries (DellFanCmd, DellFanLib, DellSetThermalSetting, driver) in a priced installer is allowed. ACE must provide the licence text, notices, and the complete corresponding source (GPL-3.0 section 6: with the binary, or a written offer valid for three years). Recipients may redistribute.
- If ACE picks GPL-3.0 for its GUI, the GUI source must be published to recipients and SignPath's "no commercial dual-licensing" test stays satisfiable. If ACE keeps the GUI proprietary, SignPath's free programme is unavailable (see section 5) and any paid-licence/EULA text must not restrict GPL rights in the bundled components.
- The "GPL-3.0 only" vs "or later" status of DellFanManagement and the bzh driver was not independently determined here; LICENSE files are plain GPL-3.0 and the driver repo has no per-file headers (grep of the source found none).
- The existing installer text "do not redistribute this preview as a commercial product" cannot override GPL recipients' rights for the GPL components. Reword.

## 4. Obligations checklist (before any distribution)

1. Replace PawnIO source with the 0.2.2 tag and correct every document that says 0.1.6.
2. Keep all four licence texts; add Costura.Fody MIT notice, and copyright holder lines for all components.
3. Ship or offer complete corresponding source for GPL components (done for Dell tools and driver; driver binary not reproducibly rebuilt) and for LGPL modules (0.2.2).
4. Keep MPL-2.0 notices and make LHM 0.9.6 source available (done by the zip).
5. Add a written source offer (3-year form, or ship source with every download). Zips in the installer already do the latter.
6. Decide the GUI licence (owner decision); document the aggregate/process-boundary design in the architecture notes.
7. Resolve LHM's missing NuGet dependencies (functional test on Windows) and the PawnIO driver prerequisite; add their licences if ever shipped.
8. Trademark and branding (Dell, Latitude) checked separately; not covered here.
9. Obtain qualified legal review before asserting commercial clearance.

## 5. Code signing

### SignPath Foundation (https://signpath.org/terms.html, https://signpath.org/apply.html, accessed 2026-10-06)

The terms page is quoted below in short phrases. The apply page contains only a heading and an embedded form that did not render in a plain fetch, so application questions are unverified.

| Topic | What the terms say | Effect on ACE Dell Control |
|---|---|---|
| Licence | "OSI-approved Open Source license without commercial dual-licensing for all components"; no "proprietary, non open-source component" | GPL-3.0 GUI would qualify. A proprietary GUI would not. Selling copies of a GPL-3.0 program is not excluded in the terms. No page text mentions price either way |
| Maintained, released, documented | "actively maintained"; "already released in the form that should be signed"; functionality "described on its download page" | A public released project with a download page is needed first |
| New project | For executables, "we require a certain verifiable reputation" (not for libraries). "we cannot sign binaries based on source code that nobody knows" | **Main risk for a brand-new project.** No stated numeric threshold |
| Sign own binaries only | Team "must only sign software artifacts built from their own source code" and be maintainers of all source and build scripts. Unsigned upstream OSS binaries "e.g. DLL files" may be included in signed packages; SignPath reserves the right to require only signed files in future | ACE may sign `ACE Dell Control.exe` and the installer. The bundled DellFanCmd, DellSetThermalSetting, LHM and the driver may ride along unsigned. Modified forks need a visible fork, upstream signed builds and code review. The driver is a third-party signed binary, which is fine to include as is, but SignPath may treat ACE's reliance on it as a risk |
| Hacking tools | "must not include features designed to identify or exploit security vulnerabilities or circumvent security measures of their execution environment" | Fan control is not a vulnerability tool, but the stack uses a kernel SMM/EC I/O driver and a 2015 cross-signed driver. SignPath may view this as security-sensitive. No explicit "system tools" restriction was found; it is the reviewer's call |
| User safety | "must not include features that compromise the privacy or security of users and their systems"; "must not modify the user's system configuration without proper warnings"; uninstall required | Needs clear warnings (EC fan override, power plan edits). Uninstall exists. EC restore on uninstall is a plus |
| Team and policy | MFA for SignPath and repo; Authors/Reviewers/Approvers roles; "Code signing policy" on home page; privacy policy or the no-transfer sentence; every release manually approved; metadata (product name/version) enforced | Needs a public repo and CI that builds from source (the current build is a local compile) |
| Publisher | Certificate is issued to "SignPath Foundation", which is shown as publisher | ACE does not appear as publisher. Windows shows SignPath Foundation |
| Decision | "it can only be our decision to accept or reject a project"; no obligation or arbitration | Approval cannot be assumed |

Verdict (inference): technically possible only if the GUI is GPL-3.0 with a public repo and a reproducible CI build, and only if SignPath accepts a new project plus a kernel-driver fan-control tool. Likelihood is uncertain. Ask SignPath directly before committing to this route.

### Paid alternatives

1. **Azure Artifact Signing** (formerly Trusted Signing).
   - Price: Basic $9.99 per account per month (5,000 signatures), Premium $99.99 per month (100,000), $0.005 per extra signature. Source: https://learn.microsoft.com/en-us/azure/artifact-signing/how-to-change-sku (the azure.microsoft.com pricing page rendered "$-" for the figures; Microsoft's pricing page also notes prices are estimates).
   - Eligibility (https://learn.microsoft.com/en-us/azure/artifact-signing/quickstart , accessed 2026-10-06): Public Trust certificates "are available to organizations in the United States, Canada, the European Union, the United Kingdom, Australia, New Zealand, Japan, South Korea, Singapore, Switzerland, Norway, and Israel. Individual developers must be located in the United States or Canada." **India is not listed**, so neither an Indian company nor an Indian individual qualifies today. It requires a paid Azure subscription, and does not issue EV or kernel-driver certificates (kernel signing is a separate Partner Center path). A non-Indian legal entity (for example in Singapore) would be needed, which is a separate business decision.
2. **OV code-signing certificate** from a commercial CA. DigiCert's own page lists OV at $44/month per certificate ($528/year, 12-month subscription), with keys on a hardware token, HSM or KeyLocker cloud (https://www.digicert.com/signing/code-signing-certificates , accessed 2026-10-06). Reseller listings show Sectigo OV from about $219 to $225 per year and DigiCert from about $400 to $550 (https://www.ssldragon.com/ssl-certificates/code-signing/ , https://www.ssl2buy.com/sectigo-code-signing-certificate.php; reseller prices, indicative only). CA/B Forum rules require hardware-protected keys. OV does not give instant SmartScreen reputation. Whether a CA validates an Indian individual or sole proprietor vs a registered company was not confirmed from an official page; ask the CA.

None of these removes the UAC prompt or the SmartScreen warm-up, and none replaces kernel-driver signing.

## 6. Other findings

- The bundled driver is cross-signed with an expired 2019 to 2020 leaf certificate and a 2015 build. Whether Windows 10/11 with Secure Boot, HVCI / memory integrity or the Microsoft vulnerable-driver blocklist will load it was **not verified** (to verify on Windows). Handoff claims it loads on a Windows 11 VM.
- The installer's `README-INSTALLER.txt` and `REVIEW-NOTICE.txt` repeat the "0.1.6" claim and "match their official releases" wording; update after source fixes.
- The preview bundle's claim that the four DLLs match the "net462 NuGet binaries" is true. They also match the files in the LHM v0.9.6 release zip.

## 7. Open questions for the owner and reviewers

1. GUI licence: GPL-3.0 (opens SignPath, requires publishing GUI source) or proprietary (closes SignPath, needs legal review of the wrapper design)?
2. Is a Singapore/US/EU entity or a paid OV certificate acceptable for signing, given Artifact Signing excludes India today?
3. Will ACE ship the PawnIO driver for CPU temperature, or accept N/A when absent?
4. Can the GUI be built reproducibly in public CI (needed for SignPath and for any signing)?
5. Will the driver load on supported Windows 10/11 configurations with Secure Boot and HVCI, and is it on the Microsoft blocklist? (Windows test needed.)
6. Is DellFanManagement "GPL-3.0 only" or "or later"? Ask the author or check file headers.
7. Does LHM 0.9.6 work with only the four System.* DLLs and without HidSharp/RAMSPDToolkit/DiskInfoToolkit on a clean machine? (Windows test needed.)
8. Should the driver be rebuilt from source (needs WDK/VS2015-era toolchain and a signing path) to prove correspondence, or kept as the upstream-signed binary?

## 8. Fixes applied 2026-10-06

1. **PawnIO.Modules source updated to 0.2.2**. Replaced `third-party/upstream-source/PawnIO.Modules-0.1.6-source.zip` with the official 0.2.2 release (`PawnIO.Modules-0.2.2-source.zip`, GitHub tag 0.2.2, commit e12a858d952461ee2e919897cacbff7f905fe370, SHA-256 `b9e05e52c07fd76b7f9db3da0f542b710d55511dc995d4f4ca08e9179b4c4032`). The 0.1.6 source did not correspond to what ships (missing `IsaBridgeEC.bin`); all 12 embedded modules in LibreHardwareMonitorLib 0.9.6 are byte-identical to the 0.2.2 release.

2. **Costura.Fody notice added**. Added MIT license notice for Costura.Fody 4.1.0 (merged into DellFanCmd.exe) to `third-party/THIRD-PARTY-NOTICES.txt` and dependency manifest.

3. **Manifests updated**. Updated `third-party/DEPENDENCY-MANIFEST.txt` and `third-party/THIRD-PARTY-NOTICES.txt` to reference PawnIO.Modules 0.2.2 with upstream URL and commit hash.
