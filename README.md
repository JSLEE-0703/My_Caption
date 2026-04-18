# My Caption

Personal Windows subtitle tool built around Windows Live Captions.

Current milestone:

- Capture text from Windows Live Captions via UI Automation
- Stabilize rapidly changing caption text before display/translation
- Render a low-distraction overlay window
- Default to click-through mode
- Hold `Alt` to enter temporary interaction mode
- Drag/resize the overlay while holding `Alt`
- Click English words to open a local JSON-backed dictionary popup
- Keep translation and lookup providers swappable

Current provider behavior:

- Translation provider is still a stub that echoes the source text
- Lookup provider loads a local `dictionary.json`, seeds a starter file on first run, and supports a configurable dictionary path in the control panel

MDX import workflow:

- `tools/import-mdx.ps1` orchestrates a one-time MDX import into the app's `dictionary.json` shape
- `tools/extract-mdict-utils.ps1` is a ready-to-use wrapper for `mdict-utils`, which can unpack the encrypted `.mdx` into an intermediate MDict text file
- The bundled `tools/MdxImportNormalizer` project converts JSONL, TSV, or MDict unpacked text into lookup-ready JSON while preserving `rawHtml` for future richer rendering

Example import command:

```powershell
powershell -ExecutionPolicy Bypass -File 'D:\My_Caption\tools\import-mdx.ps1' `
  -MdxPath 'F:\BaiduNetdiskDownload\新牛津英汉双解大词典\新牛津英汉双解大词典.mdx' `
  -OutputJsonPath 'F:\BaiduNetdiskDownload\新牛津英汉双解大词典\dictionary.json' `
  -ExtractorPath 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe' `
  -ExtractorArguments '-ExecutionPolicy Bypass -File "D:\My_Caption\tools\extract-mdict-utils.ps1" "{mdx}" "{out}" "C:\Users\76341\.conda\envs\herobot_env\Scripts\mdict.exe"'
```

Build on this machine:

```powershell
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe' 'D:\My_Caption\MyCaption.csproj' /t:Build /p:Configuration=Debug /p:Platform=x64
```

Output:

- `D:\My_Caption\bin\Debug\MyCaption.exe`

Notes:

- This implementation uses a `.NET Framework 4.8` WPF project because the current machine has MSBuild but does not have a .NET SDK installed.
- The runtime architecture still follows the planned layers so it can be migrated to SDK-style `.NET 8` later without rethinking the core flow.
