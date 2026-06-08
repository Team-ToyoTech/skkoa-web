#include "CodeGenerator.hpp"
#include "ErrorReporter.hpp"
#include "Lexer.hpp"
#include "Parser.hpp"
#include "SemanticAnalyzer.hpp"

#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <sstream>
#include <string>
#include <vector>

using namespace std;
namespace fs = std::filesystem;

struct CliOptions {
    string inputPath;
    string outputPath;
    bool emitAsm = false;
    bool emitAst = false;
    bool help = false;
};

static string readFile(const string &path) {
    ifstream file(path, ios::binary);
    if (!file) {
        throw runtime_error("파일을 열 수 없습니다: " + path);
    }
    ostringstream buffer;
    buffer << file.rdbuf();
    return buffer.str();
}

static void writeFile(const string &path, const string &content) {
    ofstream file(path, ios::binary);
    if (!file) {
        throw runtime_error("파일을 쓸 수 없습니다: " + path);
    }
    file << content;
}

static string shellQuote(const string &value) {
#if defined(_WIN32)
    string quoted = "\"";
    for (char ch : value) {
        if (ch == '"') {
            quoted += "\\\"";
        } else {
            quoted += ch;
        }
    }
    quoted += "\"";
    return quoted;
#else
    string quoted = "'";
    for (char ch : value) {
        if (ch == '\'') {
            quoted += "'\\''";
        } else {
            quoted += ch;
        }
    }
    quoted += "'";
    return quoted;
#endif
}

static string nasmFormat() {
#if defined(_WIN32)
    return "win64";
#elif defined(__APPLE__)
    return "macho64";
#else
    return "elf64";
#endif
}

static string linkCommand(const fs::path &objectPath, const string &outputPath) {
#if defined(_WIN32)
    return "gcc " + shellQuote(objectPath.string()) + " -o " +
           shellQuote(outputPath);
#elif defined(__APPLE__)
    return "cc " + shellQuote(objectPath.string()) + " -o " +
           shellQuote(outputPath);
#else
    return "gcc -no-pie " + shellQuote(objectPath.string()) + " -o " +
           shellQuote(outputPath);
#endif
}

static void printHelp() {
    cout << "SKKOA Compiler\n";
    cout << "사용법:\n";
    cout << "  skkoa <파일.koa> [-o 실행파일]\n";
    cout << "  skkoa <파일.koa> --emit-asm [-o 출력.asm]\n";
    cout << "  skkoa <파일.koa> --emit-ast\n\n";
    cout << "옵션:\n";
    cout << "  -o <path>      출력 파일 경로를 지정합니다.\n";
    cout << "  --emit-asm     NASM 어셈블리만 생성합니다.\n";
    cout << "  --emit-ast     AST를 표준 출력으로 표시합니다.\n";
    cout << "  -h, --help     도움말을 표시합니다.\n";
}

static CliOptions parseArgs(int argc, char **argv) {
    CliOptions options;
    for (int i = 1; i < argc; i++) {
        string arg = argv[i];
        if (arg == "-h" || arg == "--help") {
            options.help = true;
        } else if (arg == "--emit-asm") {
            options.emitAsm = true;
        } else if (arg == "--emit-ast") {
            options.emitAst = true;
        } else if (arg == "-o") {
            if (i + 1 >= argc) {
                throw runtime_error("-o 뒤에는 출력 경로가 필요합니다.");
            }
            options.outputPath = argv[++i];
        } else if (options.inputPath.empty()) {
            options.inputPath = arg;
        } else {
            throw runtime_error("알 수 없는 인자입니다: " + arg);
        }
    }
    return options;
}

static string defaultOutputPath(const CliOptions &options) {
    fs::path input(options.inputPath);
    if (!options.outputPath.empty()) {
        return options.outputPath;
    }
    if (options.emitAsm) {
        return input.replace_extension(".asm").string();
    }
#if defined(_WIN32)
    return input.replace_extension(".exe").string();
#else
    return input.replace_extension("").string();
#endif
}

int main(int argc, char **argv) {
    try {
        CliOptions options = parseArgs(argc, argv);
        if (options.help) {
            printHelp();
            return 0;
        }
        if (options.inputPath.empty()) {
            cerr << "오류: 입력 .koa 파일이 필요합니다.\n";
            printHelp();
            return 1;
        }

        fs::path inputPath(options.inputPath);
        if (inputPath.extension() != ".koa") {
            cerr << "오류: SKKOA 소스 파일은 .koa 확장자를 사용해야 합니다.\n";
            return 1;
        }

        ErrorReporter errors;
        string source = readFile(options.inputPath);
        Lexer lexer(source, errors);
        vector<Token> tokens = lexer.tokenize();
        if (errors.hasErrors()) {
            errors.print(cerr);
            return 1;
        }

        Parser parser(tokens, errors);
        unique_ptr<Program> program = parser.parse();
        if (errors.hasErrors()) {
            errors.print(cerr);
            return 1;
        }

        SemanticAnalyzer semantic(errors);
        semantic.analyze(*program);
        if (errors.hasErrors()) {
            errors.print(cerr);
            return 1;
        }
        if (errors.hasWarnings()) {
            errors.print(cerr);
        }

        CodeGenerator generator;
        if (options.emitAst) {
            cout << generator.generateAstDump(*program);
            return 0;
        }

        string outputPath = defaultOutputPath(options);
        fs::path asmPath = options.emitAsm ? fs::path(outputPath)
                                           : fs::path(outputPath).replace_extension(".asm");
        string assembly = generator.generateAssembly(*program, options.inputPath);
        writeFile(asmPath.string(), assembly);
        cout << "어셈블리 생성: " << asmPath.string() << '\n';

        if (options.emitAsm) {
            return 0;
        }

        fs::path objectPath = fs::path(outputPath).replace_extension(".o");
        string nasmCommand = "nasm -f " + nasmFormat() + " " +
                             shellQuote(asmPath.string()) +
                             " -o " + shellQuote(objectPath.string());
        int nasmResult = system(nasmCommand.c_str());
        if (nasmResult != 0) {
            cerr << "오류: NASM 어셈블 단계에 실패했습니다.\n";
            cerr << "명령: " << nasmCommand << '\n';
            return 1;
        }

        string linkerCommand = linkCommand(objectPath, outputPath);
        int linkResult = system(linkerCommand.c_str());
        if (linkResult != 0) {
            cerr << "오류: 링크 단계에 실패했습니다.\n";
            cerr << "명령: " << linkerCommand << '\n';
            return 1;
        }

        cout << "실행 파일 생성: " << outputPath << '\n';
        return 0;
    } catch (const exception &ex) {
        cerr << "오류: " << ex.what() << '\n';
        return 1;
    }
}
