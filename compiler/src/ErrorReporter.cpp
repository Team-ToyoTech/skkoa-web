#include "ErrorReporter.hpp"

#include <iostream>

using namespace std;

void ErrorReporter::error(SourceLocation location, const string &message,
                          const string &hint) {
    messages_.push_back({location, message, hint, false});
}

void ErrorReporter::warning(SourceLocation location, const string &message,
                            const string &hint) {
    messages_.push_back({location, message, hint, true});
}

bool ErrorReporter::hasErrors() const {
    for (const auto &message : messages_) {
        if (!message.warning) {
            return true;
        }
    }
    return false;
}

bool ErrorReporter::hasWarnings() const {
    for (const auto &message : messages_) {
        if (message.warning) {
            return true;
        }
    }
    return false;
}

void ErrorReporter::print(ostream &out) const {
    for (const auto &message : messages_) {
        out << (message.warning ? "경고" : "오류") << ": "
            << message.location.line << "번째 줄 "
            << message.location.column << "번째 글자에서 "
            << message.message << '\n';
        if (!message.hint.empty()) {
            out << "힌트: " << message.hint << '\n';
        }
    }
}

const vector<CompileMessage> &ErrorReporter::messages() const {
    return messages_;
}
