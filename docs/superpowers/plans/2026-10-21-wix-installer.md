# WiX Installer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build `TrayLeds.msi` with WiX that installs the NativeAOT `TrayLeds.exe` per-machine and registers a `TrayLeds` scheduled task that starts it elevated at any user's logon.

**Architecture:** A new SDK-style WiX project in `installer/` publishes the TrayLeds csproj (win-x64, AOT) in a pre-build target, reads `VersionPrefix` from the csproj, and packages the exe plus a task-definition XML. At install time `util:XmlFile` writes the installed exe path into the XML and deferred `WixQuietExec` custom actions call `schtasks.exe` to create/start/delete the task; `util:CloseApplication` closes a running instance with `WM_CLOSE` before files are replaced or removed.

**Tech Stack:** WiX Toolset v6 (`WixToolset.Sdk`, `WixToolset.UI.wixext`, `WixToolset.Util.wixext`), MSBuild, .NET 10 NativeAOT, Windows Task Scheduler XML schema 1.4.

**Spec:** `docs/superpowers/specs/2026-10-21-wix-installer-design.md`

## Global Constraints

- Windows-only, x64 only; install to `ProgramFiles64Folder\TrayLeds` (`C:\Program Files\TrayLeds\`).
- `Scope="perMachine"`; `UpgradeCode` = `{7525D841-D2DB-4F0B-A05A-F7A60933D06A}` — fixed forever, never regenerate.
- Version comes only from `<VersionPrefix>` in `src/TrayLeds/TrayLeds.csproj`; nothing else hard-codes a version.
- Task name is exactly `TrayLeds` (root folder), matching the README.
- Task principal: `<GroupId>S-1-5-32-545</GroupId>` (BUILTIN\Users by SID), `<RunLevel>HighestAvailable</RunLevel>`, `LogonTrigger` with no `UserId`.
- Task settings: `IgnoreNew`, no battery restrictions, `ExecutionTimeLimit` `PT0S`, `AllowHardTerminate` `false`.
- All `schtasks` calls: deferred, `Impersonate="no"`, full path `"[System64Folder]schtasks.exe"`.
- UI: `WixUI_Minimal` with `installer/License.rtf`. No Start Menu shortcut. `ARPNOMODIFY=1`.
- Only `TrayLeds.exe` (no `.pdb`) and `TrayLeds.task.xml` are installed.
- Commits: no `Co-Authored-By: Claude` trailer (user preference). Commit messages follow repo style: sentence case, ending with a period.
- This environment has no .NET SDK and no Windows. Steps marked **[Windows]** must be run by the user on a Windows machine with the .NET 10 SDK; the implementer runs every other step and reports plainly which **[Windows]** steps remain unverified.

## Review Focus

- **Installing over a manual README setup:** a user who already registered the task by hand (and has TrayLeds running) installs the MSI → expect exactly one task (overwritten by `/F`) pointing at `C:\Program Files\TrayLeds\TrayLeds.exe` and one icon. Covered by Task 4 check 9.
- **Task deleted by hand before uninstall:** `DeleteTask` fails → expect uninstall to still succeed (`Return="ignore"`). Covered by Task 4 check 10.
- **Spaces in paths:** `C:\Program Files\...` must be quoted in every `schtasks` command line → expect task created with the full path. Covered by Task 2 Step 2 assertions and Task 4 check 2.
- **Upgrade while TrayLeds runs elevated:** expect the old instance to close cleanly (no ghost icon) and a single new instance afterwards. Covered by Task 4 check 5 (verification item b).
- **Standard (non-admin) user logs on:** expect the task to start TrayLeds non-elevated in that user's session without a UAC prompt or error. Covered by Task 4 check 4.

---

## File Structure

| File | Responsibility |
|---|---|
| `installer/TrayLeds.Installer.wixproj` | WiX SDK project: package refs, x64 platform, pre-build target that publishes TrayLeds AOT and passes `ProductVersion`/`PublishDir` to WiX |
| `installer/Package.wxs` | MSI definition: package metadata, upgrade, UI, ARP, files, XmlFile patch, CloseApplication, task custom actions and sequencing |
| `installer/TrayLeds.task.xml` | Scheduled-task definition template (exe path patched at install time) |
| `installer/License.rtf` | RTF copy of `LICENSE` for the WixUI license page |
| `TrayLeds.slnx` | Add `/installer/` folder with the wixproj |
| `README.md`, `AGENTS.md` | Install/build docs, architecture and keep-in-sync rules |
| `docs/superpowers/specs/2026-10-21-wix-installer-design.md` | Sync two implementation details (publish mechanism, version read) |

Static check used by several tasks (scratch only, never committed). Save once as
`/tmp/claude-0/-workspace/c28c73ea-a866-4fd7-a24c-d6c1d0f35aaa/scratchpad/check_installer.py`:

```python
import sys, xml.etree.ElementTree as ET
from pathlib import Path

root = Path(sys.argv[1])
failures = []

def check(cond, msg):
    if not cond:
        failures.append(msg)

for name in ["TrayLeds.Installer.wixproj", "Package.wxs", "TrayLeds.task.xml"]:
    p = root / "installer" / name
    if p.exists():
        try:
            ET.parse(p)
        except ET.ParseError as e:
            failures.append(f"{name}: not well-formed: {e}")

for expectation in sys.argv[2:]:
    name, _, needle = expectation.partition("::")
    p = root / "installer" / name
    check(p.exists() and needle in p.read_text(encoding="utf-8"), f"{name}: missing {needle!r}")

print("\n".join(failures) if failures else "OK")
sys.exit(1 if failures else 0)
```

Usage: `python3 -I <scratchpad>/check_installer.py /workspace 'File::needle' ...` — prints `OK` or each failure.

---

### Task 1: Installer project that packages the AOT exe

Deliverable: `dotnet build installer -c Release` produces an MSI that installs `TrayLeds.exe` to `C:\Program Files\TrayLeds\` with the license page, ARP metadata, and major-upgrade behavior. No scheduled task yet.

**Files:**
- Create: `installer/TrayLeds.Installer.wixproj`
- Create: `installer/Package.wxs`
- Create: `installer/License.rtf`
- Modify: `docs/superpowers/specs/2026-10-21-wix-installer-design.md` (section 1, two bullets)

**Interfaces:**
- Consumes: `src/TrayLeds/TrayLeds.csproj` (`<VersionPrefix>` element, Publish target).
- Produces: preprocessor variables `$(var.ProductVersion)` and `$(var.PublishDir)` (trailing backslash); directory id `INSTALLFOLDER`; component group id `ProductComponents`; file id `TrayLedsExe`. Task 2 adds to `Package.wxs` using these ids.

- [ ] **Step 1: Write the scratch check script**

Write the script from the File Structure section to
`/tmp/claude-0/-workspace/c28c73ea-a866-4fd7-a24c-d6c1d0f35aaa/scratchpad/check_installer.py`.

- [ ] **Step 2: Run the check to confirm it fails**

Run:
```bash
python3 -I /tmp/claude-0/-workspace/c28c73ea-a866-4fd7-a24c-d6c1d0f35aaa/scratchpad/check_installer.py /workspace \
  'TrayLeds.Installer.wixproj::WixToolset.Sdk/6.0.2' \
  'TrayLeds.Installer.wixproj::<XmlPeek' \
  'Package.wxs::7525D841-D2DB-4F0B-A05A-F7A60933D06A' \
  'Package.wxs::WixUI_Minimal' \
  'Package.wxs::$(var.PublishDir)TrayLeds.exe'
```
Expected: FAIL — five `missing` lines.

- [ ] **Step 3: Create `installer/TrayLeds.Installer.wixproj`**

```xml
<Project Sdk="WixToolset.Sdk/6.0.2">

  <PropertyGroup>
    <OutputName>TrayLeds</OutputName>
    <InstallerPlatform>x64</InstallerPlatform>
    <TrayLedsProject>$(MSBuildThisFileDirectory)..\src\TrayLeds\TrayLeds.csproj</TrayLedsProject>
    <TrayLedsPublishDir>$(MSBuildThisFileDirectory)obj\publish\$(Configuration)\</TrayLedsPublishDir>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="WixToolset.UI.wixext" Version="6.0.2" />
    <PackageReference Include="WixToolset.Util.wixext" Version="6.0.2" />
  </ItemGroup>

  <!-- Publishes TrayLeds (win-x64, NativeAOT) and hands the exe location and version to the WiX compiler. -->
  <Target Name="PublishTrayLeds" BeforeTargets="BeforeBuild">
    <MSBuild Projects="$(TrayLedsProject)"
             Targets="Restore"
             Properties="RuntimeIdentifier=win-x64;_TrayLedsRestore=true" />
    <MSBuild Projects="$(TrayLedsProject)"
             Targets="Publish"
             Properties="Configuration=$(Configuration);RuntimeIdentifier=win-x64;PublishDir=$(TrayLedsPublishDir)" />
    <XmlPeek XmlInputPath="$(TrayLedsProject)" Query="/Project/PropertyGroup/VersionPrefix/text()">
      <Output TaskParameter="Result" PropertyName="TrayLedsVersion" />
    </XmlPeek>
    <Error Condition="'$(TrayLedsVersion)' == ''" Text="VersionPrefix not found in $(TrayLedsProject)." />
    <PropertyGroup>
      <DefineConstants>$(DefineConstants);ProductVersion=$(TrayLedsVersion);PublishDir=$(TrayLedsPublishDir)</DefineConstants>
    </PropertyGroup>
  </Target>

</Project>
```

Notes for the implementer:
- The separate `Restore` call with an extra dummy property (`_TrayLedsRestore`) forces MSBuild to treat it as a distinct project instance, so the win-x64 assets (ILCompiler runtime pack) are restored before `Publish`. Without it, publish fails with `NETSDK1047`.
- Before committing, check https://www.nuget.org/packages/WixToolset.Sdk for the latest 6.0.x and use that same version in all three places (Sdk, UI, Util). Do not jump to a new major version.

- [ ] **Step 4: Create `installer/License.rtf`**

```rtf
{\rtf1\ansi\ansicpg1252\deff0{\fonttbl{\f0\fswiss Segoe UI;}}\f0\fs18
\b MIT License\b0\par
\par
Copyright (c) 2017-2026 Carlos Alberto Costa Beppler\par
\par
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:\par
\par
The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.\par
\par
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.\par
}
```

- [ ] **Step 5: Create `installer/Package.wxs`**

```xml
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs"
     xmlns:ui="http://wixtoolset.org/schemas/v4/wxs/ui">

  <Package Name="TrayLeds"
           Manufacturer="Carlos Alberto Costa Beppler"
           Version="$(var.ProductVersion)"
           UpgradeCode="{7525D841-D2DB-4F0B-A05A-F7A60933D06A}"
           Scope="perMachine"
           Language="1033">

    <MajorUpgrade DowngradeErrorMessage="A newer version of [ProductName] is already installed." />
    <MediaTemplate EmbedCab="yes" />

    <Icon Id="TrayLeds.ico" SourceFile="..\src\TrayLeds\Resources\N1C1S1.ico" />
    <Property Id="ARPPRODUCTICON" Value="TrayLeds.ico" />
    <Property Id="ARPURLINFOABOUT" Value="https://github.com/beppler/trayleds" />
    <Property Id="ARPNOMODIFY" Value="1" />

    <ui:WixUI Id="WixUI_Minimal" />
    <WixVariable Id="WixUILicenseRtf" Value="License.rtf" />

    <StandardDirectory Id="ProgramFiles64Folder">
      <Directory Id="INSTALLFOLDER" Name="TrayLeds" />
    </StandardDirectory>

    <ComponentGroup Id="ProductComponents" Directory="INSTALLFOLDER">
      <Component>
        <File Id="TrayLedsExe" Source="$(var.PublishDir)TrayLeds.exe" KeyPath="yes" />
      </Component>
    </ComponentGroup>

    <Feature Id="Main">
      <ComponentGroupRef Id="ProductComponents" />
    </Feature>

  </Package>
</Wix>
```

- [ ] **Step 6: Run the check to confirm it passes**

Run the same command as Step 2.
Expected: `OK`.

- [ ] **Step 7: Sync the spec with the chosen build mechanism**

In `docs/superpowers/specs/2026-10-21-wix-installer-design.md`, section 1, replace the `ProjectReference` bullet and the "Version:" bullet with:

```markdown
- The wixproj has a `PublishTrayLeds` target (runs before `BeforeBuild`) that
  calls `MSBuild` on `src/TrayLeds/TrayLeds.csproj` — `Restore` then `Publish`
  with `RuntimeIdentifier=win-x64` — into `installer/obj/publish/<Configuration>/`,
  so building the installer runs the AOT publish first. (An explicit target was
  chosen over `ProjectReference Publish="true"` because it is deterministic with
  AOT and RID-specific restore.)
- Version: the same target reads the csproj's `VersionPrefix` with `XmlPeek`
  and appends `ProductVersion=<value>` (and `PublishDir`) to `DefineConstants`.
```

- [ ] **Step 8: [Windows] Build and inspect**

Run in PowerShell from the repo root:
```powershell
dotnet build installer -c Release
Get-ChildItem installer\bin -Recurse -Filter TrayLeds.msi
```
Expected: build succeeds with no errors (ICE warnings are acceptable only if they are not about 64-bit components/folders); one `TrayLeds.msi` is listed. If the build reports a 32-bit package with a 64-bit folder, add `-p:Platform=x64` and record the outcome for Task 3's README text.

Then install and uninstall once:
```powershell
msiexec /i <path-to>\TrayLeds.msi /l*v install.log
Test-Path 'C:\Program Files\TrayLeds\TrayLeds.exe'   # True
msiexec /x <path-to>\TrayLeds.msi /l*v uninstall.log
Test-Path 'C:\Program Files\TrayLeds'                # False
```
Expected: the license page appears; Apps & Features shows TrayLeds with the tray icon and version matching `VersionPrefix`.

- [ ] **Step 9: Commit**

```bash
git add installer/TrayLeds.Installer.wixproj installer/Package.wxs installer/License.rtf docs/superpowers/specs/2026-10-21-wix-installer-design.md
git commit -m "Add WiX installer project packaging the AOT executable."
```

---

### Task 2: Scheduled task registration

Deliverable: the MSI installs `TrayLeds.task.xml`, patches the exe path into it, creates and starts the `TrayLeds` task on install, closes TrayLeds and deletes the task on uninstall, and rolls back the task if the install fails.

**Files:**
- Create: `installer/TrayLeds.task.xml`
- Modify: `installer/Package.wxs` (add `util` namespace, a component in `ProductComponents`, `util:CloseApplication`, custom actions, `InstallExecuteSequence`)

**Interfaces:**
- Consumes: `INSTALLFOLDER`, `ProductComponents`, `TrayLedsExe` from Task 1.
- Produces: file id `TaskXml`; custom action ids `CreateTask`, `RollbackCreateTask`, `StartTask`, `DeleteTask`; `util:CloseApplication` id `CloseTrayLeds`.

- [ ] **Step 1: Run the check to confirm it fails**

Run:
```bash
python3 -I /tmp/claude-0/-workspace/c28c73ea-a866-4fd7-a24c-d6c1d0f35aaa/scratchpad/check_installer.py /workspace \
  'TrayLeds.task.xml::<GroupId>S-1-5-32-545</GroupId>' \
  'TrayLeds.task.xml::<RunLevel>HighestAvailable</RunLevel>' \
  'TrayLeds.task.xml::<AllowHardTerminate>false</AllowHardTerminate>' \
  'Package.wxs::util:XmlFile' \
  'Package.wxs::util:CloseApplication' \
  "Package.wxs::/Create /TN TrayLeds /XML &quot;[INSTALLFOLDER]TrayLeds.task.xml&quot; /F" \
  "Package.wxs::/Delete /TN TrayLeds /F" \
  "Package.wxs::/Run /TN TrayLeds"
```
Expected: FAIL — eight `missing` lines (the task XML file does not exist yet).

- [ ] **Step 2: Create `installer/TrayLeds.task.xml`**

```xml
<?xml version="1.0" encoding="UTF-8"?>
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

- [ ] **Step 3: Add the `util` namespace to `installer/Package.wxs`**

Replace:
```xml
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs"
     xmlns:ui="http://wixtoolset.org/schemas/v4/wxs/ui">
```
with:
```xml
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs"
     xmlns:ui="http://wixtoolset.org/schemas/v4/wxs/ui"
     xmlns:util="http://wixtoolset.org/schemas/v4/wxs/util">
```

- [ ] **Step 4: Add the task XML component**

In `installer/Package.wxs`, replace:
```xml
      <Component>
        <File Id="TrayLedsExe" Source="$(var.PublishDir)TrayLeds.exe" KeyPath="yes" />
      </Component>
    </ComponentGroup>
```
with:
```xml
      <Component>
        <File Id="TrayLedsExe" Source="$(var.PublishDir)TrayLeds.exe" KeyPath="yes" />
      </Component>
      <Component>
        <File Id="TaskXml" Source="TrayLeds.task.xml" KeyPath="yes" />
        <!-- ElementPath is a formatted string, so XPath brackets are escaped as [\[] and [\]].
             local-name() avoids mapping the task schema namespace. -->
        <util:XmlFile Id="SetTaskCommand"
                      File="[#TaskXml]"
                      Action="setValue"
                      SelectionLanguage="XPath"
                      ElementPath="//*[\[]local-name()='Command'[\]]"
                      Value="[INSTALLFOLDER]TrayLeds.exe" />
      </Component>
    </ComponentGroup>
```

- [ ] **Step 5: Add CloseApplication, custom actions and sequencing**

In `installer/Package.wxs`, replace:
```xml
    <Feature Id="Main">
```
with:
```xml
    <!-- WM_CLOSE makes TrayLeds exit through DefWindowProc -> WM_DESTROY -> Dispose(), removing its tray icon.
         A hard kill would leave a ghost icon. -->
    <util:CloseApplication Id="CloseTrayLeds"
                           Target="TrayLeds.exe"
                           CloseMessage="yes"
                           ElevatedCloseMessage="yes" />

    <!-- Deferred WixQuietExec reads its command line from the property named after the custom action. -->
    <SetProperty Id="RollbackCreateTask" Sequence="execute" Before="RollbackCreateTask"
                 Value='"[System64Folder]schtasks.exe" /Delete /TN TrayLeds /F' />
    <SetProperty Id="CreateTask" Sequence="execute" Before="CreateTask"
                 Value='"[System64Folder]schtasks.exe" /Create /TN TrayLeds /XML &quot;[INSTALLFOLDER]TrayLeds.task.xml&quot; /F' />
    <SetProperty Id="StartTask" Sequence="execute" Before="StartTask"
                 Value='"[System64Folder]schtasks.exe" /Run /TN TrayLeds' />
    <SetProperty Id="DeleteTask" Sequence="execute" Before="DeleteTask"
                 Value='"[System64Folder]schtasks.exe" /Delete /TN TrayLeds /F' />

    <CustomAction Id="RollbackCreateTask" BinaryRef="Wix4UtilCA_$(sys.BUILDARCHSHORT)" DllEntry="WixQuietExec"
                  Execute="rollback" Impersonate="no" Return="ignore" />
    <CustomAction Id="CreateTask" BinaryRef="Wix4UtilCA_$(sys.BUILDARCHSHORT)" DllEntry="WixQuietExec"
                  Execute="deferred" Impersonate="no" Return="check" />
    <CustomAction Id="StartTask" BinaryRef="Wix4UtilCA_$(sys.BUILDARCHSHORT)" DllEntry="WixQuietExec"
                  Execute="deferred" Impersonate="no" Return="ignore" />
    <CustomAction Id="DeleteTask" BinaryRef="Wix4UtilCA_$(sys.BUILDARCHSHORT)" DllEntry="WixQuietExec"
                  Execute="deferred" Impersonate="no" Return="ignore" />

    <InstallExecuteSequence>
      <!-- util's XmlFile actions run right after InstallFiles, so the XML already has the exe path here. -->
      <Custom Action="RollbackCreateTask" Before="CreateTask" Condition='NOT (REMOVE="ALL")' />
      <Custom Action="CreateTask" Before="InstallFinalize" Condition='NOT (REMOVE="ALL")' />
      <Custom Action="StartTask" After="CreateTask" Condition='NOT (REMOVE="ALL")' />
      <Custom Action="DeleteTask" Before="RemoveFiles" Condition='REMOVE="ALL"' />
    </InstallExecuteSequence>

    <Feature Id="Main">
```

Notes for the implementer:
- `Value='...'` uses single-quoted attributes so the inner `"` need no escaping; the `&quot;` in the `CreateTask` value is equivalent and kept so the Step 1 check matches. Both forms produce the same literal `"`.
- `StartTask` runs `schtasks /Run` as LocalSystem; `MultipleInstancesPolicy=IgnoreNew` makes it harmless on repair.

- [ ] **Step 6: Run the check to confirm it passes**

Run the same command as Step 1.
Expected: `OK`.

- [ ] **Step 7: [Windows] Build, install, inspect the task, uninstall**

Run in an elevated PowerShell:
```powershell
dotnet build installer -c Release
$msi = (Get-ChildItem installer\bin -Recurse -Filter TrayLeds.msi | Select-Object -First 1).FullName
msiexec /i $msi /l*v install.log
schtasks /Query /TN TrayLeds /XML
Get-Content 'C:\Program Files\TrayLeds\TrayLeds.task.xml'
```
Expected:
- The tray icon appears without logging off (verification item a — if it does not, record which session the process started in with `Get-Process TrayLeds | Select-Object Id, SessionId` and stop; do not improvise a fallback without asking).
- The queried XML shows `<Command>C:\Program Files\TrayLeds\TrayLeds.exe</Command>`, `<GroupId>S-1-5-32-545</GroupId>`, `<RunLevel>HighestAvailable</RunLevel>`.
- `install.log` contains the schtasks output `SUCCESS: The scheduled task "TrayLeds" has successfully been created.`

Then:
```powershell
msiexec /x $msi /l*v uninstall.log
schtasks /Query /TN TrayLeds        # ERROR: The system cannot find the file specified.
Test-Path 'C:\Program Files\TrayLeds'   # False
```
Expected: the tray icon disappears with no ghost left after hovering over the tray (verification item b — if TrayLeds is still running or a ghost remains, record it and stop).

- [ ] **Step 8: Sync the spec's XmlFile and sequencing details**

In `docs/superpowers/specs/2026-10-21-wix-installer-design.md`:

Section 3 — replace the bullet that starts with ``- `<Command>` is replaced at install time`` (through the end of that bullet) with:

```markdown
- `<Command>` is replaced at install time by `util:XmlFile` with
  `Action="setValue"`, `SelectionLanguage="XPath"`,
  `Value="[INSTALLFOLDER]TrayLeds.exe"`, and
  `ElementPath="//*[\[]local-name()='Command'[\]]"`. `ElementPath` is an MSI
  formatted string, so the XPath brackets are escaped as `[\[]`/`[\]]`;
  `local-name()` avoids mapping the task schema namespace.
```

Section 4, Install step 4 — replace `after `ExecXmlFile`, before `InstallFinalize`.` with `before `InstallFinalize` (util's XmlFile actions are scheduled right after `InstallFiles`, so the XML is already patched).`

- [ ] **Step 9: Commit**

```bash
git add installer/TrayLeds.task.xml installer/Package.wxs docs/superpowers/specs/2026-10-21-wix-installer-design.md
git commit -m "Register the TrayLeds scheduled task from the installer."
```

---

### Task 3: Solution and documentation

Deliverable: the installer appears in the solution, and README/AGENTS.md describe installing, building the MSI, and the keep-in-sync rules.

**Files:**
- Modify: `TrayLeds.slnx`
- Modify: `README.md` (add Install section, retitle the scheduled-task section)
- Modify: `AGENTS.md` (Build and Architecture sections, keep-in-sync rule)

**Interfaces:**
- Consumes: file and id names from Tasks 1–2.
- Produces: none.

- [ ] **Step 1: Add the installer to `TrayLeds.slnx`**

Replace:
```xml
  <Folder Name="/src/">
    <Project Path="src/TrayLeds/TrayLeds.csproj" />
  </Folder>
```
with:
```xml
  <Folder Name="/installer/">
    <Project Path="installer/TrayLeds.Installer.wixproj" />
  </Folder>
  <Folder Name="/src/">
    <Project Path="src/TrayLeds/TrayLeds.csproj" />
  </Folder>
```

- [ ] **Step 2: Check the slnx is well-formed**

Run: `python3 -I -c "import xml.etree.ElementTree as E; E.parse('/workspace/TrayLeds.slnx'); print('OK')"`
Expected: `OK`.

- [ ] **Step 3: Update `README.md`**

After the `## Build` section's code block, insert:

````markdown
## Install

Run `TrayLeds.msi`. It installs TrayLeds to `C:\Program Files\TrayLeds`, creates a `TrayLeds` scheduled task that starts it at every user's logon (elevated for administrators, without a UAC prompt), and starts it right away for you. Other users get it at their next logon.

To build the installer (on Windows, .NET Core 10 required):

```
dotnet build installer -c Release
```

The MSI is written under `installer\bin`. To uninstall, use Apps & Features; this also removes the scheduled task.
````

Then rename the heading `## Running elevated (scheduled task)` to `## Running elevated without the installer (scheduled task)` and add this paragraph directly under the heading:

```markdown
The installer creates an equivalent task for all users. Use the steps below only if you run TrayLeds without the installer; do not combine both (installing the MSI replaces a task named `TrayLeds`).
```

If Task 1 Step 8 needed `-p:Platform=x64`, add it to the build command above.

- [ ] **Step 4: Update `AGENTS.md`**

In `## Build`, after the existing code block, add:

````markdown
The MSI installer is built with:

```
dotnet build installer -c Release
```

It must run on Windows: the installer project publishes the win-x64 NativeAOT executable before packaging it.
````

In `## Architecture`, add this bullet after the `src/TrayLeds/Resources/*.ico` bullet:

```markdown
- `installer/` — WiX v6 MSI (`TrayLeds.Installer.wixproj`, SDK-style, in `TrayLeds.slnx`):
  - The `PublishTrayLeds` target restores and publishes `src/TrayLeds/TrayLeds.csproj` for `win-x64` into `installer/obj/publish/<Configuration>/`, reads `<VersionPrefix>` from the csproj with `XmlPeek`, and passes `ProductVersion`/`PublishDir` to WiX via `DefineConstants`. The csproj's `VersionPrefix` is the only version source.
  - `Package.wxs` — per-machine x64 package to `C:\Program Files\TrayLeds\`, `MajorUpgrade`, `WixUI_Minimal` with `License.rtf` (an RTF copy of `LICENSE`; keep them in sync), no shortcuts (the scheduled task is the launcher). The `UpgradeCode` must never change.
  - `TrayLeds.task.xml` — task definition installed next to the exe; `util:XmlFile` writes the installed exe path into `<Command>` at install time. Principal is BUILTIN\Users by SID (`S-1-5-32-545`) with `HighestAvailable`, logon trigger for any user.
  - Deferred, non-impersonated `WixQuietExec` custom actions run `schtasks.exe`: `CreateTask` (`/Create /XML /F`, with `RollbackCreateTask`) and `StartTask` (`/Run`) on install, `DeleteTask` (`/Delete`) on uninstall. `util:CloseApplication` sends `WM_CLOSE` to a running TrayLeds before files change; TrayLeds exits cleanly on it (`DefWindowProc` → `WM_DESTROY` → `Dispose()`), whereas a hard kill would leave a ghost tray icon.
```

At the end of the file, add:

```markdown
When changing the scheduled task, keep the task name (`TrayLeds`) and the settings in `installer/TrayLeds.task.xml` consistent with the manual PowerShell recipe in the README.
```

- [ ] **Step 5: Verify docs mention the key facts**

Run:
```bash
grep -c "dotnet build installer -c Release" /workspace/README.md /workspace/AGENTS.md
grep -n "Running elevated without the installer" /workspace/README.md
grep -n "S-1-5-32-545" /workspace/AGENTS.md
```
Expected: count `1` for each file; one match each for the last two commands.

- [ ] **Step 6: [Windows] Solution build**

Run: `dotnet build TrayLeds.slnx -c Release`
Expected: both projects build. If MSBuild rejects the `.wixproj` entry in the slnx (unknown project type), add `Type="B7DD6F7E-DEF8-4E67-B5B7-07EF123DB6F0"` to that `<Project>` element and rebuild; record the outcome.

- [ ] **Step 7: Commit**

```bash
git add TrayLeds.slnx README.md AGENTS.md
git commit -m "Document the installer and add it to the solution."
```

---

### Task 4: [Windows] End-to-end verification

Deliverable: the spec's manual checklist run on Windows, with results recorded. All steps are **[Windows]**; the implementer hands this list to the user and records their results in the final report. Do not mark this task complete without the user's results.

**Files:** none (if a fallback from the spec is needed, stop and bring it back as a new task — do not implement it inside this task).

- [ ] **Check 1:** `dotnet build installer -c Release` succeeds. `wix msi decompile <msi> -o out.wxs` (or Orca) shows only `TrayLeds.exe` and `TrayLeds.task.xml` in the File table.
- [ ] **Check 2:** Clean install as an admin: icon appears without logging off; `schtasks /Query /TN TrayLeds /XML` shows `<Command>C:\Program Files\TrayLeds\TrayLeds.exe</Command>`, `S-1-5-32-545`, `HighestAvailable`.
- [ ] **Check 3:** Log off and on: icon appears; with an elevated terminal focused, toggling Caps/Num/Scroll Lock updates the icon.
- [ ] **Check 4:** Log on as a standard (non-admin) user: icon appears, no UAC prompt; `Get-Process TrayLeds -IncludeUserName` shows a process for that user.
- [ ] **Check 5:** Bump `<VersionPrefix>` (e.g. `0.9.0` → `0.9.1`) locally, rebuild, install over the running version: exactly one `TrayLeds` process for the user afterwards, one task, no ghost icon (verification item b). Revert the version bump afterwards; do not commit it.
- [ ] **Check 6:** Uninstall: `schtasks /Query /TN TrayLeds` errors, `C:\Program Files\TrayLeds` is gone, no ghost icon.
- [ ] **Check 7:** With 0.9.1 installed, install the 0.9.0 MSI: blocked with "A newer version of TrayLeds is already installed."
- [ ] **Check 8:** Verification item (a): after a clean install, `Get-Process TrayLeds | Select-Object Id, SessionId` shows the process in the installing user's session (compare with `(Get-Process -Id $PID).SessionId`).
- [ ] **Check 9:** Uninstall; create the task by hand with the README script, start it; then install the MSI: one task whose command is the Program Files path, one icon.
- [ ] **Check 10:** Install; run `Unregister-ScheduledTask -TaskName TrayLeds -Confirm:$false`; uninstall: uninstall succeeds (exit code 0, `uninstall.log` shows `DeleteTask` ignored failure).
