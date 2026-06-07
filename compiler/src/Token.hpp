#ifndef SKKOA_TOKEN_HPP
#define SKKOA_TOKEN_HPP

#include <string>

using namespace std;

struct SourceLocation {
    int line = 1;
    int column = 1;
};

enum class TokenType {
    EndOfFile,
    NewLine,
    Identifier,
    Number,
    String,
    Char,

    Start,
    End,
    Var,
    Const,
    Print,
    Input,
    If,
    Then,
    Else,
    ElseIf,
    While,
    Repeat,
    Function,
    Return,

    TypeInt,
    TypeFloat,
    TypeBool,
    TypeChar,
    TypeString,
    TypeVoid,
    TrueLiteral,
    FalseLiteral,
    And,
    Or,
    Not,
    Address,
    Value,
    Pointer,
    From,
    To,

    LeftParen,
    RightParen,
    LeftBracket,
    RightBracket,
    Colon,
    Comma,
    Assign,
    Plus,
    Minus,
    Star,
    Slash,
    Percent,
    Equal,
    NotEqual,
    Less,
    LessEqual,
    Greater,
    GreaterEqual
};

struct Token {
    TokenType type = TokenType::EndOfFile;
    string lexeme;
    SourceLocation location;
};

inline string tokenTypeName(TokenType type) {
    switch (type) {
    case TokenType::EndOfFile:
        return "파일 끝";
    case TokenType::NewLine:
        return "줄바꿈";
    case TokenType::Identifier:
        return "식별자";
    case TokenType::Number:
        return "숫자";
    case TokenType::String:
        return "문자열";
    case TokenType::Char:
        return "문자";
    case TokenType::Start:
        return "시작";
    case TokenType::End:
        return "끝";
    case TokenType::Var:
        return "변수";
    case TokenType::Const:
        return "상수";
    case TokenType::Print:
        return "출력";
    case TokenType::Input:
        return "입력";
    case TokenType::If:
        return "만약";
    case TokenType::Then:
        return "이면";
    case TokenType::Else:
        return "아니면";
    case TokenType::ElseIf:
        return "아니면만약";
    case TokenType::While:
        return "동안";
    case TokenType::Repeat:
        return "반복";
    case TokenType::Function:
        return "함수";
    case TokenType::Return:
        return "반환";
    case TokenType::TypeInt:
        return "정수";
    case TokenType::TypeFloat:
        return "실수";
    case TokenType::TypeBool:
        return "논리";
    case TokenType::TypeChar:
        return "문자";
    case TokenType::TypeString:
        return "문자열";
    case TokenType::TypeVoid:
        return "없음";
    case TokenType::TrueLiteral:
        return "참";
    case TokenType::FalseLiteral:
        return "거짓";
    case TokenType::And:
        return "그리고";
    case TokenType::Or:
        return "또는";
    case TokenType::Not:
        return "아님";
    case TokenType::Address:
        return "주소";
    case TokenType::Value:
        return "값";
    case TokenType::Pointer:
        return "포인터";
    case TokenType::From:
        return "부터";
    case TokenType::To:
        return "까지";
    case TokenType::LeftParen:
        return "(";
    case TokenType::RightParen:
        return ")";
    case TokenType::LeftBracket:
        return "[";
    case TokenType::RightBracket:
        return "]";
    case TokenType::Colon:
        return ":";
    case TokenType::Comma:
        return ",";
    case TokenType::Assign:
        return "=";
    case TokenType::Plus:
        return "+";
    case TokenType::Minus:
        return "-";
    case TokenType::Star:
        return "*";
    case TokenType::Slash:
        return "/";
    case TokenType::Percent:
        return "%";
    case TokenType::Equal:
        return "==";
    case TokenType::NotEqual:
        return "!=";
    case TokenType::Less:
        return "<";
    case TokenType::LessEqual:
        return "<=";
    case TokenType::Greater:
        return ">";
    case TokenType::GreaterEqual:
        return ">=";
    }
    return "알 수 없는 토큰";
}

#endif
