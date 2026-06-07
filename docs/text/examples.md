# 예제 코드

## hello.koa

```koa
시작
    출력 "안녕하세요, SKKOA!"
끝
```

## variables.koa

```koa
시작
    변수 x: 정수 = 10
    변수 y: 정수 = 20
    출력 x + y
끝
```

## condition.koa

```koa
시작
    변수 score: 정수 = 85

    만약 score >= 80 이면
        출력 "통과"
    아니면
        출력 "실패"
    끝
끝
```

## loop.koa

```koa
시작
    변수 i: 정수 = 0

    동안 i < 5 반복
        출력 i
        i = i + 1
    끝
끝
```

## function.koa

```koa
함수 더하기(a: 정수, b: 정수): 정수
    반환 a + b
끝

시작
    변수 result: 정수 = 더하기(3, 4)
    출력 result
끝
```

## array.koa

```koa
시작
    변수 numbers: 정수[3]

    numbers[0] = 10
    numbers[1] = 20
    numbers[2] = 30

    출력 numbers[0] + numbers[1] + numbers[2]
끝
```

## input.koa

```koa
시작
    변수 age: 정수
    입력 age

    만약 age >= 20 이면
        출력 "성인"
    아니면
        출력 "미성년"
    끝
끝
```

## repeat.koa

```koa
시작
    반복 i: 1부터 3까지
        출력 i
    끝
끝
```

## strings.koa

```koa
시작
    변수 first: 문자열 = "안녕"
    변수 second: 문자열 = "SKKOA"
    변수 message: 문자열 = first + ", " + second
    출력 message
끝
```

## float.koa

```koa
시작
    변수 x: 실수 = 1.5
    변수 y: 실수 = 2.25
    출력 x + y
끝
```

## pointer.koa

```koa
시작
    변수 x: 정수 = 42
    변수 p: 포인터<정수> = 주소(x)
    출력 값(p)
끝
```
