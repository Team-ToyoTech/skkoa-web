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
#include <system_error>
#include <unordered_set>
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
    string content = buffer.str();
    if (content.size() >= 3 &&
        static_cast<unsigned char>(content[0]) == 0xEF &&
        static_cast<unsigned char>(content[1]) == 0xBB &&
        static_cast<unsigned char>(content[2]) == 0xBF) {
        content.erase(0, 3);
    }
    return content;
}

static string trim(const string &value) {
    size_t start = value.find_first_not_of(" \t\r");
    if (start == string::npos) {
        return "";
    }
    size_t end = value.find_last_not_of(" \t\r");
    return value.substr(start, end - start + 1);
}

static bool parseImportLine(const string &line, string &importPath) {
    string trimmed = trim(line);
    const string keyword = "가져오기";
    if (trimmed.rfind(keyword, 0) != 0) {
        return false;
    }
    string rest = trim(trimmed.substr(keyword.size()));
    if (rest.size() < 2 || rest.front() != '"' || rest.back() != '"') {
        throw runtime_error("가져오기 문법은 '가져오기 \"파일.koa\"' 형식이어야 합니다.");
    }
    importPath = rest.substr(1, rest.size() - 2);
    return true;
}

static bool fileExists(const fs::path &path) {
    error_code ec;
    return fs::is_regular_file(path, ec);
}

static void addSearchDir(vector<fs::path> &dirs, const fs::path &dir) {
    if (dir.empty()) {
        return;
    }
    fs::path normalized = fs::absolute(dir).lexically_normal();
    string key = normalized.string();
    for (const auto &existing : dirs) {
        if (existing.string() == key) {
            return;
        }
    }
    dirs.push_back(normalized);
}

static void addPathList(vector<fs::path> &dirs, const char *value) {
    if (!value) {
        return;
    }
#if defined(_WIN32)
    const char delimiter = ';';
#else
    const char delimiter = ':';
#endif
    string list(value);
    size_t start = 0;
    while (start <= list.size()) {
        size_t end = list.find(delimiter, start);
        string item = list.substr(start, end == string::npos ? string::npos
                                                            : end - start);
        if (!trim(item).empty()) {
            addSearchDir(dirs, item);
        }
        if (end == string::npos) {
            break;
        }
        start = end + 1;
    }
}

static vector<fs::path> standardLibraryDirs() {
    vector<fs::path> dirs;
    addPathList(dirs, getenv("SKKOA_LIB_PATH"));

    if (const char *home = getenv("SKKOA_HOME")) {
        addSearchDir(dirs, fs::path(home) / "lib");
    }
    if (const char *installRoot = getenv("SKKOA_INSTALL_ROOT")) {
        addSearchDir(dirs, fs::path(installRoot) / "lib");
    }

#if defined(_WIN32)
    if (const char *localAppData = getenv("LOCALAPPDATA")) {
        addSearchDir(dirs, fs::path(localAppData) / "SKKOA" / "lib");
    }
#else
    if (const char *home = getenv("HOME")) {
        addSearchDir(dirs, fs::path(home) / ".skkoa" / "lib");
    }
#endif

    addSearchDir(dirs, fs::current_path() / "lib");
    addSearchDir(dirs, fs::current_path() / "compiler" / "lib");
    return dirs;
}

static vector<fs::path> importNameVariants(const fs::path &path) {
    vector<fs::path> variants = {path};
    if (!path.has_extension()) {
        variants.push_back(path.string() + ".koa");
    }
    return variants;
}

static fs::path resolveImport(const string &importPath, const fs::path &baseDir,
                              const fs::path &currentFile) {
    fs::path requested(importPath);
    vector<fs::path> candidates;

    for (const auto &variant : importNameVariants(requested)) {
        if (variant.is_absolute()) {
            candidates.push_back(variant);
        } else {
            candidates.push_back(baseDir / variant);
        }
    }

    if (requested.is_relative()) {
        for (const auto &dir : standardLibraryDirs()) {
            for (const auto &variant : importNameVariants(requested)) {
                candidates.push_back(dir / variant);
            }
        }
    }

    for (const auto &candidate : candidates) {
        fs::path normalized = fs::absolute(candidate).lexically_normal();
        if (normalized.string() == currentFile.string()) {
            continue;
        }
        if (fileExists(normalized)) {
            return normalized;
        }
    }

    ostringstream message;
    message << "가져올 파일을 찾을 수 없습니다: " << importPath
            << "\n확인한 위치:";
    for (const auto &candidate : candidates) {
        message << "\n- " << fs::absolute(candidate).lexically_normal().string();
    }
    throw runtime_error(message.str());
}

static string expandImports(const fs::path &path, unordered_set<string> &active,
                            unordered_set<string> &included) {
    fs::path absolute = fs::absolute(path).lexically_normal();
    string key = absolute.string();
    if (active.count(key) > 0) {
        throw runtime_error("순환 가져오기를 발견했습니다: " + key);
    }
    if (included.count(key) > 0) {
        return "";
    }
    active.insert(key);

    string source = readFile(key);
    included.insert(key);
    istringstream input(source);
    ostringstream output;
    string line;
    fs::path baseDir = absolute.parent_path();

    while (getline(input, line)) {
        string importPath;
        if (parseImportLine(line, importPath)) {
            fs::path child = resolveImport(importPath, baseDir, absolute);
            output << expandImports(child, active, included);
            if (!output.str().empty() && output.str().back() != '\n') {
                output << '\n';
            }
        } else {
            output << line << '\n';
        }
    }

    active.erase(key);
    return output.str();
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
        unordered_set<string> activeImports;
        unordered_set<string> includedImports;
        string source = expandImports(options.inputPath, activeImports,
                                      includedImports);
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
