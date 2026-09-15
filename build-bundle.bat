@echo off
chcp 65001 >nul
setlocal

rem posteffects シェーダーバンドルのビルド (実体は build-bundle.ps1)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-bundle.ps1"
if %ERRORLEVEL% neq 0 (
    echo シェーダーバンドルのビルドに失敗しました
    exit /b 1
)

exit /b 0
