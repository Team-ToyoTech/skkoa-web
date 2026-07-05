# SKKOA Studio Design Notes

## Repository Analysis

- `compiler/` contains a C++17 CLI compiler. It lexes, parses, performs semantic analysis, emits NASM x86-64 assembly, assembles with NASM, and links with GCC/CC.
- The language uses Korean keywords such as `시작`, `끝`, `변수`, `만약`, `동안`, `반복`, `함수`, and `구조체`.
- `가져오기 "file.koa"` is expanded before lexing. It is not a parser-level module node.
- Standard functions include `길이`, `비교`, `부분문자열`, `배열길이`, `주소`, `값`, `할당`, `해제`.
- Standard modules are pure `.koa` files:
    - `stack.koa`
    - `queue.koa`
    - `structures.koa`
- Windows install scripts already exist under `compiler/download/` and can install MSYS2 GCC/NASM.
- The existing web compiler contains a JavaScript line simulator. SKKOA Studio uses the same broad strategy for debugging: native compile/run remains compiler-based, but step debugging uses a SKKOA-specific execution layer.

## Editor Component

SKKOA Studio uses `Scintilla.NET.WinForms` because it targets modern Windows .NET (`net6.0-windows` and later computed compatibility) and provides line numbers, markers, indicators, styling, completion primitives, and WinForms integration. The app implements SKKOA styling through the Core tokenizer instead of scattering keyword logic in UI code.

## Compiler Integration

The C++ compiler now supports:

- `--check`
- `--diagnostics-json`
- `--emit-ast-json`
- `--no-link`
- `--working-dir <dir>`
- `--lib-path <dir>`

Diagnostics use this shape:

```json
[
    {
        "severity": "error",
        "code": "SKK001",
        "message": "예상한 토큰: 끝",
        "file": "main.koa",
        "line": 12,
        "column": 5,
        "length": 2
    }
]
```

## Debugger

The debugger is implemented in `SkkoaStudio.Core.Debugging`. It interprets normalized SKKOA source and maintains current line, stack frames, local variables, output, and input waiting state.

Implemented for step debugging:

- `시작/끝`
- `변수`, `상수`
- assignment
- integer, float, bool, string, char literals
- arithmetic and comparison expressions
- `출력`
- `입력`
- `만약/아니면`
- `동안`
- `반복`
- simple function calls
- `반환`

Known debugger limitations:

- Native compile/run supports arrays, structs, pointers, and standard modules through the compiler, but step debugging is intentionally limited.
- Array literals and simple array indexing are partially supported in the debugger.
- Struct fields, pointers, imported standard modules, `중단`, and `계속` are reported as limited in step mode.
- Function calls embedded inside larger expressions are not fully stepped into; direct calls such as `변수 x: 정수 = 더하기(1, 2)` are stepped.

## Settings

Settings are stored at:

```text
%APPDATA%\SKKOA Studio\settings.json
```

Invalid values are sanitized on load. Theme, font, tab size, format-on-save, diagnostics-on-type, compiler path, lib path, recent files, and recent projects persist across restarts.

Default settings include:

```json
{
  "theme": "Dark",
  "primaryColor": "#a259ff",
  "fontFamily": "Consolas",
  "fontSize": 12,
  "tabSize": 4,
  "insertSpaces": true,
  "formatOnEnter": true,
  "formatOnSave": false,
  "diagnosticsOnType": true,
  "compilerPath": "",
  "libPath": "",
  "recentFiles": [],
  "recentProjects": []
}
```

The default dark palette is stored in `editor/assets/themes/dark.json`; the light palette is stored in `editor/assets/themes/light.json`.

## Icons

The app icon and file icon are generated from the repository SKKOA logo PNG at `image/skkoa_logo_remove_background_large.png`.

Generated assets:

```text
editor/assets/icons/skkoa.svg
editor/assets/icons/skkoa.ico
editor/assets/icons/skkoa-file.ico
```

The `.ico` files contain 16, 24, 32, 48, 64, 128, and 256 pixel images.

## Project Format

`.skkoaproj` files are JSON:

```json
{
    "name": "HelloSkkoa",
    "entry": "main.koa",
    "files": ["main.koa"],
    "libPaths": ["lib"],
    "outputName": "hello"
}
```

Project builds use the project directory as `--working-dir` and project library paths as `--lib-path`.
