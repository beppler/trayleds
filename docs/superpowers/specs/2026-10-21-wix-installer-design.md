# WiX installer for TrayLeds — design

Date: 2026-10-21
Status: approved design, pending spec review

## Goal

Ship TrayLeds as an MSI built with WiX that installs the NativeAOT-published
`TrayLeds.exe` and registers a scheduled task that starts TrayLeds elevated at
logon, automating the manual PowerShell recipe in the README.

Success means: after installing, the tray icon appears immediately (for the
installing user) and at every subsequent logon of any user, and it keeps
updating while an elevated window has focus. Uninstalling leaves no task, no
files, and no ghost tray icon.

## Decisions

| Topic | Decision |
|---|---|
| Who the task runs for | Any user who logs on (group principal BUILTIN\Users, `HighestAvailable`) |
| Installer UI | WixUI_Minimal (license page + Install), task started at end of install |
| Build/distribution | Local build only; CI and code signing are out of scope |
| Task creation mechanism | Task XML file + `schtasks.exe` via WixQuietExec custom actions |
| Install scope | Per-machine, `C:\Program Files\TrayLeds\`, x64 only |

Out of scope: CI/GitHub Releases, code signing, ARM64/x86 builds, a
single-instance guard in the app, Start Menu shortcuts, a configurable
per-user mode.

## 1. Project layout and build

```
installer/
  TrayLeds.Installer.wixproj   # SDK-style, Sdk="WixToolset.Sdk/<pinned 6.x version>"
  Package.wxs                  # Package, MajorUpgrade, UI, components, custom actions
  TrayLeds.task.xml            # scheduled-task definition template
  License.rtf                  # MIT license text for the WixUI license page
```

- `TrayLeds.slnx` gains an `/installer/` folder containing the wixproj.
- The wixproj has a `PublishTrayLeds` target (runs before `BeforeBuild`) that
  calls `MSBuild` on `src/TrayLeds/TrayLeds.csproj` — `Restore` then `Publish`
  with `RuntimeIdentifier=win-x64` — into `installer/obj/publish/<Configuration>/`,
  so building the installer runs the AOT publish first. (An explicit target was
  chosen over `ProjectReference Publish="true"` because it is deterministic with
  AOT and RID-specific restore.)
- Version: the same target reads the csproj's `VersionPrefix` with `XmlPeek`
  and appends `ProductVersion=<value>` (and `PublishDir`) to `DefineConstants`.
  The csproj stays the single source of truth; bumping `VersionPrefix` is the
  whole release step.
- Platform: `x64`; files go to `ProgramFiles64Folder\TrayLeds`.
- Build command (Windows only — AOT for win-x64 requires the Windows toolchain):
  `dotnet build installer -c Release`. Output lands under `installer/bin/...`
  following WiX defaults, file name `TrayLeds.msi`.
- Only `TrayLeds.exe` is packaged from the publish output (no `.pdb`).

## 2. Package contents and metadata

- `Package`: `Name="TrayLeds"`, `Manufacturer="Carlos Alberto Costa Beppler"`,
  `Scope="perMachine"`, `Version="$(var.ProductVersion)"`, and a fixed
  `UpgradeCode` GUID generated once and never changed.
- `MajorUpgrade` with a `DowngradeErrorMessage`, default scheduling
  (`afterInstallValidate`: the old product is fully removed before the new install's transaction starts). Every newer version removes the old one, which
  deletes and recreates the task — exactly one task at all times. Reinstalling
  the same version is a repair, not an upgrade.
- Files in `INSTALLFOLDER`:
  - `TrayLeds.exe`
  - `TrayLeds.task.xml` — exe path written in at install time; kept on disk.
- Add/Remove Programs: `ARPPRODUCTICON` from `src/TrayLeds/Resources/N1C1S1.ico`,
  `ARPURLINFOABOUT` set to the project URL, `ARPNOMODIFY=1`.
- No Start Menu shortcut: the task is the launcher, and a shortcut would start a
  second, non-elevated instance (the app has no single-instance guard).
- License page: `WixUILicenseRtf` points to `installer/License.rtf`, a
  hand-written RTF copy of the MIT text in `LICENSE` (WixUI requires RTF; the
  duplication of 21 static lines is accepted).
- `MediaTemplate EmbedCab="yes"` — single self-contained MSI.

## 3. Task definition (`installer/TrayLeds.task.xml`)

```xml
<?xml version="1.0" encoding="UTF-16"?>
<Task version="1.4" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
  <RegistrationInfo>
    <Author>TrayLeds</Author>
    <Description>Starts TrayLeds elevated at logon so it sees lock-key presses in elevated windows.</Description>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id="Author">
      <GroupId>S-1-5-32-545</GroupId>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <AllowHardTerminate>false</AllowHardTerminate>
    <StartWhenAvailable>false</StartWhenAvailable>
    <Enabled>true</Enabled>
  </Settings>
  <Actions Context="Author">
    <Exec>
      <Command>TrayLeds.exe</Command>
    </Exec>
  </Actions>
</Task>
```

- `LogonTrigger` without `UserId` fires for any user's logon.
- `GroupId` uses the SID of BUILTIN\Users so it works on localized Windows.
  A group principal runs with an interactive token in each user's own session,
  so the icon is visible (same effect as the README's `-LogonType Interactive`).
- `HighestAvailable`: administrators get an elevated token without a UAC prompt;
  standard users run at their normal level.
- `AllowHardTerminate=false`: Task Scheduler never kills the process (a kill
  would leave a ghost tray icon).
- File encoding is UTF-16LE with BOM and a matching declaration: `schtasks /XML`
  rejected the UTF-8 file with "The task XML is malformed". Git stores it as
  UTF-8 and checks it out as UTF-16 (`working-tree-encoding=UTF-16LE-BOM` in
  `.gitattributes`); `util:XmlFile` (MSXML) preserves the encoding when saving.
- `<Command>` is replaced at install time by `util:XmlFile` with
  `Action="setValue"`, `SelectionLanguage="XPath"`,
  `Value="[INSTALLFOLDER]TrayLeds.exe"`, and
  `ElementPath="//*[\[]local-name()='Command'[\]]"`. `ElementPath` is an MSI
  formatted string, so the XPath brackets are escaped as `[\[]`/`[\]]`;
  `local-name()` avoids mapping the task schema namespace.
- Task name: `TrayLeds` in the root folder, matching the README. Installing over
  a manually created task replaces it (`/F`).

## 4. Install, upgrade, and uninstall sequencing

All task commands run through `WixQuietExec64` as **deferred,
non-impersonated** custom actions (LocalSystem), using
`"[System64Folder]schtasks.exe"`. Each deferred CA's command line is passed as
its CustomActionData via a `SetProperty` whose `Id` equals the CA's `Id`, as
WixQuietExec requires for deferred execution.

### Install (condition `NOT REMOVE="ALL"`)

1. `InstallFiles` — exe and XML template.
2. `ExecXmlFile` (WiX util) — writes `[INSTALLFOLDER]TrayLeds.exe` into `<Command>`.
3. `RollbackCreateTask` — rollback CA: `schtasks /Delete /TN TrayLeds /F`,
   scheduled immediately before `CreateTask`.
4. `CreateTask` — `schtasks /Create /TN TrayLeds /XML "[INSTALLFOLDER]TrayLeds.task.xml" /F`,
   before `InstallFinalize` (util's XmlFile actions are scheduled right after `InstallFiles`, so the XML is already patched). `Return="check"`: failure
   fails and rolls back the install.
5. `StartTask` — `schtasks /Run /TN TrayLeds`, after `CreateTask`.
   `Return="ignore"`: if starting fails, install still succeeds and the app
   starts at next logon.

### Uninstall (condition `REMOVE="ALL"`, also runs for the old product during a MajorUpgrade)

1. `util:CloseApplication` for `TrayLeds.exe` with `CloseMessage="yes"` and
   `ElevatedCloseMessage="yes"` — sends `WM_CLOSE`. TrayLeds does not handle
   `WM_CLOSE`, so `DefWindowProc` destroys the window → `WM_DESTROY` →
   `PostQuitMessage` → `Run()` returns → `Program.cs`'s `using` calls
   `Dispose()`, removing the tray icon cleanly.
2. `DeleteTask` — `schtasks /Delete /TN TrayLeds /F`, before `RemoveFiles`.
   `Return="ignore"` so uninstall succeeds even if the task was deleted manually.
3. `RemoveFiles` — exe, XML, folder.

### Upgrade

Old package uninstall (close app, delete task) followed by new package install
(create task, start it). The icon blinks briefly.

### Known edge cases (documented, not engineered around)

- TrayLeds running in another user's session cannot be closed by
  `CloseApplication`; Restart Manager reports the file in use and Windows
  requests a reboot.

### Must be verified on Windows during implementation

- **(a)** Which session(s) `schtasks /Run` of a group-principal task starts in
  when invoked as LocalSystem. Fallback if it does not start in the installing
  user's session: launch the task from an immediate CA after `InstallFinalize`,
  or from the WixUI exit dialog.
- **(b)** Resolved: on Windows, uninstall with TrayLeds running asked for a
  reboot. util's immediate `CloseMessage` pass sends `WM_CLOSE` from a
  non-elevated process, which UIPI blocks for the elevated TrayLeds. TrayLeds
  now calls `ChangeWindowMessageFilterEx(hWindow, WM_CLOSE, MSGFLT_ALLOW)` and
  handles `WM_CLOSE` with `Dispose()`; no installer change was needed.

### Errors and diagnostics

`WixQuietExec` writes schtasks output to the MSI log; diagnose with
`msiexec /i TrayLeds.msi /l*v install.log`.

## 5. Documentation

**README.md**
- New "Install" section: run `TrayLeds.msi`; it installs to Program Files,
  creates the `TrayLeds` scheduled task for all users, and starts TrayLeds
  immediately. Uninstall via Apps & Features removes the task too. Building the
  MSI: `dotnet build installer -c Release` on Windows.
- Existing "Running elevated (scheduled task)" section kept and retitled as the
  manual alternative, noting that the installer creates the equivalent task and
  the two should not be mixed.

**AGENTS.md**
- Architecture: describe `installer/` (wixproj, AOT publish via project
  reference, version source, task XML + `util:XmlFile` patching, custom-action
  sequencing).
- Build: add the installer build command and note it requires Windows.
- Keep-in-sync rule: task name `TrayLeds` and the settings in
  `TrayLeds.task.xml` must stay consistent with the README's manual recipe.

## 6. Testing

No automated test suite exists. Verification checklist, run manually on Windows:

1. `dotnet build installer -c Release` succeeds; the MSI contains only
   `TrayLeds.exe` and `TrayLeds.task.xml` (inspect with Orca or `wix msi decompile`).
2. Clean install as an administrator: icon appears without logging off;
   `schtasks /Query /TN TrayLeds /XML` shows the installed exe path,
   `S-1-5-32-545`, and `HighestAvailable`.
3. Log off and on: icon appears and updates on lock keys while an elevated
   terminal has focus.
4. A standard (non-admin) user logs on: icon appears (non-elevated).
5. Bump `VersionPrefix`, rebuild, install over: exactly one task, one icon, no ghost.
6. Uninstall: task gone, `C:\Program Files\TrayLeds` gone, no ghost icon.
7. Install an older version over a newer one: blocked with the downgrade message.
8. Verification items (a) and (b) from section 4.

Checks possible outside Windows: task XML is well-formed; `.wxs` is internally
consistent. Whether the wixproj builds on Linux is not expected (the AOT
win-x64 publish needs Windows); the implementation report must state plainly
what was and was not verified.
