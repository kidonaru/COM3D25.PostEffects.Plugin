@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

cd /d %~dp0

set PLUGIN_NAME=COM3D25.PostEffects.Plugin
set REPO_DIR=%~dp0..\..

set MSBUILD_PATH="C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"

rem 引数: %1 = debug/release (既定 release)
set CONFIG=Release
if /i "%~1"=="debug" set CONFIG=Debug

rem .env からゲームのインストール先を読み込む ※開発者ごとの設定
set ENV_FILE=%REPO_DIR%\.env
if not exist "%ENV_FILE%" (
    echo .env が見つかりません: %ENV_FILE%
    echo .env.sample をコピーして .env を作成し、パスを設定してください
    exit /b 1
)
for /f "usebackq eol=# tokens=1,* delims==" %%a in ("%ENV_FILE%") do set "%%a=%%b"

if "%COM3D25_DIR%"=="" (
    echo .env に COM3D25_DIR が設定されていません
    exit /b 1
)

echo COM3D25_DIR: %COM3D25_DIR%

if "%CONFIG%"=="Release" (
    %MSBUILD_PATH% %PLUGIN_NAME%.csproj /t:Clean /p:Configuration=Debug
    %MSBUILD_PATH% %PLUGIN_NAME%.csproj /t:Clean /p:Configuration=Release
    if !ERRORLEVEL! neq 0 (
        echo クリーンビルドに失敗しました
        exit /b 1
    )
)

echo === ビルド中 ^(%CONFIG%^) ===
%MSBUILD_PATH% %PLUGIN_NAME%.csproj /p:Configuration=%CONFIG% "/p:COM3D25_DIR=%COM3D25_DIR%"
if !ERRORLEVEL! neq 0 (
    echo ビルドに失敗しました
    exit /b 1
)

rem リリースパッケージ用に リポジトリ内 UnityInjector へコピー
if not exist "%REPO_DIR%\UnityInjector" mkdir "%REPO_DIR%\UnityInjector"
copy /y bin\%CONFIG%\%PLUGIN_NAME%.dll "%REPO_DIR%\UnityInjector\"
if !ERRORLEVEL! neq 0 (
    echo dllのコピーに失敗しました
    exit /b 1
)

rem シェーダーバンドルのデプロイ (リポジトリ同梱の Config をゲームへコピー)
xcopy /y /e /i /q "%REPO_DIR%\UnityInjector\Config\PostEffects" "%COM3D25_DIR%\Sybaris\UnityInjector\Config\PostEffects" >nul
if !ERRORLEVEL! neq 0 (
    echo 警告: シェーダーバンドルのデプロイに失敗しました
)

rem ゲームへのデプロイ ※ゲーム起動中はロックされるため失敗しても続行
copy /y bin\%CONFIG%\%PLUGIN_NAME%.dll "%COM3D25_DIR%\Sybaris\UnityInjector\" >nul
if !ERRORLEVEL! neq 0 (
    echo 警告: ゲームへのデプロイに失敗しました ^(ゲーム起動中?^)
) else (
    echo ゲームへデプロイしました
)

exit /b 0
