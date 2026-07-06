# SKKOA Studio Installer

## Technology Choice

The installer uses Inno Setup. WiX v5 was considered first, but Inno Setup was selected for this repository because it provides a compact scriptable EXE installer with install directory selection, Start Menu shortcuts, Apps & Features registration, uninstall support, file associations, icon setup, and CI-friendly command-line builds.

The output is:

```text
skkoa-studio/installer/output/SKKOA-Studio-Setup-x64.exe
```

MSI output is not generated in this implementation.

## Install Flow

1. Welcome
2. Install location
3. Options/tasks
4. Ready summary
5. Progress
6. Finish

## Options

- Create Start Menu shortcut
- Create desktop shortcut
- Associate `.koa`
- Associate `.skkoaproj`
- Add bundled `tools\skkoa`, NASM, and GCC folders to the user PATH
- Reset existing `%APPDATA%\SKKOA Studio\settings.json`
- Launch SKKOA Studio after setup

## Install Location Policy

The default location is:

```text
%LocalAppData%\Programs\SKKOA Studio
```

The installer allows choosing another directory. Current-user installs do not require administrator rights. The Inno Setup privileges override dialog is intentionally disabled because it appears before theme code can run and would show a bright system dialog. Run the installer as administrator when a protected machine-wide location is required.

The installer payload includes `tools\skkoa\skkoa.exe`, the standard `.koa` modules, and the bundled MSYS2 NASM/GCC toolchain under `tools\skkoa\toolchain\msys64`. Native compile/run works after setup without a separate toolchain download.

## Administrator Rights

The installer defaults to `PrivilegesRequired=lowest`. This keeps setup in the current-user path by default and avoids an unthemed privilege-selection dialog. For machine-wide installs, start the setup executable from an elevated shell.

## File Associations

Associations are written under `Software\Classes` through Inno Setup's `HKA` root, so they follow the selected per-user or machine install context.

- `.koa` maps to `SKKOAStudio.koa`
- `.skkoaproj` maps to `SKKOAStudio.project`

Both use `SkkoaStudio.exe "%1"` as the open command.

## Start Menu and App Registration

Inno Setup registers `SKKOA Studio` with a fixed `AppId`, `AppName`, version, publisher, uninstall icon, and uninstall command. This makes the app appear in Windows Settings > Apps > Installed apps and Windows search.

## Uninstall

The generated uninstaller removes installed files, shortcuts, registered ProgIDs, and the compiler/toolchain PATH entries added by the installer.

## Icons

Editor and file icons are generated from the SKKOA logo PNG. The installer icon adds a download badge in the lower-right corner.

Generation script:

```powershell
skkoa-studio/installer/scripts/generate-icons.ps1
```

Generated installer files:

```text
installer/assets/icons/skkoa-installer.svg
installer/assets/icons/skkoa-installer.ico
```

The `.ico` includes 16, 24, 32, 48, 64, 128, and 256 pixel images.

## Theme

The installer applies the editor dark palette to wizard pages, task lists, edit boxes, labels, generated wizard bitmap assets, and the DWM title bar where supported by Windows. The palette uses `#111111`, `#1b1b1f`, `#24242a`, `#33333a`, and the SKKOA primary color `#a259ff`.
