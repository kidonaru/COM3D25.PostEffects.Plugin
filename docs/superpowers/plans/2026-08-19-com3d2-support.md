# COM3D2 (2.0) 対応 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
> （本リポジトリの CLAUDE.md により subagent-driven-development は使わない）

**Goal:** COM3D2.5 専用の本プラグインを、ソース 1 本のまま COM3D2 (2.0) でもビルド・動作するマルチターゲット構成にする。

**Architecture:** MSBuild プロパティ `GameVersion`（`COM3D2` / `COM3D25`、既定 `COM3D25`）で参照アセンブリ・TargetFramework・AssemblyName・プリプロセッサ定数を切り替える。C# 側の分岐は「内蔵エフェクト型の名前空間」と「`TBody.SlotID` の欠番」の 2 種のみ。配布は 1 zip に 2 フォルダ同梱。

**Tech Stack:** C# / MSBuild (VS2022, 旧形式 csproj) / Windows バッチ / PowerShell / UnityInjector (2.0) / BepInEx UnityInjectorLoader (2.5)

**Spec:** `docs/superpowers/specs/2026-08-19-com3d2-support-design.md`

## Global Constraints

- 名前空間・ソースフォルダは `COM3D25.PostEffects.Plugin` のまま変更しない（リリース済みのため）
- AssemblyName のみ分岐する: COM3D2 → `COM3D2.PostEffects.Plugin` / COM3D2.5 → `COM3D25.PostEffects.Plugin`
- TargetFrameworkVersion: COM3D2 → `v3.5` / COM3D2.5 → `v4.7.1`
- DefineConstants のバージョン定数は `GameVersion` を単一の情報源として付与する（構成別 PropertyGroup に直書きしない）
- ゲームのインストール先: COM3D2 = `W:\COM3D2`（`.env` の `COM3D2_DIR`）、COM3D2.5 = `W:\COM3D2_5`（`COM3D25_DIR`）
- COM3D2.5 側の既存挙動・出力パス（`bin\<Configuration>\`、`UnityInjector\` へのコピー）を変えない
- コメント・エラーメッセージは日本語で書く
- 自動テストは存在しない。各タスクの検証は「MSBuild が通ること」「生成物が期待の場所にあること」で行う

**Bash ツールからのバッチ実行方法:** git bash 経由では `cmd //c "debug.bat com3d25"` の形で呼ぶ（`//c` はパス変換抑止のため 2 つ重ねる）。PowerShell から実行する場合は `.\debug.bat com3d25`。

---

### Task 1: csproj のマルチターゲット化

**Files:**
- Modify: `source/COM3D25.PostEffects.Plugin/COM3D25.PostEffects.Plugin.csproj`

**Interfaces:**
- Consumes: なし
- Produces: MSBuild プロパティ `GameVersion`（`COM3D2` / `COM3D25`、既定 `COM3D25`）、`COM3D2_DIR` / `COM3D25_DIR`、`GameDir`。以降のタスクの build.bat はこれらを `/p:` で渡す

このタスクの時点では C# 側の分岐が未実装のため、COM3D2 ビルドは既知のコンパイルエラーで失敗する。それが「失敗するテスト」になる。

- [ ] **Step 1: COM3D2.5 ビルドが現状通り通ることを確認（ベースライン）**

Run: `cmd //c "cd source\COM3D25.PostEffects.Plugin && build.bat debug"`
Expected: `ビルドに成功しました`（このスクリプトはまだ第 2 引数を解釈しないので 2.5 のみビルドされる）

- [ ] **Step 2: 構成別 PropertyGroup からバージョン定数を外す**

`DefineConstants` の 2 箇所を次のように変更する（`COM3D25` を削る。バージョン定数は Step 3 で `GameVersion` から付与する）。

```xml
  <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' ">
    <DebugSymbols>true</DebugSymbols>
    <DebugType>full</DebugType>
    <Optimize>false</Optimize>
    <OutputPath>bin\Debug\</OutputPath>
    <DefineConstants>DEBUG;TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
  </PropertyGroup>
  <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' ">
    <DebugType>pdbonly</DebugType>
    <Optimize>true</Optimize>
    <OutputPath>bin\Release\</OutputPath>
    <DefineConstants>TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
  </PropertyGroup>
```

- [ ] **Step 3: 先頭 PropertyGroup をバージョン非依存にし、GameVersion 用 PropertyGroup を追加**

先頭の `PropertyGroup` から `TargetFrameworkVersion` と `AssemblyName` を削り（バージョン別に設定するため）、
`COM3D2_DIR` を追加する。

```xml
  <PropertyGroup>
    <Configuration Condition=" '$(Configuration)' == '' ">Debug</Configuration>
    <Platform Condition=" '$(Platform)' == '' ">AnyCPU</Platform>
    <ProjectGuid>{8D4F2C7A-91B3-4E6D-A5C8-2F7E0B93D614}</ProjectGuid>
    <OutputType>Library</OutputType>
    <AppDesignerFolder>Properties</AppDesignerFolder>
    <RootNamespace>COM3D25.PostEffects.Plugin</RootNamespace>
    <FileAlignment>512</FileAlignment>
    <Deterministic>true</Deterministic>
    <!-- GameVersion: COM3D25 (既定) または COM3D2。build.bat から /p:GameVersion= で指定する -->
    <GameVersion Condition=" '$(GameVersion)' == '' ">COM3D25</GameVersion>
    <!-- ゲームのインストール先。.env から build.bat 経由で渡される。未指定時は従来の相対パス -->
    <COM3D2_DIR Condition=" '$(COM3D2_DIR)' == '' ">..\..\..\..</COM3D2_DIR>
    <COM3D25_DIR Condition=" '$(COM3D25_DIR)' == '' ">..\..\..\..</COM3D25_DIR>
    <GameDir Condition=" '$(GameVersion)' == 'COM3D2' ">$(COM3D2_DIR)</GameDir>
    <GameDir Condition=" '$(GameVersion)' == 'COM3D25' ">$(COM3D25_DIR)</GameDir>
  </PropertyGroup>
```

続いて、**構成別 PropertyGroup より後**（Step 2 で編集した 2 つの直後）に次を追加する。
順序が逆だと `OutputPath` や `DefineConstants` が上書きされてしまうため必ず後ろに置くこと。

```xml
  <!-- バージョン定数は GameVersion を単一の情報源として付与する
       (構成別 PropertyGroup に直書きすると COM3D2 ビルドで両方が定義されてしまう) -->
  <PropertyGroup>
    <DefineConstants>$(DefineConstants);$(GameVersion)</DefineConstants>
  </PropertyGroup>
  <!-- COM3D2.5 (Unity 2022 / .NET 4.x) -->
  <PropertyGroup Condition=" '$(GameVersion)' == 'COM3D25' ">
    <TargetFrameworkVersion>v4.7.1</TargetFrameworkVersion>
    <AssemblyName>COM3D25.PostEffects.Plugin</AssemblyName>
  </PropertyGroup>
  <!-- COM3D2 (2.0, Unity 5.6 / Mono net35 相当) -->
  <PropertyGroup Condition=" '$(GameVersion)' == 'COM3D2' ">
    <TargetFrameworkVersion>v3.5</TargetFrameworkVersion>
    <AssemblyName>COM3D2.PostEffects.Plugin</AssemblyName>
    <OutputPath>$(OutputPath)COM3D2\</OutputPath>
  </PropertyGroup>
```

- [ ] **Step 4: 参照 ItemGroup をバージョン別に分割**

既存の参照 `ItemGroup` を、共通・COM3D25 専用・COM3D2 専用の 3 つに分ける。
`$(GameDir)` を使うことで Assembly-CSharp 等は自動的に対象バージョンのものを指す。

```xml
  <!-- 両バージョン共通の参照 -->
  <ItemGroup>
    <Reference Include="Assembly-CSharp">
      <HintPath>$(GameDir)\COM3D2x64_Data\Managed\Assembly-CSharp.dll</HintPath>
    </Reference>
    <Reference Include="Assembly-CSharp-firstpass">
      <HintPath>$(GameDir)\COM3D2x64_Data\Managed\Assembly-CSharp-firstpass.dll</HintPath>
    </Reference>
    <Reference Include="Assembly-UnityScript-firstpass">
      <HintPath>$(GameDir)\COM3D2x64_Data\Managed\Assembly-UnityScript-firstpass.dll</HintPath>
    </Reference>
    <Reference Include="UnityEngine">
      <HintPath>$(GameDir)\COM3D2x64_Data\Managed\UnityEngine.dll</HintPath>
    </Reference>
    <Reference Include="System" />
    <Reference Include="System.Core" />
    <Reference Include="System.Xml" />
    <Reference Include="System.Xml.Linq" />
  </ItemGroup>
  <!-- COM3D2 (2.0) 専用: UnityInjector.dll の実体があるのでそれを参照する -->
  <ItemGroup Condition=" '$(GameVersion)' == 'COM3D2' ">
    <Reference Include="UnityInjector">
      <HintPath>$(COM3D2_DIR)\Sybaris\lib\UnityInjector.dll</HintPath>
    </Reference>
  </ItemGroup>
  <!-- COM3D2.5 専用 -->
  <ItemGroup Condition=" '$(GameVersion)' == 'COM3D25' ">
    <!-- COM3D2.5 には UnityInjector.dll の実体がなく、UnityInjector.PluginBase 等は
         BepInEx.UnityInjectorLoader.dll に含まれるため、そちらを参照する -->
    <Reference Include="BepInEx.UnityInjectorLoader">
      <HintPath>$(GameDir)\BepInEx\plugins\UnityInjectorLoader\BepInEx.UnityInjectorLoader.dll</HintPath>
    </Reference>
    <!-- Unity 2022 では UnityEngine がモジュール分割されているため個別参照が必要 -->
    <Reference Include="UnityEngine.CoreModule">
      <HintPath>$(GameDir)\COM3D2x64_Data\Managed\UnityEngine.CoreModule.dll</HintPath>
    </Reference>
    <Reference Include="UnityEngine.IMGUIModule">
      <HintPath>$(GameDir)\COM3D2x64_Data\Managed\UnityEngine.IMGUIModule.dll</HintPath>
    </Reference>
    <Reference Include="UnityEngine.AssetBundleModule">
      <HintPath>$(GameDir)\COM3D2x64_Data\Managed\UnityEngine.AssetBundleModule.dll</HintPath>
    </Reference>
    <Reference Include="UnityEngine.AnimationModule">
      <HintPath>$(GameDir)\COM3D2x64_Data\Managed\UnityEngine.AnimationModule.dll</HintPath>
    </Reference>
    <Reference Include="UnityEngine.ImageConversionModule">
      <HintPath>$(GameDir)\COM3D2x64_Data\Managed\UnityEngine.ImageConversionModule.dll</HintPath>
    </Reference>
    <Reference Include="UnityEngine.InputLegacyModule">
      <HintPath>$(GameDir)\COM3D2x64_Data\Managed\UnityEngine.InputLegacyModule.dll</HintPath>
    </Reference>
    <Reference Include="UnityEngine.TextRenderingModule">
      <HintPath>$(GameDir)\COM3D2x64_Data\Managed\UnityEngine.TextRenderingModule.dll</HintPath>
    </Reference>
    <Reference Include="UnityEngine.ScreenCaptureModule">
      <HintPath>$(GameDir)\COM3D2x64_Data\Managed\UnityEngine.ScreenCaptureModule.dll</HintPath>
    </Reference>
    <Reference Include="UnityEngine.PhysicsModule">
      <HintPath>$(GameDir)\COM3D2x64_Data\Managed\UnityEngine.PhysicsModule.dll</HintPath>
    </Reference>
    <Reference Include="UnityEngine.UnityWebRequestModule">
      <HintPath>$(GameDir)\COM3D2x64_Data\Managed\UnityEngine.UnityWebRequestModule.dll</HintPath>
    </Reference>
  </ItemGroup>
```

`<Compile Include=... />` と `<EmbeddedResource ... />` の `ItemGroup` は変更しない。

- [ ] **Step 5: COM3D2.5 ビルドにリグレッションが無いことを確認**

Run: `cmd //c "cd source\COM3D25.PostEffects.Plugin && build.bat debug"`
Expected: `ビルドに成功しました`。`source\COM3D25.PostEffects.Plugin\bin\Debug\COM3D25.PostEffects.Plugin.dll` が更新される

- [ ] **Step 6: COM3D2 ビルドが既知のエラーだけで落ちることを確認**

Run:
```bash
cd /w/COM3D2_5/work/COM3D2.PostEffects.Plugin/source/COM3D25.PostEffects.Plugin
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" \
  COM3D25.PostEffects.Plugin.csproj /p:Configuration=Debug /p:GameVersion=COM3D2 \
  "/p:COM3D2_DIR=W:\COM3D2" 2>&1 | grep -E "error|エラー" | sort -u | head -40
```
Expected: 参照解決エラー（`HintPath` が見つからない等）は出ず、`PostEffects_Dummy` が見つからない旨と
`TBody.SlotID` のメンバーが無い旨のエラーのみが出る。

ここで**それ以外のエラーが出た場合は Task 2 の対象を広げる必要がある**ため、エラー全文を控えておくこと
（内蔵エフェクト型のメンバ差異が出る可能性を設計書が想定している）。

- [ ] **Step 7: コミット**

```bash
git add source/COM3D25.PostEffects.Plugin/COM3D25.PostEffects.Plugin.csproj
git commit -m "build: csproj を GameVersion によるマルチターゲット構成にする"
```

---

### Task 2: C# 側のバージョン分岐

**Files:**
- Modify: `source/COM3D25.PostEffects.Plugin/Effects/BloomController.cs:3-5`
- Modify: `source/COM3D25.PostEffects.Plugin/Effects/BlurController.cs:3-5`
- Modify: `source/COM3D25.PostEffects.Plugin/Effects/DepthOfFieldController.cs:3-5`
- Modify: `source/COM3D25.PostEffects.Plugin/Effects/GlobalFogController.cs:3-5`
- Modify: `source/COM3D25.PostEffects.Plugin/Effects/VignettingController.cs:3-5`
- Modify: `source/COM3D25.PostEffects.Plugin/Effects/ScreenOverlayController.cs:3`
- Modify: `source/COM3D25.PostEffects.Plugin/Effects/ColorCorrectionCurvesController.cs:4-6, 134-135, 156`
- Modify: `source/COM3D25.PostEffects.Plugin/Effects/HeadMask.cs:18-35`
- Modify: `source/COM3D25.PostEffects.Plugin/PluginInfo.cs:7`

**Interfaces:**
- Consumes: Task 1 の `GameVersion` に連動する `COM3D2` / `COM3D25` プリプロセッサ定数
- Produces: なし（既存の型名・エイリアス名は一切変えない。`BloomEffect` / `BlurFx` / `DepthOfFieldEffect` / `GlobalFogEffect` / `VignettingEffect` / `ScreenOverlayEffect` / `ColorCorrectionCurvesEffect` はそのまま）

**前提（逆コンパイルで確認済み）:** 2.0 では内蔵エフェクトはグローバル名前空間にあり
（`Assembly-UnityScript-firstpass.dll`）、`ColorCorrectionMode` も同様にグローバルな enum で
メンバーは `Simple` / `Advanced`。`ColorCorrectionCurves` の
`useDepthCorrection` / `saturation` / `selectiveCc` / `selectiveFromColor` / `selectiveToColor` /
`colorCorrectionSelectiveShader` / `mode` / `UpdateParameters()` はすべて存在する。

- [ ] **Step 1: 6 ファイルの using エイリアスを分岐させる**

`BloomController.cs` を次のようにする（`using UnityEngine;` の直後）。

```csharp
// Assembly-UnityScript-firstpass のグローバル名前空間にも旧 Bloom が残骸として存在するため、
// ゲームが実際に使う PostEffects_Dummy 側へエイリアスで束縛する
#if COM3D25
using BloomEffect = PostEffects_Dummy.Bloom;
#else
// COM3D2 (2.0) の内蔵エフェクトはグローバル名前空間 (Assembly-UnityScript-firstpass) にある
using BloomEffect = global::Bloom;
#endif
```

同じ形で残り 5 ファイルも書き換える（既存コメントは残す）。

| ファイル | COM3D25 側 | COM3D2 側 |
|---|---|---|
| `BlurController.cs` | `using BlurFx = PostEffects_Dummy.Blur;` | `using BlurFx = global::Blur;` |
| `DepthOfFieldController.cs` | `using DepthOfFieldEffect = PostEffects_Dummy.DepthOfFieldScatter;` | `using DepthOfFieldEffect = global::DepthOfFieldScatter;` |
| `GlobalFogController.cs` | `using GlobalFogEffect = PostEffects_Dummy.GlobalFog;` | `using GlobalFogEffect = global::GlobalFog;` |
| `VignettingController.cs` | `using VignettingEffect = PostEffects_Dummy.Vignetting;` | `using VignettingEffect = global::Vignetting;` |
| `ScreenOverlayController.cs` | `using ScreenOverlayEffect = PostEffects_Dummy.ScreenOverlay;` | `using ScreenOverlayEffect = global::ScreenOverlay;` |

- [ ] **Step 2: ColorCorrectionCurvesController.cs のエイリアスと本文を直す**

ファイル先頭（3 行目 `using UnityEngine;` の直後）を次のようにする。
`ColorCorrectionMode` にもエイリアスを付け、本文から名前空間修飾を外して分岐を using 行だけに閉じ込める。

```csharp
// Assembly-UnityScript-firstpass のグローバル名前空間にも旧 ColorCorrectionCurves が残骸として存在するため、
// ゲームが実際に使う PostEffects_Dummy 側へエイリアスで束縛する
#if COM3D25
using ColorCorrectionCurvesEffect = PostEffects_Dummy.ColorCorrectionCurves;
using ColorCorrectionMode = PostEffects_Dummy.ColorCorrectionMode;
#else
// COM3D2 (2.0) の内蔵エフェクトはグローバル名前空間 (Assembly-UnityScript-firstpass) にある
using ColorCorrectionCurvesEffect = global::ColorCorrectionCurves;
using ColorCorrectionMode = global::ColorCorrectionMode;
#endif
```

本文の 134-135 行目を次のように変える。

```csharp
            component.mode = setting.useDepthCorrection
                ? ColorCorrectionMode.Advanced
                : ColorCorrectionMode.Simple;
```

156 行目のフィールド宣言を次のように変える。

```csharp
        private ColorCorrectionMode _capturedMode;
```

- [ ] **Step 3: HeadMask.cs のスロット配列から 2.0 に無いメンバーを除外する**

`FaceSlots` / `HairSlots` を次のようにする。除外対象は
`accFace` / `hairS_2` / `hairT_2` / `accHead_2` / `accHat_2` の 5 つ。

```csharp
        // 顔面に付くスロット (顔・目・歯・鼻・めがね・顔アクセ)
        private static readonly TBody.SlotID[] FaceSlots =
        {
            TBody.SlotID.head, TBody.SlotID.eye,
            TBody.SlotID.accHa, TBody.SlotID.accHana, TBody.SlotID.megane,
#if COM3D25
            // COM3D2 (2.0) の TBody.SlotID には存在しないスロット
            TBody.SlotID.accFace,
#endif
        };

        // 髪と頭部装飾のスロット (髪・帽子・カチューシャ・耳/髪アクセ等)
        private static readonly TBody.SlotID[] HairSlots =
        {
            TBody.SlotID.hairF, TBody.SlotID.hairR, TBody.SlotID.hairS,
            TBody.SlotID.hairT, TBody.SlotID.hairAho,
            TBody.SlotID.headset, TBody.SlotID.accHead,
            TBody.SlotID.accHat,
            TBody.SlotID.accMiMiR, TBody.SlotID.accMiMiL,
            TBody.SlotID.accKami_1_, TBody.SlotID.accKami_2_, TBody.SlotID.accKami_3_,
            TBody.SlotID.accKamiSubR, TBody.SlotID.accKamiSubL,
#if COM3D25
            // COM3D2 (2.0) の TBody.SlotID には存在しないスロット
            TBody.SlotID.hairS_2, TBody.SlotID.hairT_2,
            TBody.SlotID.accHead_2, TBody.SlotID.accHat_2,
#endif
        };
```

- [ ] **Step 4: PluginInfo.cs の登録名を dll 名に合わせる**

```csharp
        public const string PluginName = "PostEffects";
#if COM3D25
        public const string PluginFullName = "COM3D25." + PluginName + ".Plugin";
#else
        // COM3D2 (2.0) 版は dll 名と揃える (UnityInjector のプラグイン登録名・ログ表記に使われる)
        public const string PluginFullName = "COM3D2." + PluginName + ".Plugin";
#endif
```

- [ ] **Step 5: COM3D2 ビルドが通ることを確認**

Run:
```bash
cd /w/COM3D2_5/work/COM3D2.PostEffects.Plugin/source/COM3D25.PostEffects.Plugin
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" \
  COM3D25.PostEffects.Plugin.csproj /p:Configuration=Debug /p:GameVersion=COM3D2 \
  "/p:COM3D2_DIR=W:\COM3D2" 2>&1 | tail -15
```
Expected: `Build succeeded.` / `0 Error(s)`。`bin/Debug/COM3D2/COM3D2.PostEffects.Plugin.dll` が生成される

**エラーが残った場合の対処:** 内蔵エフェクト型のメンバ名・型がバージョン間で異なるケース。
該当プロパティを `#if` で分岐させるか、それでも解決しない場合はそのエフェクトのコントローラを
COM3D2 ビルドから外す（`csproj` の `Compile Include` を `Condition` 付き ItemGroup に移し、
`PostEffectManager` の登録も `#if` で囲む）。どちらを選んだかはコミットメッセージに記録すること。

- [ ] **Step 6: COM3D2.5 ビルドにリグレッションが無いことを確認**

Run: `cmd //c "cd source\COM3D25.PostEffects.Plugin && build.bat debug"`
Expected: `ビルドに成功しました`

- [ ] **Step 7: コミット**

```bash
git add source/COM3D25.PostEffects.Plugin/Effects source/COM3D25.PostEffects.Plugin/PluginInfo.cs
git commit -m "feat: COM3D2 (2.0) 向けのバージョン分岐を追加"
```

---

### Task 3: COM3D2 用シェーダーバンドルをリポジトリに取り込む

**Files:**
- Create: `UnityInjector20/Config/PostEffects/Shaders/posteffects`
- Delete: `COM3D2.AssetBundles/`（未コミットの作業用フォルダ）

**Interfaces:**
- Consumes: なし
- Produces: `UnityInjector20/` — COM3D2 版の差分アセット置き場。Task 4 の build.bat が dll をここへコピーし、Task 5 の release.bat がここから配布フォルダを組み立てる

- [ ] **Step 1: バンドルを移動する**

```bash
cd /w/COM3D2_5/work/COM3D2.PostEffects.Plugin
mkdir -p "UnityInjector20/Config/PostEffects/Shaders"
mv COM3D2.AssetBundles/AssetBundles/posteffects "UnityInjector20/Config/PostEffects/Shaders/posteffects"
rm -rf COM3D2.AssetBundles
```

`.manifest` と `AssetBundles`（バンドルマニフェスト本体）はビルドの副産物で配布に不要なため持ってこない。

- [ ] **Step 2: 2.5 用バンドルと別物であることを確認**

Run:
```bash
cd /w/COM3D2_5/work/COM3D2.PostEffects.Plugin
ls -l UnityInjector/Config/PostEffects/Shaders/posteffects "UnityInjector20/Config/PostEffects/Shaders/posteffects"
```
Expected: 2 ファイルともサイズが表示され、サイズが異なる（2.5 用は約 21KB、2.0 用は約 27KB）

- [ ] **Step 3: dll が誤って混入しないよう .gitignore を確認する**

Run: `git check-ignore -v UnityInjector20/COM3D2.PostEffects.Plugin.dll; git status --porcelain`
Expected: `UnityInjector/COM3D25.PostEffects.Plugin.dll` が追跡されているのと同じ扱い（無視されない）。
`COM3D2.AssetBundles/` が消え、`UnityInjector20/` が新規として出る

- [ ] **Step 4: コミット**

```bash
git add UnityInjector20
git commit -m "chore: COM3D2 (2.0) 用 posteffects シェーダーバンドルを追加"
```

---

### Task 4: build.bat / debug.bat / .env のターゲット対応

**Files:**
- Modify: `source/COM3D25.PostEffects.Plugin/build.bat`
- Modify: `debug.bat`
- Modify: `.env`
- Modify: `.env.sample`

**Interfaces:**
- Consumes: Task 1 の `GameVersion` / `COM3D2_DIR` / `COM3D25_DIR`、Task 3 の `UnityInjector20/`
- Produces: `build.bat <debug|release> [com3d2|com3d25|all]`（第 2 引数の既定は `all`）、`debug.bat [com3d2|com3d25|all]`（既定は `com3d25`）

- [ ] **Step 1: .env と .env.sample に COM3D2_DIR を追加**

`.env.sample`:

```
# COM3D2.5 のインストール先 (アセンブリ参照とビルド成果物のコピーに使用)
COM3D25_DIR=W:\COM3D2_5
# COM3D2 (2.0) のインストール先 (COM3D2 版のビルドに使用)
COM3D2_DIR=W:\COM3D2
```

`.env`（gitignore 済みの開発者ローカル設定）にも同じく `COM3D2_DIR=W:\COM3D2` の行を追加する。

- [ ] **Step 2: build.bat をターゲット対応に書き換える**

`source/COM3D25.PostEffects.Plugin/build.bat` を次の内容にする。

```bat
@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

cd /d %~dp0

set REPO_DIR=%~dp0..\..

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

if "%CONFIG%"=="Release" (
    %MSBUILD_PATH% COM3D25.PostEffects.Plugin.csproj /t:Clean /p:Configuration=Debug
    %MSBUILD_PATH% COM3D25.PostEffects.Plugin.csproj /t:Clean /p:Configuration=Release
    if !ERRORLEVEL! neq 0 (
        echo クリーンビルドに失敗しました
        exit /b 1
    )
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
echo === ビルド中 ^(COM3D2.5 / %CONFIG%^) ===
%MSBUILD_PATH% COM3D25.PostEffects.Plugin.csproj /p:Configuration=%CONFIG% /p:GameVersion=COM3D25 "/p:COM3D25_DIR=%COM3D25_DIR%"
if !ERRORLEVEL! neq 0 (
    echo COM3D2.5 版のビルドに失敗しました
    exit /b 1
)

rem リリースパッケージ用に リポジトリ内 UnityInjector へコピー
if not exist "%REPO_DIR%\UnityInjector" mkdir "%REPO_DIR%\UnityInjector"
copy /y bin\%CONFIG%\COM3D25.PostEffects.Plugin.dll "%REPO_DIR%\UnityInjector\"
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
copy /y bin\%CONFIG%\COM3D25.PostEffects.Plugin.dll "%COM3D25_DIR%\Sybaris\UnityInjector\" >nul
if !ERRORLEVEL! neq 0 (
    echo 警告: COM3D2.5 へのデプロイに失敗しました ^(ゲーム起動中?^)
) else (
    echo COM3D2.5 へデプロイしました
)
exit /b 0

rem ============ COM3D2 (2.0) 版 ============
:build_com3d2
echo === ビルド中 ^(COM3D2 / %CONFIG%^) ===
%MSBUILD_PATH% COM3D25.PostEffects.Plugin.csproj /p:Configuration=%CONFIG% /p:GameVersion=COM3D2 "/p:COM3D2_DIR=%COM3D2_DIR%"
if !ERRORLEVEL! neq 0 (
    echo COM3D2 版のビルドに失敗しました
    exit /b 1
)

rem リリースパッケージ用に リポジトリ内 UnityInjector20 へコピー
if not exist "%REPO_DIR%\UnityInjector20" mkdir "%REPO_DIR%\UnityInjector20"
copy /y bin\%CONFIG%\COM3D2\COM3D2.PostEffects.Plugin.dll "%REPO_DIR%\UnityInjector20\"
if !ERRORLEVEL! neq 0 (
    echo dllのコピーに失敗しました
    exit /b 1
)

rem シェーダーバンドルのデプロイ (共通の Config を入れた後、2.0 用 posteffects で上書きする)
xcopy /y /e /i /q "%REPO_DIR%\UnityInjector\Config\PostEffects" "%COM3D2_DIR%\Sybaris\UnityInjector\Config\PostEffects" >nul
if !ERRORLEVEL! neq 0 (
    echo 警告: シェーダーバンドルのデプロイに失敗しました
)
xcopy /y /e /i /q "%REPO_DIR%\UnityInjector20\Config\PostEffects" "%COM3D2_DIR%\Sybaris\UnityInjector\Config\PostEffects" >nul
if !ERRORLEVEL! neq 0 (
    echo 警告: COM3D2 用シェーダーバンドルのデプロイに失敗しました
)

rem ゲームへのデプロイ ※ゲーム起動中はロックされるため失敗しても続行
copy /y bin\%CONFIG%\COM3D2\COM3D2.PostEffects.Plugin.dll "%COM3D2_DIR%\Sybaris\UnityInjector\" >nul
if !ERRORLEVEL! neq 0 (
    echo 警告: COM3D2 へのデプロイに失敗しました ^(ゲーム起動中?^)
) else (
    echo COM3D2 へデプロイしました
)
exit /b 0
```

`PLUGIN_NAME` 変数はバージョンごとに dll 名が変わるため廃止し、各サブルーチンで直書きしている。

- [ ] **Step 3: debug.bat に引数を通す**

`debug.bat` を次の内容にする。既定は `com3d25`（日常の開発イテレーションを軽く保つため）。

```bat
@echo off
chcp 65001
setlocal

rem 引数: %1 = com3d2/com3d25/all (既定 com3d25)
set TARGET=%~1
if "%TARGET%"=="" set TARGET=com3d25

call .\source\COM3D25.PostEffects.Plugin\build.bat debug %TARGET%
if %ERRORLEVEL% neq 0 (
    echo ビルドに失敗しました
    exit /b 1
)

echo ビルドに成功しました
exit /b 0
```

- [ ] **Step 4: 各ターゲットのビルドを確認**

Run: `cmd //c "debug.bat com3d25"`
Expected: `ビルドに成功しました`。`UnityInjector\COM3D25.PostEffects.Plugin.dll` が更新される

Run: `cmd //c "debug.bat com3d2"`
Expected: `ビルドに成功しました`。`UnityInjector20\COM3D2.PostEffects.Plugin.dll` が生成され、
`W:\COM3D2\Sybaris\UnityInjector\COM3D2.PostEffects.Plugin.dll` と
`W:\COM3D2\Sybaris\UnityInjector\Config\PostEffects\Shaders\posteffects` が配置される

Run: `cmd //c "debug.bat all"`
Expected: 両方ビルドされ `ビルドに成功しました`

- [ ] **Step 5: 不正なターゲットが弾かれることを確認**

Run: `cmd //c "debug.bat com3d3"`
Expected: `不正なターゲットです: com3d3` と表示され、終了コードが 0 以外

- [ ] **Step 6: COM3D2 用バンドルが 2.0 側に正しく置かれたことを確認**

Run:
```bash
ls -l "/w/COM3D2/Sybaris/UnityInjector/Config/PostEffects/Shaders/posteffects" \
      "/w/COM3D2_5/work/COM3D2.PostEffects.Plugin/UnityInjector20/Config/PostEffects/Shaders/posteffects"
```
Expected: 2 ファイルのサイズが一致する（共通 Config の 2.5 用バンドルで上書きされていないこと）

- [ ] **Step 7: コミット**

```bash
git add source/COM3D25.PostEffects.Plugin/build.bat debug.bat .env.sample UnityInjector20
git commit -m "build: build.bat / debug.bat をターゲット指定に対応させる"
```

---

### Task 5: release.bat の 2 フォルダ同梱パッケージ化

**Files:**
- Modify: `release.bat`

**Interfaces:**
- Consumes: Task 4 の `build.bat release all`、`UnityInjector/` と `UnityInjector20/`
- Produces: `output\COM3D25.PostEffects.Plugin-v<VERSION>.zip`（`UnityInjector\` と `UnityInjector (COM3D2)\` を含む）。zip 名は従来どおりで `deploy.ps1` の期待と一致する

- [ ] **Step 1: release.bat を書き換える**

```bat
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
```

- [ ] **Step 2: リリースパッケージを作る**

Run: `cmd //c "release.bat"`
Expected: `ビルドに成功しました`。`output\COM3D25.PostEffects.Plugin-v2.0.0.0.zip` が生成される

- [ ] **Step 3: zip の中身を検証する**

Run:
```bash
cd /w/COM3D2_5/work/COM3D2.PostEffects.Plugin
powershell -NoProfile -Command "Add-Type -A System.IO.Compression.FileSystem; [IO.Compression.ZipFile]::OpenRead((Resolve-Path 'output\COM3D25.PostEffects.Plugin-v2.0.0.0.zip')).Entries | Where-Object { \$_.FullName -match 'dll$|posteffects$' } | ForEach-Object { '{0}  {1}' -f \$_.Length, \$_.FullName }"
```
Expected: 次の 4 エントリが並ぶ。`posteffects` の 2 つはサイズが異なる
- `.../UnityInjector/COM3D25.PostEffects.Plugin.dll`
- `.../UnityInjector/Config/PostEffects/Shaders/posteffects`
- `.../UnityInjector (COM3D2)/COM3D2.PostEffects.Plugin.dll`
- `.../UnityInjector (COM3D2)/Config/PostEffects/Shaders/posteffects`

`UnityInjector (COM3D2)/COM3D25.PostEffects.Plugin.dll` が**存在しない**ことも確認する。

- [ ] **Step 4: コミット**

```bash
git add release.bat UnityInjector UnityInjector20
git commit -m "build: リリース zip に COM3D2 版フォルダを同梱する"
```

---

### Task 6: 配布ドキュメントの更新

**Files:**
- Modify: `deploy.ps1`（Release Note のダウンロード表）
- Modify: `README.md:1-46`
- Modify: `docs/com3d20-support-notes.md`（先行調査メモに決着を追記）

**Interfaces:**
- Consumes: Task 5 の zip 構成（フォルダ名 `UnityInjector` / `UnityInjector (COM3D2)`）
- Produces: なし

- [ ] **Step 1: deploy.ps1 の Release Note を 2 バージョン対応にする**

`$notes` の here-string の冒頭を次のように書き換える。

```powershell
$notes = @"
### ダウンロード
[$zipName]($downloadUrl)

zip を解凍し、お使いのバージョンに対応したフォルダの中身を ``Sybaris\UnityInjector\`` へコピーしてください。

| 対象 | 同梱フォルダ |
|---|---|
| **COM3D2** | ``UnityInjector (COM3D2)`` |
| **COM3D2.5** | ``UnityInjector`` |

``Config\PostEffects`` を入れ忘れると内蔵エフェクト以外が動作しません。

### 変更点
$changes
"@
```

- [ ] **Step 2: Release Note の生成結果を確認する**

Run: `cmd //c "deploy.bat -DryRun"`（`deploy.bat` は引数をそのまま `deploy.ps1` へ渡す）
Expected: `--- Release Note (dry run) ---` の下に上記の表が正しく描画され、
バッククォートがエスケープされずに `` `UnityInjector (COM3D2)` `` として出力される

**注意:** `-DryRun` はビルドとアップロードを行わないが、作業ツリーが汚れていると
リリース前チェックで停止する。その場合は Step 1 をコミットしてから再実行するか、
検証をスキップして Step 3 に進み、次回リリース時に確認する。

- [ ] **Step 3: README.md の非対応宣言を書き換える**

3-4 行目:

```markdown
COM3D2 / COM3D2.5 のメインカメラに多彩なポストエフェクトを適用し、GUI から制御するプラグイン。
**COM3D2 (2.0) と COM3D2.5 (Unity 2022) の両方に対応**（バージョンごとに dll が分かれています）。
```

16-35 行目の構成図とコピー手順:

````markdown
zip を解凍すると次の構成になっています。

```
COM3D25.PostEffects.Plugin\
├── README.txt
├── UnityInjector\                    ← COM3D2.5 用
│   ├── COM3D25.PostEffects.Plugin.dll
│   └── Config\
│       └── PostEffects\
└── UnityInjector (COM3D2)\           ← COM3D2 (2.0) 用
    ├── COM3D2.PostEffects.Plugin.dll
    └── Config\
        └── PostEffects\
```

**お使いのバージョンに対応したフォルダ**の中身を、ゲームフォルダの `Sybaris\UnityInjector\` へ
そのままコピーしてください。配置後は以下のようになります（COM3D2.5 の例）。

```
（ゲームフォルダ）\Sybaris\UnityInjector\
├── COM3D25.PostEffects.Plugin.dll
└── Config\
    └── PostEffects\
```
````

37-46 行目の説明:

```markdown
各ファイルの説明:
- `COM3D25.PostEffects.Plugin.dll` / `COM3D2.PostEffects.Plugin.dll`
  - プラグインの本体。バージョンに合った方だけを入れてください。
- `Config\PostEffects\`
  - シェーダーバンドル・テクスチャ・サンプルプリセット等。**必須**。
  - 入れ忘れると、ゲーム内蔵エフェクト以外が動作しません。
  - シェーダーバンドルはバージョンごとに中身が異なるため、両方のフォルダを混ぜないでください。

COM3D2.5 Ver.3.49.0 で動作確認済みです。
```

COM3D2 (2.0) 側の動作確認バージョンはユーザーの手動テスト後に追記するため、
この時点では書かない（Step 5 で確認を依頼する）。

- [ ] **Step 4: 先行調査メモに決着を追記する**

`docs/com3d20-support-notes.md` の先頭（`# COM3D2 (2.0) 対応調査メモ` の直後）に追記する。

```markdown
> **2026-08-19 追記:** 本メモの懸念は逆コンパイル調査で全て決着し、対応を実装済み。
> 確定した設計は `docs/superpowers/specs/2026-08-19-com3d2-support-design.md`、
> 実装手順は `docs/superpowers/plans/2026-08-19-com3d2-support.md` を参照。
> 本メモは調査当時の記録として残す。
```

- [ ] **Step 5: コミット**

```bash
git add deploy.ps1 README.md docs/com3d20-support-notes.md
git commit -m "docs: COM3D2 (2.0) 対応を README とリリースノートに反映"
```

- [ ] **Step 6: 実機確認をユーザーに依頼する**

以下を報告し、確認結果を待つ。

1. COM3D2 (2.0) を起動してプラグインがロードされること（ギアメニューのアイコン、または `Alt+P`）
2. ゲーム内蔵エフェクト（ブルーム・ビネット・被写界深度・カラーカーブ等）が動作すること
3. 自前シェーダー系エフェクト（CasSharpen / Diffusion / Halftone / Kuwahara / GTToneMap /
   リムライト等のキャラマスク使用エフェクト）が動作すること
4. COM3D2.5 側にリグレッションが無いこと

確認できた COM3D2 のバージョン番号を README の動作確認欄へ追記する。

---

## 実装後

CLAUDE.md のフローに従い、`code-review` スキルでレビューしてからユーザーに提示すること。
