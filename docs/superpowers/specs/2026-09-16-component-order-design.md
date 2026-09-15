# エフェクトコンポーネントの追加順固定 設計

## 目的

ポストエフェクトの適用順（`OnRenderImage` の実行順 = カメラ上のコンポーネント順）を
COM3D2.SceneCapture.Plugin と同じ固定順にする。現状はユーザーが ON にした順で
`AddComponent` されるため、操作順によって見た目が変わる。

## SceneCapture 側の仕組み（調査結果）

- `Instances.ClearCameraEffrct()` → `EffectManager.Clear()` が全 `*Def.ClearEffect()` を固定順で呼び、
  `Util.GetComponentVar` がメインカメラへ `GetComponent` or `AddComponent` する
- 以後は毎フレーム `component.enabled = pane.IsEnabled` を書くだけ
- ゲーム側に元々あるコンポーネント（Bloom 等）は `GetComponent` で拾うため位置はゲーム側のまま。
  順序制御できるのは新規追加分だけ

## 設計

1. `EffectControllerBase` に `virtual void Prepare()` を追加する
   - `EffectControllerBase<T,S>` の既定実装: `GetOrAddComponent()` し、自分で追加した場合は `enabled = false`
   - `SeparatedBloomController`: 基底の Prepare 後に `SeparatedBloomEffect` も無効状態で追加（Bloom 直後に並ぶ）
   - Paraffin / DistanceFog / Rimlight: `PostEffectHub.GetOrAdd(cameraObject)`（CommandBuffer 方式で順序非依存）
   - LightShafts / GameEffect: 何もしない（カメラにコンポーネントを持たない）
2. `PostEffectManager` に UI 表示順 (`controllers`) とは別の **コンポーネント追加順** を持ち、
   `PrepareAll()` でその順に `Prepare()` を呼ぶ
3. 呼び出しタイミング
   - `Initialize()`（`config.pluginEnabled` のとき）の `postEffectManager.Init()` 直後
   - `PostEffectManager.LateUpdate()` 冒頭で、カメラ GameObject が前回 Prepare 時と異なるとき
     （シーン遷移・VR カメラ切替で破棄された場合の再構築）
   - 既存の遅延 `GetOrAddComponent` はフォールバックとして残す
4. `Apply()` / `Restore()` の enabled 切替は現状のまま

## 追加順

SceneCapture の `EffectManager.Clear()` 順を骨格に、こちら独自のエフェクト（★）を用途の近い位置へ挿入する。

```
MaidHide, ColorCorrectionCurves, Contrast, Crease, EdgeDetect, ★OutlineColor(COM3D25 のみ),
FilmicMedianFilter, Antialiasing, NoiseAndGrain, MotionBlur, TiltShiftHdr, SunShafts,
AnalogGlitch, DigitalGlitch, Isoline, ★Halftone, ★Kuwahara, Obscurance, ★GlobalFog, StylisticFog,
CinematicBloom, FilmicBloom, Streak, Bloom, ★Diffusion,
DepthOfField, CinematicDepthOfField, Bokeh, FilmicBokeh, ★Blur, ★RadialBlur,
Ramp, CinematicLensAberrations, Fisheye, ★Vignetting,
ColorCorrectionLut, TonemappingColorGrading, ★WhiteBalance, ★GTToneMap, Sepia, Grayscale,
★CasSharpen, FilmicLetterBox, ★ScreenOverlay
```

Hub 系（Paraffin / DistanceFog / Rimlight）と LightShafts / GameEffect は順序非依存のため先頭にまとめる。

## 検証

debug ビルド後、ゲームを再起動して devbridge の `eval_csharp` で
`GameMain.Instance.MainCamera.GetComponents<UnityEngine.Behaviour>()` の型名列を取り、
上記の順で並んでいること・全て `enabled == false` で始まることを確認する。
