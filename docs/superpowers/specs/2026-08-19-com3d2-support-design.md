# COM3D2 (2.0) 対応 設計書

作成日: 2026-08-19

COM3D2.5 専用だった本プラグインを、COM3D2 (2.0, Unity 5.6 世代) でも動作するマルチターゲット構成にする。

先行調査は `docs/com3d20-support-notes.md`（2026-08-10）。本設計書はその後の逆コンパイル確認結果を反映し、
実装対象を確定させたもの。

## 前提と決定事項

| 項目 | 決定 |
|---|---|
| ソースの分岐方式 | ソース 1 本 + MSBuild プロパティ `GameVersion` による条件ビルド（COM3D2.ModItemExplorer.Plugin と同じ仕組み。ただし既定バージョンは逆で、本プラグインは `COM3D25` を既定とする） |
| 名前空間 / ソースフォルダ | `COM3D25.PostEffects.Plugin` のまま変更しない（リリース済みのため） |
| アセンブリ名 | ビルドごとに分ける（`COM3D2.PostEffects.Plugin` / `COM3D25.PostEffects.Plugin`） |
| シェーダーバンドル | COM3D2 用 `posteffects`（Unity 5.6 ビルド）は生成済み。**生成元の Unity プロジェクトはリポジトリに取り込まない**（成果物のみコミット） |
| 配布形態 | 1 zip に 2 フォルダ同梱（ユーザーは自分のバージョンのフォルダを丸ごとコピー） |
| 実機検証 | 2.0 は devbridge 非対応のため、逆コンパイル確認 + ユーザーによる手動テスト |

## ビルド構成

| | COM3D2 (2.0) | COM3D2.5 |
|---|---|---|
| `GameVersion` | `COM3D2` | `COM3D25`（既定） |
| AssemblyName | `COM3D2.PostEffects.Plugin` | `COM3D25.PostEffects.Plugin` |
| RootNamespace | `COM3D25.PostEffects.Plugin`（共通） | 同左 |
| TargetFrameworkVersion | `v3.5` | `v4.7.1` |
| DefineConstants | `COM3D2` | `COM3D25` |
| UnityEngine 参照 | 単一 `UnityEngine.dll` | + モジュール分割 DLL 12 本 |
| ローダー参照 | `$(COM3D2_DIR)\Sybaris\lib\UnityInjector.dll` | `BepInEx.UnityInjectorLoader.dll` |
| OutputPath | `bin\<Configuration>\COM3D2\` | `bin\<Configuration>\` |
| ゲームへのデプロイ先 | `$(COM3D2_DIR)\Sybaris\UnityInjector\` | `$(COM3D25_DIR)\Sybaris\UnityInjector\` |

### `DefineConstants` の切り替え方

現行 csproj は Debug / Release の各 `PropertyGroup` で `DefineConstants` に `COM3D25` を直書きしている。
このまま `GameVersion` 用の `PropertyGroup` で `COM3D2` を追記すると、2.0 ビルドで
`COM3D25;COM3D2` の両方が定義されてしまう。そこで**各構成の `PropertyGroup` から `COM3D25` を外し**、
`GameVersion` を単一の情報源としてバージョン定数を付与する。

```xml
<!-- 構成別 PropertyGroup: DEBUG;TRACE / TRACE のみ（バージョン定数は書かない） -->

<!-- 構成別 PropertyGroup より後に置くこと -->
<PropertyGroup>
  <GameVersion Condition=" '$(GameVersion)' == '' ">COM3D25</GameVersion>
  <DefineConstants>$(DefineConstants);$(GameVersion)</DefineConstants>
</PropertyGroup>
<PropertyGroup Condition=" '$(GameVersion)' == 'COM3D2' ">
  <TargetFrameworkVersion>v3.5</TargetFrameworkVersion>
  <AssemblyName>COM3D2.PostEffects.Plugin</AssemblyName>
  <OutputPath>$(OutputPath)COM3D2\</OutputPath>
</PropertyGroup>
```

参照 `ItemGroup` はバージョン共通のものと、`Condition=" '$(GameVersion)' == 'COM3D25' "` / `'COM3D2'` で
分けたものに整理する。

2.0 の `W:\COM3D2\COM3D2x64_Data\Managed\` には `UnityEngine.dll` / `UnityEngine.UI.dll` /
`UnityEngine.Networking.dll` / `UnityEngine.VR.dll` しか無く、`UnityEngine.CoreModule` 等は存在しない。
`Assembly-CSharp` / `Assembly-CSharp-firstpass` / `Assembly-UnityScript-firstpass` は両バージョンとも同名で存在する。

## コード変更（確定分）

逆コンパイル確認の結果、C# 側で分岐が必要なのは以下の 2 点のみ。

### 1. 内蔵ポストエフェクトの名前空間差

ゲーム内蔵のイメージエフェクト（Bloom 等）の配置が異なる。

- COM3D2.5: `PostEffects_Dummy` 名前空間（`Assembly-CSharp.dll`）
- COM3D2 (2.0): **グローバル名前空間**（`Assembly-UnityScript-firstpass.dll`。UnityScript 実装をコンパイルしたもの）

対象は `using` エイリアス行と型参照のみで、以下 7 ファイル 13 行。

| ファイル | 型 |
|---|---|
| `Effects/BloomController.cs` | `PostEffects_Dummy.Bloom` |
| `Effects/BlurController.cs` | `PostEffects_Dummy.Blur` |
| `Effects/DepthOfFieldController.cs` | `PostEffects_Dummy.DepthOfFieldScatter` |
| `Effects/GlobalFogController.cs` | `PostEffects_Dummy.GlobalFog` |
| `Effects/VignettingController.cs` | `PostEffects_Dummy.Vignetting` |
| `Effects/ScreenOverlayController.cs` | `PostEffects_Dummy.ScreenOverlay` |
| `Effects/ColorCorrectionCurvesController.cs` | `PostEffects_Dummy.ColorCorrectionCurves`, `PostEffects_Dummy.ColorCorrectionMode`（3 箇所） |

`using` エイリアスはファイルスコープのため一箇所に集約できない。各ファイルで `#if` 分岐する。

```csharp
#if COM3D25
using BloomEffect = PostEffects_Dummy.Bloom;
#else
using BloomEffect = global::Bloom;
#endif
```

`ColorCorrectionCurvesController.cs` の本文中にある `PostEffects_Dummy.ColorCorrectionMode` 参照
（134-135, 156 行目）は、ファイル先頭に `ColorCorrectionMode` のエイリアスを追加して本文からは
名前空間修飾を外し、分岐を `using` 行だけに閉じ込める。

### 2. `TBody.SlotID` の欠番

`Effects/HeadMask.cs` が参照するスロットのうち、2.0 の enum に存在しないのは次の 5 つ。

`accFace` / `hairS_2` / `hairT_2` / `accHead_2` / `accHat_2`

スロット配列の該当要素を `#if COM3D25` で囲み、2.0 ビルドでは除外する。
2.0 に存在することを確認済みのスロット: `head`, `eye`, `accHa`, `accHana`, `megane`, `hairF`, `hairR`,
`hairS`, `hairT`, `hairAho`, `headset`, `accHead`, `accHat`, `accMiMiR`, `accMiMiL`, `accKami_1_`,
`accKami_2_`, `accKami_3_`, `accKamiSubR`, `accKamiSubL`。

### 3. プラグイン登録名

`PluginInfo.PluginFullName` は現在 `"COM3D25." + PluginName + ".Plugin"` 固定。dll 名と一致させるため
`#if` でビルドごとに切り替える（UnityInjector のプラグイン登録名とログ表記に使われる）。

## 確認済み・修正不要

先行調査で懸念とされていた項目は、2.0 の `Assembly-CSharp.dll` /
`Assembly-UnityScript-firstpass.dll` の逆コンパイルにより全て解消した。

- **`CameraMain.Update` の毎フレーム上書き**: 2.0 でも `Update()` 内で `m_gcBloom.enabled` と
  `m_gcBloom.bloomIntensity` を `CMSystem` の設定値で毎フレーム書き戻している。
  LateUpdate で対抗する既存方式（`EffectControllerBase.SuppressGameEffect` /
  `PostEffectManager` の LateUpdate 適用）はそのまま有効
- **`CameraMain.UIHide` / `UIResume`**: 2.0 にも private メソッドとして存在（`MTEUtils.cs:172-182` の
  リフレクション呼び出しはそのまま動く）
- **内蔵エフェクト型の存在**: `Bloom` / `Blur` / `DepthOfFieldScatter` / `GlobalFog` / `Vignetting` /
  `ColorCorrectionCurves` / `ScreenOverlay` / `SunShafts` / `Fisheye` / `TiltShiftHdr` / `NoiseAndGrain` /
  `Crease` / `ContrastEnhance` / `AntialiasingAsPostEffect` / `ColorCorrectionLut` / `Tonemapping` は
  `Assembly-UnityScript-firstpass.dll` に、`GrayscaleEffect` は `Assembly-CSharp-firstpass.dll` に存在する
- **net3.5 互換性**: プラグイン本体・MTEUtils サブモジュールとも `Task` / `ConcurrentDictionary` 等の
  .NET 4.x 専用型を使用していない。既存の `#if` は `#if DEBUG` 2 箇所のみ
- **シェーダーバンドル**: SceneCapture 由来の 5 バンドル（imageeffects / kino / cinematic / lightshafts /
  filmic）は元々 Unity 5.6 世代ビルドで両バージョン共用。自前 `posteffects` のみ 2.0 用が別途必要で、
  これは生成済み（12 シェーダー全て収録: CasSharpen, CharMaskChannel, CharMaskComposite, CharMaskWhite,
  Diffusion, GTToneMap, Halftone, Kuwahara, ObscuranceMask, PostEffect, RadialBlur, WhiteBalance）

### 残るリスク

内蔵エフェクト型の**メンバ名・型の差異**は静的比較では判別しきれなかった（2.0 は UnityScript 由来のため
逆コンパイル結果にコンパイラ生成メンバが混ざる）。2.0 ビルドを通し、コンパイルエラーとして洗い出して
個別に対処する。差異があった場合の対処は `#if` によるプロパティ名分岐、または該当エフェクトの
2.0 ビルドからの除外とする。

## リポジトリ構成

```
UnityInjector/                              … 共通アセット + COM3D2.5 版 dll（現状維持）
  COM3D25.PostEffects.Plugin.dll
  Config/PostEffects/{Images,Presets,Shaders,License}
UnityInjector20/                            … 新規。COM3D2 版の差分のみ
  COM3D2.PostEffects.Plugin.dll
  Config/PostEffects/Shaders/posteffects     … Unity 5.6 ビルドのバンドル
```

現在 `COM3D2.AssetBundles/AssetBundles/` に未コミットで置かれているバンドルを
`UnityInjector20/Config/PostEffects/Shaders/posteffects` へ移してコミットし、`COM3D2.AssetBundles/` は削除する。
`.manifest` はビルド時の副産物で配布に不要なため含めない。

## ビルド / 配布スクリプト

### `.env` / `.env.sample`

`COM3D2_DIR`（既定 `W:\COM3D2`）を追加する。

### `source/COM3D25.PostEffects.Plugin/build.bat`

引数を `build.bat <debug|release> [com3d2|com3d25|all]` に拡張する（第 2 引数の既定は `all`）。
各ターゲットについて次を行う。

1. `msbuild /p:GameVersion=<ver> /p:COM3D2_DIR=... /p:COM3D25_DIR=...` でビルド
2. リポジトリ内の `UnityInjector/`（2.5）または `UnityInjector20/`（2.0）へ dll をコピー
3. 対応するゲームの `Sybaris\UnityInjector\` へ dll と `Config\PostEffects` をデプロイ
   （2.0 では `posteffects` を `UnityInjector20` 側のもので上書きする）

ゲーム起動中のロックでコピーに失敗しても警告のみで継続する既存挙動は維持する。
`.env` の変数チェックは**ビルド対象のターゲットに必要なものだけ**行う
（`com3d2` 単独ビルドなら `COM3D2_DIR` のみ必須）。不足していれば黙ってスキップせずエラー終了する。

`all` 指定時に一方のターゲットのコンパイルが失敗した場合は、**その時点で中断してエラー終了する**
（内蔵エフェクト型のメンバ差異で 2.0 ビルドが落ちる可能性があるため、失敗を見落とさないことを優先する）。

### `debug.bat` / `release.bat`

`debug.bat [com3d2|com3d25|all]` として引数を build.bat に透過する。
**`debug.bat` の無引数時の既定は `com3d25` を維持する**（日常の開発イテレーションを 2.5 単独ビルドのまま
高速に回すため。また `.env` に `COM3D2_DIR` が無い環境で無引数実行が突然失敗するのを避けるため）。
`build.bat` を直接呼ぶ場合の第 2 引数の既定は `all`。
`release.bat` は常に `all` でビルドし、zip を次の構成で作る。

```
COM3D25.PostEffects.Plugin/
  UnityInjector/                … COM3D2.5 用（リポジトリの UnityInjector をそのままコピー）
  UnityInjector (COM3D2)/       … 上を丸ごとコピーしたうえで UnityInjector20 の内容で上書き
  README.txt
```

`UnityInjector (COM3D2)/` に COM3D2.5 版 dll が残らないよう、上書き後に
`COM3D25.PostEffects.Plugin.dll` を削除する。両フォルダの dll 存在チェックを行い、
欠けていたらエラー終了する（現行の dll 存在チェックの拡張）。

### `deploy.ps1`

Release Note のダウンロード表を 2 行にする。zip は 1 個のままなのでファイル名は変わらない。

```
| 対象 | フォルダ |
|---|---|
| **COM3D2** | UnityInjector (COM3D2) |
| **COM3D2.5** | UnityInjector |
```

### `README.md`

既存の**非対応宣言を削除・書き換える**必要がある。追記だけでは自己矛盾した README になる。

| 行 | 現在の記述 | 対応 |
|---|---|---|
| 4 | `**COM3D2.5 (Unity 2022) 専用**（COM3D2 2.0 には対応しない）。` | COM3D2 / COM3D2.5 両対応である旨に書き換え |
| 27 | `UnityInjector` フォルダの中身を〜コピー | バージョンごとにコピー元フォルダが異なることを明記 |
| 44 | `COM3D2.5 専用です。COM3D2 (2.0) には対応していないため、2.0 のフォルダには入れないでください。` | 両対応と、バージョンに合ったフォルダを使う旨に書き換え |
| 46 | 動作確認バージョン | COM3D2 側の確認バージョンを追記 |

## 影響範囲外（今回やらないこと）

- 名前空間 / ソースフォルダ / GitHub リポジトリ名のリネーム
- Unity 5.6 プロジェクトのリポジトリ取り込み、およびシェーダーソースの二重管理解消
- 2.0 向け devbridge の整備
- 2.0 実機での動作確認（ユーザーによる手動テストに委ねる）

## 完了条件

1. `debug.bat com3d2` と `debug.bat com3d25` の双方がビルドを通り、各ゲームへデプロイされる
2. `release.bat` が 2 フォルダ同梱の zip を生成し、各フォルダに正しい dll と `posteffects` が入っている
3. COM3D2 (2.0) を起動してプラグインがロードされ、内蔵エフェクトと自前シェーダー系エフェクトが動作する
   （ユーザーによる手動確認）
4. COM3D2.5 側の動作にリグレッションが無い（ユーザーによる手動確認）

## レビュー却下メモ

- `UnityInjector20` というフォルダ名が曖昧（`UnityInjector.COM3D2` 等の方が明確）
  — 姉妹プラグイン COM3D2.ModItemExplorer.Plugin の `UnityInjector25` と対称な命名を優先するため見送り
