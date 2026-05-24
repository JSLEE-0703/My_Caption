# Project State

Last updated: 2026-05-24

## Project Summary

`My Caption` is a personal Windows subtitle companion built around Windows Live Captions. It captures Live Captions text through UI Automation, stabilizes fast-changing partial captions, optionally translates captions, and displays them in a lightweight WPF overlay with word lookup.

## Current Goal

Prepare the project for release packaging while keeping the repository state accurate for future handoff. The immediate focus is removing installer-readiness risks, documenting the bundled offline runtime, and keeping validation status explicit.

## Current Repository Status

- Branch at update time: `release`
- Git status at update time:
  - `AGENTS.md` has pre-existing uncommitted changes.
  - `src/Infrastructure/Persistence/SettingsStore.cs`, `README.md`, `docs/notes/next-steps.md`, and `PROJECT_STATE.md` were updated for AppData settings persistence and release validation status.
- Changes in this update:
  - `SettingsStore.cs`: stores settings in `%AppData%\My Caption\settings.json` and migrates an existing application-directory `settings.json` on first load.
  - `README.md`: documents the AppData settings path, legacy migration behavior, Release output expectations, and bundled offline smoke checks.
  - `docs/notes/next-steps.md`: removed the stale `StubTranslationProvider` follow-up and replaced it with current release-readiness work.
  - `PROJECT_STATE.md`: refreshed as the persistent handoff document.
- Recent commits before this update:
  - `df6b620` Merge PR #2: Document runtime LFS and release state
  - `0cf87f5` Document runtime LFS and release state
  - `2c44c27` Merge PR #1: Bundle offline translator runtime assets
  - `45e27ee` Track large runtime assets with Git LFS
  - `7effa3d` Move repository state to handoff document
- Remote context: PR `https://github.com/JSLEE-0703/My_Caption/pull/1` was merged into `master` earlier on 2026-05-24 with merge commit `2c44c274d4644f6d3989de517fd98757b50d7256`.

## How to Run

- Verified:
  - Build Release - run on 2026-05-24 and completed successfully with 0 warnings and 0 errors:

```powershell
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe' 'D:\My_Caption\MyCaption.csproj' /t:Build /p:Configuration=Release /p:Platform=x64
```

  - Release output directory check - run on 2026-05-24; confirmed `bin\Release\MyCaption.exe`, `bin\Release\runtime`, `bin\Release\dictionary`, and `bin\Release\tools` exist.
  - Argos bridge smoke test - run on 2026-05-24 and translated `hello` to Chinese text:

```powershell
"hello" | .\bin\Release\runtime\python\python.exe .\bin\Release\tools\argos_translate_stdin.py --from en --to zh
```

  - MDict bridge smoke test - run on 2026-05-24 and returned a dictionary result for `hello`:

```powershell
'{"text":"hello"}' | .\bin\Release\runtime\python\python.exe .\bin\Release\tools\mdict_query_stdin.py .\bin\Release\dictionary\default.mdx
```

- Not verified during this update:
  - Launching the WPF app to exercise AppData settings creation and legacy migration.

- Expected Release output:
  - `D:\My_Caption\bin\Release\MyCaption.exe`

## How to Test or Validate

- Verified during this update:
  - `Release|x64` MSBuild completed successfully after the AppData settings persistence change.
  - Release output check confirmed `runtime`, `dictionary`, and `tools` are present under `bin\Release`.
  - Argos bridge smoke test translated `hello` successfully.
  - MDict bridge smoke test returned a dictionary result for `hello`.
  - `git lfs ls-files` - run on 2026-05-24; confirmed exactly three LFS-tracked runtime files.
  - Read-only repository inspection commands - run on 2026-05-24; confirmed current branch, git status, recent commits, top-level structure, and relevant README notes.
- Not run during this update:
  - Launching the WPF app to validate live UI behavior and AppData settings migration.

- Recently verified before this update in the same working session:
  - `Release|x64` MSBuild completed with 0 warnings and 0 errors after the `.NET Framework 4.8` targeting pack was available.
  - Argos bridge translated `hello` successfully.
  - MDict bridge returned a dictionary result for `hello`.

## Key Files and Directories

- `MyCaption.csproj`: classic MSBuild project targeting `.NET Framework 4.8`; includes `dictionary`, `runtime`, and selected `tools` scripts as content copied to output.
- `App.xaml.cs`: composition root.
- `src/App/AppRuntime.cs`: main runtime coordinator for capture, settings, translation, lookup, and UI state.
- `src/Core/Models/AppSettings.cs`: persisted settings model and bundled default paths.
- `src/Core/Translation/`: translation abstractions, provider host, factory, dispatcher, and provider implementations.
- `src/Core/Lookup/`: lookup abstractions, JSON lookup, MDict lookup, parsing, and provider factory.
- `src/Infrastructure/Persistence/SettingsStore.cs`: stores `settings.json` in `%AppData%\My Caption\settings.json` and migrates an existing application-directory settings file.
- `src/UI/MainWindow/`: control panel and provider settings UI.
- `src/UI/Overlay/`: subtitle overlay and dictionary popup UI.
- `dictionary/default.mdx`: bundled default offline MDict dictionary.
- `runtime/python/`: bundled Python runtime used by Argos translation and MDict flows.
- `runtime/argos-data/`: bundled Argos Translate offline model data.
- `tools/argos_translate_stdin.py`: Argos translation bridge script.
- `tools/mdict_query_stdin.py`: persistent MDict query bridge script.
- `.gitattributes`: tracks exactly three oversized runtime files through Git LFS.

## Recent Changes

- Moved settings persistence out of the application base directory into `%AppData%\My Caption\settings.json`.
- Added first-load migration from legacy application-directory `settings.json` to the AppData settings path.
- Re-ran `Release|x64` build and verified the release output directories plus Argos and MDict smoke tests.
- Removed stale `StubTranslationProvider` next-step language from `docs/notes/next-steps.md`.
- Bundled Python runtime and Argos offline data for release-oriented translation.
- Added Git LFS tracking for three oversized runtime files:
  - `runtime/python/Lib/site-packages/torch/lib/torch_cpu.dll`
  - `runtime/argos-data/argos-translate/packages/translate-en_zh-1_9/model/model.bin`
  - `runtime/python/Lib/site-packages/ctranslate2/ctranslate2.dll`
- Documented that the rest of `runtime` remains normal Git content.
- Added and refreshed `PROJECT_STATE.md` as the repository handoff document.
- Updated README before this skill run with Git LFS setup notes, Release build notes, and Argos pointer-file troubleshooting.

## Current Decisions and Assumptions

- UI stack remains WPF on `.NET Framework 4.8`.
- Build style remains classic MSBuild, not SDK-style.
- Fresh settings should prefer bundled offline runtime assets rather than requiring system Python.
- Translation defaults are release-oriented toward `ExternalCli` with Argos.
- Dictionary defaults are release-oriented toward `MdictCli` with `dictionary/default.mdx`.
- User settings are now release-oriented toward a user-writable AppData path instead of the application base directory.
- Only the three oversized runtime files listed above are in Git LFS; this is intentional and narrow.
- Most of `runtime` is still tracked as normal Git content.
- The current machine has the `.NET Framework 4.8` targeting pack configured.

## Known Issues / Risks

- AppData settings persistence was implemented on 2026-05-24 but has not yet been validated by launching the WPF app or exercising a real legacy settings migration.
- A fresh clone needs Git LFS installed and hydrated; otherwise the three LFS files may be pointer files and Argos can fail, including with `WinError 193` around `ctranslate2.dll`.
- The repository is large because most bundled runtime files are still normal Git content.
- Runtime asset strategy is not final: keep mostly in Git, move more files to LFS, or move runtime assets to release artifacts.
- No automated test suite was found during this update.
- Installer project files were not found during earlier inspection; packaging work still needs a chosen installer system.

## Next Steps

- [ ] Decide the long-term runtime asset strategy: mostly normal Git, more Git LFS, or release artifact workflow.
- [ ] Validate AppData settings persistence and legacy `settings.json` migration by launching the WPF app.
- [ ] Add installer metadata such as app icon, version information, product name, and third-party notices.
- [ ] Create an installer script or project, likely Inno Setup or another Windows installer workflow.
- [ ] Validate on a clean Windows machine without system Python or development-only paths.
- [ ] Improve dictionary morphology fallback and richer dictionary entry handling.
- [ ] Improve settings UX for language direction and provider-specific configuration.

## Notes for Future Codex Sessions

- Read `AGENTS.md` first. Reply to the user in Chinese; write code, comments, commit messages, and docs in English unless asked otherwise.
- Inspect relevant files before changing anything and state which files will change before editing.
- Do not install, remove, or upgrade dependencies without approval.
- Do not change environment, configs, paths, or tooling without approval.
- Preserve existing style and avoid broad refactors.
- For runtime/LFS issues, inspect `.gitattributes` and run `git lfs ls-files` before changing tracking rules.
- If Argos fails after clone, check whether `ctranslate2.dll`, `model.bin`, and `torch_cpu.dll` are real binaries rather than LFS pointer files.
- If working on installer readiness, validate the AppData settings path and legacy migration behavior in `SettingsStore.cs`.
