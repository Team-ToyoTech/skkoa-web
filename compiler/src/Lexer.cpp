#include "Lexer.hpp"

#include <cctype>

using namespace std;

Lexer::Lexer(string source, ErrorReporter &errors)
    : source_(move(source)), errors_(errors) {
    keywords_ = {
        {"시작", TokenType::Start},       {"끝", TokenType::End},
        {"변수", TokenType::Var},         {"상수", TokenType::Const},
        {"출력", TokenType::Print},       {"입력", TokenType::Input},
        {"만약", TokenType::If},          {"이면", TokenType::Then},
        {"아니면", TokenType::Else},      {"아니면만약", TokenType::ElseIf},
        {"동안", TokenType::While},       {"반복", TokenType::Repeat},
        {"함수", TokenType::Function},    {"반환", TokenType::Return},
        {"구조체", TokenType::Struct},
        {"정수", TokenType::TypeInt},     {"실수", TokenType::TypeFloat},
        {"논리", TokenType::TypeBool},    {"문자", TokenType::TypeChar},
        {"문자열", TokenType::TypeString},
        {"없음", TokenType::TypeVoid},    {"참", TokenType::TrueLiteral},
        {"거짓", TokenType::FalseLiteral},
        {"그리고", TokenType::And},       {"또는", TokenType::Or},
        {"아님", TokenType::Not},         {"주소", TokenType::Address},
        {"값", TokenType::Value},         {"포인터", TokenType::Pointer},
        {"부터", TokenType::From},         {"까지", TokenType::To},
    };
}

vector<Token> Lexer::tokenize() {
    vector<Token> tokens;

    while (!isAtEnd()) {
        skipIgnored();
        if (isAtEnd()) {
            break;
        }

        SourceLocation location = currentLocation();
        char byte = currentByte();

        if (byte == '\n') {
            advanceUtf8();
            tokens.push_back(makeToken(TokenType::NewLine, "\n", location));
            continue;
        }

        if (isdigit(static_cast<unsigned char>(byte))) {
            tokens.push_back(number());
            continue;
        }

        if (byte == '"') {
            tokens.push_back(stringLiteral());
            continue;
        }

        if (byte == '\'') {
            tokens.push_back(charLiteral());
            continue;
        }

        if (!isIdentifierBoundary(byte)) {
            tokens.push_back(identifier());
            continue;
        }

        advanceUtf8();
        switch (byte) {
        case '(':
            tokens.push_back(makeToken(TokenType::LeftParen, "(", location));
            break;
        case ')':
            tokens.push_back(makeToken(TokenType::RightParen, ")", location));
            break;
        case '[':
            tokens.push_back(makeToken(TokenType::LeftBracket, "[", location));
            break;
        case ']':
            tokens.push_back(makeToken(TokenType::RightBracket, "]", location));
            break;
        case ':':
            tokens.push_back(makeToken(TokenType::Colon, ":", location));
            break;
        case ',':
            tokens.push_back(makeToken(TokenType::Comma, ",", location));
            break;
        case '.':
            tokens.push_back(makeToken(TokenType::Dot, ".", location));
            break;
        case '+':
            tokens.push_back(makeToken(TokenType::Plus, "+", location));
            break;
        case '-':
            tokens.push_back(makeToken(TokenType::Minus, "-", location));
            break;
        case '*':
            tokens.push_back(makeToken(TokenType::Star, "*", location));
            break;
        case '%':
            tokens.push_back(makeToken(TokenType::Percent, "%", location));
            break;
        case '/':
            tokens.push_back(makeToken(TokenType::Slash, "/", location));
            break;
        case '=':
            if (matchByte('=')) {
                tokens.push_back(makeToken(TokenType::Equal, "==", location));
            } else {
                tokens.push_back(makeToken(TokenType::Assign, "=", location));
            }
            break;
        case '!':
            if (matchByte('=')) {
                tokens.push_back(makeToken(TokenType::NotEqual, "!=", location));
            } else {
                errors_.error(location, "알 수 없는 문자 '!'를 발견했습니다.",
                              "'!=' 비교 연산자를 쓰려면 등호를 함께 적어야 합니다.");
            }
            break;
        case '<':
            if (matchByte('=')) {
                tokens.push_back(makeToken(TokenType::LessEqual, "<=", location));
            } else {
                tokens.push_back(makeToken(TokenType::Less, "<", location));
            }
            break;
        case '>':
            if (matchByte('=')) {
                tokens.push_back(makeToken(TokenType::GreaterEqual, ">=", location));
            } else {
                tokens.push_back(makeToken(TokenType::Greater, ">", location));
            }
            break;
        default:
            errors_.error(location, "알 수 없는 문자를 발견했습니다.",
                          "SKKOA 키워드, 식별자, 숫자, 문자열 또는 지원되는 연산자를 사용하세요.");
            break;
        }
    }

    tokens.push_back(makeToken(TokenType::EndOfFile, "", currentLocation()));
    return tokens;
}

bool Lexer::isAtEnd() const {
    return index_ >= source_.size();
}

char Lexer::currentByte() const {
    return isAtEnd() ? '\0' : source_[index_];
}

char Lexer::peekByte(size_t offset) const {
    size_t position = index_ + offset;
    return position >= source_.size() ? '\0' : source_[position];
}

SourceLocation Lexer::currentLocation() const {
    return {line_, column_};
}

string Lexer::advanceUtf8() {
    if (isAtEnd()) {
        return "";
    }

    unsigned char first = static_cast<unsigned char>(source_[index_]);
    size_t length = 1;
    if ((first & 0b11100000) == 0b11000000) {
        length = 2;
    } else if ((first & 0b11110000) == 0b11100000) {
        length = 3;
    } else if ((first & 0b11111000) == 0b11110000) {
        length = 4;
    }

    string result = source_.substr(index_, length);
    index_ += length;
    if (result == "\n") {
        line_++;
        column_ = 1;
    } else {
        column_++;
    }
    return result;
}

bool Lexer::matchByte(char expected) {
    if (isAtEnd() || currentByte() != expected) {
        return false;
    }
    advanceUtf8();
    return true;
}

void Lexer::skipIgnored() {
    bool advanced = true;
    while (advanced && !isAtEnd()) {
        advanced = false;
        while (!isAtEnd()) {
            char byte = currentByte();
            if (byte == ' ' || byte == '\t' || byte == '\r') {
                advanceUtf8();
                advanced = true;
            } else {
                break;
            }
        }

        if (currentByte() == '#') {
            while (!isAtEnd() && currentByte() != '\n') {
                advanceUtf8();
            }
            advanced = true;
        } else if (currentByte() == '/' && peekByte() == '/') {
            while (!isAtEnd() && currentByte() != '\n') {
                advanceUtf8();
            }
            advanced = true;
        } else if (currentByte() == '/' && peekByte() == '*') {
            SourceLocation start = currentLocation();
            advanceUtf8();
            advanceUtf8();
            bool closed = false;
            while (!isAtEnd()) {
                if (currentByte() == '*' && peekByte() == '/') {
                    advanceUtf8();
                    advanceUtf8();
                    closed = true;
                    break;
                }
                advanceUtf8();
            }
            if (!closed) {
                errors_.error(start, "닫히지 않은 여러 줄 주석입니다.",
                              "'*/'로 주석을 닫아야 합니다.");
            }
            advanced = true;
        }
    }
}

Token Lexer::makeToken(TokenType type, const string &lexeme,
                       SourceLocation location) const {
    return {type, lexeme, location};
}

Token Lexer::number() {
    SourceLocation location = currentLocation();
    string value;
    while (!isAtEnd() && isdigit(static_cast<unsigned char>(currentByte()))) {
        value += advanceUtf8();
    }
    if (!isAtEnd() && currentByte() == '.' &&
        isdigit(static_cast<unsigned char>(peekByte()))) {
        value += advanceUtf8();
        while (!isAtEnd() &&
               isdigit(static_cast<unsigned char>(currentByte()))) {
            value += advanceUtf8();
        }
    }
    return makeToken(TokenType::Number, value, location);
}

Token Lexer::stringLiteral() {
    SourceLocation location = currentLocation();
    advanceUtf8();
    string value;

    while (!isAtEnd() && currentByte() != '"') {
        if (currentByte() == '\n') {
            errors_.error(location, "닫히지 않은 문자열입니다.",
                          "문자열은 같은 줄에서 큰따옴표로 닫아야 합니다.");
            return makeToken(TokenType::String, value, location);
        }
        if (currentByte() == '\\') {
            advanceUtf8();
            if (isAtEnd()) {
                break;
            }
            char escaped = currentByte();
            if (escaped == 'n') {
                value += '\n';
            } else if (escaped == 't') {
                value += '\t';
            } else if (escaped == '"' || escaped == '\\') {
                value += escaped;
            } else {
                value += escaped;
            }
            advanceUtf8();
        } else {
            value += advanceUtf8();
        }
    }

    if (isAtEnd()) {
        errors_.error(location, "닫히지 않은 문자열입니다.",
                      "문자열 끝에 큰따옴표를 추가하세요.");
        return makeToken(TokenType::String, value, location);
    }

    advanceUtf8();
    return makeToken(TokenType::String, value, location);
}

Token Lexer::charLiteral() {
    SourceLocation location = currentLocation();
    advanceUtf8();
    string value;

    if (isAtEnd() || currentByte() == '\n') {
        errors_.error(location, "닫히지 않은 문자 리터럴입니다.",
                      "문자는 작은따옴표로 감싸야 합니다. 예: 'A'");
        return makeToken(TokenType::Char, value, location);
    }

    if (currentByte() == '\\') {
        advanceUtf8();
        if (isAtEnd()) {
            errors_.error(location, "닫히지 않은 문자 리터럴입니다.");
            return makeToken(TokenType::Char, value, location);
        }
        char escaped = currentByte();
        if (escaped == 'n') {
            value += '\n';
        } else if (escaped == 't') {
            value += '\t';
        } else {
            value += escaped;
        }
        advanceUtf8();
    } else {
        value += advanceUtf8();
    }

    if (isAtEnd() || currentByte() != '\'') {
        errors_.error(location, "문자 리터럴은 작은따옴표로 닫아야 합니다.",
                      "예: 'A'");
        return makeToken(TokenType::Char, value, location);
    }

    advanceUtf8();
    return makeToken(TokenType::Char, value, location);
}

Token Lexer::identifier() {
    SourceLocation location = currentLocation();
    string value;
    while (!isAtEnd() && !isIdentifierBoundary(currentByte())) {
        value += advanceUtf8();
    }

    auto keyword = keywords_.find(value);
    if (keyword != keywords_.end()) {
        return makeToken(keyword->second, value, location);
    }
    return makeToken(TokenType::Identifier, value, location);
}

bool Lexer::isIdentifierBoundary(char byte) const {
    if (byte == '\0') {
        return true;
    }
    if (isspace(static_cast<unsigned char>(byte))) {
        return true;
    }
    switch (byte) {
    case '(':
    case ')':
    case '[':
    case ']':
    case ':':
    case ',':
    case '.':
    case '+':
    case '-':
    case '*':
    case '/':
    case '%':
    case '=':
    case '!':
    case '<':
    case '>':
    case '"':
    case '#':
        return true;
    default:
        return false;
    }
}
