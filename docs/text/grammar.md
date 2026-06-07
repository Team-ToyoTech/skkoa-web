# 전체 문법 요약

아래 문법은 학습용으로 단순화한 EBNF(Extended Backus-Naur Form)이다.

```ebnf
program        = { function } start_block ;
start_block    = "시작" { statement } "끝" ;

function       = "함수" identifier "(" [ params ] ")" ":" type
                 { statement } "끝" ;
params         = param { "," param } ;
param          = identifier ":" type ;

statement      = var_decl
               | const_decl
               | assignment
               | print
               | input
               | if_stmt
               | while_stmt
               | repeat_stmt
               | return_stmt ;

var_decl       = "변수" identifier ":" type [ "=" expression ] ;
const_decl     = "상수" identifier ":" type "=" expression ;
assignment     = identifier [ "[" expression "]" ] "=" expression ;
print          = "출력" expression ;
input          = "입력" identifier [ "[" expression "]" ] ;
return_stmt    = "반환" [ expression ] ;

if_stmt        = "만약" expression "이면" { statement }
                 { "아니면만약" expression "이면" { statement } }
                 [ "아니면" { statement } ]
                 "끝" ;

while_stmt     = "동안" expression "반복" { statement } "끝" ;
repeat_stmt    = "반복" identifier ":" expression "부터"
                 expression "까지" { statement } "끝" ;

type           = "정수" [ "[" number "]" ]
               | "논리"
               | "문자열"
               | "없음"
               | "실수"
               | "문자"
               | "포인터" "<" type ">" ;

expression     = logic_or ;
logic_or       = logic_and { "또는" logic_and } ;
logic_and      = equality { "그리고" equality } ;
equality       = comparison { ( "==" | "!=" ) comparison } ;
comparison     = term { ( "<" | "<=" | ">" | ">=" ) term } ;
term           = factor { ( "+" | "-" ) factor } ;
factor         = unary { ( "*" | "/" | "%" ) unary } ;
unary          = ( "-" | "아님" ) unary | primary ;
primary        = number
               | string
               | char
               | "참"
               | "거짓"
               | "주소" "(" identifier [ "[" expression "]" ] ")"
               | "값" "(" expression ")"
               | identifier
               | identifier "(" [ arguments ] ")"
               | identifier "[" expression "]"
               | "(" expression ")" ;
arguments      = expression { "," expression } ;
```

## 현재 컴파일러 지원 범위

현재 컴파일러는 위 문법 중 정수, 실수, 논리, 문자, 문자열, 입력, 조건문, 동안 반복, 횟수 반복, 함수, 정수 배열, 기본 포인터 읽기를 지원한다.

## 향후 확장 계획

- 실수와 문자 연산
- 문자열 변수와 문자열 함수
- 포인터와 동적 메모리
- 구조체
- 모듈 시스템
- 표준 라이브러리 확장
