# SKKOA Studio

SKKOA Studio is the Windows desktop IDE for SKKOA; LTW, short for Starter Kit with Korean Oriented Architecture; Language to Write. It includes a Scintilla-based editor, bundled compiler integration, diagnostics, native run support, a minimal SKKOA step debugger, and a Windows installer.

## Structure

```text
skkoa-studio/
  editor/
    SkkoaStudio.sln
    src/
    tools/skkoa/
    assets/
    docs/
  update/
    New-SkkoaStudioUpdateManifest.ps1
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
- Bundled `skkoa.exe`, standard modules, NASM, and GCC toolchain
- Compile, run, stdin/stdout console, and stop
- Minimal language-level step debugger
- Installer registration for Start Menu, Apps & Features, uninstall, and file associations
- Startup update notification backed by a static update manifest

## Build Editor

Required build tools:

- .NET 8 SDK
- Python 3 with `skkoa-studio/installer/scripts/requirements.txt`
- Existing MinGW-w64/MSYS2 with GCC/NASM, or network access so the script can bootstrap MSYS2

The release build stages a bundled MSYS2 toolchain under `tools/skkoa/toolchain/msys64`.
If an existing MSYS2 install with GCC and NASM is available, it is copied into the
artifact; otherwise the build script downloads MSYS2 and installs the needed packages.

```powershell
python -m pip install -r .\skkoa-studio\installer\scripts\requirements.txt
.\skkoa-studio\build-editor.ps1
```

This restores, tests, publishes, verifies required assets, and creates:

```text
skkoa-studio/editor/artifacts/SKKOA-Studio-win-x64/
skkoa-studio/editor/artifacts/SKKOA-Studio-win-x64.zip
skkoa-studio/editor/artifacts/updates/win-x64/manifest.json
skkoa-studio/editor/artifacts/updates/win-x64/files/
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
download/studio/updates/win-x64/manifest.json
download/studio/updates/win-x64/files/
```

The public download page for the IDE is:

```text
download/studio/index.html
```

## Updates

At startup, SKKOA Studio checks the update manifest once. The default sources are:

```text
https://skkoa.toyotech.dev/download/studio/updates/win-x64/manifest.json
https://raw.githubusercontent.com/Team-ToyoTech/skkoa-web/main/download/studio/updates/win-x64/manifest.json
```

When a newer manifest version is found, the app shows an update dialog. Pressing the update button launches `SkkoaStudio.Updater.exe` from a temporary copy, closes Studio, verifies local file hashes, downloads only files whose SHA-256 differs from the manifest, applies them, and restarts Studio.

`SKKOA_STUDIO_UPDATE_MANIFEST_URL` or `UpdateManifestUrl` in the user settings file can override or prepend custom manifest locations.

Release version is read from `skkoa-studio/editor/src/SkkoaStudio/SkkoaStudio.csproj` by the update manifest and installer build scripts, so bump that project version before publishing a new update.

## Install

Run `SKKOA-Studio-Setup-x64.exe`. The installer defaults to:

```text
%LocalAppData%\Programs\SKKOA Studio
```

It can create Start Menu and desktop shortcuts, associate `.koa` and `.skkoaproj`, optionally add the bundled compiler, NASM, and GCC paths to the user PATH, and launch Studio after install.

## Uninstall

Use Windows Settings > Apps > Installed apps, or the Start Menu uninstall shortcut if selected.

## File Associations

- `.koa`: `SKKOA Source File`, opens in SKKOA Studio
- `.skkoaproj`: `SKKOA Studio Project`, opens as a project

`SkkoaStudio.exe <file.koa>` opens files in tabs. `SkkoaStudio.exe <project.skkoaproj>` opens the project.

## Limitations

- Step debugging supports the core education-oriented subset. Native compile/run remains the full compiler path.
- The installer currently produces an Inno Setup EXE. MSI is not generated in this implementation.
- The installer EXE can be large because it includes the native NASM/GCC toolchain required for compile/run.
