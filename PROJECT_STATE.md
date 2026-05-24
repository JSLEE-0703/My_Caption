# PROJECT_STATE

## Repository Summary

- Project name: `My Caption`
- Type: personal Windows subtitle companion built around `Windows Live Captions`
- UI stack: WPF on `.NET Framework 4.8`
- Build style: classic MSBuild project, not SDK-style
- Main entry: `App.xaml.cs`
- Main coordinator: `src/App/AppRuntime.cs`

## Main Product Behavior

- Captures subtitle text from the Windows Live Captions window through UI Automation
- Stabilizes rapidly changing partial captions before display
- Shows subtitles in a lightweight overlay window
- Supports optional translation through swappable translation providers
- Supports word lookup through swappable dictionary providers
- Supports temporary interaction through holding `Alt`

## Current Runtime Shape

- `src/Core/Capture`: Live Captions connection and polling
- `src/Core/Stabilization`: subtitle stabilization and commit behavior
- `src/Core/Translation`: provider abstraction, host, dispatcher, and concrete translation providers
- `src/Core/Lookup`: provider abstraction, host, JSON lookup, and MDict lookup
- `src/Core/Models`: settings models, view models, and caption-related models
- `src/Infrastructure/Automation`: Windows Live Captions UI Automation access
- `src/Infrastructure/Persistence`: settings storage in `settings.json`
- `src/Infrastructure/Windows`: Win32 helpers and keyboard monitoring
- `src/UI/MainWindow`: control panel and settings UI
- `src/UI/Overlay`: subtitle overlay and dictionary popup UI

## Important Current Defaults

- The app starts capture automatically on startup
- Translation is release-oriented toward `ExternalCli` with Argos
- Dictionary is release-oriented toward `MdictCli`
- Bundled default dictionary path: `dictionary/default.mdx`
- Bundled Python runtime path: `runtime/python/python.exe`
- Bundled Argos offline data path: `runtime/argos-data`
- Bundled MDict CLI path: `runtime/python/Scripts/mdict.exe`
- Optional standalone MDict runtime path: `runtime/mdict/mdict.exe`

## Important Current Behavior Notes

- `AppRuntime` is the main place to inspect when behavior spans UI, settings, capture, translation, and lookup
- Lookup and translation both use provider host layers instead of binding directly to concrete implementations
- Fresh settings should run translation and MDict lookup through bundled runtime assets without requiring system Python
- MDict lookup supports persistent query mode and startup warmup to reduce first-query latency
- Windows Live Captions can be auto-launched and optionally auto-hidden after connection
- This machine can run existing builds, but clean MSBuild rebuilds may be blocked if the `.NET Framework 4.8` targeting pack is missing
