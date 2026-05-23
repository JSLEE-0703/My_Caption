Runtime Layout
==============

This directory contains bundled runtime dependencies used by release-oriented builds.

Current layout:

- `python\python.exe`
- `python\Scripts\mdict.exe`
- `argos-data\argos-translate\packages\translate-en_zh-1_9`
- optional `mdict\mdict.exe`

Notes:

- Translation defaults now prefer `runtime\python\python.exe` before falling back to the known development environment.
- `tools\argos_translate_stdin.py` loads Argos model data from `runtime\argos-data` first, then falls back to local development data in `tools\argos-data`.
- MDict lookup now prefers `runtime\mdict\mdict.exe` and the bundled Python runtime before falling back to manual overrides or the known development environment.
- The bundled Python runtime is copied from the project development environment and includes the Python packages needed by Argos Translate and mdict-utils.
- Keep third-party runtime license files alongside the bundled binaries when packaging a distributable build.
