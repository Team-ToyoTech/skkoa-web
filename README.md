# SKKOA

SKKOA는 **Starter Kit with Korean Oriented Architecture**의 약자로, 한국어 기반 문법을 사용하는 교육용 프로그래밍 언어이다.

이 저장소는 다음을 포함한다.

- SKKOA 소개 웹사이트
- Markdown 기반 문서 웹 페이지
- 브라우저용 문법 미리보기 / 예제 실행 시뮬레이터
- C++17로 작성된 x86-64용 NASM 기반 CLI 컴파일러

## 컴파일러 빌드

수동 빌드에 필요한 도구:

- C++17 컴파일러
- CMake
- NASM
- GCC 또는 Clang
- x86-64 Windows, Linux, macOS 환경

웹사이트의 Download 버튼은 Windows 통합 설치 파일 `skkoa-windows.cmd`를 기본으로 내려받는다. 운영체제별 파일은 `/download/` 페이지에서 선택할 수 있다.

- Windows: `skkoa-windows.cmd`가 SKKOA CLI, MSYS2 GCC, NASM을 함께 설치한다.
- macOS: `skkoa-macos.command`가 Xcode Command Line Tools, Homebrew, NASM을 확인하고 설치한다.
- Linux: `skkoa-linux.sh`가 배포판 패키지 관리자로 GCC와 NASM을 설치한다.

설치 파일은 필요한 도구를 함께 준비하므로 별도로 C++ 컴파일러, NASM, GCC를 미리 설치하지 않아도 된다. 인터넷 연결과 OS별 설치 권한은 필요하다.

Linux 터미널에서는 다음처럼 받을 수 있다.

```bash
curl -fsSL https://skkoa.toyotech.dev/compiler/download/skkoa-install.sh -o skkoa-install.sh
bash skkoa-install.sh
skkoa hello.koa
```

저장소를 직접 받은 경우에는 아래처럼 수동 빌드할 수 있다.

```bash
cd compiler
cmake -S . -B build
cmake --build build
./build/skkoa examples/hello.koa -o hello
./hello
```

## 중간 산출물

```bash
./build/skkoa examples/hello.koa --emit-asm -o hello.asm
./build/skkoa examples/function.koa --emit-ast
```

## 현재 지원 기능

- `.koa` 파일 읽기
- 토큰화, 파싱, AST 생성, 의미 분석
- 정수 변수와 상수
- 실수, 문자, 문자열 변수
- 정수 산술/비교 연산
- 실수 산술/비교 연산
- 문자열 결합
- 논리 값과 논리 연산
- 문자열 리터럴 출력
- 정수/논리 입력
- 조건문, `동안` 반복문
- `반복 i: 0부터 10까지` 횟수 반복
- 정수 반환 함수
- 고정 크기 정수 배열
- `주소(x)`, `값(p)` 기본 포인터 읽기
- `할당(크기)`, `해제(p)` 기본 동적 메모리 호출
- NASM 어셈블리 생성과 실행 파일 링크

## 제한 사항

구조체, 모듈 시스템, 문자열 입력, 포인터 쓰기(`값(p) = ...`)는 아직 예정 기능이다.

## 테스트

```bash
cd compiler
./tests/run_tests.sh
```

웹사이트: <https://skkoa.toyotech.dev/>
