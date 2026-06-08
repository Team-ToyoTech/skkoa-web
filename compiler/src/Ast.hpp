#ifndef SKKOA_AST_HPP
#define SKKOA_AST_HPP

#include "Token.hpp"

#include <memory>
#include <string>
#include <utility>
#include <vector>

using namespace std;

enum class ValueType {
    Int,
    Bool,
    String,
    Void,
    Float,
    Char,
    Pointer,
    Struct,
    Unknown
};

inline string valueTypeName(ValueType type) {
    switch (type) {
    case ValueType::Int:
        return "정수";
    case ValueType::Bool:
        return "논리";
    case ValueType::String:
        return "문자열";
    case ValueType::Void:
        return "없음";
    case ValueType::Float:
        return "실수";
    case ValueType::Char:
        return "문자";
    case ValueType::Pointer:
        return "포인터";
    case ValueType::Struct:
        return "구조체";
    case ValueType::Unknown:
        return "알 수 없음";
    }
    return "알 수 없음";
}

struct TypeName {
    ValueType base = ValueType::Unknown;
    ValueType pointerTarget = ValueType::Unknown;
    string structName;
    bool isArray = false;
    int arraySize = 0;
    SourceLocation location;

    string display() const {
        if (base == ValueType::Struct) {
            string name = structName;
            if (isArray) {
                name += "[" + (arraySize > 0 ? to_string(arraySize) : "") + "]";
            }
            return name;
        }
        string name = valueTypeName(base);
        if (isArray) {
            name += "[" + (arraySize > 0 ? to_string(arraySize) : "") + "]";
        }
        return name;
    }
};

enum class BinaryOp {
    Add,
    Subtract,
    Multiply,
    Divide,
    Modulo,
    Equal,
    NotEqual,
    Less,
    LessEqual,
    Greater,
    GreaterEqual,
    And,
    Or
};

enum class UnaryOp {
    Negate,
    Not
};

struct Expr {
    explicit Expr(SourceLocation loc) : location(loc) {}
    virtual ~Expr() = default;
    SourceLocation location;
    ValueType inferredType = ValueType::Unknown;
};

struct IntLiteralExpr : Expr {
    IntLiteralExpr(SourceLocation loc, long long val) : Expr(loc), value(val) {}
    long long value;
};

struct FloatLiteralExpr : Expr {
    FloatLiteralExpr(SourceLocation loc, double val) : Expr(loc), value(val) {}
    double value;
};

struct StringLiteralExpr : Expr {
    StringLiteralExpr(SourceLocation loc, string val)
        : Expr(loc), value(move(val)) {}
    string value;
};

struct CharLiteralExpr : Expr {
    CharLiteralExpr(SourceLocation loc, long long val) : Expr(loc), value(val) {}
    long long value;
};

struct BoolLiteralExpr : Expr {
    BoolLiteralExpr(SourceLocation loc, bool val) : Expr(loc), value(val) {}
    bool value;
};

struct VariableExpr : Expr {
    VariableExpr(SourceLocation loc, string variableName)
        : Expr(loc), name(move(variableName)) {}
    string name;
};

struct ArrayAccessExpr : Expr {
    ArrayAccessExpr(SourceLocation loc, string arrayName,
                    unique_ptr<Expr> indexExpr)
        : Expr(loc), name(move(arrayName)), index(move(indexExpr)) {}
    string name;
    unique_ptr<Expr> index;
};

struct FieldAccessExpr : Expr {
    FieldAccessExpr(SourceLocation loc, string objectName, string fieldName)
        : Expr(loc), object(move(objectName)), field(move(fieldName)) {}
    string object;
    string field;
};

struct ArrayLiteralExpr : Expr {
    explicit ArrayLiteralExpr(SourceLocation loc) : Expr(loc) {}
    vector<unique_ptr<Expr>> elements;
};

struct BinaryExpr : Expr {
    BinaryExpr(SourceLocation loc, BinaryOp binaryOp, unique_ptr<Expr> leftExpr,
               unique_ptr<Expr> rightExpr)
        : Expr(loc), op(binaryOp), left(move(leftExpr)), right(move(rightExpr)) {}
    BinaryOp op;
    unique_ptr<Expr> left;
    unique_ptr<Expr> right;
};

struct UnaryExpr : Expr {
    UnaryExpr(SourceLocation loc, UnaryOp unaryOp, unique_ptr<Expr> operandExpr)
        : Expr(loc), op(unaryOp), operand(move(operandExpr)) {}
    UnaryOp op;
    unique_ptr<Expr> operand;
};

struct CallExpr : Expr {
    CallExpr(SourceLocation loc, string functionName)
        : Expr(loc), name(move(functionName)) {}
    string name;
    vector<unique_ptr<Expr>> arguments;
};

struct AddressExpr : Expr {
    AddressExpr(SourceLocation loc, string variableName)
        : Expr(loc), name(move(variableName)) {}
    string name;
    unique_ptr<Expr> index;
};

struct DereferenceExpr : Expr {
    DereferenceExpr(SourceLocation loc, unique_ptr<Expr> pointerExpr)
        : Expr(loc), pointer(move(pointerExpr)) {}
    unique_ptr<Expr> pointer;
};

struct Stmt {
    explicit Stmt(SourceLocation loc) : location(loc) {}
    virtual ~Stmt() = default;
    SourceLocation location;
};

struct VarDeclStmt : Stmt {
    VarDeclStmt(SourceLocation loc, bool constantFlag, string variableName,
                TypeName variableType)
        : Stmt(loc), isConst(constantFlag), name(move(variableName)),
          type(variableType) {}
    bool isConst = false;
    string name;
    TypeName type;
    unique_ptr<Expr> initializer;
};

struct AssignmentStmt : Stmt {
    AssignmentStmt(SourceLocation loc, string targetName)
        : Stmt(loc), name(move(targetName)) {}
    string name;
    unique_ptr<Expr> index;
    unique_ptr<Expr> value;
};

struct FieldAssignmentStmt : Stmt {
    FieldAssignmentStmt(SourceLocation loc, string objectName, string fieldName)
        : Stmt(loc), object(move(objectName)), field(move(fieldName)) {}
    string object;
    string field;
    unique_ptr<Expr> value;
};

struct PointerAssignmentStmt : Stmt {
    explicit PointerAssignmentStmt(SourceLocation loc) : Stmt(loc) {}
    unique_ptr<Expr> pointer;
    unique_ptr<Expr> value;
};

struct PrintStmt : Stmt {
    explicit PrintStmt(SourceLocation loc) : Stmt(loc) {}
    unique_ptr<Expr> expression;
};

struct InputStmt : Stmt {
    InputStmt(SourceLocation loc, string targetName)
        : Stmt(loc), name(move(targetName)) {}
    string name;
    unique_ptr<Expr> index;
};

struct ExpressionStmt : Stmt {
    explicit ExpressionStmt(SourceLocation loc) : Stmt(loc) {}
    unique_ptr<Expr> expression;
};

struct IfBranch {
    unique_ptr<Expr> condition;
    vector<unique_ptr<Stmt>> body;
};

struct IfStmt : Stmt {
    explicit IfStmt(SourceLocation loc) : Stmt(loc) {}
    vector<IfBranch> branches;
    vector<unique_ptr<Stmt>> elseBody;
};

struct WhileStmt : Stmt {
    explicit WhileStmt(SourceLocation loc) : Stmt(loc) {}
    unique_ptr<Expr> condition;
    vector<unique_ptr<Stmt>> body;
};

struct RepeatStmt : Stmt {
    RepeatStmt(SourceLocation loc, string iteratorName)
        : Stmt(loc), iterator(move(iteratorName)) {}
    string iterator;
    unique_ptr<Expr> start;
    unique_ptr<Expr> end;
    vector<unique_ptr<Stmt>> body;
};

struct ReturnStmt : Stmt {
    explicit ReturnStmt(SourceLocation loc) : Stmt(loc) {}
    unique_ptr<Expr> value;
};

struct Param {
    string name;
    TypeName type;
    SourceLocation location;
};

struct FunctionDecl {
    SourceLocation location;
    string name;
    vector<Param> params;
    TypeName returnType;
    vector<unique_ptr<Stmt>> body;
};

struct StructField {
    string name;
    TypeName type;
    SourceLocation location;
};

struct StructDecl {
    SourceLocation location;
    string name;
    vector<StructField> fields;
};

struct Program {
    vector<unique_ptr<StructDecl>> structs;
    vector<unique_ptr<FunctionDecl>> functions;
    vector<unique_ptr<Stmt>> mainStatements;
};

#endif
