#ifndef SKKOA_SEMANTIC_ANALYZER_HPP
#define SKKOA_SEMANTIC_ANALYZER_HPP

#include "Ast.hpp"
#include "ErrorReporter.hpp"

#include <string>
#include <unordered_map>
#include <vector>

using namespace std;

class SemanticAnalyzer {
  public:
    explicit SemanticAnalyzer(ErrorReporter &errors);
    void analyze(Program &program);

  private:
    struct Symbol {
        TypeName type;
        bool isConst = false;
    };

    struct FunctionInfo {
        TypeName returnType;
        vector<TypeName> params;
        SourceLocation location;
    };

    ErrorReporter &errors_;
    unordered_map<string, FunctionInfo> functions_;
    unordered_map<string, Symbol> symbols_;
    TypeName currentReturnType_;
    bool insideFunction_ = false;

    void analyzeFunction(FunctionDecl &function);
    void analyzeStatements(vector<unique_ptr<Stmt>> &statements);
    void analyzeStatement(Stmt &statement);
    void analyzeVarDecl(VarDeclStmt &statement);
    void analyzeAssignment(AssignmentStmt &statement);
    void analyzeExpressionStatement(ExpressionStmt &statement);
    void analyzePrint(PrintStmt &statement);
    void analyzeInput(InputStmt &statement);
    void analyzeIf(IfStmt &statement);
    void analyzeWhile(WhileStmt &statement);
    void analyzeRepeat(RepeatStmt &statement);
    void analyzeReturn(ReturnStmt &statement);
    ValueType analyzeExpr(Expr &expression);

    bool sameType(ValueType expected, ValueType actual) const;
    bool isIntegerLike(ValueType type) const;
    bool isNumeric(ValueType type) const;
    bool isConditionType(ValueType type) const;
};

#endif
