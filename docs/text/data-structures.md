# 자료구조

자료구조는 값을 편하게 넣고 꺼내는 방법입니다.

SKKOA는 설치하면 스택과 큐 모듈을 함께 제공합니다.

```koa
가져오기 "stack.koa"
가져오기 "queue.koa"
```

둘 다 한 번에 쓰고 싶다면 아래처럼 가져올 수도 있습니다.

```koa
가져오기 "structures.koa"
```

## 스택

스택은 나중에 넣은 값을 먼저 꺼냅니다.

```koa
가져오기 "stack.koa"

시작
    변수 stack: 정수스택
    변수 data: 정수[5]

    스택초기화(stack, 5)
    스택넣기(stack, data, 10)
    스택넣기(stack, data, 20)

    출력 스택빼기(stack, data)
    출력 스택빼기(stack, data)
끝
```

위 코드는 `20`, `10` 순서로 출력합니다.

자주 쓰는 함수:

- `스택초기화(stack, 크기)`
- `스택넣기(stack, data, 값)`
- `스택빼기(stack, data)`
- `스택보기(stack, data)`
- `스택비었나(stack)`
- `스택가득찼나(stack)`
- `스택크기(stack)`

## 큐

큐는 먼저 넣은 값을 먼저 꺼냅니다.

```koa
가져오기 "queue.koa"

시작
    변수 queue: 정수큐
    변수 data: 정수[5]

    큐초기화(queue, 5)
    큐넣기(queue, data, 10)
    큐넣기(queue, data, 20)

    출력 큐빼기(queue, data)
    출력 큐빼기(queue, data)
끝
```

위 코드는 `10`, `20` 순서로 출력합니다.

자주 쓰는 함수:

- `큐초기화(queue, 크기)`
- `큐넣기(queue, data, 값)`
- `큐빼기(queue, data)`
- `큐보기(queue, data)`
- `큐비었나(queue)`
- `큐가득찼나(queue)`
- `큐크기(queue)`

## data 배열은 왜 필요한가요?

현재 스택과 큐는 값을 담을 배열을 직접 준비합니다.

```koa
변수 data: 정수[5]
스택초기화(stack, 5)
```

배열 크기와 초기화할 때 넣는 크기를 같게 맞추면 됩니다.
