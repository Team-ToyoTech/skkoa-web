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

- C++17 컴파일러
- CMake 3.16 이상
- NASM
- GCC 또는 Clang
- Linux x86-64 환경

NASM을 선택한 이유는 Intel 문법이 초보자에게 비교적 읽기 쉽고, `nasm -f elf64`로 Linux x86-64 오브젝트를 직접 생성할 수 있기 때문이다. 링크는 C 표준 라이브러리의 `printf`를 사용하기 위해 `gcc -no-pie`를 사용한다.

## 빌드

웹사이트에서 설치 스크립트를 내려받아 `skkoa` 명령으로 설치할 수 있다.

```bash
curl -fsSL https://skkoa.toyotech.dev/compiler/download/skkoa-install.sh -o skkoa-install.sh
bash skkoa-install.sh
skkoa examples/hello.koa -o hello
./hello
```

브라우저에서 Download를 눌러 `skkoa-install.sh`를 받은 경우에도 같은 방식으로 실행한다.

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
- 함수 인자는 Linux x86-64 System V ABI의 정수 인자 레지스터 범위에 맞춰 최대 6개까지 지원한다.
- 대상 플랫폼은 Linux x86-64이다.

## 테스트

```bash
cd compiler
./tests/run_tests.sh
```

테스트는 예제 컴파일, 어셈블리 생성, 실행 파일 생성, 실행 결과, 실패해야 하는 코드의 실패 여부를 확인한다.
