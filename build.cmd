@echo off
rem Builds a single-file, self-contained, portable GitPeach.exe into .\dist
rem Optional version (build.cmd 1.2.0), stamped into the exe; without it the csproj's <Version> is used.
setlocal
cd /d "%~dp0"
set VERSIONARG=
if not "%~1"=="" set VERSIONARG=-p:Version=%~1
if exist dist rmdir /s /q dist
rem Always from scratch: a stale incremental Release build can ship without the compiled XAML (the exe dies at startup).
if exist src\GitPeach\obj\Release rmdir /s /q src\GitPeach\obj\Release
dotnet publish src\GitPeach\GitPeach.csproj -c Release -r win-x64 --self-contained ^
  -p:PublishSingleFile=true ^
  -p:PublishReadyToRun=true ^
  -p:PublishTrimmed=true ^
  -p:TrimMode=partial ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=false ^
  -p:DebugType=none -p:DebugSymbols=false %VERSIONARG% ^
  -o dist
if errorlevel 1 exit /b 1
echo.
echo Listo: %~dp0dist\GitPeach.exe
echo Para modo 100%% portable (datos junto al exe) crea un archivo "portable.txt" al lado del exe.
