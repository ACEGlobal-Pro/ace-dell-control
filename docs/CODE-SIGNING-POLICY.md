# Code signing policy

Intended route: free code signing provided by [SignPath.io](https://signpath.io),
certificate by [SignPath Foundation](https://signpath.org) (not yet accepted).

Status (October 2026): **releases are unsigned.** The project applied to the
SignPath Foundation open-source programme; the Foundation declined for now
because the project does not yet have enough public visibility, and invited a
new application later. Until a release is signed, Windows shows an "Unknown
publisher" warning and Windows Smart App Control blocks the app. The rest of
this policy describes how signing will work if the project is accepted.

## Roles

- **Committers and reviewers:** ACE Global Pro maintainers.
- **Approvers:** ACE Global Pro maintainers. Every signing request is approved
  manually by a maintainer in SignPath before anything is signed.

## What is signed

`ACE Dell Control.exe` and the installer, built from this repository's own
source by the release workflow (`.github/workflows/release.yml`) from a tagged
commit. Upstream binaries (DellFanCmd, DellSetThermalSetting,
LibreHardwareMonitor, the fan driver) are third-party components shipped as
received, unmodified and matched by hash to the upstream release. They are not
re-signed by this project.

## Build and verification

Release builds run on a GitHub-hosted Windows runner using `scripts/build.ps1`
and are submitted to SignPath for signing. All other building and testing is
done locally (`docs/BUILD.md`). Each release publishes SHA-256 hashes
(`SHA256SUMS.txt`). A release is approved only after the review items listed in
`docs/STATUS.md` that gate a release are closed.

## Privacy

This program will not transfer any information to other networked systems
unless specifically requested by the user or the person installing or operating
it. See `PRIVACY.md`.
