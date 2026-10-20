# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

TrayLeds is a Windows tray-notification-area utility that shows the current state of the keyboard LEDs (Num Lock, Caps Lock, Scroll Lock) as a tray icon. It exists because many laptops and Bluetooth keyboards lack physical LED indicators for these keys.

It is a single-project, Windows-only app targeting .NET 10 (`net10.0`), published as NativeAOT (`PublishAot=true`, `PublishTrimmed=true`, `InvariantGlobalization=true`). It has no UI framework dependency (no WinForms/WPF) — the tray icon, context menu, and message loop are all implemented directly via Win32 P/Invoke so the app stays AOT-compatible. There is no cross-platform concern and no test suite.

## Build

```
dotnet publish -r win-x64 -c Release
```

Requires .NET Core 10. The solution file is `TrayLeds.slnx`, referencing the single project at `src/TrayLeds/TrayLeds.csproj`. Since this is a Windows-only, AOT-published GUI app, build/publish can be checked in any environment with the .NET SDK, but running/testing it requires an actual Windows machine.

## Architecture

- `src/TrayLeds/Program.cs` — entry point; constructs `TrayApp` and calls `Run()` (no visible main window, tray-only app, no COM apartment requirement so no `[STAThread]`), wrapped in a try/catch that shows any unhandled exception in a `MessageBoxW` instead of crashing silently.
- `src/TrayLeds/TrayApp.cs` — the entire application logic, built around a hidden top-level window and a manual Win32 message loop (not `HWND_MESSAGE`, since that variant doesn't receive the `WM_QUERYENDSESSION`/`WM_ENDSESSION` broadcasts needed for clean shutdown on logoff):
  - Registers a window class and creates a hidden window; `Run()` pumps it with `GetMessage`/`TranslateMessage`/`DispatchMessage`.
  - Owns the tray icon via `Shell_NotifyIcon` (add/modify/delete) and a right-click context menu built on demand via `CreatePopupMenu`/`TrackPopupMenuEx` (with the `SetForegroundWindow` + follow-up `PostMessage(WM_NULL)` dance needed for the menu to dismiss correctly).
  - Uses a Win32 `SetTimer`/`KillTimer` (100ms) on the hidden window, instead of a WinForms `Timer`, to debounce LED-state updates.
  - Installs a low-level keyboard hook (`WH_KEYBOARD_LL` via `SetWindowsHookEx`) to detect Num Lock/Caps Lock/Scroll Lock key-up events; on a relevant key-up it restarts the timer rather than updating immediately (debounce). The hook and the message pump must stay on the same thread.
  - On `WM_TIMER`, reads actual lock-key state via `GetKeyState(vk) & 1` (toggle-state bit, not `GetAsyncKeyState`) and encodes it as a 3-bit `state` int (bit 4 = NumLock, bit 2 = CapsLock, bit 1 = ScrollLock), then swaps the tray icon to one of 8 precomputed `HICON`s (`N{0,1}C{0,1}S{0,1}` naming convention) only when the state changed. All 8 icons are preloaded once at startup (`LoadEmbeddedIcon`) from the assembly's embedded `.ico` resources — not through `System.Drawing`/resx (AOT/trimming risk) and not from loose files on disk. Each `.ico` is a single-image classic icon; `LoadEmbeddedIcon` reads it via `Assembly.GetManifestResourceStream`, parses the fixed 22-byte `ICONDIR`/`ICONDIRENTRY` header to find the image offset/size, and calls `CreateIconFromResourceEx` on the sliced DIB bytes to get a real `HICON` — all in memory, no temp files.
  - Handles `WM_QUERYENDSESSION` (must return nonzero to allow shutdown) and `WM_ENDSESSION` (triggers cleanup, only when `wParam != 0`) for session-end handling, and `WM_DESTROY` → `PostQuitMessage` to exit the message loop.
  - `TrayApp` implements `IDisposable`; `Dispose()` idempotently unhooks the keyboard hook, kills the timer, removes the tray icon, `DestroyIcon`s all 8 preloaded icons (they're kept alive for the process lifetime rather than swapped in/out), and destroys the window. It's called from the Exit menu item (`WM_COMMAND`/`IdmExit`) and from `WM_ENDSESSION`; calling it is also what triggers `WM_DESTROY` → `PostQuitMessage`, which is the only way `Run()`'s `GetMessage` loop returns and lets `Program.cs`'s `using` block complete — so `WM_ENDSESSION` must call `Dispose()` directly rather than relying on process exit, or the tray icon is left behind as a ghost icon when the OS kills the process. A failure to install the keyboard hook in the constructor throws before `Dispose()` is wired up, so that path is not cleaned up by `Dispose()`.
- `src/TrayLeds/NativeMethods.cs` — all P/Invoke declarations (`user32.dll`, `shell32.dll`, `kernel32.dll`): window class/message loop, keyboard hook, timer, tray icon (`NOTIFYICONDATA`/`Shell_NotifyIcon`), popup menu, `GetKeyState`, `CreateIconFromResourceEx`, `MessageBoxW`.
- `src/TrayLeds/Resources/*.ico` — the 8 precomputed tray icons for each LED-state combination, embedded into the assembly (`EmbeddedResource` in the csproj, manifest name `TrayLeds.Resources.{filename}`) rather than shipped as loose files or through resx.

When changing LED-state handling, keep the bit encoding (Num=4, Caps=2, Scroll=1) and the `N{n}C{n}S{n}` file naming in sync — `TrayApp.SetIconForState` maps one to the other directly.
