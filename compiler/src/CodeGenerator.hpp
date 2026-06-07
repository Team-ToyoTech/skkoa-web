#ifndef SKKOA_CODE_GENERATOR_HPP
#define SKKOA_CODE_GENERATOR_HPP

#include "Ast.hpp"

#include <ostream>
#include <sstream>
#include <string>
#include <unordered_map>
#include <vector>

using namespace std;

class CodeGenerator {
  public:
    string generateAssembly(Program &program, const string &sourceName);
    string generateAstDump(const Program &program);

  private:
    struct LocalSlot {
        TypeName type;
        int offset = 0;
    };

    struct FunctionContext {
        string label;
        string endLabel;
        unordered_map<string, LocalSlot> locals;
        int stackSize = 0;
    };

    struct StringData {
        string label;
        string value;
    };

    struct FloatData {
        string label;
        double value = 0.0;
    };

    ostringstream text_;
    vector<StringData> strings_;
    vector<FloatData> floats_;
    unordered_map<string, string> functionLabels_;
    FunctionContext *current_ = nullptr;
    int labelCounter_ = 0;
    int stringCounter_ = 0;

    void prepareFunctionLabels(Program &program);
    FunctionContext buildContext(const string &label,
                                 const vector<Param> &params,
                                 const vector<unique_ptr<Stmt>> &body);
    void collectLocals(const vector<unique_ptr<Stmt>> &statements,
                       FunctionContext &context, int &offset);
    void collectLocalFromStmt(const Stmt &statement, FunctionContext &context,
                              int &offset);
    int alignTo16(int value) const;

    void emitFunction(FunctionDecl &function);
    void emitMain(Program &program);
    void emitPrologue(const vector<Param> &params, bool isMain);
    void emitEpilogue();
    void emitStatements(const vector<unique_ptr<Stmt>> &statements);
    void emitStatement(const Stmt &statement);
    void emitVarDecl(const VarDeclStmt &statement);
    void emitAssignment(const AssignmentStmt &statement);
    void emitPrint(const PrintStmt &statement);
    void emitInput(const InputStmt &statement);
    void emitIf(const IfStmt &statement);
    void emitWhile(const WhileStmt &statement);
    void emitRepeat(const RepeatStmt &statement);
    void emitReturn(const ReturnStmt &statement);
    void emitExpr(const Expr &expression);
    void emitFloatExpr(const Expr &expression);
    void emitAddress(const AddressExpr &expression);
    void emitArrayAddress(const string &name, const Expr &index);
    void emitStringConcat();

    string newLabel(const string &prefix);
    string addStringLiteral(const string &value);
    string addFloatLiteral(double value);
    string bytesForString(const string &value) const;
    string binaryOpName(BinaryOp op) const;
    string unaryOpName(UnaryOp op) const;
    void dumpStatements(const vector<unique_ptr<Stmt>> &statements,
                        ostringstream &out, int indent) const;
    void dumpStatement(const Stmt &statement, ostringstream &out,
                       int indent) const;
    void dumpExpr(const Expr &expression, ostringstream &out, int indent) const;
    string indent(int count) const;
};

#endif
