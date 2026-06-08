# 전체 문법 요약

아래 문법은 학습용으로 단순화한 EBNF(Extended Backus-Naur Form)이다.

```ebnf
program        = { import_decl | struct_decl | function } start_block ;
import_decl    = "가져오기" string ;
start_block    = "시작" { statement } "끝" ;

struct_decl    = "구조체" identifier { struct_field } "끝" ;
struct_field   = identifier ":" type ;

function       = "함수" identifier "(" [ params ] ")" ":" type
                 { statement } "끝" ;
params         = param { "," param } ;
param          = identifier ":" type ;

statement      = var_decl
               | const_decl
               | assignment
               | field_assignment
               | pointer_assignment
               | print
               | input
               | if_stmt
               | while_stmt
               | repeat_stmt
               | return_stmt ;

var_decl       = "변수" identifier ":" type [ "=" expression ] ;
const_decl     = "상수" identifier ":" type "=" expression ;
assignment     = identifier [ "[" expression "]" ] "=" expression ;
field_assignment = identifier "." identifier "=" expression ;
pointer_assignment = "값" "(" expression ")" "=" expression ;
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
               | "포인터" "<" type ">"
               | identifier ;

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
               | "[" [ expression { "," expression } ] "]"
               | identifier
               | identifier "." identifier
               | identifier "(" [ arguments ] ")"
               | identifier "[" expression "]"
               | "(" expression ")" ;
arguments      = expression { "," expression } ;
```

## 현재 컴파일러 지원 범위

현재 컴파일러는 위 문법 중 정수, 실수, 논리, 문자, 문자열, 입력, 조건문, 동안 반복, 횟수 반복, 함수, 배열, 배열 리터럴, 포인터 읽기/쓰기, 구조체, 모듈 가져오기를 지원한다.

## 향후 확장 계획

- 구조체 리터럴
- 구조체 값 반환
- 포인터 산술(pointer arithmetic)
- 네임스페이스(namespace)를 가진 모듈 시스템
- 자료구조 표준 라이브러리 확장
