# 자료구조

자료구조(data structure)는 데이터를 목적에 맞게 저장하고 다루는 방법이다. SKKOA에서는 자료구조를 언어 내장 문법보다 표준 라이브러리 또는 예제 코드로 제공하는 방향을 우선한다.

## 배열(Array)

배열은 현재 컴파일러에서 지원한다.

```koa
변수 numbers: 정수[3]
numbers[0] = 10
```

## 스택(Stack)

스택은 마지막에 넣은 값을 먼저 꺼내는 LIFO(Last In, First Out) 구조이다.

```koa
시작
    변수 stack: 정수[10]
    변수 top: 정수 = 0

    stack[top] = 10
    top = top + 1

    stack[top] = 20
    top = top + 1

    top = top - 1
    출력 stack[top]
끝
```

## 큐(Queue)

큐는 먼저 넣은 값을 먼저 꺼내는 FIFO(First In, First Out) 구조이다. 현재는 배열과 `front`, `back` 변수를 이용해 예제로 학습한다.

## 연결 리스트(Linked List)

연결 리스트는 노드(node)가 다음 노드의 주소를 가리키는 구조이다. 현재 컴파일러는 구조체와 포인터를 지원하므로 개념 학습은 가능하지만, 편의 표준 라이브러리는 아직 제공하지 않는다.

## 딕셔너리/맵(Dictionary/Map)

키(key)로 값을 찾는 구조이다. 해시 테이블(hash table) 기반 표준 라이브러리 기능으로 확장할 예정이다.
