@echo off
set "SKKOA_HOME=%~dp0"
set "PATH=%SKKOA_HOME%toolchain\msys64\mingw64\bin;%SKKOA_HOME%toolchain\msys64\usr\bin;%PATH%"
"%SKKOA_HOME%skkoa.exe" %*
