# COM3D2 (2.0) 対応調査メモ

COM3D2.5 向けの本プラグインを COM3D2 (2.0, Unity 5.6 世代, `W:\COM3D2`) でも動かす場合の障害点調査（2026-08-10 時点）。

## 結論

作り直しは不要。本質的な障壁は以下の 2 点で、C# コード側は 2.5 固有ゲーム API への依存がほぼない。

1. 自前シェーダーバンドル `posteffects` の Unity 5.6 リビルド
2. csproj の参照 DLL / TargetFramework の二重化

## 致命的（対応必須）

### 1. `posteffects` バンドルが Unity 2022.3 ビルド

- 根拠: `UnityProject/ProjectSettings/ProjectVersion.txt`（2022.3.62f2）、`Effects/EffectShaders.cs:20-21`
- AssetBundle は前方互換がないため、Unity 5.6 ランタイムでは 2022.3 製バンドルをロードできない
- `UnityProject/Assets/Shaders/`（CasSharpen, Diffusion, CharacterMask/マスク系等）を **Unity 5.6 エディタで別途リビルド**する必要がある
- シェーダー記法（HLSL 機能・SM ターゲット）が 5.6 のシェーダーコンパイラを通るかも個別検証が必要
- 未対応の場合、このバンドルに依存するエフェクト（CasSharpen, Diffusion, CharacterMask, リムライト系）は全滅

### 2. 参照アセンブリ構成の全面差し替え

- 2.0 の `W:\COM3D2\COM3D2x64_Data\Managed\` は**単一 `UnityEngine.dll`**（+ UI/Networking/VR のみ）で、csproj が参照するモジュール分割 DLL 12 本（CoreModule 等）が存在しない
- TargetFramework v4.7.1 → 2.0 の Mono（net35 相当）へ変更
- 機械的作業だが csproj はほぼ作り直し

## 要修正（確認込み）

| 項目 | 箇所 | 内容 |
|---|---|---|
| ローダー参照 | csproj:50-51 | `BepInEx.UnityInjectorLoader.dll` → 2.0 の本物の `UnityInjector.dll`。API 互換のためコード変更はほぼ不要 |
| CameraMain 上書き対抗 | `EffectControllerBase.cs:28-33`, `BloomController.cs:114`, `PostEffectManager.cs:122` | 「CameraMain.Update が毎フレーム bloom を上書きするので LateUpdate で対抗」は 2.5 実機挙動前提。2.0 の `CameraMain.Update` が同挙動か ilspycmd で要確認 |
| private リフレクション | `MTEUtils.cs:174,180` | `CameraMain.UIHide` / `UIResume` が 2.0 に存在するか要確認 |
| MTEUtils 分岐 | csproj DefineConstants, `MTEUtils.cs:100-113` 等 | `#if COM3D25` を外した 2.0 ビルド構成を追加（MTEUtils は元々 2.0 MTE 由来） |
| 内蔵シェーダー解決 | `DepthOfFieldController.cs:355-358` | `Shader.Find` で解決するゲーム内蔵シェーダーが 2.0 側にあるか要確認 |
| BCL 使用箇所 | 全体 | net35 に無い 4.x 専用型（`ConcurrentDictionary`, `Task` 等）を使っていないか全数確認 |

## 軽微・朗報

- **2.5 固有ボディ API への依存はゼロ**: `TBody.goSlot` の 2 次元アクセス、`IsCrcBody`、`CM3.newBodyPartsMask` は grep で不検出
- 使用中の Unity 描画 API（`CommandBuffer` / `CameraEvent` / `GetTemporaryRT` / `Graphics.Blit` / `DepthTextureMode` 等）は全て Unity 5.x 世代から存在。SRP / GraphicsFormat / TextMeshPro の使用なし。`SceneManager.sceneLoaded` も 5.4+ で OK
- SceneCapture 由来の 5 バンドル（imageeffects / kino / cinematic / lightshafts / filmic）は**元々 Unity 5.6 世代ビルド**（`EffectShaders.cs:9-11`）のためそのまま動く見込み
- `TBody.SlotID` の 2.5 追加スロット（`HeadMask.cs:18-34` の hairS_2, accHead_2 等）は 2.0 の enum に無ければ該当スロットを削るだけ
- 名前空間 / アセンブリ名の COM3D25 表記のリネームは任意

## 2.0 側で ilspycmd による差分確認が必要なシンボル候補

例: `ilspycmd -t CameraMain "W:/COM3D2/COM3D2x64_Data/Managed/Assembly-CSharp.dll"`

- `CameraMain`: `Update`（bloom 上書きの有無）、`UIHide` / `UIResume`（private）、`GetBloom` 相当
- `GameMain.Instance.MainCamera` の型と公開メンバー
- `TBody.SlotID` の enum メンバー（HeadMask で使う全スロット）
- `Maid` 一覧取得経路（CharacterMgr 系）
- ゲーム内蔵 Bloom / DepthOfField コンポーネント（UnityStandardAssets 系）の同梱有無

## 着手時の最初の関門

Unity 5.6 エディタ環境の用意（`posteffects` バンドルの再生成用）。
