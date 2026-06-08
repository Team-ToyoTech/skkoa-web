#!/bin/bash
set -euo pipefail

BASE_URL="${SKKOA_BASE_URL:-https://skkoa.toyotech.dev/compiler}"
INSTALL_ROOT="${SKKOA_INSTALL_ROOT:-$HOME/.skkoa}"
SOURCE_DIR="$INSTALL_ROOT/source"
BUILD_DIR="$INSTALL_ROOT/build"
BIN_DIR="${SKKOA_BIN_DIR:-$HOME/.local/bin}"
if [[ -d /usr/local/bin && -w /usr/local/bin ]]; then
    BIN_DIR="${SKKOA_BIN_DIR:-/usr/local/bin}"
fi

FILES=(
    "CMakeLists.txt"
    "src/Ast.hpp"
    "src/CodeGenerator.cpp"
    "src/CodeGenerator.hpp"
    "src/ErrorReporter.cpp"
    "src/ErrorReporter.hpp"
    "src/Lexer.cpp"
    "src/Lexer.hpp"
    "src/main.cpp"
    "src/Parser.cpp"
    "src/Parser.hpp"
    "src/SemanticAnalyzer.cpp"
    "src/SemanticAnalyzer.hpp"
    "src/Token.hpp"
)

info() {
    printf '[skkoa] %s\n' "$1"
}

fail() {
    printf '[skkoa] error: %s\n' "$1" >&2
    exit 1
}

find_cxx() {
    if [[ -n "${CXX:-}" ]] && command -v "$CXX" >/dev/null 2>&1; then
        printf '%s\n' "$CXX"
        return
    fi
    for candidate in clang++ g++ c++; do
        if command -v "$candidate" >/dev/null 2>&1; then
            printf '%s\n' "$candidate"
            return
        fi
    done
    fail "C++17 compiler not found. Install Xcode Command Line Tools with: xcode-select --install"
}

download_file() {
    local path="$1"
    local url="$BASE_URL/$path"
    local dest="$SOURCE_DIR/$path"
    mkdir -p "$(dirname "$dest")"
    curl -fsSL "$url" -o "$dest"
}

download_sources() {
    rm -rf "$SOURCE_DIR"
    mkdir -p "$SOURCE_DIR/src"
    for path in "${FILES[@]}"; do
        download_file "$path"
    done
}

build_compiler() {
    local cxx
    cxx="$(find_cxx)"
    rm -rf "$BUILD_DIR"
    mkdir -p "$BUILD_DIR"

    if command -v cmake >/dev/null 2>&1; then
        info "Building with CMake"
        cmake -S "$SOURCE_DIR" -B "$BUILD_DIR" -DCMAKE_BUILD_TYPE=Release
        cmake --build "$BUILD_DIR" --config Release
    else
        info "CMake not found. Building directly with $cxx"
        "$cxx" -std=c++17 -O2 -Wall -Wextra -pedantic \
            "$SOURCE_DIR/src/main.cpp" \
            "$SOURCE_DIR/src/ErrorReporter.cpp" \
            "$SOURCE_DIR/src/Lexer.cpp" \
            "$SOURCE_DIR/src/Parser.cpp" \
            "$SOURCE_DIR/src/SemanticAnalyzer.cpp" \
            "$SOURCE_DIR/src/CodeGenerator.cpp" \
            -I "$SOURCE_DIR/src" \
            -o "$BUILD_DIR/skkoa"
    fi
}

install_command() {
    local built_bin="$BUILD_DIR/skkoa"
    if [[ ! -x "$built_bin" ]]; then
        fail "Build succeeded but $built_bin was not created."
    fi

    mkdir -p "$BIN_DIR"
    cp "$built_bin" "$BIN_DIR/skkoa"
    chmod +x "$BIN_DIR/skkoa"
}

warn_runtime_tools() {
    local missing=()
    for tool in nasm gcc; do
        if ! command -v "$tool" >/dev/null 2>&1; then
            missing+=("$tool")
        fi
    done
    if (( ${#missing[@]} > 0 )); then
        printf '[skkoa] warning: missing runtime tool(s): %s\n' "${missing[*]}" >&2
        printf '[skkoa] warning: SKKOA uses NASM and GCC when it links a .koa file into an executable.\n' >&2
    fi
}

info "Installing SKKOA compiler for macOS"
download_sources
build_compiler
install_command
warn_runtime_tools
info "Installed: $BIN_DIR/skkoa"
if [[ ":$PATH:" != *":$BIN_DIR:"* ]]; then
    info "Add this to your shell profile if skkoa is not found:"
    printf 'export PATH="%s:$PATH"\n' "$BIN_DIR"
fi
info "Try: skkoa hello.koa"
echo
read -r -p "Press Enter to close this window..." _
