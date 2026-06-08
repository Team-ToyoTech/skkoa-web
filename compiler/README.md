# SKKOA Compiler

SKKOA는 **Starter Kit with Korean Oriented Architecture**의 약자로, 한국어 기반 명령어와 직관적인 구조를 사용하는 교육용 프로그래밍 언어이다.

이 컴파일러는 `.koa` 파일을 직접 인터프리트하지 않고 다음 단계를 거친다.

```text
.koa 소스
-> lexical analysis
-> parsing
-> AST
-> semantic analysis
-> NASM x86-64 assembly
-> object file
-> executable
```

## 필요한 도구

- x86-64 Windows, Linux, macOS 환경

통합 설치 파일을 사용할 경우 C++17 컴파일러, NASM, GCC/Clang은 설치 파일이 함께 준비한다. 수동 빌드만 직접 도구 설치가 필요하다.

NASM을 선택한 이유는 Intel 문법이 초보자에게 비교적 읽기 쉽고, 같은 코드 생성 흐름에서 Linux `elf64`, Windows `win64`, macOS `macho64` 오브젝트를 만들 수 있기 때문이다. 링크는 C 표준 라이브러리의 `printf`, `scanf`, `malloc`, `free`를 사용하기 위해 OS별 C toolchain을 사용한다.

## 빌드

웹사이트의 `/download/` 페이지에서 운영체제별 통합 설치 파일을 선택할 수 있다.

- Windows: `skkoa-windows.cmd`가 SKKOA CLI, MSYS2 GCC, NASM을 함께 설치한다.
- macOS: `skkoa-macos.command`가 Xcode Command Line Tools, Homebrew, NASM을 확인하고 설치한다.
- Linux: `skkoa-linux.sh`가 배포판 패키지 관리자로 GCC와 NASM을 설치한다.

Windows에서는 메인 화면 Download 버튼이 `skkoa-windows.cmd`를 기본으로 내려받는다. 설치 파일은 필요한 도구를 함께 준비하므로 별도로 C++ 컴파일러, NASM, GCC를 미리 설치하지 않아도 된다. Linux에서는 아래처럼 설치 스크립트를 받을 수 있다.

```bash
curl -fsSL https://skkoa.toyotech.dev/compiler/download/skkoa-install.sh -o skkoa-install.sh
bash skkoa-install.sh
skkoa examples/hello.koa -o hello
./hello
```

브라우저에서 Linux 설치 파일을 받은 경우에도 같은 방식으로 실행한다.

```bash
bash skkoa-install.sh
skkoa hello.koa
```

저장소를 직접 받은 경우에는 아래처럼 수동 빌드할 수 있다.

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

어셈블리만 생성:

```bash
./build/skkoa examples/hello.koa --emit-asm -o hello.asm
```

AST 확인:

```bash
./build/skkoa examples/function.koa --emit-ast
```

## 현재 지원 기능

- `.koa` 파일 읽기
- 토큰화, 파싱, AST 생성
- 정수 변수 선언과 대입
- 실수, 문자, 문자열 변수 선언과 대입
- 정수 산술 연산: `+`, `-`, `*`, `/`, `%`
- 실수 산술 연산: `+`, `-`, `*`, `/`
- 문자열 결합: `+`
- 비교 연산: `==`, `!=`, `<`, `<=`, `>`, `>=`
- 논리 값 `참`, `거짓`과 논리 연산 `그리고`, `또는`, `아님`
- 문자열 리터럴 출력
- 정수/논리 입력
- 정수와 논리 출력
- `시작` / `끝` 프로그램 구조
- `만약` / `아니면만약` / `아니면` 조건문
- `동안` 반복문
- `반복 i: 0부터 10까지` 횟수 반복
- 정수 반환 함수 정의와 호출
- 정수 배열 선언, 원소 대입, 원소 접근
- `주소(x)`, `값(p)` 기본 포인터 읽기
- `할당(크기)`, `해제(p)` 기본 동적 메모리 호출
- NASM 어셈블리 생성
- NASM과 GCC를 이용한 실행 파일 생성

## 현재 제한 사항

- 포인터 쓰기, 구조체, 모듈 시스템은 문서상 설계됨 / 예정 기능이다.
- 배열은 고정 크기 정수 배열만 지원한다.
- 함수 인자는 현재 기본 정수 인자 레지스터 범위만 지원한다. Windows는 최대 4개, Linux/macOS x86-64는 최대 6개이다.
- 대상 플랫폼은 x86-64 Windows, Linux, macOS이다. macOS Apple Silicon에서는 x86-64/Rosetta 환경을 기준으로 한다.

## 테스트

```bash
cd compiler
./tests/run_tests.sh
```

테스트는 예제 컴파일, 어셈블리 생성, 실행 파일 생성, 실행 결과, 실패해야 하는 코드의 실패 여부를 확인한다.
