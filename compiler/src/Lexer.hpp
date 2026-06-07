#ifndef SKKOA_LEXER_HPP
#define SKKOA_LEXER_HPP

#include "ErrorReporter.hpp"
#include "Token.hpp"

#include <string>
#include <unordered_map>
#include <vector>

using namespace std;

class Lexer {
  public:
    Lexer(string source, ErrorReporter &errors);
    vector<Token> tokenize();

  private:
    string source_;
    size_t index_ = 0;
    int line_ = 1;
    int column_ = 1;
    ErrorReporter &errors_;
    unordered_map<string, TokenType> keywords_;

    bool isAtEnd() const;
    char currentByte() const;
    char peekByte(size_t offset = 1) const;
    SourceLocation currentLocation() const;
    string advanceUtf8();
    bool matchByte(char expected);
    void skipIgnored();
    Token makeToken(TokenType type, const string &lexeme,
                    SourceLocation location) const;
    Token number();
    Token stringLiteral();
    Token charLiteral();
    Token identifier();
    bool isIdentifierBoundary(char byte) const;
};

#endif
