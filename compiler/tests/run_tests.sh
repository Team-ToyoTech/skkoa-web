#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BUILD_DIR="$ROOT_DIR/build"
BIN="$BUILD_DIR/skkoa"
TMP_DIR="$ROOT_DIR/tests/tmp"

require_tool() {
    if ! command -v "$1" >/dev/null 2>&1; then
        echo "필수 도구를 찾을 수 없습니다: $1" >&2
        exit 1
    fi
}

require_tool cmake
require_tool nasm
require_tool gcc

cmake -S "$ROOT_DIR" -B "$BUILD_DIR"
cmake --build "$BUILD_DIR"

rm -rf "$TMP_DIR"
mkdir -p "$TMP_DIR"

run_case() {
    local name="$1"
    local expected="$2"
    local stdin="${3:-}"
    local source="$ROOT_DIR/examples/$name.koa"
    local output="$TMP_DIR/$name"

    "$BIN" "$source" -o "$output" >/dev/null

    if [[ ! -f "$output.asm" ]]; then
        echo "실패: $name.asm 파일이 생성되지 않았습니다." >&2
        exit 1
    fi
    if [[ ! -x "$output" ]]; then
        echo "실패: $name 실행 파일이 생성되지 않았습니다." >&2
        exit 1
    fi

    local actual
    if [[ -n "$stdin" ]]; then
        actual="$(printf "%s" "$stdin" | "$output")"
    else
        actual="$("$output")"
    fi
    if [[ "$actual" != "$expected" ]]; then
        echo "실패: $name 실행 결과가 다릅니다." >&2
        echo "예상: $expected" >&2
        echo "실제: $actual" >&2
        exit 1
    fi
    echo "통과: $name"
}

run_case "hello" "안녕하세요, SKKOA!"
run_case "variables" "30"
run_case "condition" "통과"
run_case "loop" $'0\n1\n2\n3\n4'
run_case "function" "7"
run_case "array" "60"
run_case "input" "성인" $'21\n'
run_case "repeat" $'1\n2\n3'
run_case "strings" "안녕, SKKOA"
run_case "float" "3.750000"
run_case "char" "A"
run_case "pointer" "42"
run_case "pointer_write" "25"
run_case "array_literal" $'10\n15'
run_case "function_params" $'SKKOA\n3.500000\n9\n6'
run_case "struct" $'SKKOA\n5\n5'
run_case "module" "21"
run_case "string_input" "Daniel" $'Daniel\n'
run_case "stdlib_strings" $'5\n0\nSK'

if "$BIN" "$ROOT_DIR/tests/invalid_syntax.koa" --emit-asm >/dev/null 2>&1; then
    echo "실패: invalid_syntax.koa가 실패해야 합니다." >&2
    exit 1
fi
echo "통과: invalid_syntax"

if "$BIN" "$ROOT_DIR/tests/undefined_variable.koa" --emit-asm >/dev/null 2>&1; then
    echo "실패: undefined_variable.koa가 실패해야 합니다." >&2
    exit 1
fi
echo "통과: undefined_variable"

echo "모든 테스트 통과"
