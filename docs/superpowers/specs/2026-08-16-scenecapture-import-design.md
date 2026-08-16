# SceneCapture プリセット取り込み 設計

SceneEditor が読み込んだ SceneCapture プリセットの `<Effects>` セクションを、
本プラグインのエフェクト設定へ適用できるようにする。

- 契約元: `W:\COM3D2_5\work\COM3D2.SceneEditor.Plugin\docs\scenecapture-import-guide.md`
- 移植元の実装: `W:\COM3D2_5\work\COM3D2.SceneCapture.Plugin`
  （`CM3D2/SceneCapture/Plugin/SerializeStatic.cs` の `SaveDef` / `LoadDef`、`Util.cs` の各変換）

## 1. 目的とスコープ

SceneEditor は SceneCapture プリセットを読み込むと、`<Models>` / `<Effects>` に中身がある場合に
`ApplySceneCaptureXml(string xml)` を実装した全プロバイダへ `<Preset>` XML 全体を渡す。
本プラグインは `<Effects>` だけを解釈し、他セクションには触れない
（`<Camera>` / `<Misc>` / `<Lights>` / `<LightShafts>` は SceneEditor 本体が適用するため、
触ると二重適用になる）。

対象は SceneCapture が書き出す 34 Def のうち 32 Def。
除外する 2 件の理由は §4 に記す。

### やらないこと

- UI の追加（本プラグインのウィンドウから SceneCapture プリセットを直接開く導線は作らない）
- SceneCapture 形式での書き出し（取り込みの一方向のみ）
- `<Models>` / `<Camera>` / `<Lights>` / `<Misc>` の解釈

## 2. 適用セマンティクス

**全リセット後に適用**する。既定値の `PostEffectsPreset` を新規生成し、XML に記載のある Def だけを
埋めてから `ApplyTo(EffectSettings.instance)` を呼ぶ。

- SceneCapture は `enabled == true` のエフェクトだけを Def として書き出す。
  つまり「要素が無い = そのエフェクトは無効」という意味論であり、プリセットは全体の状態を表す
- 本プラグイン独自のエフェクト（Paraffin / Rimlight / DistanceFog / GTToneMap / Halftone /
  Kuwahara / Diffusion / CasSharpen / RadialBlur / WhiteBalance / ScreenOverlay 等）も既定へ戻る。
  SceneCapture プリセットの絵を忠実に再現するための選択
- 既存の `PostEffectsPreset.ApplyTo` をそのまま使うため、このリセット挙動は構造的に保証される
  （`ApplyTo` は preset 側が null のフィールドを既定値インスタンスで埋める）

例外として、`<Effects>` 要素が無い / 子要素が 0 件のときは**何もせず `true` を返す**。
`<Models>` だけを持つプリセットで現在の設定が消えないようにするため。

## 3. 構造とデータフロー

```
SceneEditor
  └ PostEffectsScenePresetProvider.ApplySceneCaptureXml(xml)   ← 新規 public static
      └ PresetManager.ApplySceneCaptureXml(xml)
          ├ SceneCaptureImporter.Parse(xml) → PostEffectsPreset （既定値から組み立て）
          ├ preset.ApplyTo(EffectSettings.instance)
          └ EffectSettings.instance.dirty = true
```

### ファイル構成

| ファイル | 変更 |
|---|---|
| `Manager/SceneCaptureImporter.cs` | 新規。Def 表・例外表・型変換。`PostEffectsPreset` を返す純粋な変換で `EffectSettings` には触らない |
| `Manager/PresetManager.cs` | `ApplySceneCaptureXml(string)` を追加。Importer を呼んで `ApplyTo` するだけ |
| `ScenePresetProvider.cs` | `public static bool ApplySceneCaptureXml(string xml)` を追加 |
| `docs/scenecapture-ui-diff.md` | 取り込み対応の節を追記（取り込めない項目の一覧を残す） |

`ScenePresetProvider` の契約は既存どおり `PresetProviderId` / `PresetProviderDisplayName` /
`CapturePresetXml` / `ApplyPresetXml` を維持したまま、メソッドを 1 つ足すだけでよい。
シグネチャは `public static bool ApplySceneCaptureXml(string)` の完全一致が必要。

### 変換の基本規則

子要素名の先頭 `_` を落とし、同名の `XxxSetting` フィールドへ型変換して代入する。
書式は SceneCapture の `SerializeStatic.GetValue` に対応させる。

| 型 | 書式 |
|---|---|
| 数値 | InvariantCulture |
| enum | int 値 |
| bool | `True` / `False` |
| Color / Color32 | `r,g,b,a`（各 0-255）。`Color` へは 255 で割る |
| Vector3 | `x,y,z` |
| AnimationCurve | `outTangent0,value0,inTangent1,value1`（時刻 0 と 1 の 2 キー固定） |
| Texture / Texture2D / Texture3D | Config ルートからの相対パス文字列 |

Def 要素が存在すれば、対応する Setting の `enabled` を `true` にする。

移植元にはバージョン差があり、手元の実プリセットには移植元ソースの
フィールド一覧に無い要素（`BloomDef` の `blurWidth`、`DepthOfFieldDef` の `dx11BokehThreshhold` 綴り）が
含まれている。**古い綴り・古い要素も受け付ける**方針とし、対応先が無いものは §5 の無視リストに載せる。

## 4. Def 単位の扱い

対象 34 Def のうち、下記 2 件は Def ごと扱いを変える。

- **`CinematicBloomLayerDef`** — 丸ごと無視。移植元の実装は入力が EffectMask の発光 RT に固定されており、
  EffectMask 抜きでは描くものが無い（`docs/scenecapture-ui-diff.md` §5.1）。本プラグインに対応エフェクトが無い
- **`EdgeDetect2Def`** — `EdgeDetectDef` が無いときだけ `edgeDetect` へ流すフォールバック。
  移植元の `EdgeDetectEffectNormals2` は `EdgeDetectEffectNormals` とフィールド順以外同一で、
  シェーダー解決先も同じ。両方あれば `EdgeDetectDef` を優先する

## 5. マッピング例外表

既定規則（先頭 `_` を落として同名代入）で解決しない項目だけを列挙する。
ここに無い子要素は既定規則で通る。

| Def | 例外 |
|---|---|
| `BloomDef` | `bloomIntensity`→`intensity`（**値 ≥ 2.86 なら 100 で割る**。本家 `LoadDef` の互換処理を踏襲）/ `bloomThreshhold`→`threshold` / `bloomThreshholdColor`→`thresholdColor` / `bloomBlurIterations`→`blurIterations` / `sepBlurSpread`→`blurSpread` / `lensflareMode`→`lensFlareMode` / `lensflareIntensity`→`lensFlareIntensity` / `lensflareThreshhold`→`lensFlareThreshold` / `quality`→`highQuality`（`BloomQuality.High` = 1） |
| `ColorCorrectionCurvesDef` | `redChannel`/`greenChannel`/`blueChannel`→`redCurve`/`greenCurve`/`blueCurve`、`depthRedChannel` 等→`depthRedCurve` 等、`zCurve`→`zCurve`。いずれもカーブ文字列→`CurveData` |
| `TonemappingColorGradingDef` | `_EyeAdaptationEnabled`→`eyeAdaptationEnabled` / `_TonemappingEnabled`→`tonemappingEnabled`（移植元だけ先頭が大文字）/ `_LUTEnabled`→`userLutEnabled` / `_contribution`→`userLutContribution` / `texture`→`userLutPath` / `_tonemappingCurve`・`_masterCurve`・`_redCurve`・`_greenCurve`・`_blueCurve`→`CurveData` |
| `StylisticFogDef` | `distanceGradientFirstColor`→`distanceFirstColor` / `distanceGradientLastColor`→`distanceLastColor` / `heightGradientFirstColor`→`heightFirstColor` / `heightGradientLastColor`→`heightLastColor` / `distanceFogColorSelectionType`→`distanceColorSource` / `heightFogColorSelectionType`→`heightColorSource` / `distanceColorRamp`→`distanceRampPath` / `heightColorRamp`→`heightRampPath` |
| `FilmicBloomDef` | `_streakthreshold`→`streakThreshold` / `_streaksoftKnee`→`streakSoftKnee` / `_streakstretch`→`streakStretch` / `_streakintensity`→`streakIntensity` / `_streaktint`→`streakTint` / `_bDirtTexture`→`useDirtTexture` / `dirtTexture`→`dirtTexturePath` |
| `CinematicBloomDef` | `_bDirtTexture`→`useDirtTexture` / `dirtTexture`→`dirtTexturePath` |
| `DepthOfFieldDef` | `blurType`→`useDX11Bokeh`（`BlurType.DX11` = 1）/ `dx11BokehThreshhold`・`dx11BokehThreshold` の**両綴り**→`dx11BokehThreshold` / `dx11BokehTexture`→`dx11BokehTexturePath` |
| `CinematicDepthOfFieldDef` | `bokehTexture`→`bokehTexturePath` |
| `ColorCorrectionLutDef` | `converted3DLut`→`lutTexturePath` |
| `BokehDef` / `FilmicBokehDef` | `_focalrange`→`focalRange` |
| `ObscuranceDef` | `_sampleCountValue`→`variableSampleCount` |
| `FilmicMedianFilterDef` | `medianFilter`→`quality` |
| `IsolineDef` | `_axis`→`axisX`/`axisY`/`axisZ`、`_direction`→`directionX`/`directionY`/`directionZ`、`_modulationAxis`→`modulationAxisX`/`modulationAxisY`/`modulationAxisZ`（Vector3 の成分分解） |
| `NoiseAndGrainDef` | `tiling`→`tilingX`/`tilingY`/`tilingZ`（Vector3 の成分分解） |
| `SunShaftsDef` | `sunTransform`（位置の Vector3）→ `followMainLight` = false かつ `sunPosX`/`sunPosY`/`sunPosZ`。本家 `LoadDef` が位置を復元する唯一の Transform フィールド |

### 明示的に無視する要素

警告を出さずに捨てる。対応する設定が本プラグインに無い、または移植時に意図的に落とした項目。

- `_maidMask` / `_enabledTransparentMode`（EffectMask 依存。Streak / CinematicBloom / FilmicBloom）
- `tweakMode` / `blurWidth` / `lensFlareVignetteMask`（`BloomDef`）
- `_ambientOnly`（`ObscuranceDef`。移植元でも機能していないデッドコード）
- `_debug`（`RampDef`）
- `_prefilterBlur` / `_medianFilter` / `_dilateNearBlur`（`CinematicDepthOfFieldDef`）
- `_depthCutoffMode` / `_depthCutoff` / `_medianFilter`（`FilmicBokehDef`。移植元でもシェーダーへ渡らないデッドコード）
- `dx11Grain` / `filterMode` / `intensities` / `noiseTexture`（`NoiseAndGrainDef`）
- `mode` / `updateTextures`（`ColorCorrectionCurvesDef`）
- `_precision` / `_eyeAdaptationShowDebug` / `_showDebug` / `minSizePerWheel` / `maxSizePerWheel` / `color`（`TonemappingColorGradingDef`）
- `textureRamp`（`GrayscaleDef`）
- `_offset` / `_modulationTime`（`IsolineDef`）
- ピント用 Transform: `focalTransform`（`DepthOfFieldDef`）/ `_pointOfFocus`（`BokehDef` / `FilmicBokehDef`）/
  `_focusTransform`（`CinematicDepthOfFieldDef`）。**本家 `LoadDef` も SunShafts 以外の Transform は読み戻さない**

### そもそも書き出されない値

移植元の `SerializeStatic.ALLOWED_TYPES` に `Vector2` と `Gradient` が無いため、
以下は SceneCapture 側が保存していない。取り込み後も既定値のままになる。

- `vignetteCenter`（`CinematicLensAberrationsDef`）
- `center`（`FilmicLetterBoxDef`）
- `distanceColorGradient` / `heightColorGradient`（`StylisticFogDef`）

### 中身が空になる Def

`MaidHideDef` / `SepiaDef` / `AnalogGlitchDef` / `DigitalGlitchDef` は、移植元コンポーネントが
パラメータを public プロパティで持つ（`SaveDef` は public フィールドしか見ない）ため、
**常に子要素の無い空要素**として書き出される。有効化のみ行い、強度等は取り込めない。
これは移植元の仕様上の欠落であり、本プラグイン側では補えない。

### テクスチャパス

Def の値は Config ルートからの相対パス（例 `SceneCapture\Images\bokeh.png`）。
本プラグインの `TextureFileCache` も相対パスを `PluginUtils.UserDataPath`（= `Config`）基準で解決し、
SceneEditor の SceneCapture プリセット探索も同じ Config ルート基準（`ScenePresetManager.cs:55`）。
よって**値をそのまま `xxxTexturePath` へ代入**すればよい。
ファイルが存在しない場合は各エフェクトの既存フォールバック（LUT なら無変換テーブル等）に従う。

## 6. enum の値対応

SceneCapture は enum を int 値で保存する。本プラグインの enum が同じ順序とは限らないため、
実装時に移植元の enum 宣言と 1 つずつ突き合わせて検証する。範囲外の int は代入せずスキップする。

検証対象:

- `AntialiasingDef.mode`
- `BloomDef.hdr` / `screenBlendMode` / `lensflareMode` / `quality`
- `TiltShiftHdrDef.mode` / `quality`
- `DepthOfFieldDef.blurSampleCount` / `blurType`
- `CinematicDepthOfFieldDef.tweakMode` / `filteringQuality` / `apertureShape`
- `BokehDef.kernelSize` / `FilmicBokehDef.kernelSize`
- `ObscuranceDef.sampleCount` / `occlusionSource`
- `RampDef.blendMode` / `StreakDef.blendMode` / `FilmicBloomDef.blendMode`
- `IsolineDef.modulationMode`
- `SunShaftsDef.resolution` / `screenBlendMode`
- `EdgeDetectDef.mode`
- `StylisticFogDef.distanceFogColorSelectionType` / `heightFogColorSelectionType`
- `TonemappingColorGradingDef.tonemapper`
- `FilmicMedianFilterDef.medianFilter`

## 7. エラー処理

- `ApplySceneCaptureXml` は例外を外へ出さない。XML パース自体が失敗したときだけ `false` を返す
- Def 単位の失敗（未知 Def・値の変換失敗）は**その Def / その要素だけスキップして続行**し、全体としては `true`。
  壊れた 1 要素で他のエフェクトを巻き添えにしない
- ログは 1 回の適用につきまとめて 1 行の警告にする。要素ごとに出すと 34 Def × 数十要素でログが埋まる。
  内容は「未知の Def 名」と「既定規則でも例外表でも解決できなかった子要素名」。
  §5 の「明示的に無視する要素」は警告に含めない
- 適用後に `EffectSettings.instance.dirty = true` を立てる（既存 `ApplyPresetXml` と同じ理由。
  選択中のプリセットとは別経路で設定が変わるため未保存扱いにする）
- HistoryAPI へは一切 `Register` しない。SceneEditor は適用開始時にホストの操作履歴を全クリアするため、
  ここで積むとエントリが残ってしまう

## 8. 検証

このリポジトリには単体テスト基盤が無いため、実機ベースで 2 段構えにする。

1. **静的スイープ** — 実在する SceneCapture プリセット（`Config\SceneCapture\Presets\*.xml`、20 件以上）を
   全部インポータに通し、「未解決要素ゼロ」をログで確認する。想定外の要素名や書式はここで洗い出す
2. **実機確認** — devbridge 経由で代表プリセット（Bloom / DepthOfField / TonemappingColorGrading /
   StylisticFog / Isoline を含むもの）を適用し、SceneCapture 本家で読んだときの絵と見比べる。
   カーブ・テクスチャ・Vector3 分解のように変換を挟む箇所を重点的に確認する
