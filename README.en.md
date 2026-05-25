# My Caption

Language: [简体中文](README.md) | English

My Caption is a Windows subtitle companion for Windows Live Captions. It captures caption text, stabilizes fast-changing partial captions, optionally translates the result, and displays everything in a lightweight overlay with word lookup.

## Features

- Captures text from Windows Live Captions through UI Automation.
- Reduces flicker from rapidly changing partial caption output.
- Shows captions in a low-distraction WPF overlay.
- Supports offline translation with bundled Argos Translate assets.
- Supports word lookup with a bundled MDict dictionary.
- Runs fully offline; after installation, caption capture, offline translation, and dictionary lookup work without an internet connection.
- Lets the user hold `Alt` to temporarily interact with the overlay.
- Stores user settings in a user-writable AppData location.
- Provides a Windows installer built with Inno Setup.

## Release Version

The recommended way to install My Caption is to download the Windows installer from GitHub Releases.

- [Download latest release](https://github.com/JSLEE-0703/My_Caption/releases/latest)
- [View all release versions](https://github.com/JSLEE-0703/My_Caption/releases)

## Installation

Download the installer from the release page and run it on Windows.

The installer copies the application into `Program Files`, including the bundled `runtime`, `dictionary`, and `tools` assets needed for the default offline translation and dictionary lookup paths.

The application targets `.NET Framework 4.8`. The installer checks for .NET Framework 4.8 and shows a warning when it is not detected, but it does not install .NET automatically.

## Usage

> Before using My Caption, make sure the computer is running Windows 11 and Windows Live Captions can be enabled normally. My Caption automatically starts and connects to Windows Live Captions; if Live Captions cannot start, caption capture, translation, and word lookup will not work correctly.

1. Start My Caption.
2. Wait for the app to automatically start and connect to Windows Live Captions.
3. Use the control panel to configure caption display, translation, and dictionary lookup.
4. Keep the overlay click-through during normal use.
5. Hold `Alt` to interact with the overlay, move it, or click an English word to open the dictionary popup.

Fresh settings default to the bundled offline assets when they are present:

- Translation uses the bundled Python runtime and Argos bridge script.
- Dictionary lookup uses the bundled MDict dictionary at `dictionary\default.mdx`.

## Settings

Settings are stored in:

```text
%AppData%\My Caption\settings.json
```

If an older application-directory `settings.json` exists and the AppData settings file does not, My Caption copies the legacy file into the AppData location on first load.

Uninstalling the application preserves `%AppData%\My Caption` by default. During uninstall, the uninstaller asks whether to delete that user settings directory.

## Uninstall Behavior

The Inno Setup uninstaller removes installed program files and shortcuts.

Runtime-generated cache files under the install directory are also cleaned by recursively removing these installed payload folders:

- `runtime`
- `tools`
- `dictionary`

User settings are separate from the install directory and are only removed when the user chooses to delete `%AppData%\My Caption` during uninstall.

## Bundled Assets

Current release-oriented assets:

- `runtime\python\`: bundled Python runtime used by translation and MDict lookup.
- `runtime\argos-data\`: bundled Argos Translate offline data.
- `dictionary\default.mdx`: bundled default MDict dictionary.
- `dictionary\ATTRIBUTION.txt`: source and license note for the bundled dictionary.
- `tools\argos_translate_stdin.py`: Argos translation bridge script.
- `tools\mdict_query_stdin.py`: MDict query bridge script.
- `assets\icon\MyCaption.ico`: application and installer icon.

The repository uses Git LFS for selected large runtime files. If building or packaging from a fresh clone, initialize Git LFS and hydrate the checkout before validating the bundled offline runtime.

## Repository Layout

- `src\`: WPF application source.
- `installer\`: Inno Setup installer script.
- `runtime\`: bundled offline runtime assets.
- `dictionary\`: bundled dictionary assets and attribution.
- `tools\`: helper scripts used by translation, lookup, and dictionary import workflows.
- `assets\icon\`: source icon and Windows icon file.
- `docs\notes\`: project notes that are not part of the user-facing README.

## Attribution

The default bundled dictionary comes from [skywind3000/ECDICT](https://github.com/skywind3000/ECDICT).

The bundled dictionary attribution is recorded in `dictionary\ATTRIBUTION.txt`.
