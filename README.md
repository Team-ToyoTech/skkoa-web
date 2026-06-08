# SKKOA

SKKOA는 **Starter Kit with Korean Oriented Architecture**의 약자로, 한국어 기반 문법을 사용하는 교육용 프로그래밍 언어이다.

이 저장소는 다음을 포함한다.

- SKKOA 소개 웹사이트
- Markdown 기반 문서 웹 페이지
- 브라우저용 문법 미리보기 / 예제 실행 시뮬레이터
- C++17로 작성된 x86-64용 NASM 기반 CLI 컴파일러
- 다운로드 설치 시 함께 제공되는 표준 모듈(`stack.koa`, `queue.koa`)

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
또한 설치 파일은 표준 모듈을 함께 설치한다.

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

## 표준 모듈

SKKOA는 `가져오기 "파일.koa"` 문법으로 다른 파일을 가져온다.

```koa
가져오기 "stack.koa"
가져오기 "queue.koa"
```

다운로드 설치 후에는 아래 모듈을 바로 사용할 수 있다.

- `stack.koa`: `정수스택`, `스택초기화`, `스택넣기`, `스택빼기`, `스택보기`, `스택비었나`, `스택가득찼나`, `스택크기`
- `queue.koa`: `정수큐`, `큐초기화`, `큐넣기`, `큐빼기`, `큐보기`, `큐비었나`, `큐가득찼나`, `큐크기`
- `structures.koa`: 스택과 큐를 한 번에 가져오는 묶음 모듈

현재 스택과 큐는 정수 전용 모듈이다. 값을 저장할 배열은 사용자가 준비하고, 구조체는 위치와 크기 같은 상태를 보관한다.

```koa
가져오기 "stack.koa"

시작
    변수 stack: 정수스택
    변수 data: 정수[5]

    스택초기화(stack, 5)
    스택넣기(stack, data, 10)
    스택넣기(stack, data, 20)
    출력 스택빼기(stack, data)
끝
```

모듈 검색 순서:

1. 가져오기를 작성한 `.koa` 파일의 폴더
2. `SKKOA_LIB_PATH` 환경 변수에 들어 있는 폴더
3. `SKKOA_HOME/lib`
4. `SKKOA_INSTALL_ROOT/lib`
5. 기본 설치 위치: Windows `%LOCALAPPDATA%\SKKOA\lib`, Linux/macOS `$HOME/.skkoa/lib`
6. 개발용 현재 작업 폴더의 `lib`, `compiler/lib`

확장자를 생략하면 `.koa`를 자동으로 붙여서 한 번 더 찾는다. 예를 들어 `가져오기 "stack"`도 `stack.koa`를 찾는다. 같은 모듈을 여러 번 가져와도 한 번만 포함한다.

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
- 정수/논리/실수/문자/문자열/포인터 입력
- 조건문, `동안` 반복문
- `반복 i: 0부터 10까지` 횟수 반복
- 정수, 실수, 논리, 문자, 문자열, 포인터, 배열 매개변수 함수
- 구조체 변수, 필드 읽기/쓰기, 구조체 참조 매개변수
- 고정 크기 배열과 배열 리터럴 초기화/재대입
- `주소(x)`, `값(p)` 포인터 읽기와 `값(p) = ...` 포인터 쓰기
- `할당(크기)`, `해제(p)` 기본 동적 메모리 호출
- 문자열 표준 함수 `길이`, `비교`, `부분문자열`
- `가져오기 "파일.koa"` 모듈 가져오기
- `stack.koa`, `queue.koa`, `structures.koa` 표준 모듈
- NASM 어셈블리 생성과 실행 파일 링크

## 제한 사항

- 표준 자료구조 모듈은 현재 정수 스택과 정수 큐를 우선 제공한다.
- 스택과 큐는 내부 저장 배열을 구조체 필드로 숨기지 않고, 사용자가 정수 배열을 함께 넘기는 방식이다.
- 구조체 배열, 구조체 값 반환, 구조체 리터럴은 현재 제한한다.
- 모듈은 별도 네임스페이스 없이 `가져오기 "파일.koa"` 형태로 소스를 병합한다.

## 테스트

```bash
cd compiler
./tests/run_tests.sh
```

웹사이트: <https://skkoa.toyotech.dev/>
