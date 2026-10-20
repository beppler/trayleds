# TrayLeds

TrayLeds is a Windows tray-notification-area utility that shows the current state of the keyboard LEDs (Num Lock, Caps Lock, Scroll Lock) as a tray icon. It exists because many laptops and Bluetooth keyboards lack physical LED indicators for these keys.

This is usefull on notebooks or on many bluetooth keyboards because many of them does not have leds to show their state.

## Build

To build the final version use the follwing command (.NET Core 10 is required).

```
dotnet publish -r win-x64 -c Release
```

## Running elevated (scheduled task)

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
