cmake_minimum_required(VERSION 3.16)

project(skkoa LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

add_executable(skkoa
    src/main.cpp
    src/ErrorReporter.cpp
    src/Lexer.cpp
    src/Parser.cpp
    src/SemanticAnalyzer.cpp
    src/CodeGenerator.cpp
)

target_include_directories(skkoa PRIVATE src)
