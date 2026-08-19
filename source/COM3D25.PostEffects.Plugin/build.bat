@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

cd /d %~dp0

set REPO_DIR=%~dp0..\..
set PROJECT_FILE=COM3D25.PostEffects.Plugin.csproj

set MSBUILD_PATH="C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"

rem 引数: %1 = debug/release (既定 release), %2 = com3d2/com3d25/all (既定 all)
set CONFIG=Release
if /i "%~1"=="debug" set CONFIG=Debug

set TARGET=%~2
if "%TARGET%"=="" set TARGET=all
if /i not "%TARGET%"=="com3d2" if /i not "%TARGET%"=="com3d25" if /i not "%TARGET%"=="all" (
    echo 不正なターゲットです: %TARGET% ^(com3d2 / com3d25 / all のいずれかを指定してください^)
    exit /b 1
)

rem .env からゲームのインストール先を読み込む ※開発者ごとの設定
set ENV_FILE=%REPO_DIR%\.env
if not exist "%ENV_FILE%" (
    echo .env が見つかりません: %ENV_FILE%
    echo .env.sample をコピーして .env を作成し、パスを設定してください
    exit /b 1
)
for /f "usebackq eol=# tokens=1,* delims==" %%a in ("%ENV_FILE%") do set "%%a=%%b"

rem ビルド対象に必要な変数だけ検証する
if /i not "%TARGET%"=="com3d2" (
    if "%COM3D25_DIR%"=="" (
        echo .env に COM3D25_DIR が設定されていません
        exit /b 1
    )
    echo COM3D25_DIR: %COM3D25_DIR%
)
if /i not "%TARGET%"=="com3d25" (
    if "%COM3D2_DIR%"=="" (
        echo .env に COM3D2_DIR が設定されていません
        exit /b 1
    )
    echo COM3D2_DIR: %COM3D2_DIR%
)

if /i not "%TARGET%"=="com3d2" (
    call :build_com3d25
    if !ERRORLEVEL! neq 0 exit /b 1
)
if /i not "%TARGET%"=="com3d25" (
    call :build_com3d2
    if !ERRORLEVEL! neq 0 exit /b 1
)

exit /b 0

rem ============ COM3D2.5 版 ============
:build_com3d25
set DLL_NAME=COM3D25.PostEffects.Plugin.dll
set MSBUILD_ARGS=/p:GameVersion=COM3D25 "/p:COM3D25_DIR=%COM3D25_DIR%"

rem Release はクリーンビルドする。ターゲットごとに出力先が違うので GameVersion を渡すこと
if "%CONFIG%"=="Release" (
    %MSBUILD_PATH% %PROJECT_FILE% /t:Clean /p:Configuration=Debug %MSBUILD_ARGS%
    %MSBUILD_PATH% %PROJECT_FILE% /t:Clean /p:Configuration=Release %MSBUILD_ARGS%
    if !ERRORLEVEL! neq 0 (
        echo COM3D2.5 版のクリーンに失敗しました
        exit /b 1
    )
)

echo === ビルド中 ^(COM3D2.5 / %CONFIG%^) ===
%MSBUILD_PATH% %PROJECT_FILE% /p:Configuration=%CONFIG% %MSBUILD_ARGS%
if !ERRORLEVEL! neq 0 (
    echo COM3D2.5 版のビルドに失敗しました
    exit /b 1
)

rem リリースパッケージ用に リポジトリ内 UnityInjector (COM3D2.5) へコピー
if not exist "%REPO_DIR%\UnityInjector (COM3D2.5)" mkdir "%REPO_DIR%\UnityInjector (COM3D2.5)"
copy /y bin\%CONFIG%\%DLL_NAME% "%REPO_DIR%\UnityInjector (COM3D2.5)\"
if !ERRORLEVEL! neq 0 (
    echo dllのコピーに失敗しました
    exit /b 1
)

rem シェーダーバンドルのデプロイ (共通の Config を入れた後、2.5 用 posteffects で上書きする)
xcopy /y /e /i /q "%REPO_DIR%\UnityInjector\Config\PostEffects" "%COM3D25_DIR%\Sybaris\UnityInjector\Config\PostEffects" >nul
if !ERRORLEVEL! neq 0 (
    echo 警告: シェーダーバンドルのデプロイに失敗しました
)
xcopy /y /e /i /q "%REPO_DIR%\UnityInjector (COM3D2.5)\Config\PostEffects" "%COM3D25_DIR%\Sybaris\UnityInjector\Config\PostEffects" >nul
if !ERRORLEVEL! neq 0 (
    echo 警告: COM3D2.5 用シェーダーバンドルのデプロイに失敗しました
)

rem ゲームへのデプロイ ※ゲーム起動中はロックされるため失敗しても続行
copy /y bin\%CONFIG%\%DLL_NAME% "%COM3D25_DIR%\Sybaris\UnityInjector\" >nul
if !ERRORLEVEL! neq 0 (
    echo 警告: COM3D2.5 へのデプロイに失敗しました ^(ゲーム起動中?^)
) else (
    echo COM3D2.5 へデプロイしました
)
exit /b 0

rem ============ COM3D2 (2.0) 版 ============
:build_com3d2
set DLL_NAME=COM3D2.PostEffects.Plugin.dll
set MSBUILD_ARGS=/p:GameVersion=COM3D2 "/p:COM3D2_DIR=%COM3D2_DIR%"
rem COM3D2 版の出力は bin\<Config>\COM3D2\ 配下 (2.5 版と混ざらないよう分けている)
set OUT_DIR=bin\%CONFIG%\COM3D2

if "%CONFIG%"=="Release" (
    %MSBUILD_PATH% %PROJECT_FILE% /t:Clean /p:Configuration=Debug %MSBUILD_ARGS%
    %MSBUILD_PATH% %PROJECT_FILE% /t:Clean /p:Configuration=Release %MSBUILD_ARGS%
    if !ERRORLEVEL! neq 0 (
        echo COM3D2 版のクリーンに失敗しました
        exit /b 1
    )
)

echo === ビルド中 ^(COM3D2 / %CONFIG%^) ===
%MSBUILD_PATH% %PROJECT_FILE% /p:Configuration=%CONFIG% %MSBUILD_ARGS%
if !ERRORLEVEL! neq 0 (
    echo COM3D2 版のビルドに失敗しました
    exit /b 1
)

rem リリースパッケージ用に リポジトリ内 UnityInjector へコピー
rem (UnityInjector は COM3D2 (2.0) 用。共通 Config と 2.0 用 posteffects もここで管理する)
if not exist "%REPO_DIR%\UnityInjector" mkdir "%REPO_DIR%\UnityInjector"
copy /y %OUT_DIR%\%DLL_NAME% "%REPO_DIR%\UnityInjector\"
if !ERRORLEVEL! neq 0 (
    echo dllのコピーに失敗しました
    exit /b 1
)

rem シェーダーバンドルのデプロイ (UnityInjector の Config は 2.0 用 posteffects 込みなのでそのままコピー)
xcopy /y /e /i /q "%REPO_DIR%\UnityInjector\Config\PostEffects" "%COM3D2_DIR%\Sybaris\UnityInjector\Config\PostEffects" >nul
if !ERRORLEVEL! neq 0 (
    echo 警告: シェーダーバンドルのデプロイに失敗しました
)

rem ゲームへのデプロイ ※ゲーム起動中はロックされるため失敗しても続行
copy /y %OUT_DIR%\%DLL_NAME% "%COM3D2_DIR%\Sybaris\UnityInjector\" >nul
if !ERRORLEVEL! neq 0 (
    echo 警告: COM3D2 へのデプロイに失敗しました ^(ゲーム起動中?^)
) else (
    echo COM3D2 へデプロイしました
)
exit /b 0
