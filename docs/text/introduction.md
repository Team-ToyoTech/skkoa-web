# 소개

**SKKOA**는 **Starter Kit with Korean Oriented Architecture**의 약자이다. 한국어 기반 명령어를 사용해 초보자가 프로그램의 흐름을 자연어에 가깝게 읽을 수 있도록 설계했다.

SKKOA는 단순히 “한글로 출력하는 언어”가 아니라 컴퓨터 과학(computer science)의 기본 개념을 단계적으로 배우는 언어를 목표로 한다.

## 설계 철학

- 문법은 한국어 키워드를 중심으로 한다.
- 한 줄의 의미가 초보자에게 바로 읽혀야 한다.
- 변수(variable), 조건문(conditional statement), 반복문(loop), 함수(function), 배열(array)을 단계적으로 배울 수 있어야 한다.
- 포인터(pointer)와 자료구조(data structure)는 어렵지만 컴퓨터 구조 이해를 위해 문서와 예제에서 다룬다.
- 현재 컴파일러에서 실제로 동작하는 기능과 예정 기능을 명확히 구분한다.

## 파일 확장자

SKKOA 소스 파일은 `.koa` 확장자를 사용한다.

```text
hello.koa
variables.koa
loop.koa
```

## 기본 프로그램

```koa
시작
    출력 "안녕하세요, SKKOA!"
끝
```

`시작`은 프로그램 실행 지점(entry point)이고, `끝`은 블록(block)의 끝을 나타낸다.
