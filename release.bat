@echo off
chcp 65001
setlocal

set PLUGIN_NAME=COM3D25.PostEffects.Plugin

call .\source\%PLUGIN_NAME%\build.bat release all
if %ERRORLEVEL% neq 0 (
    echo ビルドに失敗しました
    exit /b 1
)

for /f "tokens=*" %%i in ('powershell -NoProfile -Command "$content = Get-Content 'source/%PLUGIN_NAME%/PluginInfo.cs'; $version = [regex]::Match($content, 'PluginVersion = \""(.*?)\""').Groups[1].Value; echo $version"') do set VERSION=%%i
if "%VERSION%"=="" (
    echo PluginInfo.cs から PluginVersion を読み取れませんでした
    exit /b 1
)
echo VERSION: %VERSION%

if exist output rmdir /s /q output

rem ============ 配布パッケージ (COM3D2 版 / COM3D2.5 版を同梱) ============
rem dll だけでなく Config\PostEffects (シェーダーバンドル等) も必須なのでフォルダごとコピーする
md output\%PLUGIN_NAME%
xcopy UnityInjector output\%PLUGIN_NAME%\UnityInjector /E /I
if %ERRORLEVEL% neq 0 (
    echo UnityInjector のコピーに失敗しました
    exit /b 1
)

rem COM3D2 版は共通 Config を丸ごとコピーしたうえで、dll と posteffects を 2.0 用に差し替える
xcopy UnityInjector "output\%PLUGIN_NAME%\UnityInjector (COM3D2)" /E /I
if %ERRORLEVEL% neq 0 (
    echo COM3D2 版のコピーに失敗しました
    exit /b 1
)
del /q "output\%PLUGIN_NAME%\UnityInjector (COM3D2)\%PLUGIN_NAME%.dll"
if %ERRORLEVEL% neq 0 (
    echo COM3D2 版フォルダからの COM3D2.5 版 dll の削除に失敗しました
    exit /b 1
)
xcopy UnityInjector20 "output\%PLUGIN_NAME%\UnityInjector (COM3D2)" /E /I /Y
if %ERRORLEVEL% neq 0 (
    echo COM3D2 版の差分コピーに失敗しました
    exit /b 1
)

rem 取り違え防止のため、各フォルダに正しい dll だけが入っていることを検証する
if not exist "output\%PLUGIN_NAME%\UnityInjector\%PLUGIN_NAME%.dll" (
    echo dll がパッケージに含まれていません: UnityInjector\%PLUGIN_NAME%.dll
    exit /b 1
)
if not exist "output\%PLUGIN_NAME%\UnityInjector (COM3D2)\COM3D2.PostEffects.Plugin.dll" (
    echo dll がパッケージに含まれていません: UnityInjector ^(COM3D2^)\COM3D2.PostEffects.Plugin.dll
    exit /b 1
)
if exist "output\%PLUGIN_NAME%\UnityInjector (COM3D2)\%PLUGIN_NAME%.dll" (
    echo COM3D2 版のフォルダに COM3D2.5 版 dll が残っています
    exit /b 1
)

set README_TXT=output\%PLUGIN_NAME%\README.txt
echo このテキストはWeb上で見ることを推奨しています。 > %README_TXT%
echo https://github.com/kidonaru/COM3D25.PostEffects.Plugin/blob/main/README.md >> %README_TXT%
echo. >> %README_TXT%
echo. >> %README_TXT%
type README.md >> %README_TXT%

rem zip 名は deploy.ps1 が参照するため、変更するときは両方合わせること
powershell -NoProfile Compress-Archive -Path "output\%PLUGIN_NAME%" -DestinationPath "output\%PLUGIN_NAME%-v%VERSION%.zip" -Force
if %ERRORLEVEL% neq 0 (
    echo zip の作成に失敗しました
    exit /b 1
)

rmdir /s /q output\%PLUGIN_NAME%

echo ビルドに成功しました
exit /b 0
