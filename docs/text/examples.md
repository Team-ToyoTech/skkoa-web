# 예제

## hello.koa

```koa
시작
    출력 "안녕하세요, SKKOA!"
끝
```

## 변수

```koa
시작
    변수 x: 정수 = 10
    변수 y: 정수 = 20
    출력 x + y
끝
```

## 조건문

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

## 반복

```koa
시작
    반복 i: 1부터 3까지
        출력 i
    끝
끝
```

## 함수

```koa
함수 더하기(a: 정수, b: 정수): 정수
    반환 a + b
끝

시작
    출력 더하기(3, 4)
끝
```

## 배열

```koa
시작
    변수 numbers: 정수[] = [1, 2, 3]
    출력 numbers[0] + numbers[1] + numbers[2]
끝
```

## 스택

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

## 큐

```koa
가져오기 "queue.koa"

시작
    변수 queue: 정수큐
    변수 data: 정수[5]

    큐초기화(queue, 5)
    큐넣기(queue, data, 10)
    큐넣기(queue, data, 20)
    출력 큐빼기(queue, data)
끝
```
