# My Caption

My Caption is a personal Windows subtitle companion built around Windows Live Captions. The application captures caption text from the Live Captions window, stabilizes fast-changing output, optionally translates the stabilized text, and renders the result in a lightweight overlay that supports word lookup.

This README is the primary technical document for developers who need to understand, maintain, or extend the project.

## Overview

Current product goals:

- Read caption text from Windows Live Captions through UI Automation
- Reduce flicker caused by rapidly changing partial captions
- Display a low-distraction overlay window over other apps
- Support optional translation through swappable translation providers
- Support word lookup through swappable dictionary providers
- Allow temporary interaction by holding `Alt`

Current implementation shape:

- Desktop UI: WPF on `.NET Framework 4.8`
- Build style: classic MSBuild project, not SDK-style
- Main runtime model: a single coordinating `AppRuntime`
- Primary extension points: translation providers and lookup providers

Current release-oriented asset layout:

- `dictionary\default.mdx`: bundled default offline MDict dictionary
- `dictionary\ATTRIBUTION.txt`: source and license note for the bundled dictionary
- `runtime\python\`: bundled Python runtime used by the translation and MDict flows
- `runtime\python\Scripts\mdict.exe`: bundled MDict CLI entry point from `mdict-utils`
- `runtime\argos-data\`: bundled Argos Translate offline data, including the default `en -> zh` model
- `runtime\mdict\`: optional bundled standalone mdict executable location
- `tools\argos_translate_stdin.py`: bundled Argos translation bridge script
- `tools\mdict_query_stdin.py`: persistent MDict query bridge script

Current repository note:

- The repository already carries the default MDX dictionary and bridge scripts.
- The repository now carries a bundled Python runtime and Argos offline data so a fresh settings file can use translation and MDict lookup without a separately installed Python environment.
- A standalone `runtime\mdict\mdict.exe` remains optional because the default MDict path works through bundled Python and `mdict-utils`.
- Only three oversized runtime files are tracked through Git LFS; the rest of `runtime` is currently tracked as normal Git content.

Git LFS tracked runtime files:

- `runtime\python\Lib\site-packages\torch\lib\torch_cpu.dll`
- `runtime\argos-data\argos-translate\packages\translate-en_zh-1_9\model\model.bin`
- `runtime\python\Lib\site-packages\ctranslate2\ctranslate2.dll`

Clone/setup implication:

- Install and initialize Git LFS before building or running from a fresh clone.
- If any of the files above are checked out as small pointer files, run `git lfs pull` or `git lfs checkout`.
- A pointer-file checkout can make Argos fail with errors such as `WinError 193` when loading `ctranslate2.dll`.

## Build And Run

Build Release on this machine:

```powershell
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe' 'D:\My_Caption\MyCaption.csproj' /t:Build /p:Configuration=Release /p:Platform=x64
```

Output:

- `D:\My_Caption\bin\Release\MyCaption.exe`
- Release output should include `runtime\`, `dictionary\`, and `tools\`.

Smoke checks for the bundled offline paths:

```powershell
"hello" | .\bin\Release\runtime\python\python.exe .\bin\Release\tools\argos_translate_stdin.py --from en --to zh
'{"text":"hello"}' | .\bin\Release\runtime\python\python.exe .\bin\Release\tools\mdict_query_stdin.py .\bin\Release\dictionary\default.mdx
```

Notes:

- The repository currently targets `.NET Framework 4.8`.
- The machine can build with MSBuild even if the newer `.NET SDK` is not installed.
- The current build environment has the `.NET Framework 4.8` targeting pack installed and built `Release|x64` with 0 warnings and 0 errors on 2026-05-24.
- The bundled Argos and MDict smoke checks passed on 2026-05-24.
- Git LFS content must be hydrated before building if the checkout is fresh or if the LFS files were left as pointer files.

## Installer

The repository includes an Inno Setup script at `installer\MyCaption.iss`.

Build prerequisites:

- Build `Release|x64` first.
- Install Inno Setup on the packaging machine so `ISCC.exe` is available.
- Make sure Git LFS files are hydrated before packaging.

Build the installer:

```powershell
ISCC.exe .\installer\MyCaption.iss
```

Expected installer output:

- `dist\MyCaptionSetup-0.1.0.exe`

The installer copies the `bin\Release` application payload into `Program Files`, including `runtime\`, `dictionary\`, and `tools\`. It excludes `settings.json`, creates Start Menu shortcuts, optionally creates a desktop shortcut, warns when `.NET Framework 4.8` is not detected, and launches the app after installation when selected.

Uninstall behavior:

- Installed program files and shortcuts are removed by the Inno Setup uninstaller.
- `%AppData%\My Caption` is preserved by default.
- After uninstalling program files, the uninstaller asks whether to delete `%AppData%\My Caption`; choosing Yes removes the user settings directory.

## Architecture

The codebase follows a layered structure even though it is a small desktop app.

### App And Runtime

- `App.xaml.cs` is the composition root.
- It loads persisted settings, constructs the capture, stabilization, translation, and lookup services, then passes them into `AppRuntime`.
- `AppRuntime` is the operational coordinator. It owns the application state exposed to the windows, subscribes to service events, saves settings, and routes user actions back into the underlying services.

### Core

The `src/Core` area contains domain and runtime logic:

- `Capture`: reads the Windows Live Captions window through UI Automation
- `Stabilization`: turns noisy partial captions into more stable display text
- `Translation`: provider interfaces, factories, hosts, dispatching, and concrete providers
- `Lookup`: provider interfaces, factories, hosts, JSON lookup, and MDX lookup
- `Models`: settings models, view models, and caption-related data models

### Infrastructure

The `src/Infrastructure` area contains environment-specific support:

- `Automation`: access to the Live Captions UI Automation surface
- `Persistence`: settings storage in `%AppData%\My Caption\settings.json`
- `Windows`: keyboard state monitoring and native Win32 helpers

### UI

The `src/UI` area contains WPF windows and interaction code:

- `MainWindow`: control panel and configuration UI
- `OverlayWindow`: live subtitle display and interaction surface
- `LookupCardWindow`: dictionary result popup

## End-To-End Runtime Flow

The main execution flow is:

1. `App.xaml.cs` starts the app and loads persisted `AppSettings`.
2. `App.xaml.cs` builds concrete services:
   - `LiveCaptionsAutomationClient`
   - `LiveCaptionsCaptureService`
   - `CaptionStabilizer`
   - `TranslationProviderHost`
   - `TranslationDispatcher`
   - `LookupProviderHost`
   - `AltKeyMonitor`
3. `AppRuntime` initializes the `OverlayViewModel` and `ControlPanelViewModel`, then subscribes to capture, translation, lookup, and keyboard events.
4. `AppRuntime.Start()` starts the capture loop immediately on app startup and starts the `Alt` key monitor.
5. `LiveCaptionsCaptureService` connects to Windows Live Captions, auto-launches it when needed, and can auto-hide the Live Captions window depending on settings.
6. `CaptionStabilizer` converts noisy incremental text into more stable display text.
7. `AppRuntime` updates the overlay with original text and optionally asks `TranslationDispatcher` to translate it.
8. `TranslationDispatcher` invokes the currently selected `ITranslationProvider` asynchronously and raises completion events back to `AppRuntime`.
9. `MainWindow` triggers lookup provider warmup in the background so the first MDict query is less likely to pay the full cold-start cost.
10. When the user clicks a word in the overlay, `AppRuntime` routes the request to `LookupProviderHost`, which delegates to the active `ILookupProvider`.
11. The lookup result is shown in `LookupCardWindow` while the main overlay remains focused on caption display.

Key coordination point:

- `AppRuntime` is the place to start when behavior spans UI, settings, capture state, translation, and lookup at the same time.

## Translation Providers

Translation is defined by the `ITranslationProvider` interface:

```csharp
public interface ITranslationProvider
{
    string DisplayName { get; }
    string Description { get; }
    Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken);
}
```

The runtime does not talk directly to concrete providers. It talks to `TranslationProviderHost`, which:

- owns the current `TranslationSettings`
- creates the active provider through `TranslationProviderFactory`
- reloads the provider whenever a provider-related setting changes
- exposes provider status and normalized config values back to the UI

Current provider modes:

- `Stub`
  - Returns predictable output for simple plumbing checks
  - Useful when testing the runtime without calling external services
- `ExternalCli`
  - Runs a local executable or script
  - Best for offline or self-hosted translation workflows such as Argos Translate
- `DeepL`, `AzureTranslator`, `GoogleCloud`
  - Use `OfficialApiTranslationProvider`
  - Best when translation quality and supported languages matter more than offline behavior

Current translation settings surfaced through the UI:

- `ProviderName`
- `Enabled`
- `SourceLanguage`
- `TargetLanguage`
- `ExecutablePath`
- `ArgumentsTemplate`
- `ApiUrl`
- `ApiKey`
- `ApiRegion`

Provider behavior details:

- `TranslationProviderFactory` normalizes executable paths to absolute paths when possible.
- Official API providers receive default API URLs when the configured URL is empty.
- `TranslationProviderHost` raises `ProviderStatusChanged` whenever a provider reload changes its effective status or normalized configuration.
- `ExternalCliTranslationProvider` supports persistent Argos mode so repeated subtitle translation does not relaunch Python for every line.

### External CLI Setup

The simplest local offline setup uses:

- provider: `External CLI`
- executable: Python
- argument template: `tools/argos_translate_stdin.py --from {from} --to {to}`

Recommended control panel values:

```text
Translation provider: External CLI
Executable path: <your-python-path>\python.exe
Arguments template: <project-root>\tools\argos_translate_stdin.py --from {from} --to {to}
Source language: en
Target language: zh-CN
```

Recommended validation command:

```powershell
'Hello, this is a test.' | <your-python-path>\python.exe <project-root>\tools\argos_translate_stdin.py --from en --to zh
```

Important current packaging note:

- A fresh settings file defaults to `runtime\python\python.exe` plus `tools\argos_translate_stdin.py`.
- `tools\argos_translate_stdin.py` loads `runtime\argos-data` first, then falls back to `tools\argos-data` for local development.
- If a saved settings file points at a Python executable or Argos script path that no longer exists, defaults can fall back to the bundled runtime path when it is present.

## Lookup Providers

Lookup is defined by the `ILookupProvider` interface:

```csharp
public interface ILookupProvider
{
    string DisplayName { get; }
    Task<LookupResult> LookupAsync(string word, CancellationToken cancellationToken);
}
```

As with translation, the runtime uses a host layer instead of directly binding to one implementation:

- `LookupProviderFactory` selects the concrete lookup provider
- `LookupProviderHost` owns `DictionarySettings`, reloads providers when settings change, and exposes normalized values and status back to the UI

Current lookup modes:

- `JsonFile`
  - Loads a local JSON dictionary file
  - Seeds a starter `dictionary.json` on first run if the file does not exist
  - Best for predictable local lookup and custom dictionary data
- `MdictCli`
  - Queries an `.mdx` dictionary through `mdict_utils`
  - Best when you want to use an existing MDict dictionary directly

Current lookup settings surfaced through the UI:

- `ProviderName`
- `DictionaryFilePath`
- `MdictExecutablePath`

### JSON Dictionary Mode

Behavior:

- Default provider is no longer the primary release path.
- Default dictionary path for JSON mode is `<app-base-directory>\dictionary.json`.
- `JsonFileLookupProvider` creates a small starter dictionary if the file is missing.
- Lookup includes simple morphology fallback for common English forms.

### MDict Mode

Current runtime behavior is important:

- The current default release-oriented lookup provider is `MdictCli`.
- The default bundled dictionary path is `<app-base-directory>\dictionary\default.mdx`.
- The repository currently carries `default.mdx` from the official `skywind3000/ECDICT` release assets. See `dictionary\ATTRIBUTION.txt` for source and license notes.

- Selecting `MdictCli` does not require the user to always hand-configure `mdict executable`.
- The provider now tries to auto-detect a usable `mdict_utils` runtime first.
- The `mdict executable` field is an optional override, not a required field.
- If auto-detection fails, the user can still set a specific executable manually.

In practice, `MdictLookupProvider` tries to work with:

- an explicitly configured `mdict executable` path
- a bundled runtime in `<app-base-directory>\runtime\mdict\mdict.exe`
- a bundled Python runtime in `<app-base-directory>\runtime\python\python.exe`
- the bundled `mdict-utils` entry point in `<app-base-directory>\runtime\python\Scripts\mdict.exe`
- a Python runtime located relative to the app directory when available
- the known local Python environment used by this project on this machine

Current behavior details:

- MDict lookup supports a persistent query process to avoid paying full process startup and dictionary load time for every word.
- The app warms up the lookup provider on startup and after dictionary selection changes when possible.
- The lookup card currently favors showing the cleaned full dictionary page content rather than aggressively restructuring entries.

Status behavior:

- If the `.mdx` path is empty or missing, the provider reports that immediately.
- If no usable `mdict` runtime can be found, the provider reports that the MDict runtime is unavailable.
- If the runtime is available, the provider queries metadata and reports a ready status, including `.mdd` sidecar detection when present.

Important current packaging note:

- A fresh settings file defaults to `MdictCli` with `<app-base-directory>\dictionary\default.mdx`.
- MDict lookup works without a separately installed Python environment when the bundled `runtime\python` directory is present.

### MDX Import Workflow

The repository also includes a one-time import path for converting MDX data into the JSON dictionary shape:

- `tools/import-mdx.ps1` orchestrates the import flow
- `tools/extract-mdict-utils.ps1` extracts MDX contents through `mdict-utils`
- `tools/MdxImportNormalizer` converts intermediate output into the app's JSON lookup format

This import workflow is useful when:

- you want a fully local JSON-backed dictionary for predictable runtime behavior
- you want to preprocess or normalize data once instead of querying MDX at runtime

Example import command:

```powershell
powershell -ExecutionPolicy Bypass -File 'D:\My_Caption\tools\import-mdx.ps1' `
  -MdxPath 'F:\BaiduNetdiskDownload\新牛津英汉双解大词典\新牛津英汉双解大词典.mdx' `
  -OutputJsonPath 'F:\BaiduNetdiskDownload\新牛津英汉双解大词典\dictionary.json' `
  -ExtractorPath 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe' `
  -ExtractorArguments '-ExecutionPolicy Bypass -File "<project-root>\tools\extract-mdict-utils.ps1" "{mdx}" "{out}" "<your-mdict-path>\mdict.exe"'
```

## Settings And Persistence

Settings are stored in `%AppData%\My Caption\settings.json`.

Persistence behavior:

- `SettingsStore.Load()` creates a default settings file if none exists
- if `%AppData%\My Caption\settings.json` does not exist but an application-directory `settings.json` does, the legacy file is copied into the AppData location on first load
- deserialization failures fall back to a new in-memory `AppSettings`
- `ApplyDefaults()` is called after loading and before saving
- Fresh defaults select bundled translation and dictionary assets when they are available.

Top-level settings groups:

- `Overlay`
  - overlay bounds, font sizes, opacity, display ordering, translation visibility
- `LiveCaptions`
  - polling interval, auto-launch behavior, original window visibility, stabilization thresholds
- `Interaction`
  - click-through and `Alt` interaction defaults
- `Translation`
  - active provider and provider-specific translation config
- `Dictionary`
  - active lookup provider, dictionary path, and optional MDict runtime override

Save behavior:

- `AppRuntime` is responsible for persisting user-facing settings changes.
- Settings are saved immediately when runtime update methods commit a change.
- There is no separate deferred save queue or profile system today.

Operational implication:

- If you change configuration behavior, check both the settings model defaults and the corresponding `AppRuntime.Update...` method to keep persistence and UI state aligned.

## Extensibility Guide

### Adding A Translation Provider

To add a new translation provider:

1. Implement `ITranslationProvider`.
2. If the provider exposes configuration or status details to the control panel, also follow the internal status shape expected by `TranslationProviderHost`.
3. Update `TranslationProviderFactory` so it can instantiate the new provider from `TranslationSettings.ProviderName`.
4. Update the control panel UI so the provider can be selected and configured.
5. Confirm that `AppRuntime` persists any new provider-specific settings you introduce.

Start reading from:

- `src/Core/Translation/ITranslationProvider.cs`
- `src/Core/Translation/TranslationProviderFactory.cs`
- `src/Core/Translation/TranslationProviderHost.cs`
- `src/UI/MainWindow/MainWindow.xaml.cs`

### Adding A Lookup Provider

To add a new lookup provider:

1. Implement `ILookupProvider`.
2. If the provider needs to surface normalized paths or provider-specific status, follow the host status pattern already used by lookup.
3. Update `LookupProviderFactory` so it can create the new provider from `DictionarySettings.ProviderName`.
4. Extend the control panel UI to select and configure the provider.
5. Make sure `AppRuntime` persists any new lookup-related settings.

Start reading from:

- `src/Core/Lookup/ILookupProvider.cs`
- `src/Core/Lookup/LookupProviderFactory.cs`
- `src/UI/MainWindow/MainWindow.xaml.cs`

### Changing Runtime Or UI Behavior

Use these entry points:

- cross-cutting runtime behavior: `src/App/AppRuntime.cs`
- control panel behavior: `src/UI/MainWindow/MainWindow.xaml.cs`
- overlay interaction and display: `src/UI/Overlay/OverlayWindow.xaml.cs`
- caption acquisition issues: `src/Core/Capture/LiveCaptionsCaptureService.cs`
- subtitle stability issues: `src/Core/Stabilization/CaptionStabilizer.cs`

## Debugging Notes

When debugging issues, it helps to separate them by subsystem:

- No captions arriving:
  - check Windows Live Captions state
  - inspect `LiveCaptionsAutomationClient` and `LiveCaptionsCaptureService`
  - confirm whether Windows Live Captions was auto-launched successfully
- Captions are noisy or unstable:
  - inspect `CaptionStabilizer`
  - review `SyncCommitThreshold` and `IdleCommitThreshold`
- Translation not appearing:
  - confirm `TranslationEnabled`
  - inspect provider status shown in the control panel
  - validate provider-specific config such as executable path or API key
- Dictionary popup is empty:
  - verify the active lookup provider
  - verify the dictionary file path
  - for MDX, verify that the runtime can auto-detect or use the configured `mdict` override
  - if only the first lookup is slow, check whether lookup warmup completed successfully
- Bundled Argos translation fails to start:
  - verify `runtime\python\python.exe` exists in the app base directory
  - verify the Git LFS files are real binaries, not pointer files
  - run `git lfs pull` or `git lfs checkout` if `ctranslate2.dll`, `model.bin`, or `torch_cpu.dll` is unexpectedly tiny

## Known Limitations

- The application depends on the Windows Live Captions UI surface remaining discoverable through UI Automation.
- The project currently targets WPF on `.NET Framework 4.8`, which keeps the build story tied to classic MSBuild on this machine.
- Settings are stored as a single local JSON file with immediate saves; there is no profile system, sync layer, or config migration framework.
- Provider extensibility exists, but provider-specific configuration still lives in a relatively manual control-panel flow.
- Lookup morphology fallback is intentionally lightweight and does not aim to be a full linguistic engine.
- The repository carries a large bundled Python runtime and Argos model data. Three oversized files use Git LFS, but most runtime files are still normal Git content.
- Developers and build machines need Git LFS available before validating the bundled offline translation path.

## Next Steps

Highest-value follow-up work currently identified in the repository:

1. Decide whether the bundled `runtime` should stay mostly in normal Git, move more files to Git LFS, or move to a release artifact workflow.
2. Validate install, launch, offline translation, MDict lookup, and uninstall behavior with the generated Inno Setup installer.
3. Validate AppData settings creation and legacy application-directory `settings.json` migration by launching the WPF app.
4. Improve dictionary morphology fallback and support richer entry shapes where cleaned full-page display is not enough.
5. Improve settings UX for language direction and provider-specific configuration.
6. Revisit whether the long-term app shape should stay control-panel-first or move toward tray-first behavior.

## File Map

Useful starting points:

- `App.xaml.cs`: composition root
- `src/App/AppRuntime.cs`: main runtime coordinator
- `src/Core/Models/AppSettings.cs`: persisted settings model and defaults
- `src/Core/Translation/*`: translation interfaces, hosts, factories, and providers
- `src/Core/Lookup/*`: lookup interfaces, hosts, factories, and providers
- `src/UI/MainWindow/*`: control panel UI and provider settings interactions
- `src/UI/Overlay/*`: overlay rendering and word lookup interaction
