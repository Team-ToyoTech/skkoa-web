#ifndef SKKOA_PARSER_HPP
#define SKKOA_PARSER_HPP

#include "Ast.hpp"
#include "ErrorReporter.hpp"
#include "Token.hpp"

#include <memory>
#include <vector>

using namespace std;

class Parser {
  public:
    Parser(vector<Token> tokens, ErrorReporter &errors);
    unique_ptr<Program> parse();

  private:
    vector<Token> tokens_;
    size_t current_ = 0;
    ErrorReporter &errors_;

    bool isAtEnd() const;
    const Token &peek() const;
    const Token &previous() const;
    const Token &advance();
    bool check(TokenType type) const;
    bool checkAny(initializer_list<TokenType> types) const;
    bool match(TokenType type);
    bool matchAny(initializer_list<TokenType> types);
    bool consume(TokenType type, const string &message, const string &hint);
    void synchronize();
    void skipNewLines();

    unique_ptr<FunctionDecl> parseFunction();
    vector<unique_ptr<Stmt>> parseBlock(initializer_list<TokenType> terminators,
                                        const string &blockName);
    unique_ptr<Stmt> parseStatement();
    unique_ptr<Stmt> parseVarDecl(bool isConst);
    unique_ptr<Stmt> parseAssignment();
    unique_ptr<Stmt> parseExpressionStatement();
    unique_ptr<Stmt> parsePrint();
    unique_ptr<Stmt> parseInput();
    unique_ptr<Stmt> parseIf();
    unique_ptr<Stmt> parseWhile();
    unique_ptr<Stmt> parseRepeat();
    unique_ptr<Stmt> parseReturn();
    TypeName parseType();

    unique_ptr<Expr> parseExpression();
    unique_ptr<Expr> parseOr();
    unique_ptr<Expr> parseAnd();
    unique_ptr<Expr> parseEquality();
    unique_ptr<Expr> parseComparison();
    unique_ptr<Expr> parseTerm();
    unique_ptr<Expr> parseFactor();
    unique_ptr<Expr> parseUnary();
    unique_ptr<Expr> parsePrimary();
    BinaryOp binaryOpFromToken(TokenType type) const;
};

#endif
