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

ビルド成果物はリポジトリルートの `UnityInjector\` に集約され、そのままリリースパッケージのレイアウトになる。ビルドスクリプトは同時に `%COM3D25_DIR%\Sybaris\UnityInjector\` へ DLL とシェーダーバンドル（`Config\PostEffects`）をデプロイする（ゲーム起動中は DLL コピーが失敗するが続行される）。

## 補足

- 参照 DLL はすべて `.env` の `COM3D25_DIR` 配下から解決する。COM3D2.5 に `UnityInjector.dll` の実体は無いが、`UnityInjector.PluginBase` などは `BepInEx\plugins\UnityInjectorLoader\BepInEx.UnityInjectorLoader.dll` に含まれるため、そちらを参照している
- MTEUtils は COM3D2.5 向け API 分岐のためにコンパイル定数 `COM3D25` を必要とする。csproj の `DefineConstants` で定義済み

## シェーダーバンドルのビルド

自前シェーダー（`UnityProject\Assets\Shaders`）は Unity 2022.3 プロジェクト `UnityProject\` でビルドする。

- エディタスクリプト: `UnityProject\Assets\Editor\BuildShaderBundles.cs`
- 出力先: `UnityInjector\Config\PostEffects\Shaders\`（リポジトリに同梱してコミットする）
- Unity エディタから、または batchmode で実行できる

同梱シェーダーのライセンス表記は `UnityInjector\Config\PostEffects\License` にまとめている。シェーダーを追加した場合はここにも追記すること。
