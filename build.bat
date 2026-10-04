@echo off
REM Нужен .NET 8 SDK. Результат: Release\JARVIS.exe (Python не нужен).
dotnet publish JARVIS.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o Release
if errorlevel 1 (echo ОШИБКА СБОРКИ & exit /b 1)
echo Готово: Release\JARVIS.exe
