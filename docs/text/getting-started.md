# 시작하기

## 설치

SKKOA는 `/download/` 페이지에서 운영체제별 통합 설치 파일을 제공한다.

- Windows: `skkoa-windows.cmd`
- macOS: `skkoa-macos.command`
- Linux: `skkoa-linux.sh`

설치 파일은 SKKOA 컴파일러와 어셈블리 변환/링크에 필요한 도구를 함께 설치한다. 따라서 C++17 컴파일러, NASM, GCC를 미리 준비하지 않아도 된다. 단, 도구 다운로드를 위한 인터넷 연결과 OS별 설치 권한은 필요하다.

설치 후 새 터미널에서 다음 명령을 사용할 수 있다.

```bash
skkoa hello.koa
```

## 수동 빌드

저장소에서 직접 빌드하려면 다음 도구가 필요하다.

- C++17 컴파일러
- CMake
- NASM
- GCC 또는 Clang

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
