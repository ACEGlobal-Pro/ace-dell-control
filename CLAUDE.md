# ACE Dell Control: contributor and agent guide

A Windows (x64) fan and thermal control utility for the Dell Latitude 7400.
Technical preview: not a released or cleared product. Licensed GPL-3.0-only.

## Hard safety rules

- Dell fan and thermal controls stay gated to physically validated models only.
- Never leave Dell automatic (EC) fan control disabled. Do not test fan control
  on hardware unless the maintainers have explicitly asked for that test.
- No BIOS or firmware writes, no UAC or SmartScreen bypass, no custom root certificate.
- `third-party/` holds upstream binaries and sources verbatim; never edit them.
- Building and testing are done locally (see `docs/BUILD.md`). GitHub Actions is
  used for release builds only (`.github/workflows/release.yml`).

## Where things are

- Project status and known limitations: `docs/STATUS.md`
- Layout and how it works: `docs/overview.md`
- Build and smoke test: `docs/BUILD.md`
- Safety review, licensing audit: `docs/audit/`
- Hardware and sandbox test results: `docs/test-runs/`
- Licensing: `docs/LICENSING.md`; code signing: `docs/CODE-SIGNING-POLICY.md`
- Security reports: `SECURITY.md`; privacy: `PRIVACY.md`
