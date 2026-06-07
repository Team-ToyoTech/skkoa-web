# 시작하기

## 설치

현재 네이티브 컴파일러는 Linux x86-64 환경을 최소 대상으로 한다.

필요한 도구:

- C++17 컴파일러
- CMake
- NASM
- GCC 또는 Clang

## 빌드

```bash
cd compiler
cmake -S . -B build
cmake --build build
```

## 실행

```bash
./build/skkoa examples/hello.koa -o hello
./hello
```

## 중간 산출물 보기

어셈블리(assembly)만 보고 싶다면 다음 명령을 사용한다.

```bash
./build/skkoa examples/hello.koa --emit-asm -o hello.asm
```

AST(Abstract Syntax Tree)를 보고 싶다면 다음 명령을 사용한다.

```bash
./build/skkoa examples/function.koa --emit-ast
```

## 웹 컴파일러

브라우저 페이지는 실제 네이티브 C++ 컴파일러를 실행하지 않는다. 웹 컴파일러는 **문법 미리보기 / 예제 실행 시뮬레이터**이고, 실제 실행 파일 생성은 로컬 CLI 컴파일러에서 수행한다.
