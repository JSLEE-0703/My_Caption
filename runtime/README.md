Runtime Layout
==============

This directory is reserved for bundled runtime dependencies used by release-oriented builds.

Expected layout:

- `python\python.exe`
- `python\Scripts\mdict.exe`
- `mdict\mdict.exe`

Notes:

- Translation defaults now prefer `runtime\python\python.exe` before falling back to the known development environment.
- MDict lookup now prefers `runtime\mdict\mdict.exe` and `runtime\python\Scripts\mdict.exe` before falling back to manual overrides or the known development environment.
- Keep third-party runtime license files alongside the bundled binaries when packaging a distributable build.
