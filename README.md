# My Caption

Personal Windows subtitle tool built around Windows Live Captions.

Current milestone:

- Capture text from Windows Live Captions via UI Automation
- Stabilize rapidly changing caption text before display/translation
- Render a low-distraction overlay window
- Default to click-through mode
- Hold `Alt` to enter temporary interaction mode
- Drag/resize the overlay while holding `Alt`
- Click English words to open a pluggable lookup popup
- Keep translation and lookup providers swappable

Current provider behavior:

- Translation provider is a stub that echoes the source text
- Lookup provider is a stub that proves token hit-testing and popup flow

Build on this machine:

```powershell
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe' 'D:\My_Caption\MyCaption.csproj' /t:Build /p:Configuration=Debug /p:Platform=x64
```

Output:

- `D:\My_Caption\bin\Debug\MyCaption.exe`

Notes:

- This implementation uses a `.NET Framework 4.8` WPF project because the current machine has MSBuild but does not have a .NET SDK installed.
- The runtime architecture still follows the planned layers so it can be migrated to SDK-style `.NET 8` later without rethinking the core flow.
