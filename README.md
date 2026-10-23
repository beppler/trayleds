# TrayLeds

TrayLeds is a Windows tray-notification-area utility that shows the current state of the keyboard LEDs (Num Lock, Caps Lock, Scroll Lock) as a tray icon. It exists because many laptops and Bluetooth keyboards lack physical LED indicators for these keys.

This is usefull on notebooks or on many bluetooth keyboards because many of them does not have leds to show their state.

## Build

To build the final version use the follwing command (.NET Core 10 is required).

```
dotnet publish -r win-x64 -c Release
```

## Install

Run `TrayLeds.msi`. It is a per-user installer (no administrator rights or UAC prompt): it installs TrayLeds to `%LOCALAPPDATA%\Programs\TrayLeds`, adds a `TrayLeds` entry to your `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` key so it starts at every logon, and starts it right away. Other users of the computer have to run the installer themselves.

To build the installer (on Windows, .NET Core 10 required):

```
dotnet build installer -c Release
```

The MSI is written under `installer\bin`. To uninstall, use Apps & Features; this also removes the startup entry.

> **Upgrading from the per-machine installer:** earlier versions installed per machine to `C:\Program Files\TrayLeds` and created a `TrayLeds` scheduled task. This per-user installer does not remove that version. Uninstall it first from Apps & Features (this also deletes its scheduled task), then run the new `TrayLeds.msi`.

Also remove any other setup, otherwise the old copy keeps running next to the installed one and you get two icons: exit TrayLeds from its tray icon menu; remove any Startup folder shortcut or other `Run` registry entry for it; and, if you created a `TrayLeds` scheduled task by hand, run `Unregister-ScheduledTask -TaskName 'TrayLeds' -Confirm:$false` from an administrator PowerShell.

## Starting at logon without the installer

TrayLeds does not need to run elevated: it polls the lock-key state, so its icon keeps updating even when an elevated window (for example an administrator terminal) has focus. To start it at logon without the installer, put a shortcut to `TrayLeds.exe` in your Startup folder (`shell:startup`), or add the same `TrayLeds` value to `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. Do not combine this with the installer, or you get two icons.
