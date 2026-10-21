# TrayLeds

TrayLeds is a Windows tray-notification-area utility that shows the current state of the keyboard LEDs (Num Lock, Caps Lock, Scroll Lock) as a tray icon. It exists because many laptops and Bluetooth keyboards lack physical LED indicators for these keys.

This is usefull on notebooks or on many bluetooth keyboards because many of them does not have leds to show their state.

## Build

To build the final version use the follwing command (.NET Core 10 is required).

```
dotnet publish -r win-x64 -c Release
```

## Install

Run `TrayLeds.msi`. It installs TrayLeds to `C:\Program Files\TrayLeds`, creates a `TrayLeds` scheduled task that starts it at every user's logon (elevated for administrators, without a UAC prompt), and starts it right away for you. Other users get it at their next logon.

To build the installer (on Windows, .NET Core 10 required):

```
dotnet build installer -c Release
```

The MSI is written under `installer\bin`. To uninstall, use Apps & Features; this also removes the scheduled task.

Before installing, remove a manual setup if you made one (see below): exit TrayLeds from its tray icon menu, remove any Startup folder shortcut or `Run` registry entry for it, and from an administrator PowerShell run `Unregister-ScheduledTask -TaskName 'TrayLeds' -Confirm:$false`. Otherwise the old copy keeps running next to the installed one and you get two icons.

## Running elevated without the installer (scheduled task)

The installer creates an equivalent task for all users. Use the steps below only if you run TrayLeds without the installer; do not combine both (installing the MSI replaces a task named `TrayLeds`).

Windows does not deliver keyboard events from elevated applications to non-elevated processes. When an elevated window has focus (for example an administrator terminal), TrayLeds does not see the Num Lock, Caps Lock or Scroll Lock key presses and its icon stops updating. To avoid this, TrayLeds must run elevated too.

One way to do this without a UAC prompt on every logon is a scheduled task that starts TrayLeds elevated when you log on. To create it, open PowerShell **as administrator** under your own user account (the account must be a member of the Administrators group), set `$exe` to the path of `TrayLeds.exe`, and run:

```powershell
$exe  = 'C:\Tools\TrayLeds\TrayLeds.exe'
$user = "$env:USERDOMAIN\$env:USERNAME"

$action    = New-ScheduledTaskAction -Execute $exe
$trigger   = New-ScheduledTaskTrigger -AtLogOn -User $user
$principal = New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Highest
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

To remove the task, run from an administrator PowerShell:

```powershell
Unregister-ScheduledTask -TaskName 'TrayLeds' -Confirm:$false
```
