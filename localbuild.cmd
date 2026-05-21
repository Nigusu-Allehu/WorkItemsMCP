@echo off
setlocal

set PROJECT=src\WorkItemsMcp\WorkItemsMcp.csproj
set OUT=packages

echo Building...
dotnet build %PROJECT% --configuration Release
if %ERRORLEVEL% neq 0 (echo Build failed. & exit /b 1)

echo Packing...
dotnet pack %PROJECT% --no-build --configuration Release --output %OUT%
if %ERRORLEVEL% neq 0 (echo Pack failed. & exit /b 1)

echo.
echo Package written to %~dp0%OUT%\
dir /b %OUT%\*.nupkg
