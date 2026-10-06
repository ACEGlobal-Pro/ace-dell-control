# Security

ACE Dell Control is a technical preview that runs as administrator and loads a
kernel driver. Treat it accordingly.

## Reporting a vulnerability

Report privately through GitHub's private vulnerability reporting on this
repository, not in a public issue. Include the Windows build, the app version
and steps to reproduce. There is no fixed response time for a free project, but
reports are read. Fixes ship as a new release.

## Known residual risk (H5, driver exposure)

The bundled bzh Dell SMM driver exposes raw Dell SMM (BIOS) calls. While it is
loaded, which is during manual fan modes, keep-alive and the fan test, any
local process may be able to issue those calls if the driver's device access
rights are open. The app no longer loads the driver in Dell Auto mode and
removes a leftover driver service after a killed tool and at uninstall, but the
driver itself is a third-party binary that has not been changed or secured, and
whether its access rights are open has not yet been checked. A secured driver
fork has not been made. Details: `docs/audit/safety-review.md`, item H5.

On a machine with Memory Integrity (HVCI) the driver fails the signing
requirement and may be blocked on future Windows policy; do not disable
security features to work around that.

Other open review items are listed in `docs/STATUS.md`.
