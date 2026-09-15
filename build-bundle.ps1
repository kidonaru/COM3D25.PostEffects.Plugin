<#
.SYNOPSIS
    posteffects シェーダーバンドルを Unity 5.6.4f1 の batchmode でビルドし、配布フォルダへコピーする。
    build-bundle.bat から呼ばれる。

.DESCRIPTION
    Unity 5.6 でビルドしたバンドルは COM3D2 (2.0) と COM3D2.5 (Unity 2022.3) の両方で読めるため、
    1 本を UnityInjector\Config\PostEffects\Shaders\posteffects として両バージョン共通で配布する。
    Unity の場所を変えたいときは環境変数 UNITY56_EXE で上書きする。
#>
$ErrorActionPreference = 'Stop'

$repoDir = $PSScriptRoot
$projectDir = Join-Path $repoDir 'UnityProject'
$bundleSrc = Join-Path $projectDir 'AssetBundles\posteffects'
$bundleDstDir = Join-Path $repoDir 'UnityInjector\Config\PostEffects\Shaders'
$logFile = Join-Path $repoDir 'bundle_build.log'

$unityExe = $env:UNITY56_EXE
if ([string]::IsNullOrEmpty($unityExe)) {
    $unityExe = 'C:\Program Files\Unity\Hub\Editor\5.6.4f1\Editor\Unity.exe'
}
if (-not (Test-Path $unityExe)) {
    Write-Error "Unity 5.6.4f1 が見つかりません: $unityExe`n環境変数 UNITY56_EXE に Unity.exe のパスを設定してください"
}

if (Test-Path $bundleSrc) { Remove-Item $bundleSrc -Force }

Write-Host '=== シェーダーバンドルをビルド中 (Unity 5.6.4f1 batchmode) ==='
$unityArgs = @(
    '-quit', '-batchmode', '-nographics',
    '-projectPath', $projectDir,
    '-executeMethod', 'BuildShaderBundles.Build',
    '-logFile', $logFile
)
$proc = Start-Process -FilePath $unityExe -ArgumentList $unityArgs -Wait -PassThru -NoNewWindow
if ($proc.ExitCode -ne 0) {
    Write-Error "シェーダーバンドルのビルドに失敗しました (終了コード $($proc.ExitCode)、詳細: $logFile)"
}

# Unity はシェーダーのコンパイルエラーがあっても終了コード 0 で抜けるため、ログを検査する
$shaderErrors = Select-String -Path $logFile -Pattern '^Shader error in ' | ForEach-Object { $_.Line }
if ($shaderErrors) {
    $shaderErrors | ForEach-Object { Write-Host $_ }
    Write-Error "シェーダーのコンパイルエラーがあります (詳細: $logFile)"
}
if (-not (Test-Path $bundleSrc)) {
    Write-Error "バンドルが生成されていません: $bundleSrc (詳細: $logFile)"
}

New-Item -ItemType Directory -Path $bundleDstDir -Force | Out-Null
Copy-Item $bundleSrc $bundleDstDir -Force
Write-Host "シェーダーバンドルを配置しました: $(Join-Path $bundleDstDir 'posteffects')"
