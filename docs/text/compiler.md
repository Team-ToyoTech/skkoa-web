# 컴파일러

SKKOA 컴파일러는 C++17로 작성된다. 직접 실행하는 인터프리터(interpreter)가 아니라 NASM 어셈블리를 생성한 뒤 실행 파일을 만드는 컴파일러(compiler)이다.

## 컴파일 과정

```text
hello.koa
-> lexical analysis
-> parsing
-> AST
-> semantic analysis
-> x86-64 assembly
-> object file
-> executable
```

## 선택한 어셈블러

SKKOA는 1차 구현에서 **NASM**을 사용한다.

선택 이유:

- Intel 문법이 비교적 읽기 쉽다.
- Linux x86-64 ELF 오브젝트를 `nasm -f elf64`로 만들 수 있다.
- 교육용으로 레지스터(register), 스택(stack), 호출 규약(calling convention)을 설명하기 좋다.

## 빌드와 실행

```bash
cd compiler
cmake -S . -B build
cmake --build build
./build/skkoa examples/hello.koa -o hello
./hello
```

## 어셈블리 생성

```bash
./build/skkoa examples/hello.koa --emit-asm -o hello.asm
```

생성된 어셈블리는 `printf`, `scanf`를 위해 C 표준 라이브러리와 링크한다.

```bash
nasm -f elf64 hello.asm -o hello.o
gcc -no-pie hello.o -o hello
```

## 오류 메시지 설계

오류 메시지는 줄(line), 열(column), 문제 설명, 힌트(hint)를 포함한다.

```text
오류: 3번째 줄 12번째 글자에서 예상하지 못한 토큰을 발견했습니다.
힌트: 변수 선언은 '변수 이름: 자료형 = 값' 형식이어야 합니다.
```

처리 대상:

- 알 수 없는 문자
- 닫히지 않은 문자열
- 잘못된 변수 선언
- 선언되지 않은 변수 사용
- 중복 변수 선언
- 타입 불일치
- 함수 인자 개수 불일치
- 배열 인덱스 오류 가능성
- `시작` / `끝` 블록 불일치
- 조건문 / 반복문 블록 불일치
