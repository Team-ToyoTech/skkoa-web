#ifndef SKKOA_ERROR_REPORTER_HPP
#define SKKOA_ERROR_REPORTER_HPP

#include "Token.hpp"

#include <ostream>
#include <string>
#include <vector>

using namespace std;

struct CompileMessage {
    SourceLocation location;
    string message;
    string hint;
    bool warning = false;
};

class ErrorReporter {
  public:
    void error(SourceLocation location, const string &message,
               const string &hint = "");
    void warning(SourceLocation location, const string &message,
                 const string &hint = "");
    bool hasErrors() const;
    bool hasWarnings() const;
    void print(ostream &out) const;
    const vector<CompileMessage> &messages() const;

  private:
    vector<CompileMessage> messages_;
};

#endif
