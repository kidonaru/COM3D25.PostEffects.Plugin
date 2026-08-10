# SceneCapture → PostEffects 移植 UI 差分調査

SceneCapture.Plugin（移植元）と PostEffects.Plugin（移植先）のポストエフェクト UI の差分をまとめたもの。
操作感を揃える際の TODO 判断材料として使う。

調査日: 2026-08-09

- 移植元: `W:\COM3D2_5\work\COM3D2.SceneCapture.Plugin`
- 移植先: `W:\COM3D2_5\work\COM3D25.PostEffects.Plugin`

## 1. UI パラダイムの違い（最大の差分）

| 観点 | 移植元 | 移植先 |
|---|---|---|
| エフェクト一覧 | 全ペインを縦スクロールで一覧表示、複数同時展開可（`EffectWindow.cs:156-198`） | カテゴリ ComboBox で絞り込み、縦スクロールで一覧表示、複数同時展開可（`MainWindow.cs:275-328`） |
| 有効/無効 | ペイン見出し自体がチェックボックス。ON で展開、OFF で折り畳み（`BasePane.cs:33-36`） | 行のチェックボックスが有効トグルを兼ねる。ON で展開、OFF で折り畳み（`MainWindow.cs:330-363`） |
| ウィンドウ | 画面右端に画面高いっぱい・解像度依存フォントサイズ（`SceneCapture.cs:318-328,403-433`） | 右下グリップでリサイズ可能（最小 400x320）・位置とサイズを Config に永続化（`MainWindow.cs:13-20,87-106,201-227`） |

一覧性・複数同時編集は移植先でも同等になった。移植先はさらにカテゴリ絞り込みと位置記憶を持つ。
移植元にある一括 ON/OFF は未移植。

## 2. エフェクト別パラメータ差分

### Bloom
移植元 `BloomPane.cs` / 移植先 `Effects/BloomController.cs`

| パラメータ | 移植元 | 移植先 | 差分 |
|---|---|---|---|
| tweakMode (Basic/Complex) | ComboBox `:25` | **なし** | 移植不要と判断済み。`PostEffects_Dummy.Bloom.tweakMode` はフィールドが存在するだけでエフェクト側から一切参照されない（Unity 標準 Bloom の Inspector 表示切替専用フィールド）ため、移植しても描画は変わらない |
| bloomIntensity | Slider 0〜2.85 | Slider 0〜5 | max 差 |
| flareRotation | 0〜50 `:61` | 0〜6.28 | 対応不要と判断済み。`PostEffects_Dummy.Bloom` は `Mathf.Cos(flareRotation)` を直接呼ぶラジアン単位のため移植先が正しい（移植元の 0〜50 が異常値） |
| その他（hdr / blendMode / quality / threshold / iterations / spread / lensflare 系 / 各色） | — | — | ほぼ同等 |

### Blur
移植元 `BlurPane.cs` / 移植先 `Effects/BlurController.cs`

※ 移植元の BlurPane は `EffectWindow` から未使用のデッドコード（UI に出ない）。範囲差の実害は薄い。

| パラメータ | 移植元 | 移植先 | 差分 |
|---|---|---|---|
| blurIterations | 0〜10 | 1〜4 | 上限減 |
| downsample | 1〜4 | 0〜2 | 範囲不一致 |
| blurType (SGX ガウス) | なし | Toggle `:876` | 移植先で追加 |

### Vignetting
移植元 `VignettingPane.cs`（同じくデッドコード） / 移植先 `Effects/VignettingController.cs`

| パラメータ | 移植元 | 移植先 | 差分 |
|---|---|---|---|
| mode | Toggle（Advanced 可否）`:25` | ComboBox `:967` | ウィジェット種別差 |
| intensity | -20〜20 | -5〜5 | 範囲が狭い（ゲーム側の既定値 -3.98 を収めるため -5〜5 に拡大済み） |
| blur / blurSpread / blurDistance | -20〜20 | 0〜5 / 0〜10 / 0〜10 | 負値不可に |
| chromaticAberration | -20〜20 | -5〜5 | 範囲差 |
| axialAberration | 0〜20 | 0〜5 | 範囲差 |
| Advanced 項目の出し分け | なし（常時表示） | mode==Advanced 時のみ表示 `:1037` | 移植先で改善 |

### DepthOfField
移植元 `DepthOfFieldPane.cs` / 移植先 `Effects/DepthOfFieldController.cs`

| パラメータ | 移植元 | 移植先 | 差分 |
|---|---|---|---|
| focalLength | 0〜20 | 0〜50 | 範囲拡大 |
| maxBlurSize | 0〜20 | 0〜20 | 対応済み（移植元に合わせて上限拡大） |
| blurType (DX11) | Toggle `:29` | 「DX11 ボケ」Toggle | 対応済み。未対応環境（shaderLevel < 50 / コンピュート非対応）ではその旨を表示 |
| dx11BokehScale / Intensity / Threshold / SpawnHeuristic | Slider ×4 `:48-57` | Slider ×4（DX11 ボケ時のみ表示） | 対応済み。しきい値はエフェクト側のクランプに合わせて 0.005〜4 |
| dx11BokehTexture | CustomImagePicker `:60` | パス入力＋画像選択ウィンドウ | 対応済み（`TextureFileCache`。テクスチャ指定を持つ全エフェクト共通の UI） |
| foregroundOverlap | -20〜20（常時） | 0.1〜5（nearBlur 時のみ） | 範囲＋出し分け差 |
| メイド追従 | Toggle + `<` `>` 送りボタン + 再読込ボタン `:86-114` | Toggle + ComboBox `:137-152` | 操作系が別物 |
| その他（visualizeFocus / focalSize / aperture / sampleCount / highRes / nearBlur） | — | — | 同等 |

### ColorCorrectionCurves
移植元 `ColorCorrectionCurvesPane.cs` / 移植先 `Effects/ColorCorrectionCurvesController.cs`

| パラメータ | 移植元 | 移植先 | 差分 |
|---|---|---|---|
| red/green/blueChannel | **CustomCurve（カーブエディタ）** `:23-29` | CurveEditorWindow によるカーブ編集 | 対応済み（GUIView 基盤で新規実装。プロット上でキー追加/移動/削除） |
| mode (深度補正) | Toggle `:17` | 「深度補正」Toggle | 対応済み。ON 時は選択的色補正を排他（後述のゲーム側バグの回避策） |
| depthRed/Green/BlueChannel | CustomCurve ×3 `:32-38` | CurveEditorWindow ×3（深度補正時のみ表示） | 対応済み |
| zCurve | CustomCurve `:41` | CurveEditorWindow「奥行き」 | 対応済み |
| saturation | 0〜10 | 0〜10 | 対応済み（効果側にクランプなしを確認の上、上限拡大） |
| selectiveCc / from / to | — | — | 同等（深度補正 ON 時は使用不可） |

> **ゲーム側のバグ**: `PostEffects_Dummy.ColorCorrectionCurves.CheckResources` は深度補正用の `ccDepthMaterial` を
> `colorCorrectionCurvesShader` ではなく `colorCorrectionSelectiveShader` から生成している（Unity 標準実装との差異）。
> そのままでは深度補正が `Hidden/ColorCorrectionSelective`（`_RgbTex` / `_ZCurve` を持たない）で描画されて絵が壊れるため、
> プラグイン側では深度補正中だけ `colorCorrectionSelectiveShader` に `Hidden/ColorCorrectionCurves` を差し込んで回避している。
> 同フィールドを選択的色補正と共有する構造上、深度補正と選択的色補正は併用できない。

### GlobalFog
移植元 `GlobalFogPane.cs`（デッドコード） / 移植先 `Effects/GlobalFogController.cs`

| パラメータ | 移植元 | 移植先 | 差分 |
|---|---|---|---|
| fogMode 既定値 | AbsoluteYAndDistance（`GlobalFogDef.cs:12`） | AbsoluteYAndDistance | 対応済み |
| globalDensity | 0〜5 | 0〜10 | 範囲差 |
| height | 0〜50 | -100〜100 | 負値可に |
| heightScale | 1〜100 | 0.1〜500 | 範囲差 |
| startDistance | 0〜200（既定 200） | 0〜200（既定 200） | 対応済み |
| adjustHeightScale / adjustStartDistance（自動調整） | ToggleButton ×2 `:42-45` | **なし** | 移植不要と判断済み。移植元でも `GlobalFogDef.AdjustHeightScale` / `AdjustStartDistance` は値を保持するだけでどこからも参照されないデッドコード（自動調整の実装自体が存在しない） |

### ScreenOverlay
移植元に該当ペインなし（移植先の新規実装、`Effects/ScreenOverlayController.cs`）。
テクスチャ指定はパス入力＋「選択」ボタン（`Effects/TextureFileCache.cs` / `MTEUtils/TexturePickerWindow.cs`）。

## 3. 共通 UI 操作の差分

| 観点 | 移植元 | 移植先 | 評価 |
|---|---|---|---|
| リセット | ペイン共通 `\|` ボタン + ペイン固有 `×`（`BasePane.cs:37-45`）。スライダー個別リセットは UI 露出なし | エフェクト単位「リセット」+ スライダー個別 `R` + カラー個別 `R`（`GUIView.cs:2245-2251,2287`） | 移植先が優位 |
| 数値直接入力 | 全スライダーに常設テキストフィールド（`CustomSlider.cs:60-75`） | FloatField + `<` `>` ステップボタン（`GUIView.cs:2217-2238`） | 移植先が優位 |
| カラーピッカー | スウォッチ横にインラインポップアップ、RGB/RGBA 切替（`CustomColorPicker.cs:40-48`） | 別ウィンドウ。SV マップ / Hue / アルファ / HSVA スライダー / hex 入力（`ColorPickerWindow.cs:320-385`） | 機能は移植先が上、導線は 1 段深い |
| 画像選択 | サムネイル 1 枚クリック → インラインポップアップの一覧（`CustomImagePicker.cs:49`） | パス入力欄＋`<` `>` 送り＋フォルダアイコン → 別ウィンドウの一覧（`MTEUtils/TexturePickerWindow.cs`） | 対応済み。導線は 1 段深いが、前後送りと任意パス指定が使える |
| プリセット | 保存/読込/削除 + **読込対象の部分選択チェック（Effects/Lights/Models/Camera/Misc）** + 自動プリセット（`DataWindow.cs:31-68,220`） | 名前入力＋保存、一覧から読込/削除、固定プリセット「デフォルト」＋起動時プリセット指定（`MainWindow.cs:365-425`） | 起動時読込は対応済み。部分読込が未移植 |
| 言語切替 | languageBox ComboBox + `Translation.GetText`（`DataWindow.cs:25-26`） | 日本語ハードコード | 未移植 |
| 強制更新 | ForceUpdate チェックボックス（`EffectWindow.cs:19-21`） | なし（常時 LateUpdate 適用 `EffectControllerBase.cs:312-315`） | 未移植（常時適用のため不要かも） |

## 4. 未移植の UI 機能一覧

1. ~~全エフェクトの縦スクロール一覧＋複数同時展開~~ — 対応済み。`MainWindow.cs` をカテゴリ ComboBox（`Effects/EffectCategory.cs`）＋固定高スクロールの行一覧に組み替え、行のチェックボックスで有効化と展開を兼ねる
2. モード切替ウィンドウ（効果/モデル/環境/データのタブ）— `ModeSelectWindow.cs`
3. 言語切替 / 多言語ラベル — `DataWindow.cs:25`
4. ForceUpdate トグル — `EffectWindow.cs:19`
5. プリセット部分読込チェック群 — `DataWindow.cs:41-68`（自動プリセット読込は対応済み。編集不可の固定プリセット「デフォルト」を一覧先頭に置き、行の「既定」ボタンで起動時に読むプリセットを指定する）
6. ~~アニメーションカーブエディタ~~ — 対応済み。`MTEUtils/CurveEditorWindow.cs` + `GUIView.DrawCurve` として新規実装（深度カーブ用途への展開は未対応）
7. ~~画像ファイルピッカー~~ — 対応済み。`MTEUtils/TexturePickerWindow.cs` として新規実装し、`TextureFileCache.DrawPathField` に「選択」ボタンを追加した。既定画像は `Config\PostEffects\Images\{Bokeh,LUTs,LensDirt}` に同梱（パス直接入力欄も併存）
8. ~~Bloom tweakMode 切替~~ — 移植不要。エフェクト側から参照されない Inspector 専用フィールドのため効果なし
9. ~~DoF DX11 ボケ一式~~ — 対応済み。`Effects/DepthOfFieldController.cs`（blurType トグル + 4 スライダー + ボケテクスチャパス）
10. DoF メイド送り `<` `>` / メイド再読込ボタン — `DepthOfFieldPane.cs:86-106`（移植先は ComboBox で代替）
11. ~~ColorCorrection 深度カーブ（depthR/G/B, zCurve）と mode トグル~~ — 対応済み。`Effects/ColorCorrectionCurvesController.cs`
12. ~~GlobalFog 自動調整トグル~~ — 移植不要。移植元でもデッドコードで自動調整の実装が存在しない
13. 解像度に応じたフォントサイズ自動調整 — `SceneCapture.cs:403-411`

## 5. 未移植のエフェクト一覧

移植元 `EffectWindow.cs:22-88` に存在した 32 種の一覧。**2026-08-09 時点ですべて決着済み**
（30 種を移植、CinematicBloomLayer は移植不可、EffectMask は移植対象外。いずれも下記に理由を記載）:

| 分類 | エフェクト |
|---|---|
| 色調 | ~~Sepia, Grayscale, Contrast, Ramp, ColorCorrectionLut, TonemappingColorGrading~~（すべて移植済み・下記参照） |
| ブルーム/光 | ~~CinematicBloom, FilmicBloom, Streak, SunShafts, LightShafts~~（移植済み）, ~~CinematicBloomLayer~~（移植不可・下記参照） |
| ボケ/被写界深度 | ~~CinematicDepthOfField, Bokeh, FilmicBokeh, FilmicMedianFilter, TiltShiftHdr~~（すべて移植済み） |
| 輪郭/線画 | ~~EdgeDetect~~（移植済み）, ~~EdgeDetect2~~（移植不要・下記参照）, ~~Crease~~（移植済み）, ~~Isoline~~（移植済み） |
| ノイズ/グリッチ | ~~NoiseAndGrain, AnalogGlitch, DigitalGlitch~~（すべて移植済み） |
| レンズ/歪み | ~~Fisheye, CinematicLensAberrations~~（すべて移植済み） |
| フォグ/遮蔽 | ~~StylisticFog, Obscurance~~（すべて移植済み） |
| その他 | ~~Antialiasing, MotionBlur, FilmicLetterBox, MaidHide~~（移植済み）, ~~EffectMask~~（移植対象外・下記参照） |

## 5.1 未移植エフェクトの移植可否調査（2026-08-09 実機検証）

結論: **32 種すべて技術的に移植可能**。ブロッカーは存在しない。
（その後の実作業で 30 種を移植し、残る 2 種は移植する意味が無いと判断した。詳細は下記「移植進捗」の末尾）

- 移植元の 32 エフェクトはゲーム側実装ではなく、**SceneCapture 自前の MonoBehaviour（逆コンパイル済みソースあり）＋別配布のシェーダー AssetBundle 5 個**で実現されている
  - バンドル: `W:\COM3D2\Sybaris\UnityInjector\Config\SceneCapture\Shaders\{imageeffects, kino, cinematic, lightshafts, filmic}`（計約 2MB）
  - シェーダーの結線は `Util.GetComponentVar<T>` がリフレクションで `Shader` 型フィールドを命名規則ベースにバンドルから解決（`Util.cs:191-241`）
- **実機検証（COM3D2.5 = Unity 2022.3.62f2、devbridge 経由）**:
  - 5 バンドルすべて `AssetBundle.LoadFromFile` で読み込み成功（Unity 5.6 世代ビルドだが 2022.3 で読める）
  - 全シェーダー（imageeffects 36 + kino 7 + cinematic 18 + lightshafts 8 + filmic 6）が `isSupported == true`
  - SepiaTone シェーダーで実際に Blit して正しくセピア変換されることを画素値で確認
- 移植時の作業内容:
  1. 5 バンドルをプラグイン Config 配下に同梱・配布し、ローダー（`Util.LoadShaders` 相当）を実装
  2. 各エフェクト MonoBehaviour を SceneCapture ソースから移植（OnRenderImage ベース。2.5 もビルトインパイプラインなのでそのまま動く想定）
  3. 各エフェクトの Controller（UI）を本プラグインの GUIView 基盤で新規作成
- 個別の注意点:
  - **EffectMask / MaidHide** はシェーダーエフェクトではなくレイヤーマスク/メイド非表示機能。性質が異なり別枠で検討
  - **LightShafts** はカメラではなくライト側にコンポーネントを付ける方式で、他より移植コストが高い
  - MotionBlur / Antialiasing 等の古い Unity 標準系は動作はするが品質が時代相応

### 移植進捗

- **Sepia / Grayscale / Contrast**（2026-08-09 移植）: シェーダーバンドル読込基盤 `Effects/EffectShaders.cs`（`Config\PostEffects\Shaders\` の 5 バンドルを遅延読込）を新設。
  Sepia は 2.5 に型が残っていないためプラグイン内 `SepiaToneEffect` として移植、Grayscale / Contrast はゲーム側 firstpass アセンブリの
  `GrayscaleEffect` / `ContrastEnhance` を利用しシェーダーだけバンドルから補う。バンドルは repo `UnityInjector\Config\PostEffects\Shaders\` に同梱し build.bat でデプロイ
- **MotionBlur / Fisheye / NoiseAndGrain**（2026-08-09 移植）: いずれもゲーム側 firstpass アセンブリのクラスを利用。
  NoiseAndGrain の元ノイズテクスチャはアセットとして入手できないため、seed 固定のランタイム生成 (64x64) で代替
  （null のままだとグレインが乗らないことを実機確認済み）。dx11Grain トグルは未対応（既定 false のまま）
- **AnalogGlitch / DigitalGlitch**（2026-08-09 移植）: kino 系。ゲーム側に該当型がないため MonoBehaviour ごと
  `Effects/AnalogGlitchEffect.cs` / `Effects/DigitalGlitchEffect.cs` として移植し、シェーダーは kino バンドルの
  `analogglitch` / `digitalglitch` を割り当てる。移植元との差分は下記。
  - シェーダー未取得時は素通し Blit にフォールバック（移植元は null シェーダーで Material を作って落ちる）
  - DigitalGlitch のトラッシュフレーム（RenderTexture ×2）は解像度変更時に作り直し、`OnDisable` で
    Material / ノイズテクスチャとともに破棄する（移植元は生成したまま解放しない）
  - パラメータは移植元と同じ 0〜1 レンジ（シェーダー側が 0〜1 前提の係数として使うため上限拡張はしない）
  - 実機検証: 両シェーダーとも `isSupported == true`、`_ScanLineJitter` / `_VerticalJump` / `_HorizontalShake` /
    `_ColorDrift` / `_Intensity` / `_NoiseTex` / `_TrashTex` を設定した Blit で画素が変化することを確認済み
    （これらのプロパティは Properties ブロック外の uniform 宣言のため `Material.HasProperty` は false を返すが、
    `SetVector` / `SetFloat` は正しく効く）
- **Ramp / Streak**（2026-08-09 移植）: 同じく kino 系で、MonoBehaviour ごと `Effects/RampEffect.cs` /
  `Effects/StreakEffect.cs` として移植（シェーダーは kino バンドルの `ramp` / `streak`）。差分は下記。
  - Ramp: 移植元の `_debug`（グラデーション単体表示）は SceneCapture の UI にも出ていないため省略
  - Streak: 移植元の `maidMask` / `enabledTransparentMode` は EffectMask（未移植）の背景 RT に依存するため省略。
    UI 項目としても出さない
  - Streak: ミップ段が 1 段も作れない極小解像度（幅か高さが 32 以下）では素通しにする。
    移植元は空スタックを `Pop()` して落ちる
  - 実機検証: `ramp` は 2 パス、`streak` は 6 パス（使うのは 0/1/2/3/5）で `isSupported == true`。
    Ramp は角度 90° で上下にグラデーションが乗ること、Streak は明点が横方向にのみ滲むことを画素値で確認
  - **注意**: 両者の `BlendMode` を入れ子 enum で定義したところ XML 型名が衝突して Config 保存が全滅した。
    `[XmlType]` で回避済み（詳細は `com3d25-porting-tips.md` の「6.」）
- **Bokeh / Isoline / Obscurance**（2026-08-09 移植）: kino 系の残り。MonoBehaviour ごと
  `Effects/BokehEffect.cs` / `IsolineEffect.cs` / `ObscuranceEffect.cs` として移植
  （シェーダーは kino バンドルの `bokeh` / `isoline` / `obscurance`）。差分は下記。
  - Bokeh: `pointOfFocus` は移植元の「メイドから Transform を取る」トグルの代わりに、
    DepthOfField と同じ「メイドの頭に追従」トグル＋メイド ComboBox で指定する
  - Bokeh: 移植元の `focusSpeed`（ピント送りの速度）は移植元でも値を保持するだけで
    どこからも参照されないデッドコードのため省略
  - Obscurance: `ambientOnly`（G-Buffer への CommandBuffer 合成）は移植元でも
    `AddCommandBuffer` を一度も呼んでおらず機能していないデッドコードのため省略。
    併せて CommandBuffer / 全画面クアッド / PropertyObserver 一式も不要になり削除した
  - Obscurance: `occlusionSource` が GBuffer でもディファードでなければ DepthNormals へ落とす挙動は移植元と同じ。
    深度テクスチャの要求は設定変更に追従するよう描画時にも呼び直す
  - Isoline: 線の間隔は密度 `1/interval` として渡されるため、UI の下限を 0.01 にして発散を避けた
  - 3 種とも、シェーダー未取得時は素通し Blit にフォールバック
  - 実機検証: `bokeh` = 8 パス / `isoline` = 1 パス / `obscurance` = 10 パスで `isSupported == true`。
    使用するパス番号（Bokeh 0〜7、Obscurance 0〜6・8）がすべて範囲内であることを確認
- **EdgeDetect / Crease / SunShafts / TiltShiftHdr**（2026-08-09 移植）: imageeffects バンドル系。
  Crease / SunShafts / TiltShiftHdr はゲーム側 firstpass アセンブリのクラス（`Crease` / `SunShafts` / `TiltShiftHdr`）を
  そのまま利用し、シェーダーだけバンドルから補う。EdgeDetect のみ `Effects/EdgeDetectEffect.cs` として自前移植。差分は下記。
  - EdgeDetect: ゲーム側の `EdgeDetectEffectNormals` には線の色 (`edgeColor`) と濃さ (`edgePower`) が無い。
    バンドルの `edgedetectshader` は `_EdgeColor` / `_EdgePower` を持つ（実機の Blit で色が変わることを確認済み）ため、
    これらを持つ SceneCapture 同梱実装の方を移植した
  - EdgeDetect: 検出方法に応じて深度感度 / 法線感度 / 輝度しきい値を出し分ける（移植元は常時表示）。
    深度テクスチャの要求は設定変更に追従するよう描画時にも呼び直す
  - **EdgeDetect2 は移植不要**。移植元の `EdgeDetectEffectNormals2` は `EdgeDetectEffectNormals` とフィールド順以外
    完全に同一で、シェーダー解決先も同じ `edgedetectshader`（imageeffects バンドルに輪郭検出シェーダーは 1 本しかない）。
    輪郭検出を 2 枚重ねるためだけの複製なので省略した
  - SunShafts: 移植元は光源位置をシーン上のドラッグ用オブジェクト（`DragManager`）で指定するが、
    本プラグインには同等の導線が無いため「メインライトに追従」トグル＋距離スライダー（既定 ON）と、
    OFF 時の光源ワールド座標 XYZ スライダーで代替した。光源用の空オブジェクトはカメラの子として作り、
    シーン遷移でカメラごと破棄させる
  - SunShafts: `useDepthTexture` の既定を Unity 標準と同じ true にした（移植元は false。
    OFF だと遮蔽判定をスカイボックスのアルファで行うため空の無いシーンで光芒が出ない）。
    スカイボックス透過スライダーは OFF 時のみ表示する
  - SunShafts: ぼかし回数はゲーム側で 1〜4 にクランプされるため UI もその範囲に合わせた（移植元は 0〜10）
  - SunShafts: 光芒は**遮蔽物のない画素（深度＝far、つまり空）からしか出ない**。COM3D2 の屋内背景は
    全面が形状で埋まるため、そのままでは何も見えない。UI に注意書きを出している。
    実機では far クリップを 2.2 まで縮めて背景を消すと放射状の光芒が出ることを確認した
  - 実機検証（2026-08-09、実際に有効化して確認）:
    - 輪郭検出: 「線画のみ」1.0 で線画化されることを目視確認
    - チルトシフト: 画面上下がぼけて中央が残ることを目視確認
    - 光芒: 上記の far クリップ短縮で放射状の光芒を目視確認
    - 折り目: カメラ上でコンポーネントが有効かつ 3 シェーダーとも解決済みであることを確認
      （屋内背景では効果自体が非常に淡く、目視での差は小さい）
    - シェーダーはすべて `isSupported == true`。`edgedetectshader` = 7 パス（検出方法 7 種と対応）/
      `separableblurshader`・`depthfetchshader`・`creaseapplyshader` = 各 1 パス /
      `sunshaftsshader` = 5 パス（使うのは 0〜3）/ `tiltshiftshader` = 9 パス（使うのは 0〜6）

- **Antialiasing / FilmicLetterBox / FilmicMedianFilter / CinematicLensAberrations**（2026-08-09 移植）:
  Antialiasing のみゲーム側 firstpass のクラス（`AntialiasingAsPostEffect`）を利用し、残り 3 種は
  `Effects/FilmicLetterBoxEffect.cs` / `FilmicMedianFilterEffect.cs` / `CinematicLensAberrationsEffect.cs` として自前移植。差分は下記。
  - Antialiasing: `CheckResources` が `ssaaShader.isSupported` を無条件に参照するため、シェーダーが
    取れないまま有効化すると毎フレーム NullReference で落ちる。取得できなければコンポーネントを無効にして回避している
  - Antialiasing: 方式（FXAA2 / FXAA3Console / FXAA1PresetA・B / NFAA / SSAA / DLAA）ごとに参照される
    パラメータが異なるため、効くものだけ出し分ける（移植元は全項目を常時表示）
  - FilmicMedianFilter / CinematicLensAberrations: 移植元の `RenderTextureUtility`（一時 RT のプール）は使わず
    `RenderTexture.GetTemporary` / `ReleaseTemporary` に置き換えた。挙動は同じで解放漏れの経路が減る
  - CinematicLensAberrations: 歪み・色収差・ビネットの 3 ブロックをトグルで畳み、有効なものだけ項目を出す
    （移植元は常時表示）。シェーダー側は 3 つの有効な組み合わせごとに専用パス（0〜7）を持つ構造をそのまま踏襲
  - 3 種とも、シェーダー未取得時は素通し Blit にフォールバック
  - 実機検証: `cinematiclensaberrationsshader` = 8 パス（組み合わせ 0〜7 と対応）/
    `filmiceletterboxshader` = 1 パス / `filmicmedianfiltershader` = 2 パス /
    AA 用 7 本（ssaa・dlaa・nfaa・fxaapreset2/3・fxaaII・fxaaIII）すべて `isSupported == true`
- **CinematicBloom / FilmicBloom / ColorCorrectionLut**（2026-08-09 移植）: いずれもゲーム側に該当型がないため
  MonoBehaviour ごと `Effects/CinematicBloomEffect.cs` / `FilmicBloomEffect.cs` / `ColorCorrectionLutEffect.cs` として移植
  （シェーダーは cinematic バンドルの `cinematicbloomshader`、filmic バンドルの `filmicbloomshader` + `filmicstreakshader`、
  imageeffects バンドルの `colorcorrectionlutshader`）。差分は下記。
  - 3 種とも移植元の `maidMask` / `enabledTransparentMode` は EffectMask（未移植）の背景 RT に依存するため省略。
    合成パスも非マスク側（ブルーム 7〜10 / 光条 3）だけを使う
  - FilmicBloom はブルーム段の結果をそのまま光条段へ通す 2 段構え。移植元は光条段でも
    レンズダートを二重に適用していた（しかも縦横両方向のときだけ）ため、ダートはブルーム段のみに統一した
  - FilmicBloom: ミップ段が 1 段も作れない極小解像度（幅か高さが 32 以下）では素通しにする。
    移植元は空スタックを `Pop()` して落ちる。中間 RT の解放も Streak と同じ経路に揃えた
    （移植元は縮小段が 0 段のとき同じ RT を二重解放する）
  - ColorCorrectionLut: LUT は「横一列に並んだ 2D ストリップ（幅 = 高さの 2 乗）」をテクスチャパスで指定し、
    Texture3D へ組み直して使う。未指定・サイズ不正時は無変換テーブルへ落とす（同じテクスチャでは再試行しない）。
    LUT はガンマ補正をかけずに読む必要があるため `TextureFileCache` に linear 読み込みを追加した
  - **CinematicBloomLayer は移植不可**。移植元の実装は入力が `EffectMaskDef.effectMask.m_Emission`（EffectMask が
    生成する発光専用 RT）に固定されており、EffectMask 抜きでは描くものが無い。パラメータ構成は CinematicBloom と同一
  - 実機検証: `cinematicbloomshader` = 15 パス / `filmicbloomshader` = 15 パス / `filmicstreakshader` = 6 パス /
    `colorcorrectionlutshader` = 2 パスで、すべて `isSupported == true`。使用するパス番号はすべて範囲内。
    実際に Blit して、CinematicBloom は明点の周囲だけが滲むこと、FilmicBloom の光条段は横方向にのみ滲むこと、
    ColorCorrectionLut は反転 LUT で (0.80,0.20,0.40) → (0.20,0.80,0.60) と正確に変換されることを画素値で確認
- **StylisticFog / FilmicBokeh / MaidHide**（2026-08-09 移植）: `Effects/StylisticFogEffect.cs` /
  `FilmicBokehEffect.cs` / `MaidHideEffect.cs` として移植。差分は下記。
  - StylisticFog: 移植元は `Gradient` を持つが UI に出るのは開始色・終了色の 2 つだけなので、
    2 色の線形補間をテクスチャに焼く形に整理した。「もう一方と共通」を距離・高さの両方に指定した場合は
    移植元と同じく距離側をグラデーションへ倒し、その旨を UI に表示する
  - FilmicBokeh: 移植元は透過込みの深度を EffectMask（未移植）から受け取っていたため、
    必要な部分（`renderdepthcutoutshader` によるカットオフ深度の描画）だけをコンポーネント内の
    サブカメラとして持たせた。ピント合わせは DepthOfField / Bokeh と同じ「メイドの頭に追従」方式
  - FilmicBokeh: 移植元の `medianFilter` は「マテリアルが null でないこと」を確認するだけで
    一度も描画に使われないデッドコードのため省略。`depthCutoffMode` / `depthCutoff` も
    シェーダーへ渡されていないデッドコードなので省略した
  - FilmicBokeh: ピント距離と焦点距離が一致すると錯乱円係数が 0 除算になるため下限を入れた（移植元は未対策）
  - MaidHide: メイド (`Charactor` / `Face` レイヤー) をカリングマスクから外すだけの機能。
    移植元は無効化時にマスクを -1（全レイヤー）へ戻すが、2.5 のメインカメラは元から一部レイヤーを
    外している（`cullingMask` = -1342214433）ため、掴んだ時点の値を覚えて戻すようにした
- **CinematicDepthOfField / TonemappingColorGrading / LightShafts**（2026-08-09 移植）:
  残りの大物 3 種。`Effects/CinematicDepthOfFieldEffect.cs` / `TonemappingColorGradingEffect.cs` /
  `LightShaftsEffect.cs` として移植。差分は下記。
  - CinematicDepthOfField: 移植元は DX11 テクスチャボケの合成先 RT を
    `GetTemporaryRenderTexture(source.height, source.width, ...)` と縦横入れ違いに作っていたので直した
  - CinematicDepthOfField: 移植元の `RenderTextureUtility`（一時 RT のプール）は使わず、
    1 フレーム分の一時 RT をリストで持って描画の最後にまとめて返す方式に置き換えた
  - CinematicDepthOfField: `ComputeCocParameters` が近景・遠景の境界値をフィールドへ書き戻していたが、
    本プラグインは毎フレーム設定値を流し込むので書き戻しは意味がなく省いた。
    `Graphics.DrawProceduralIndirect` は Unity 2022 で `DrawProceduralIndirectNow` に改名されている
  - TonemappingColorGrading: 開発用のデバッグ表示（内部 LUT と順応輝度を画面左上に焼き込む `OnGUI`）は省略。
    `precision`（ColorGradingPrecision）も LUT サイズが 32 固定で参照されないデッドコードなので省略した
  - TonemappingColorGrading: カーブ 5 本（トーンカーブ + 全体/RGB）は `CurveData` + `CurveEditorWindow` で編集し、
    値が変わったフレームだけ 256 点のカーブテクスチャを焼き直す（移植元は毎フレーム焼き直していた）
  - TonemappingColorGrading: 移植元でチャンネルミキサーとカーブは UI 上のトグルだけを持ち、
    エフェクト側は常に適用していた。本プラグインはトグルが OFF のとき無変換の値を流し込んで挙動を合わせている
  - LightShafts: カメラではなくライトに付くため、`EffectControllerBase<TComponent, TSetting>`（カメラ上の
    コンポーネントを掴む基底）ではなく `EffectControllerBase` を直接継承し、専用の光源 GameObject ごと
    コントローラーが管理する。位置・回転・色・強さ・到達距離・照射角を UI から指定する
  - LightShafts: 移植元のサンプル点可視化（`m_ShowSamples` / `samplepositions` シェーダー）は
    開発用デバッグ表示なので省略し、`SHOW_SAMPLES_OFF` キーワードを常時立てる
  - LightShafts: 固定シャドウマップは光源が動いても描き直されないため、配置と描画範囲の設定が
    変わったフレームだけ明示的に再描画させる
  - **EffectMask は移植対象外**。可視のエフェクトではなく、maidMask 系（Streak / CinematicBloom /
    FilmicBloom / CinematicBloomLayer）と FilmicBokeh の透過深度に渡す補助 RT を作るだけの下請け。
    maidMask 系は移植時に落としており、FilmicBokeh が必要とする透過深度は同コンポーネント内に取り込んだため、
    単体で移植しても UI に出せるものが無い。キャラを後処理から除外したいという用途は
    後述の「環境遮蔽をキャラに適用しない方法」で別途対応済み

### 環境遮蔽をキャラに適用しない方法（実装済み）

「環境遮蔽（Obscurance / SSAO）をキャラには効かせたくない」という要望に対する調査結果。
2026-08-09 に下記「マスクテクスチャ方式」で実装済み（UI: 環境遮蔽の「キャラに適用しない」トグル）。

SSAO は深度／深度＋法線テクスチャだけを見て画面全体に効く後処理で、ピクセルの由来（キャラか背景か）を持たない。
除外するには何らかのマスクが要る。

#### 一般的な手法はステンシルマスク

キャラ描画時にステンシルの 1 ビットを「AO を受けない」印として立てておき、**AO の合成パスでそのピクセルを弾く**
（AO 係数を 1 に固定する）のが定石。per-material に「SSAO を受けない」設定を持つエンジンは、
内部的にこの種のフラグ（ステンシルビットや G-Buffer の 1 ビット）で実現している。

**本プラグインではステンシルが取れない**。AO の合成は `OnRenderImage` の Blit チェーン上で行われ、
一時 RT はカメラのステンシルバッファを共有しないため、キャラ描画時に立てたステンシルビットを合成パスから参照できない。

#### 「受けない」と「落とさない」は別物

| 方式 | キャラが AO を受ける | キャラが背景へ AO を落とす |
|---|---|---|
| ステンシル方式（一般的） | しない | **する**（首元の影が服に落ちる等は残る） |
| 深度から除外（下記 A） | しない | **しない**（遮蔽計算からキャラの存在自体が消える） |

#### 採用した実装: マスクテクスチャ方式（ステンシル相当）

前提（実機確認済み）: メインカメラは Forward レンダリング、メイドはレイヤー `Charactor` (10)。

`UnityProject/`（Unity 2022.3.62f2、ゲームと同世代）で自前シェーダー 2 本を `posteffects` バンドルとしてビルドし、
`Config\PostEffects\Shaders\posteffects` に配置する（`build.bat` が他バンドルと一緒にデプロイする）。

- `CharMaskWhite` — `OnPreCull` でメインカメラを `CopyFrom` したサブカメラが `Charactor` レイヤーだけを
  白塗りで自前 RT に描く（置き換えタグ空の `RenderWithShader`）。`OnRenderImage` 中の `Camera.Render` は
  非サポートのため描画タイミングは `OnPreCull`
- `ObscuranceMask` — ぼかし後の遮蔽 RT をマスクで消し込む（`occ * (1 - mask)`）。kino バンドルの合成パス
  （単色 6 / カラーモード 8）はそのまま使うので、配布バンドルの改変や合成式の再現は不要

挙動は上表のステンシル方式と同等（キャラは AO を**受けない**が背景へ AO を**落とす**）。深度入力は無改変。
コストはトグル ON のときだけキャラのマスク描画 1 回/フレーム。バンドル未配置ならトグルは何もしない（素通し）。

検討したが不採用の選択肢:
- **深度入力からキャラを除外** — キャラの存在ごと遮蔽計算から消えるため首元の影なども消える
- **AO 適用後にキャラだけ描き直す** — スキンメッシュ二重描画で重く、半透明（髪・まつ毛）のソート崩れリスク

## 6. 移植先で追加・改善された点（戻す必要なし）

- スライダー個別 `R` リセット・`<` `>` ステップボタン・FloatField 直接入力
- 高機能カラーピッカー（SV マップ / HSVA / アルファ / hex）
- 条件付き表示（Vignetting Advanced、DoF nearBlur / DX11 ボケ、選択的色補正、色補正の深度補正）
- 深度補正のシェーダー差し替え回避策（ゲーム側 `ccDepthMaterial` の実装ミスを補正。移植元では絵が壊れる）
- エフェクト一覧のカテゴリ絞り込みとカテゴリ単位の一括リセット、ウィンドウの位置・サイズの永続化
- ScreenOverlay エフェクト（新規）、Blur の SGX ガウス切替
