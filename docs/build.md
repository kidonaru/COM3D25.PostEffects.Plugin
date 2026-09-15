# ビルド手順（開発者向け）

エンドユーザー向けの情報は [README.md](../README.md) を参照。

## 前提

- Visual Studio 2022 (MSBuild)
- COM3D2.5 のインストール環境（参照 DLL の取得先）

## 手順

1. サブモジュールを取得（[COM3D2.MTEUtils](https://github.com/kidonaru/COM3D2.MTEUtils) を利用している）

   ```sh
   git clone --recursive https://github.com/kidonaru/COM3D25.PostEffects.Plugin
   # クローン済みの場合
   git submodule update --init --recursive
   ```

2. `.env.sample` をコピーして `.env` を作成し、`COM3D25_DIR` に COM3D2.5 のインストール先を設定
3. リポジトリルートの `debug.bat` を実行（Debug ビルド + ゲームへのデプロイ）
   - Release ビルドは `source\COM3D25.PostEffects.Plugin\build.bat release`

ビルド成果物はリポジトリルートの `UnityInjector\`（COM3D2 (2.0) 用。両バージョン共通の `Config\PostEffects` もここで管理）と `UnityInjector (COM3D2.5)\`（COM3D2.5 用の dll）に集約され、そのままリリースパッケージのレイアウトになる。ビルドスクリプトは同時に各ゲームの `Sybaris\UnityInjector\` へ DLL とシェーダーバンドル（`Config\PostEffects`）をデプロイする（ゲーム起動中は DLL コピーが失敗するが続行される）。

## 補足

- 参照 DLL はすべて `.env` の `COM3D25_DIR` 配下から解決する。COM3D2.5 に `UnityInjector.dll` の実体は無いが、`UnityInjector.PluginBase` などは `BepInEx\plugins\UnityInjectorLoader\BepInEx.UnityInjectorLoader.dll` に含まれるため、そちらを参照している
- MTEUtils は COM3D2.5 向け API 分岐のためにコンパイル定数 `COM3D25` を必要とする。csproj の `DefineConstants` で定義済み

## シェーダーバンドルのビルド

自前シェーダー（`UnityProject\Assets\Shaders`）は Unity 5.6.4f1 プロジェクト `UnityProject\` でビルドする（MotionTimelineEditor と同じ構成）。
Unity 5.6 でビルドしたバンドルは COM3D2 (2.0) と COM3D2.5 (Unity 2022.3) の両方で読めるため、1 本を両バージョン共通で配布する（2022.3 ビルドのバンドルは Unity 5.6 では読み込めないので逆は不可）。

- リポジトリルートの `build-bundle.bat` を実行する（batchmode で `BuildShaderBundles.Build` を呼び、成果物を配布フォルダへコピーする）
  - Unity の場所は既定で `C:\Program Files\Unity\Hub\Editor\5.6.4f1\Editor\Unity.exe`。別の場所なら環境変数 `UNITY56_EXE` で指定する
  - ログは `bundle_build.log` に出る。シェーダーのコンパイルエラーがあるとビルドは失敗扱いになる
- エディタスクリプト: `UnityProject\Assets\Editor\BuildShaderBundles.cs`（Unity エディタのメニュー `PostEffects/Build Shader Bundles` からも実行できる）
- 出力先: `UnityInjector\Config\PostEffects\Shaders\posteffects`（リポジトリに同梱してコミットする）
- 可変長ループを含むシェーダーは d3d9 で展開に失敗するため `#pragma exclude_renderers d3d9` を付ける（両ゲームとも d3d11 で動く）

同梱シェーダーのライセンス表記は `UnityInjector\Config\PostEffects\License` にまとめている。シェーダーを追加した場合はここにも追記すること。
