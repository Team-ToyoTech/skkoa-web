# SKKOA Studio

SKKOA Studio is the Windows desktop IDE for SKKOA; LTW. It includes a Scintilla-based editor, bundled compiler integration, diagnostics, native run support, a minimal SKKOA step debugger, and a Windows installer.

## Structure

```text
skkoa-studio/
  editor/
    SkkoaStudio.sln
    src/
    tools/skkoa/
    assets/
    docs/
  installer/
    SkkoaStudioInstaller.sln
    src/SkkoaStudio.Installer/
    assets/
    scripts/
    output/
    docs/
  build-editor.ps1
  build-installer.ps1
  build-all.ps1
```

## Features

- Multi-tab `.koa` editing
- SKKOA syntax highlighting and Ctrl+Space completion
- Auto indentation and document formatting
- Real-time compiler diagnostics with Problems navigation
- Light/Dark theme support, defaulting to Dark
- Primary brand color `#a259ff`
- Single-file and `.skkoaproj` project mode
- Bundled `skkoa.exe` plus standard modules
- Compile, run, stdin/stdout console, and stop
- Minimal language-level step debugger
- Installer registration for Start Menu, Apps & Features, uninstall, and file associations

## Build Editor

Required build tools:

- .NET 8 SDK
- Python 3 with `skkoa-studio/installer/scripts/requirements.txt`
- MinGW-w64/MSYS2 `g++`

```powershell
python -m pip install -r .\skkoa-studio\installer\scripts\requirements.txt
.\skkoa-studio\build-editor.ps1
```

This restores, tests, publishes, verifies required assets, and creates:

```text
skkoa-studio/editor/artifacts/SKKOA-Studio-win-x64/
skkoa-studio/editor/artifacts/SKKOA-Studio-win-x64.zip
```

## Build Installer

```powershell
.\skkoa-studio\build-installer.ps1
```

The installer build uses Inno Setup. If `ISCC.exe` is missing and Chocolatey is available, the script attempts to install Inno Setup.

Output:

```text
skkoa-studio/installer/output/SKKOA-Studio-Setup-x64.exe
```

## Build All

```powershell
.\skkoa-studio\build-all.ps1
```

`build-all.ps1` also copies the generated installer to the static website path:

```text
download/studio/SKKOA-Studio-Setup-x64.exe
```

The public download page for the IDE is:

```text
download/studio/index.html
```

## Install

Run `SKKOA-Studio-Setup-x64.exe`. The installer defaults to:

```text
%LocalAppData%\Programs\SKKOA Studio
```

It can create Start Menu and desktop shortcuts, associate `.koa` and `.skkoaproj`, optionally add the bundled compiler to the user PATH, and launch Studio after install.

## Uninstall

Use Windows Settings > Apps > Installed apps, or the Start Menu uninstall shortcut if selected.

## File Associations

- `.koa`: `SKKOA Source File`, opens in SKKOA Studio
- `.skkoaproj`: `SKKOA Studio Project`, opens as a project

`SkkoaStudio.exe <file.koa>` opens files in tabs. `SkkoaStudio.exe <project.skkoaproj>` opens the project.

## Limitations

- Step debugging supports the core education-oriented subset. Native compile/run remains the full compiler path.
- The installer currently produces an Inno Setup EXE. MSI is not generated in this implementation.
- NASM/GCC are required for native linking; Studio offers a toolchain repair/install action.
