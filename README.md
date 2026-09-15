# TrayLeds

TrayLeds is a Windows tray-notification-area utility that shows the current state of the keyboard LEDs (Num Lock, Caps Lock, Scroll Lock) as a tray icon. It exists because many laptops and Bluetooth keyboards lack physical LED indicators for these keys.

This is usefull on notebooks or on many bluetooth keyboards because many of them does not have leds to show their state.

## Build

To build the final version use the follwing command (.NET Core 10 is required).

```
dotnet publish -r win-x64 -c Release
```
