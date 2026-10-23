# TrayLeds

TrayLeds is a Windows tray-notification-area utility that shows the current state of the keyboard LEDs (Num Lock, Caps Lock, Scroll Lock) as a tray icon. It exists because many laptops and Bluetooth keyboards lack physical LED indicators for these keys.

This is usefull on notebooks or on many bluetooth keyboards because many of them does not have leds to show their state.

## Build

To build the final version use the follwing command (.NET Core 10 is required).

```
dotnet publish -r win-x64 -c Release
```

## Install

Run `TrayLeds.msi`. It installs TrayLeds to `C:\Program Files\TrayLeds`, creates a `TrayLeds` scheduled task that starts it at every user's logon, and starts it right away for you. Other users get it at their next logon.

To build the installer (on Windows, .NET Core 10 required):

```
dotnet build installer -c Release
```

The MSI is written under `installer\bin`. To uninstall, use Apps & Features; this also removes the scheduled task.

Before installing, remove a manual setup if you made one (see below): exit TrayLeds from its tray icon menu, remove any Startup folder shortcut or `Run` registry entry for it, and from an administrator PowerShell run `Unregister-ScheduledTask -TaskName 'TrayLeds' -Confirm:$false`. Otherwise the old copy keeps running next to the installed one and you get two icons.

## Starting at logon without the installer (scheduled task)

The installer creates an equivalent task for all users. Use the steps below only if you run TrayLeds without the installer; do not combine both (installing the MSI replaces a task named `TrayLeds`).

TrayLeds does not need to run elevated: it polls the lock-key state, so its icon keeps updating even when an elevated window (for example an administrator terminal) has focus.

One way to start it at every logon is a scheduled task. To create it, open PowerShell under your own user account, set `$exe` to the path of `TrayLeds.exe`, and run:

```powershell
$exe  = 'C:\Tools\TrayLeds\TrayLeds.exe'
$user = "$env:USERDOMAIN\$env:USERNAME"

$action    = New-ScheduledTaskAction -Execute $exe
$trigger   = New-ScheduledTaskTrigger -AtLogOn -User $user
$principal = New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Limited
$settings  = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
                 -ExecutionTimeLimit ([TimeSpan]::Zero) -MultipleInstances IgnoreNew

Register-ScheduledTask -TaskName 'TrayLeds' -Action $action -Trigger $trigger `
    -Principal $principal -Settings $settings

Start-ScheduledTask -TaskName 'TrayLeds'   # optional: start it now, without logging off
```

Notes:

- `-LogonType Interactive` runs the task only while you are logged on, in your own session, so the tray icon is visible. A task set to run "whether the user is logged on or not" runs in a separate session and its icon never appears.
- The task scheduler defaults stop tasks on battery power and after 3 days of running. The settings above turn both off.
- Remove any other way of starting TrayLeds at logon (Startup folder shortcut, `Run` registry key), otherwise you get two icons.

To remove the task, run from PowerShell:

```powershell
Unregister-ScheduledTask -TaskName 'TrayLeds' -Confirm:$false
```
