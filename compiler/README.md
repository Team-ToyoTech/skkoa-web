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
설치 파일은 `stack.koa`, `queue.koa`, `structures.koa` 표준 모듈도 함께 설치한다.

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
- 정수/논리/실수/문자/문자열/포인터 입력
- 정수, 논리, 실수, 문자, 문자열, 포인터 출력
- `시작` / `끝` 프로그램 구조
- `만약` / `아니면만약` / `아니면` 조건문
- `동안` 반복문
- `반복 i: 0부터 10까지` 횟수 반복
- 정수, 실수, 논리, 문자, 문자열, 포인터, 배열 매개변수 함수 정의와 호출
- 구조체 변수, 필드 읽기/쓰기, 구조체 참조 매개변수
- 고정 크기 배열, 배열 리터럴 초기화, 배열 리터럴 재대입
- `주소(x)`, `값(p)` 포인터 읽기와 `값(p) = ...` 포인터 쓰기
- `할당(크기)`, `해제(p)` 기본 동적 메모리 호출
- 문자열 표준 함수 `길이`, `비교`, `부분문자열`
- `가져오기 "파일.koa"` 모듈 가져오기
- `stack.koa`, `queue.koa`, `structures.koa` 표준 모듈 가져오기
- NASM 어셈블리 생성
- NASM과 GCC를 이용한 실행 파일 생성

## 모듈 시스템

`가져오기 "파일.koa"`는 파싱 전에 대상 파일 내용을 현재 소스 앞에 붙이는 방식으로 동작한다. 모듈 전용 네임스페이스는 아직 없다.

가져오기 해석 순서:

1. 현재 소스 파일의 폴더
2. `SKKOA_LIB_PATH`
3. `SKKOA_HOME/lib`
4. `SKKOA_INSTALL_ROOT/lib`
5. OS별 기본 설치 위치
6. 개발용 `lib`, `compiler/lib`

상대 경로를 찾을 때 확장자가 없으면 `.koa`를 붙인 후보도 함께 확인한다. 순환 가져오기는 오류로 처리하고, 이미 포함된 파일은 다시 포함하지 않는다.

표준 자료구조 모듈:

- `stack.koa`: 정수 스택
- `queue.koa`: 정수 큐
- `structures.koa`: 스택과 큐 묶음

현재 자료구조 모듈은 언어 기능 확장 없이 `.koa` 표준 모듈로 구현한다. 구조체 필드에 배열을 직접 넣을 수 없는 현재 제한 때문에, 스택/큐 상태 구조체와 저장용 정수 배열을 함께 넘기는 API를 사용한다.

## 현재 제한 사항

- 구조체 배열, 구조체 값 반환, 구조체 리터럴은 현재 제한한다.
- 배열 전체 대입은 배열 리터럴만 지원한다. 예: `numbers = [1, 2, 3]`
- 자료구조 표준 라이브러리는 정수 스택과 정수 큐를 우선 제공한다.
- 모듈은 별도 네임스페이스 없이 소스 파일을 병합하는 방식이다.
- 함수 인자는 현재 기본 정수 인자 레지스터 범위만 지원한다. Windows는 최대 4개, Linux/macOS x86-64는 최대 6개이다.
- 대상 플랫폼은 x86-64 Windows, Linux, macOS이다. macOS Apple Silicon에서는 x86-64/Rosetta 환경을 기준으로 한다.

## 테스트

```bash
cd compiler
./tests/run_tests.sh
```

테스트는 예제 컴파일, 어셈블리 생성, 실행 파일 생성, 실행 결과, 실패해야 하는 코드의 실패 여부를 확인한다.
