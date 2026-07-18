# PowerModeTray

A lightweight Windows system tray app for switching power plans with one click.

## What it does

Sits in the system tray as a battery-shaped icon whose fill color/level shows
the active power plan. Right-click it to switch between:

- Power Saver
- Balanced
- High Performance
- Ultimate Performance (created automatically if not already present)

Each switch runs `powercfg /setactive` under the hood and confirms the change
with a balloon tip. The menu also includes **About** (version/date/license)
and **Exit**.

## Why it exists

The Redmond malware continues its deathspiral into uselessness and unusability by 
removing or hiding the features we need the most.

`setPowerMode.bat` (also in this folder) did the same job from a console menu,
but that meant opening a terminal every time. This app gives the same
functionality one right-click away, with no window or console flash.

## Dependencies

None beyond what Windows already ships with:

- **.NET Framework 4.x** (present by default on Windows 10/11) — provides
  both the `csc.exe` compiler used to build it and the WinForms/GDI+ runtime
  it runs on.
- No NuGet packages, no external icon files, no installer.

## Build instructions

From this folder, run:

```
build.bat
```

This locates `csc.exe` (checking the standard 64-bit path first, then 32-bit)
and compiles `PowerModeTray.cs` into `PowerModeTray.exe` with the required
`System`, `System.Core`, `System.Drawing`, and `System.Windows.Forms`
references. No project file, SDK install, or NuGet restore needed.

## Running

Double-click `PowerModeTray.exe`, or launch it from a terminal. No admin
rights are needed for normal use. If setting a mode ever fails (rare — only
if a scheme is missing and Windows blocks creating it), try running the exe
as administrator.

## Files

| File | Purpose |
|---|---|
| `PowerModeTray.cs` | App source (single file) |
| `build.bat` | Compiles the app via `csc.exe` |
| `PowerModeTray.exe` | Compiled app (tray GUI) |
| `setPowerMode.bat` | Original console-menu equivalent |
| `LICENSE` | MIT license |

## License

MIT — see [LICENSE](LICENSE).
