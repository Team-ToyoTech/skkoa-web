# SKKOA Studio Editor

SKKOA Studio is a Windows desktop IDE for SKKOA; LTW `.koa` files. SKKOA; LTW stands for Starter Kit with Korean Oriented Architecture; Language to Write. It includes a WinForms UI, Scintilla-based editor, compiler integration, diagnostics, native run support, and a minimal language-level step debugger.

## Requirements

- Windows x64
- .NET 8 SDK for building
- Visual Studio 2022 or `dotnet` CLI

The app looks for the bundled compiler at `tools/skkoa/skkoa.exe` relative to the app folder. The bundled NASM/GCC toolchain is expected at `tools/skkoa/toolchain/msys64/`. The repository copy is under `editor/tools/skkoa/`.

## Build

```powershell
cd skkoa-studio/editor
dotnet restore
dotnet build .\SkkoaStudio.sln -c Release
```

## Run

```powershell
cd skkoa-studio/editor
dotnet run --project .\src\SkkoaStudio\SkkoaStudio.csproj
```

## Publish Windows x64

```powershell
cd skkoa-studio/editor
dotnet publish .\src\SkkoaStudio\SkkoaStudio.csproj -c Release -r win-x64 --self-contained false
```

The publish output includes `tools/skkoa/skkoa.exe`, `stack.koa`, `queue.koa`, `structures.koa`, the Windows toolchain repair script, and the bundled MSYS2 NASM/GCC toolchain.

## Toolchain

`--check` diagnostics only need the bundled compiler. Native compile/run also needs NASM and GCC, which are included in installer builds under `tools/skkoa/toolchain/msys64`. SKKOA Studio still exposes `Tools > Install/Repair Toolchain` as a recovery action if a development checkout or damaged install is missing the bundled toolchain.

## Main Features

- Multi-tab `.koa` editing
- UTF-8 save/load
- SKKOA syntax highlighting
- Ctrl+Space completion and snippets
- Auto indentation, format document, format selection
- Real-time `skkoa.exe --check --diagnostics-json` diagnostics
- Problems panel navigation and editor underlines
- Light/Dark themes and font options
- Single-file and `.skkoaproj` project mode
- Compile and run through bundled compiler
- Console stdin/stdout integration
- Breakpoints and minimal line-level step debugging

## Tests

```powershell
cd skkoa-studio/editor
dotnet test .\SkkoaStudio.sln
```
