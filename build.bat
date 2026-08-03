@echo off
setlocal
title TXT2IMG - Build

cd /d "%~dp0"

echo.
echo  ============================================================
echo   TXT2IMG - Build
echo  ============================================================
echo.

where dotnet >nul 2>&1
if errorlevel 1 (
    echo  [ERROR] .NET SDK not found. Install the .NET 8 SDK from:
    echo          https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

echo  [INFO] Publishing Release build (win-x64)...
dotnet publish TXT2IMG.csproj -c Release -r win-x64 --self-contained true -p:WindowsAppSDKSelfContained=false -p:WindowsPackageType=None -o publish
if errorlevel 1 (
    echo.
    echo  [ERROR] Build failed. See errors above.
    pause
    exit /b 1
)

echo.
echo  [OK] Build complete. TXT2IMG.exe is in publishpause
