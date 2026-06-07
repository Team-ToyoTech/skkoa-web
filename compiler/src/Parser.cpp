#include "Parser.hpp"

#include <cstdlib>
#include <initializer_list>

using namespace std;

Parser::Parser(vector<Token> tokens, ErrorReporter &errors)
    : tokens_(move(tokens)), errors_(errors) {}

unique_ptr<Program> Parser::parse() {
    auto program = make_unique<Program>();
    bool hasMain = false;

    skipNewLines();
    while (!isAtEnd()) {
        if (match(TokenType::Function)) {
            current_--;
            auto function = parseFunction();
            if (function) {
                program->functions.push_back(move(function));
            }
        } else if (match(TokenType::Start)) {
            if (hasMain) {
                errors_.error(previous().location,
                              "'시작' 블록은 프로그램에 하나만 둘 수 있습니다.");
            }
            hasMain = true;
            skipNewLines();
            program->mainStatements =
                parseBlock({TokenType::End}, "시작");
            consume(TokenType::End, "'시작' 블록을 닫는 '끝'이 필요합니다.",
                    "프로그램 본문 마지막 줄에 '끝'을 추가하세요.");
        } else {
            errors_.error(peek().location,
                          "최상위에는 '함수' 정의 또는 '시작' 블록만 올 수 있습니다.",
                          "실행할 코드는 '시작'과 '끝' 사이에 작성하세요.");
            synchronize();
        }
        skipNewLines();
    }

    if (!hasMain) {
        errors_.error({1, 1}, "프로그램에 '시작' 블록이 없습니다.",
                      "SKKOA 프로그램은 '시작'으로 시작하고 '끝'으로 닫아야 합니다.");
    }

    return program;
}

bool Parser::isAtEnd() const {
    return peek().type == TokenType::EndOfFile;
}

const Token &Parser::peek() const {
    return tokens_[current_];
}

const Token &Parser::previous() const {
    return tokens_[current_ - 1];
}

const Token &Parser::advance() {
    if (!isAtEnd()) {
        current_++;
    }
    return previous();
}

bool Parser::check(TokenType type) const {
    return !isAtEnd() && peek().type == type;
}

bool Parser::checkAny(initializer_list<TokenType> types) const {
    for (TokenType type : types) {
        if (check(type)) {
            return true;
        }
    }
    return false;
}

bool Parser::match(TokenType type) {
    if (check(type)) {
        advance();
        return true;
    }
    return false;
}

bool Parser::matchAny(initializer_list<TokenType> types) {
    for (TokenType type : types) {
        if (match(type)) {
            return true;
        }
    }
    return false;
}

bool Parser::consume(TokenType type, const string &message, const string &hint) {
    if (check(type)) {
        advance();
        return true;
    }
    errors_.error(peek().location, message, hint);
    return false;
}

void Parser::synchronize() {
    while (!isAtEnd() && !check(TokenType::NewLine) && !check(TokenType::End)) {
        advance();
    }
    skipNewLines();
}

void Parser::skipNewLines() {
    while (match(TokenType::NewLine)) {
    }
}

unique_ptr<FunctionDecl> Parser::parseFunction() {
    SourceLocation location = peek().location;
    consume(TokenType::Function, "'함수' 키워드가 필요합니다.", "");

    if (!check(TokenType::Identifier)) {
        errors_.error(peek().location, "함수 이름이 필요합니다.",
                      "예: 함수 더하기(a: 정수, b: 정수): 정수");
        synchronize();
        return nullptr;
    }

    auto function = make_unique<FunctionDecl>();
    function->location = location;
    function->name = advance().lexeme;

    consume(TokenType::LeftParen, "함수 이름 뒤에는 '('가 필요합니다.",
            "매개변수가 없어도 '()'를 작성하세요.");
    if (!check(TokenType::RightParen)) {
        do {
            if (!check(TokenType::Identifier)) {
                errors_.error(peek().location, "매개변수 이름이 필요합니다.",
                              "예: a: 정수");
                synchronize();
                return function;
            }
            Token name = advance();
            consume(TokenType::Colon, "매개변수 이름 뒤에는 ':'가 필요합니다.",
                    "예: a: 정수");
            TypeName type = parseType();
            function->params.push_back({name.lexeme, type, name.location});
        } while (match(TokenType::Comma));
    }
    consume(TokenType::RightParen, "매개변수 목록 뒤에는 ')'가 필요합니다.", "");
    consume(TokenType::Colon, "함수 반환 자료형 앞에는 ':'가 필요합니다.",
            "예: 함수 더하기(a: 정수, b: 정수): 정수");
    function->returnType = parseType();

    skipNewLines();
    function->body = parseBlock({TokenType::End}, "함수");
    consume(TokenType::End, "함수 블록을 닫는 '끝'이 필요합니다.",
            "함수 본문 마지막에 '끝'을 추가하세요.");
    return function;
}

vector<unique_ptr<Stmt>> Parser::parseBlock(initializer_list<TokenType> terminators,
                                            const string &blockName) {
    vector<unique_ptr<Stmt>> statements;
    skipNewLines();

    while (!isAtEnd() && !checkAny(terminators)) {
        auto statement = parseStatement();
        if (statement) {
            statements.push_back(move(statement));
        } else {
            synchronize();
        }
        skipNewLines();
    }

    if (isAtEnd()) {
        errors_.error(previous().location,
                      "'" + blockName + "' 블록이 닫히기 전에 파일이 끝났습니다.",
                      "열린 블록마다 '끝'을 작성했는지 확인하세요.");
    }
    return statements;
}

unique_ptr<Stmt> Parser::parseStatement() {
    if (match(TokenType::Var)) {
        current_--;
        return parseVarDecl(false);
    }
    if (match(TokenType::Const)) {
        current_--;
        return parseVarDecl(true);
    }
    if (check(TokenType::Identifier)) {
        if (current_ + 1 < tokens_.size() &&
            tokens_[current_ + 1].type == TokenType::LeftParen) {
            return parseExpressionStatement();
        }
        return parseAssignment();
    }
    if (match(TokenType::Print)) {
        current_--;
        return parsePrint();
    }
    if (match(TokenType::Input)) {
        current_--;
        return parseInput();
    }
    if (match(TokenType::If)) {
        current_--;
        return parseIf();
    }
    if (match(TokenType::While)) {
        current_--;
        return parseWhile();
    }
    if (match(TokenType::Repeat)) {
        current_--;
        return parseRepeat();
    }
    if (match(TokenType::Return)) {
        current_--;
        return parseReturn();
    }
    errors_.error(peek().location, "예상하지 못한 토큰 '" + peek().lexeme + "'을 발견했습니다.",
                  "변수 선언, 대입, 출력, 조건문, 반복문 또는 반환문을 작성하세요.");
    synchronize();
    return nullptr;
}

unique_ptr<Stmt> Parser::parseVarDecl(bool isConst) {
    SourceLocation location = peek().location;
    advance();

    if (!check(TokenType::Identifier)) {
        errors_.error(peek().location, "변수 이름이 필요합니다.",
                      "변수 선언은 '변수 이름: 자료형 = 값' 형식이어야 합니다.");
        return nullptr;
    }
    Token name = advance();
    consume(TokenType::Colon, "변수 이름 뒤에는 ':'가 필요합니다.",
            "예: 변수 x: 정수 = 10");
    TypeName type = parseType();

    auto statement = make_unique<VarDeclStmt>(location, isConst, name.lexeme, type);
    if (match(TokenType::Assign)) {
        statement->initializer = parseExpression();
    }
    return statement;
}

unique_ptr<Stmt> Parser::parseAssignment() {
    Token name = advance();
    auto statement = make_unique<AssignmentStmt>(name.location, name.lexeme);
    if (match(TokenType::LeftBracket)) {
        statement->index = parseExpression();
        consume(TokenType::RightBracket, "배열 인덱스 뒤에는 ']'가 필요합니다.",
                "예: numbers[0] = 10");
    }
    consume(TokenType::Assign, "대입문에는 '='가 필요합니다.",
            "예: x = x + 1");
    statement->value = parseExpression();
    return statement;
}

unique_ptr<Stmt> Parser::parseExpressionStatement() {
    SourceLocation location = peek().location;
    auto statement = make_unique<ExpressionStmt>(location);
    statement->expression = parseExpression();
    return statement;
}

unique_ptr<Stmt> Parser::parsePrint() {
    SourceLocation location = peek().location;
    consume(TokenType::Print, "'출력' 키워드가 필요합니다.", "");
    auto statement = make_unique<PrintStmt>(location);
    statement->expression = parseExpression();
    return statement;
}

unique_ptr<Stmt> Parser::parseInput() {
    SourceLocation location = peek().location;
    consume(TokenType::Input, "'입력' 키워드가 필요합니다.", "");

    if (!check(TokenType::Identifier)) {
        errors_.error(peek().location, "입력 대상 변수 이름이 필요합니다.",
                      "예: 입력 age");
        synchronize();
        return nullptr;
    }

    Token name = advance();
    auto statement = make_unique<InputStmt>(location, name.lexeme);
    if (match(TokenType::LeftBracket)) {
        statement->index = parseExpression();
        consume(TokenType::RightBracket, "배열 인덱스 뒤에는 ']'가 필요합니다.",
                "예: 입력 numbers[0]");
    }
    return statement;
}

unique_ptr<Stmt> Parser::parseIf() {
    SourceLocation location = peek().location;
    consume(TokenType::If, "'만약' 키워드가 필요합니다.", "");

    auto statement = make_unique<IfStmt>(location);
    IfBranch firstBranch;
    firstBranch.condition = parseExpression();
    consume(TokenType::Then, "조건식 뒤에는 '이면'이 필요합니다.",
            "예: 만약 score >= 80 이면");
    skipNewLines();
    firstBranch.body = parseBlock({TokenType::ElseIf, TokenType::Else, TokenType::End},
                                  "만약");
    statement->branches.push_back(move(firstBranch));

    while (match(TokenType::ElseIf)) {
        IfBranch branch;
        branch.condition = parseExpression();
        consume(TokenType::Then, "'아니면만약' 조건 뒤에는 '이면'이 필요합니다.",
                "예: 아니면만약 score >= 80 이면");
        skipNewLines();
        branch.body = parseBlock({TokenType::ElseIf, TokenType::Else, TokenType::End},
                                 "아니면만약");
        statement->branches.push_back(move(branch));
    }

    if (match(TokenType::Else)) {
        skipNewLines();
        statement->elseBody = parseBlock({TokenType::End}, "아니면");
    }

    consume(TokenType::End, "조건문을 닫는 '끝'이 필요합니다.",
            "만약/아니면 블록 마지막에 '끝'을 추가하세요.");
    return statement;
}

unique_ptr<Stmt> Parser::parseWhile() {
    SourceLocation location = peek().location;
    consume(TokenType::While, "'동안' 키워드가 필요합니다.", "");

    auto statement = make_unique<WhileStmt>(location);
    statement->condition = parseExpression();
    consume(TokenType::Repeat, "동안 조건 뒤에는 '반복'이 필요합니다.",
            "예: 동안 i < 5 반복");
    skipNewLines();
    statement->body = parseBlock({TokenType::End}, "동안");
    consume(TokenType::End, "반복문을 닫는 '끝'이 필요합니다.",
            "동안 블록 마지막에 '끝'을 추가하세요.");
    return statement;
}

unique_ptr<Stmt> Parser::parseRepeat() {
    SourceLocation location = peek().location;
    consume(TokenType::Repeat, "'반복' 키워드가 필요합니다.", "");

    if (!check(TokenType::Identifier)) {
        errors_.error(peek().location, "반복 변수 이름이 필요합니다.",
                      "예: 반복 i: 0부터 10까지");
        synchronize();
        return nullptr;
    }

    Token iterator = advance();
    auto statement = make_unique<RepeatStmt>(location, iterator.lexeme);
    consume(TokenType::Colon, "반복 변수 뒤에는 ':'가 필요합니다.",
            "예: 반복 i: 0부터 10까지");
    statement->start = parseExpression();
    consume(TokenType::From, "반복 시작값 뒤에는 '부터'가 필요합니다.",
            "예: 반복 i: 0부터 10까지");
    statement->end = parseExpression();
    consume(TokenType::To, "반복 끝값 뒤에는 '까지'가 필요합니다.",
            "예: 반복 i: 0부터 10까지");
    skipNewLines();
    statement->body = parseBlock({TokenType::End}, "반복");
    consume(TokenType::End, "횟수 반복문을 닫는 '끝'이 필요합니다.",
            "반복 블록 마지막에 '끝'을 추가하세요.");
    return statement;
}

unique_ptr<Stmt> Parser::parseReturn() {
    SourceLocation location = peek().location;
    consume(TokenType::Return, "'반환' 키워드가 필요합니다.", "");

    auto statement = make_unique<ReturnStmt>(location);
    if (!check(TokenType::NewLine) && !check(TokenType::EndOfFile) &&
        !check(TokenType::End)) {
        statement->value = parseExpression();
    }
    return statement;
}

TypeName Parser::parseType() {
    TypeName type;
    type.location = peek().location;

    if (match(TokenType::TypeInt)) {
        type.base = ValueType::Int;
    } else if (match(TokenType::TypeBool)) {
        type.base = ValueType::Bool;
    } else if (match(TokenType::TypeString)) {
        type.base = ValueType::String;
    } else if (match(TokenType::TypeVoid)) {
        type.base = ValueType::Void;
    } else if (match(TokenType::TypeFloat)) {
        type.base = ValueType::Float;
    } else if (match(TokenType::TypeChar)) {
        type.base = ValueType::Char;
    } else if (match(TokenType::Pointer)) {
        type.base = ValueType::Pointer;
        if (match(TokenType::Less)) {
            TypeName inner = parseType();
            type.pointerTarget = inner.base;
            consume(TokenType::Greater, "포인터 자료형은 '>'로 닫아야 합니다.",
                    "예: 포인터<정수>");
        }
    } else {
        errors_.error(peek().location, "자료형이 필요합니다.",
                      "지원되는 1차 자료형은 정수, 논리, 문자열입니다.");
        if (!isAtEnd()) {
            advance();
        }
    }

    if (match(TokenType::LeftBracket)) {
        type.isArray = true;
        if (check(TokenType::Number)) {
            type.arraySize = static_cast<int>(stoll(advance().lexeme));
        }
        consume(TokenType::RightBracket, "배열 자료형은 ']'로 닫아야 합니다.",
                "예: 변수 numbers: 정수[3]");
    }

    return type;
}

unique_ptr<Expr> Parser::parseExpression() {
    return parseOr();
}

unique_ptr<Expr> Parser::parseOr() {
    auto expression = parseAnd();
    while (match(TokenType::Or)) {
        Token op = previous();
        auto right = parseAnd();
        expression = make_unique<BinaryExpr>(op.location, BinaryOp::Or,
                                             move(expression), move(right));
    }
    return expression;
}

unique_ptr<Expr> Parser::parseAnd() {
    auto expression = parseEquality();
    while (match(TokenType::And)) {
        Token op = previous();
        auto right = parseEquality();
        expression = make_unique<BinaryExpr>(op.location, BinaryOp::And,
                                             move(expression), move(right));
    }
    return expression;
}

unique_ptr<Expr> Parser::parseEquality() {
    auto expression = parseComparison();
    while (matchAny({TokenType::Equal, TokenType::NotEqual})) {
        Token op = previous();
        auto right = parseComparison();
        expression = make_unique<BinaryExpr>(op.location, binaryOpFromToken(op.type),
                                             move(expression), move(right));
    }
    return expression;
}

unique_ptr<Expr> Parser::parseComparison() {
    auto expression = parseTerm();
    while (matchAny({TokenType::Less, TokenType::LessEqual, TokenType::Greater,
                     TokenType::GreaterEqual})) {
        Token op = previous();
        auto right = parseTerm();
        expression = make_unique<BinaryExpr>(op.location, binaryOpFromToken(op.type),
                                             move(expression), move(right));
    }
    return expression;
}

unique_ptr<Expr> Parser::parseTerm() {
    auto expression = parseFactor();
    while (matchAny({TokenType::Plus, TokenType::Minus})) {
        Token op = previous();
        auto right = parseFactor();
        expression = make_unique<BinaryExpr>(op.location, binaryOpFromToken(op.type),
                                             move(expression), move(right));
    }
    return expression;
}

unique_ptr<Expr> Parser::parseFactor() {
    auto expression = parseUnary();
    while (matchAny({TokenType::Star, TokenType::Slash, TokenType::Percent})) {
        Token op = previous();
        auto right = parseUnary();
        expression = make_unique<BinaryExpr>(op.location, binaryOpFromToken(op.type),
                                             move(expression), move(right));
    }
    return expression;
}

unique_ptr<Expr> Parser::parseUnary() {
    if (match(TokenType::Minus)) {
        Token op = previous();
        return make_unique<UnaryExpr>(op.location, UnaryOp::Negate, parseUnary());
    }
    if (match(TokenType::Not)) {
        Token op = previous();
        return make_unique<UnaryExpr>(op.location, UnaryOp::Not, parseUnary());
    }
    return parsePrimary();
}

unique_ptr<Expr> Parser::parsePrimary() {
    if (match(TokenType::Number)) {
        Token token = previous();
        if (token.lexeme.find('.') != string::npos) {
            return make_unique<FloatLiteralExpr>(token.location, stod(token.lexeme));
        }
        return make_unique<IntLiteralExpr>(token.location, stoll(token.lexeme));
    }
    if (match(TokenType::String)) {
        Token token = previous();
        return make_unique<StringLiteralExpr>(token.location, token.lexeme);
    }
    if (match(TokenType::Char)) {
        Token token = previous();
        if (token.lexeme.size() != 1) {
            errors_.error(token.location,
                          "현재 문자 리터럴은 ASCII 한 글자만 지원합니다.",
                          "예: 'A'");
            return make_unique<CharLiteralExpr>(token.location, 0);
        }
        return make_unique<CharLiteralExpr>(
            token.location,
            static_cast<unsigned char>(token.lexeme.empty() ? '\0' : token.lexeme[0]));
    }
    if (match(TokenType::TrueLiteral)) {
        Token token = previous();
        return make_unique<BoolLiteralExpr>(token.location, true);
    }
    if (match(TokenType::FalseLiteral)) {
        Token token = previous();
        return make_unique<BoolLiteralExpr>(token.location, false);
    }
    if (match(TokenType::Identifier)) {
        Token name = previous();
        if (match(TokenType::LeftParen)) {
            auto call = make_unique<CallExpr>(name.location, name.lexeme);
            if (!check(TokenType::RightParen)) {
                do {
                    call->arguments.push_back(parseExpression());
                } while (match(TokenType::Comma));
            }
            consume(TokenType::RightParen, "함수 호출 인자 뒤에는 ')'가 필요합니다.",
                    "예: 더하기(3, 4)");
            return call;
        }
        if (match(TokenType::LeftBracket)) {
            auto index = parseExpression();
            consume(TokenType::RightBracket, "배열 인덱스 뒤에는 ']'가 필요합니다.",
                    "예: numbers[0]");
            return make_unique<ArrayAccessExpr>(name.location, name.lexeme,
                                                move(index));
        }
        return make_unique<VariableExpr>(name.location, name.lexeme);
    }
    if (match(TokenType::Address)) {
        Token token = previous();
        consume(TokenType::LeftParen, "'주소' 뒤에는 '('가 필요합니다.",
                "예: 주소(x)");
        if (!check(TokenType::Identifier)) {
            errors_.error(peek().location, "주소를 얻을 변수 이름이 필요합니다.",
                          "예: 주소(x)");
            synchronize();
            return make_unique<IntLiteralExpr>(token.location, 0);
        }
        Token name = advance();
        auto expression = make_unique<AddressExpr>(token.location, name.lexeme);
        if (match(TokenType::LeftBracket)) {
            expression->index = parseExpression();
            consume(TokenType::RightBracket, "배열 인덱스 뒤에는 ']'가 필요합니다.",
                    "예: 주소(numbers[0])");
        }
        consume(TokenType::RightParen, "'주소' 호출은 ')'로 닫아야 합니다.",
                "예: 주소(x)");
        return expression;
    }
    if (match(TokenType::Value)) {
        Token token = previous();
        consume(TokenType::LeftParen, "'값' 뒤에는 '('가 필요합니다.",
                "예: 값(p)");
        auto pointer = parseExpression();
        consume(TokenType::RightParen, "'값' 호출은 ')'로 닫아야 합니다.",
                "예: 값(p)");
        return make_unique<DereferenceExpr>(token.location, move(pointer));
    }
    if (match(TokenType::LeftParen)) {
        auto expression = parseExpression();
        consume(TokenType::RightParen, "괄호로 묶은 식은 ')'로 닫아야 합니다.", "");
        return expression;
    }

    errors_.error(peek().location, "표현식이 필요합니다.",
                  "숫자, 문자열, 변수 이름, 함수 호출 또는 괄호식을 작성하세요.");
    SourceLocation location = peek().location;
    if (!isAtEnd()) {
        advance();
    }
    return make_unique<IntLiteralExpr>(location, 0);
}

BinaryOp Parser::binaryOpFromToken(TokenType type) const {
    switch (type) {
    case TokenType::Plus:
        return BinaryOp::Add;
    case TokenType::Minus:
        return BinaryOp::Subtract;
    case TokenType::Star:
        return BinaryOp::Multiply;
    case TokenType::Slash:
        return BinaryOp::Divide;
    case TokenType::Percent:
        return BinaryOp::Modulo;
    case TokenType::Equal:
        return BinaryOp::Equal;
    case TokenType::NotEqual:
        return BinaryOp::NotEqual;
    case TokenType::Less:
        return BinaryOp::Less;
    case TokenType::LessEqual:
        return BinaryOp::LessEqual;
    case TokenType::Greater:
        return BinaryOp::Greater;
    case TokenType::GreaterEqual:
        return BinaryOp::GreaterEqual;
    case TokenType::And:
        return BinaryOp::And;
    case TokenType::Or:
        return BinaryOp::Or;
    default:
        return BinaryOp::Add;
    }
}
