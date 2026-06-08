#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${SKKOA_BASE_URL:-https://skkoa.toyotech.dev/compiler}"
INSTALL_ROOT="${SKKOA_INSTALL_ROOT:-$HOME/.skkoa}"
BIN_DIR="${SKKOA_BIN_DIR:-$HOME/.local/bin}"
SOURCE_DIR="$INSTALL_ROOT/source"
BUILD_DIR="$INSTALL_ROOT/build"

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

run_sudo() {
    if [ "$(id -u)" -eq 0 ]; then
        "$@"
    elif command -v sudo >/dev/null 2>&1; then
        sudo "$@"
    else
        fail "sudo is required to install missing toolchain packages."
    fi
}

install_toolchain() {
    if [[ -n "${SKKOA_SKIP_TOOLCHAIN_INSTALL:-}" ]]; then
        return
    fi

    local missing=()
    for tool in g++ gcc nasm; do
        if ! command -v "$tool" >/dev/null 2>&1; then
            missing+=("$tool")
        fi
    done
    if (( ${#missing[@]} == 0 )); then
        return
    fi

    info "Installing compiler, assembler, and linker tools"
    if command -v apt-get >/dev/null 2>&1; then
        run_sudo apt-get update
        run_sudo apt-get install -y build-essential g++ gcc nasm curl ca-certificates
    elif command -v dnf >/dev/null 2>&1; then
        run_sudo dnf install -y gcc gcc-c++ nasm make curl ca-certificates
    elif command -v yum >/dev/null 2>&1; then
        run_sudo yum install -y gcc gcc-c++ nasm make curl ca-certificates
    elif command -v pacman >/dev/null 2>&1; then
        run_sudo pacman -Sy --needed --noconfirm base-devel gcc nasm curl ca-certificates
    elif command -v zypper >/dev/null 2>&1; then
        run_sudo zypper --non-interactive install -y gcc gcc-c++ nasm make curl ca-certificates
    elif command -v apk >/dev/null 2>&1; then
        run_sudo apk add build-base g++ gcc nasm curl ca-certificates
    else
        fail "Unsupported Linux package manager. Install g++, gcc, and nasm manually, then rerun."
    fi
}

find_cxx() {
    if [[ -n "${CXX:-}" ]] && command -v "$CXX" >/dev/null 2>&1; then
        printf '%s\n' "$CXX"
        return
    fi
    for candidate in c++ g++ clang++; do
        if command -v "$candidate" >/dev/null 2>&1; then
            printf '%s\n' "$candidate"
            return
        fi
    done
    fail "C++17 compiler not found. Install g++ or clang++."
}

download_file() {
    local path="$1"
    local url="$BASE_URL/$path"
    local dest="$SOURCE_DIR/$path"
    mkdir -p "$(dirname "$dest")"

    if command -v curl >/dev/null 2>&1; then
        curl -fsSL "$url" -o "$dest"
    elif command -v wget >/dev/null 2>&1; then
        wget -qO "$dest" "$url"
    else
        fail "curl or wget is required to download compiler source files."
    fi
}

copy_or_download_sources() {
    local script_dir local_compiler_dir source_path
    script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
    local_compiler_dir="${SKKOA_LOCAL_COMPILER_DIR:-}"

    if [[ -z "$local_compiler_dir" && -f "$script_dir/../CMakeLists.txt" && -d "$script_dir/../src" ]]; then
        local_compiler_dir="$(cd "$script_dir/.." && pwd)"
    fi

    rm -rf "$SOURCE_DIR"
    mkdir -p "$SOURCE_DIR/src"

    for path in "${FILES[@]}"; do
        if [[ -n "$local_compiler_dir" && -f "$local_compiler_dir/$path" ]]; then
            source_path="$local_compiler_dir/$path"
            mkdir -p "$(dirname "$SOURCE_DIR/$path")"
            cp "$source_path" "$SOURCE_DIR/$path"
        else
            download_file "$path"
        fi
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
        local sources=(
            "$SOURCE_DIR/src/main.cpp"
            "$SOURCE_DIR/src/ErrorReporter.cpp"
            "$SOURCE_DIR/src/Lexer.cpp"
            "$SOURCE_DIR/src/Parser.cpp"
            "$SOURCE_DIR/src/SemanticAnalyzer.cpp"
            "$SOURCE_DIR/src/CodeGenerator.cpp"
        )
        if ! "$cxx" -std=c++17 -O2 -Wall -Wextra -pedantic "${sources[@]}" -I "$SOURCE_DIR/src" -o "$BUILD_DIR/skkoa"; then
            info "Retrying with -lstdc++fs for older GCC"
            "$cxx" -std=c++17 -O2 -Wall -Wextra -pedantic "${sources[@]}" -I "$SOURCE_DIR/src" -o "$BUILD_DIR/skkoa" -lstdc++fs
        fi
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
        printf '[skkoa] warning: skkoa uses NASM and GCC to produce executables from .koa files.\n' >&2
    fi
}

main() {
    info "Installing SKKOA compiler"
    install_toolchain
    copy_or_download_sources
    build_compiler
    install_command
    warn_runtime_tools

    info "Installed: $BIN_DIR/skkoa"
    if [[ ":$PATH:" != *":$BIN_DIR:"* ]]; then
        info "Add this to your shell profile if skkoa is not found:"
        printf 'export PATH="$HOME/.local/bin:$PATH"\n'
    fi
    info "Try: skkoa hello.koa"
}

main "$@"
