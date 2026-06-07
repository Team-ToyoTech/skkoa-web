#include "SemanticAnalyzer.hpp"

#include <sstream>

using namespace std;

SemanticAnalyzer::SemanticAnalyzer(ErrorReporter &errors) : errors_(errors) {}

void SemanticAnalyzer::analyze(Program &program) {
    functions_.clear();
    for (auto &function : program.functions) {
        if (functions_.count(function->name) > 0) {
            errors_.error(function->location,
                          "중복된 함수 이름 '" + function->name + "'입니다.",
                          "함수 이름은 프로그램 안에서 하나만 사용할 수 있습니다.");
            continue;
        }
        FunctionInfo info;
        info.returnType = function->returnType;
        info.location = function->location;
        for (const auto &param : function->params) {
            info.params.push_back(param.type);
        }
        functions_[function->name] = info;
    }

    for (auto &function : program.functions) {
        analyzeFunction(*function);
    }

    symbols_.clear();
    insideFunction_ = false;
    currentReturnType_ = {};
    currentReturnType_.base = ValueType::Void;
    currentReturnType_.location = {1, 1};
    analyzeStatements(program.mainStatements);
}

void SemanticAnalyzer::analyzeFunction(FunctionDecl &function) {
    symbols_.clear();
    insideFunction_ = true;
    currentReturnType_ = function.returnType;

    if (function.params.size() > 6) {
        errors_.error(function.location,
                      "현재 컴파일러는 함수 매개변수를 최대 6개까지 지원합니다.",
                      "Linux x86-64 System V ABI의 기본 정수 인자 레지스터 범위만 사용합니다.");
    }

    for (const auto &param : function.params) {
        if (symbols_.count(param.name) > 0) {
            errors_.error(param.location,
                          "중복된 매개변수 이름 '" + param.name + "'입니다.");
            continue;
        }
        if (param.type.isArray) {
            errors_.error(param.location,
                          "배열 매개변수는 현재 컴파일러에서 아직 지원하지 않습니다.");
        }
        if (param.type.base != ValueType::Int && param.type.base != ValueType::Bool) {
            errors_.error(param.location,
                          "현재 함수 매개변수는 정수와 논리만 지원합니다.",
                          "문자열, 실수, 문자, 포인터 매개변수는 예정 기능입니다.");
        }
        symbols_[param.name] = {param.type, false};
    }

    analyzeStatements(function.body);
    insideFunction_ = false;
}

void SemanticAnalyzer::analyzeStatements(vector<unique_ptr<Stmt>> &statements) {
    for (auto &statement : statements) {
        analyzeStatement(*statement);
    }
}

void SemanticAnalyzer::analyzeStatement(Stmt &statement) {
    if (auto *varDecl = dynamic_cast<VarDeclStmt *>(&statement)) {
        analyzeVarDecl(*varDecl);
    } else if (auto *assignment = dynamic_cast<AssignmentStmt *>(&statement)) {
        analyzeAssignment(*assignment);
    } else if (auto *expression = dynamic_cast<ExpressionStmt *>(&statement)) {
        analyzeExpressionStatement(*expression);
    } else if (auto *print = dynamic_cast<PrintStmt *>(&statement)) {
        analyzePrint(*print);
    } else if (auto *input = dynamic_cast<InputStmt *>(&statement)) {
        analyzeInput(*input);
    } else if (auto *ifStmt = dynamic_cast<IfStmt *>(&statement)) {
        analyzeIf(*ifStmt);
    } else if (auto *whileStmt = dynamic_cast<WhileStmt *>(&statement)) {
        analyzeWhile(*whileStmt);
    } else if (auto *repeatStmt = dynamic_cast<RepeatStmt *>(&statement)) {
        analyzeRepeat(*repeatStmt);
    } else if (auto *returnStmt = dynamic_cast<ReturnStmt *>(&statement)) {
        analyzeReturn(*returnStmt);
    }
}

void SemanticAnalyzer::analyzeVarDecl(VarDeclStmt &statement) {
    if (symbols_.count(statement.name) > 0) {
        errors_.error(statement.location,
                      "중복 변수 선언 '" + statement.name + "'입니다.",
                      "같은 함수 또는 시작 블록 안에서는 같은 이름을 다시 선언할 수 없습니다.");
        return;
    }

    if (statement.type.base == ValueType::String && statement.type.isArray) {
        errors_.error(statement.type.location,
                      "문자열 배열은 현재 미지원입니다.");
    }

    if (statement.type.isArray) {
        if (statement.type.base != ValueType::Int) {
            errors_.error(statement.type.location,
                          "현재 배열은 정수 배열만 지원합니다.");
        }
        if (statement.type.arraySize <= 0) {
            errors_.error(statement.type.location,
                          "배열 크기는 1 이상의 정수여야 합니다.",
                          "예: 변수 numbers: 정수[3]");
        }
        if (statement.initializer) {
            errors_.error(statement.location,
                          "배열 리터럴 초기화는 현재 미지원입니다.",
                          "배열 선언 뒤에 각 원소를 따로 대입하세요.");
        }
    }

    if (statement.isConst && !statement.initializer) {
        errors_.error(statement.location,
                      "상수 선언에는 초기값이 필요합니다.",
                      "예: 상수 limit: 정수 = 100");
    }

    if (statement.initializer) {
        ValueType initializerType = analyzeExpr(*statement.initializer);
        if (!statement.type.isArray && !sameType(statement.type.base, initializerType)) {
            errors_.error(statement.initializer->location,
                          "초기값 타입이 변수 타입과 다릅니다.",
                          "선언한 타입은 " + valueTypeName(statement.type.base) +
                              "이고 초기값 타입은 " + valueTypeName(initializerType) + "입니다.");
        }
    }

    symbols_[statement.name] = {statement.type, statement.isConst};
}

void SemanticAnalyzer::analyzeAssignment(AssignmentStmt &statement) {
    auto symbol = symbols_.find(statement.name);
    if (symbol == symbols_.end()) {
        errors_.error(statement.location,
                      "선언되지 않은 변수 '" + statement.name + "'를 사용했습니다.",
                      "먼저 '변수 " + statement.name + ": 정수 = 값' 형태로 선언하세요.");
        if (statement.value) {
            analyzeExpr(*statement.value);
        }
        return;
    }

    if (symbol->second.isConst) {
        errors_.error(statement.location,
                      "상수 '" + statement.name + "'에는 다시 대입할 수 없습니다.");
    }

    if (statement.index) {
        if (!symbol->second.type.isArray) {
            errors_.error(statement.location,
                          "'" + statement.name + "'는 배열이 아닙니다.",
                          "배열 인덱스는 정수 배열 변수에만 사용할 수 있습니다.");
        }
        ValueType indexType = analyzeExpr(*statement.index);
        if (indexType != ValueType::Int) {
            errors_.error(statement.index->location,
                          "배열 인덱스는 정수여야 합니다.");
        }
        if (auto *literal = dynamic_cast<IntLiteralExpr *>(statement.index.get())) {
            if (literal->value < 0 || literal->value >= symbol->second.type.arraySize) {
                errors_.warning(statement.index->location,
                                "배열 인덱스가 선언된 크기를 벗어날 수 있습니다.",
                                "배열 크기는 " + to_string(symbol->second.type.arraySize) +
                                    "이고 접근 인덱스는 " + to_string(literal->value) + "입니다.");
            }
        }
    } else if (symbol->second.type.isArray) {
        errors_.error(statement.location,
                      "배열 전체에 한 번에 대입하는 기능은 현재 미지원입니다.",
                      "예: numbers[0] = 10");
    }

    if (statement.value) {
        ValueType valueType = analyzeExpr(*statement.value);
        ValueType targetType = symbol->second.type.isArray
                                   ? symbol->second.type.base
                                   : symbol->second.type.base;
        if (!sameType(targetType, valueType)) {
            errors_.error(statement.value->location,
                          "대입 값의 타입이 대상 변수와 다릅니다.",
                          "대상 타입은 " + valueTypeName(targetType) +
                              "이고 값 타입은 " + valueTypeName(valueType) + "입니다.");
        }
    }
}

void SemanticAnalyzer::analyzeExpressionStatement(ExpressionStmt &statement) {
    if (statement.expression) {
        analyzeExpr(*statement.expression);
    }
}

void SemanticAnalyzer::analyzePrint(PrintStmt &statement) {
    if (statement.expression) {
        ValueType type = analyzeExpr(*statement.expression);
        if (type != ValueType::Unknown && type != ValueType::Int &&
            type != ValueType::Bool && type != ValueType::String &&
            type != ValueType::Float && type != ValueType::Char &&
            type != ValueType::Pointer) {
            errors_.error(statement.expression->location,
                          "지원하지 않는 타입은 출력할 수 없습니다.");
        }
    }
}

void SemanticAnalyzer::analyzeInput(InputStmt &statement) {
    auto symbol = symbols_.find(statement.name);
    if (symbol == symbols_.end()) {
        errors_.error(statement.location,
                      "선언되지 않은 변수 '" + statement.name + "'에 입력할 수 없습니다.",
                      "먼저 '변수 " + statement.name + ": 정수' 형태로 선언하세요.");
        if (statement.index) {
            analyzeExpr(*statement.index);
        }
        return;
    }

    if (symbol->second.isConst) {
        errors_.error(statement.location,
                      "상수 '" + statement.name + "'에는 입력값을 저장할 수 없습니다.");
    }

    if (statement.index) {
        if (!symbol->second.type.isArray) {
            errors_.error(statement.location,
                          "'" + statement.name + "'는 배열이 아닙니다.",
                          "배열 원소 입력은 정수 배열에만 사용할 수 있습니다.");
        }
        ValueType indexType = analyzeExpr(*statement.index);
        if (indexType != ValueType::Int) {
            errors_.error(statement.index->location,
                          "배열 인덱스는 정수여야 합니다.");
        }
        if (auto *literal = dynamic_cast<IntLiteralExpr *>(statement.index.get())) {
            if (literal->value < 0 || literal->value >= symbol->second.type.arraySize) {
                errors_.warning(statement.index->location,
                                "배열 인덱스가 선언된 크기를 벗어날 수 있습니다.",
                                "배열 크기는 " + to_string(symbol->second.type.arraySize) +
                                    "이고 접근 인덱스는 " + to_string(literal->value) + "입니다.");
            }
        }
    } else if (symbol->second.type.isArray) {
        errors_.error(statement.location,
                      "배열 전체에 한 번에 입력할 수 없습니다.",
                      "예: 입력 numbers[0]");
    }

    ValueType targetType = symbol->second.type.base;
    if (targetType != ValueType::Int && targetType != ValueType::Bool &&
        targetType != ValueType::Float && targetType != ValueType::Char) {
        errors_.error(statement.location,
                      "현재 입력은 정수, 논리, 실수, 문자 변수만 지원합니다.",
                      "문자열과 포인터 입력은 아직 지원하지 않습니다.");
    }
}

void SemanticAnalyzer::analyzeIf(IfStmt &statement) {
    for (auto &branch : statement.branches) {
        ValueType conditionType = analyzeExpr(*branch.condition);
        if (!isConditionType(conditionType)) {
            errors_.error(branch.condition->location,
                          "조건식은 정수 또는 논리여야 합니다.");
        }
        analyzeStatements(branch.body);
    }
    analyzeStatements(statement.elseBody);
}

void SemanticAnalyzer::analyzeWhile(WhileStmt &statement) {
    ValueType conditionType = analyzeExpr(*statement.condition);
    if (!isConditionType(conditionType)) {
        errors_.error(statement.condition->location,
                      "반복 조건은 정수 또는 논리여야 합니다.");
    }
    analyzeStatements(statement.body);
}

void SemanticAnalyzer::analyzeRepeat(RepeatStmt &statement) {
    ValueType startType = analyzeExpr(*statement.start);
    ValueType endType = analyzeExpr(*statement.end);
    if (startType != ValueType::Int || endType != ValueType::Int) {
        errors_.error(statement.location,
                      "횟수 반복의 시작값과 끝값은 정수여야 합니다.",
                      "예: 반복 i: 0부터 10까지");
    }

    bool hadPrevious = symbols_.count(statement.iterator) > 0;
    Symbol previous;
    if (hadPrevious) {
        previous = symbols_[statement.iterator];
    }

    TypeName iteratorType;
    iteratorType.base = ValueType::Int;
    iteratorType.location = statement.location;
    symbols_[statement.iterator] = {iteratorType, false};
    analyzeStatements(statement.body);

    if (hadPrevious) {
        symbols_[statement.iterator] = previous;
    } else {
        symbols_.erase(statement.iterator);
    }
}

void SemanticAnalyzer::analyzeReturn(ReturnStmt &statement) {
    if (!insideFunction_) {
        errors_.error(statement.location,
                      "'반환'은 함수 안에서만 사용할 수 있습니다.");
        return;
    }

    if (currentReturnType_.base == ValueType::Void) {
        if (statement.value) {
            errors_.error(statement.location,
                          "반환값이 없는 함수에서는 값을 반환할 수 없습니다.");
        }
        return;
    }

    if (!statement.value) {
        errors_.error(statement.location,
                      "반환 타입이 " + currentReturnType_.display() +
                          "인 함수는 반환값이 필요합니다.");
        return;
    }

    ValueType valueType = analyzeExpr(*statement.value);
    if (!sameType(currentReturnType_.base, valueType)) {
        errors_.error(statement.value->location,
                      "반환값 타입이 함수 반환 타입과 다릅니다.",
                      "함수 반환 타입은 " + currentReturnType_.display() +
                          "이고 실제 반환값은 " + valueTypeName(valueType) + "입니다.");
    }
}

ValueType SemanticAnalyzer::analyzeExpr(Expr &expression) {
    if (auto *literal = dynamic_cast<IntLiteralExpr *>(&expression)) {
        literal->inferredType = ValueType::Int;
        return literal->inferredType;
    }
    if (auto *literal = dynamic_cast<FloatLiteralExpr *>(&expression)) {
        literal->inferredType = ValueType::Float;
        return literal->inferredType;
    }
    if (auto *literal = dynamic_cast<StringLiteralExpr *>(&expression)) {
        literal->inferredType = ValueType::String;
        return literal->inferredType;
    }
    if (auto *literal = dynamic_cast<CharLiteralExpr *>(&expression)) {
        literal->inferredType = ValueType::Char;
        return literal->inferredType;
    }
    if (auto *literal = dynamic_cast<BoolLiteralExpr *>(&expression)) {
        literal->inferredType = ValueType::Bool;
        return literal->inferredType;
    }
    if (auto *variable = dynamic_cast<VariableExpr *>(&expression)) {
        auto symbol = symbols_.find(variable->name);
        if (symbol == symbols_.end()) {
            errors_.error(variable->location,
                          "선언되지 않은 변수 '" + variable->name + "'를 사용했습니다.");
            variable->inferredType = ValueType::Unknown;
            return variable->inferredType;
        }
        if (symbol->second.type.isArray) {
            errors_.error(variable->location,
                          "배열 '" + variable->name + "'는 인덱스와 함께 사용해야 합니다.",
                          "예: " + variable->name + "[0]");
            variable->inferredType = ValueType::Unknown;
            return variable->inferredType;
        }
        variable->inferredType = symbol->second.type.base;
        return variable->inferredType;
    }
    if (auto *arrayAccess = dynamic_cast<ArrayAccessExpr *>(&expression)) {
        auto symbol = symbols_.find(arrayAccess->name);
        if (symbol == symbols_.end()) {
            errors_.error(arrayAccess->location,
                          "선언되지 않은 배열 '" + arrayAccess->name + "'를 사용했습니다.");
            arrayAccess->inferredType = ValueType::Unknown;
            return arrayAccess->inferredType;
        }
        if (!symbol->second.type.isArray) {
            errors_.error(arrayAccess->location,
                          "'" + arrayAccess->name + "'는 배열이 아닙니다.");
        }
        ValueType indexType = analyzeExpr(*arrayAccess->index);
        if (indexType != ValueType::Int) {
            errors_.error(arrayAccess->index->location,
                          "배열 인덱스는 정수여야 합니다.");
        }
        if (auto *literal = dynamic_cast<IntLiteralExpr *>(arrayAccess->index.get())) {
            if (literal->value < 0 || literal->value >= symbol->second.type.arraySize) {
                errors_.warning(arrayAccess->index->location,
                                "배열 인덱스가 선언된 크기를 벗어날 수 있습니다.",
                                "배열 크기는 " + to_string(symbol->second.type.arraySize) +
                                    "이고 접근 인덱스는 " + to_string(literal->value) + "입니다.");
            }
        }
        arrayAccess->inferredType = symbol->second.type.base;
        return arrayAccess->inferredType;
    }
    if (auto *address = dynamic_cast<AddressExpr *>(&expression)) {
        auto symbol = symbols_.find(address->name);
        if (symbol == symbols_.end()) {
            errors_.error(address->location,
                          "선언되지 않은 변수 '" + address->name + "'의 주소를 얻을 수 없습니다.");
            address->inferredType = ValueType::Unknown;
            return address->inferredType;
        }
        if (address->index) {
            if (!symbol->second.type.isArray) {
                errors_.error(address->location,
                              "'" + address->name + "'는 배열이 아닙니다.");
            }
            ValueType indexType = analyzeExpr(*address->index);
            if (indexType != ValueType::Int) {
                errors_.error(address->index->location,
                              "배열 인덱스는 정수여야 합니다.");
            }
        } else if (symbol->second.type.isArray) {
            errors_.error(address->location,
                          "배열 주소는 원소를 지정해야 합니다.",
                          "예: 주소(numbers[0])");
        }
        address->inferredType = ValueType::Pointer;
        return address->inferredType;
    }
    if (auto *deref = dynamic_cast<DereferenceExpr *>(&expression)) {
        ValueType pointerType = analyzeExpr(*deref->pointer);
        if (pointerType != ValueType::Pointer && pointerType != ValueType::Unknown) {
            errors_.error(deref->location,
                          "'값'은 포인터에만 사용할 수 있습니다.",
                          "예: 값(p)");
        }
        deref->inferredType = ValueType::Int;
        return deref->inferredType;
    }
    if (auto *unary = dynamic_cast<UnaryExpr *>(&expression)) {
        ValueType operandType = analyzeExpr(*unary->operand);
        if (unary->op == UnaryOp::Negate) {
            if (!isNumeric(operandType)) {
                errors_.error(unary->location,
                              "'-' 단항 연산자는 숫자에만 사용할 수 있습니다.");
            }
            unary->inferredType = operandType == ValueType::Float
                                      ? ValueType::Float
                                      : ValueType::Int;
            return unary->inferredType;
        }
        if (operandType != ValueType::Bool) {
            errors_.error(unary->location,
                          "'아님' 연산자는 논리 값에만 사용할 수 있습니다.");
        }
        unary->inferredType = ValueType::Bool;
        return unary->inferredType;
    }
    if (auto *binary = dynamic_cast<BinaryExpr *>(&expression)) {
        ValueType leftType = analyzeExpr(*binary->left);
        ValueType rightType = analyzeExpr(*binary->right);

        switch (binary->op) {
        case BinaryOp::Add:
            if (leftType == ValueType::String || rightType == ValueType::String) {
                if (leftType != ValueType::String || rightType != ValueType::String) {
                    errors_.error(binary->location,
                                  "문자열 결합은 문자열끼리만 할 수 있습니다.");
                }
                binary->inferredType = ValueType::String;
                return binary->inferredType;
            }
            [[fallthrough]];
        case BinaryOp::Subtract:
        case BinaryOp::Multiply:
        case BinaryOp::Divide:
        case BinaryOp::Modulo:
            if (!isNumeric(leftType) || !isNumeric(rightType)) {
                errors_.error(binary->location,
                              "산술 연산자는 숫자끼리만 사용할 수 있습니다.");
            }
            if (binary->op == BinaryOp::Modulo &&
                (leftType == ValueType::Float || rightType == ValueType::Float)) {
                errors_.error(binary->location,
                              "나머지 연산자는 정수에만 사용할 수 있습니다.");
            }
            binary->inferredType =
                (leftType == ValueType::Float || rightType == ValueType::Float)
                    ? ValueType::Float
                    : ValueType::Int;
            return binary->inferredType;
        case BinaryOp::Less:
        case BinaryOp::LessEqual:
        case BinaryOp::Greater:
        case BinaryOp::GreaterEqual:
            if (!isNumeric(leftType) || !isNumeric(rightType)) {
                errors_.error(binary->location,
                              "크기 비교 연산자는 숫자끼리만 사용할 수 있습니다.");
            }
            binary->inferredType = ValueType::Bool;
            return binary->inferredType;
        case BinaryOp::Equal:
        case BinaryOp::NotEqual:
            if (!sameType(leftType, rightType)) {
                errors_.error(binary->location,
                              "같다/다르다 비교는 같은 타입끼리만 사용할 수 있습니다.");
            }
            binary->inferredType = ValueType::Bool;
            return binary->inferredType;
        case BinaryOp::And:
        case BinaryOp::Or:
            if (leftType != ValueType::Bool || rightType != ValueType::Bool) {
                errors_.error(binary->location,
                              "논리 연산자는 논리 값끼리만 사용할 수 있습니다.");
            }
            binary->inferredType = ValueType::Bool;
            return binary->inferredType;
        }
    }
    if (auto *call = dynamic_cast<CallExpr *>(&expression)) {
        if (call->name == "할당") {
            if (call->arguments.size() != 1) {
                errors_.error(call->location,
                              "'할당'은 크기 인자 1개가 필요합니다.",
                              "예: 할당(8)");
            } else {
                ValueType argType = analyzeExpr(*call->arguments[0]);
                if (argType != ValueType::Int) {
                    errors_.error(call->arguments[0]->location,
                                  "할당 크기는 정수여야 합니다.");
                }
            }
            call->inferredType = ValueType::Pointer;
            return call->inferredType;
        }
        if (call->name == "해제") {
            if (call->arguments.size() != 1) {
                errors_.error(call->location,
                              "'해제'는 포인터 인자 1개가 필요합니다.",
                              "예: 해제(p)");
            } else {
                ValueType argType = analyzeExpr(*call->arguments[0]);
                if (argType != ValueType::Pointer && argType != ValueType::Unknown) {
                    errors_.error(call->arguments[0]->location,
                                  "해제 인자는 포인터여야 합니다.");
                }
            }
            call->inferredType = ValueType::Void;
            return call->inferredType;
        }
        auto function = functions_.find(call->name);
        if (function == functions_.end()) {
            errors_.error(call->location,
                          "선언되지 않은 함수 '" + call->name + "'를 호출했습니다.");
            call->inferredType = ValueType::Unknown;
            return call->inferredType;
        }
        if (call->arguments.size() != function->second.params.size()) {
            errors_.error(call->location,
                          "함수 인자 개수가 일치하지 않습니다.",
                          "'" + call->name + "' 함수는 " +
                              to_string(function->second.params.size()) +
                              "개를 받지만 " +
                              to_string(call->arguments.size()) + "개를 전달했습니다.");
        }
        size_t count = min(call->arguments.size(), function->second.params.size());
        for (size_t i = 0; i < count; i++) {
            ValueType argType = analyzeExpr(*call->arguments[i]);
            ValueType paramType = function->second.params[i].base;
            if (!sameType(paramType, argType)) {
                errors_.error(call->arguments[i]->location,
                              "함수 인자 타입이 일치하지 않습니다.",
                              to_string(i + 1) + "번째 인자는 " +
                                  valueTypeName(paramType) + "여야 합니다.");
            }
        }
        call->inferredType = function->second.returnType.base;
        return call->inferredType;
    }

    expression.inferredType = ValueType::Unknown;
    return expression.inferredType;
}

bool SemanticAnalyzer::sameType(ValueType expected, ValueType actual) const {
    return expected == actual || expected == ValueType::Unknown ||
           actual == ValueType::Unknown;
}

bool SemanticAnalyzer::isIntegerLike(ValueType type) const {
    return type == ValueType::Int || type == ValueType::Bool;
}

bool SemanticAnalyzer::isNumeric(ValueType type) const {
    return type == ValueType::Int || type == ValueType::Bool ||
           type == ValueType::Char || type == ValueType::Float;
}

bool SemanticAnalyzer::isConditionType(ValueType type) const {
    return isIntegerLike(type);
}
