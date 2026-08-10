# キャラ除外（excludeCharacters）の他エフェクトへの展開

環境遮蔽（Obscurance）に実装したキャラ除外マスクを、他のエフェクトへ展開した際の検討結果と実装記録。

## 実装済みの構成（2026-08-10）

- `Effects/CharacterMask.cs` — 共有マスクプロバイダ。サブカメラで `Charactor` レイヤー（名前解決不可時はレイヤー 10）を `CharMaskWhite` で白塗り描画し、フレーム内で複数エフェクトが同じマスク RT を使い回す（最初の要求時だけ描画）。解放判定は `PostEffectManager.LateUpdate` の `CharacterMask.Tick()` が行う
- `CharacterMaskableEffect` 基底クラス — `excludeCharacters` が有効なとき、エフェクト適用結果と元画像を `CharMaskComposite` シェーダー（`lerp(effected, original, mask)`）で合成する。ブラー系は `maskSpread`（境界膨張 px、Pass 1 の 9 タップ max）を正値にする
- 対応済みエフェクト: Obscurance（従来の消し込み方式のままマスクだけ共有化）/ Sepia / Ramp / Halftone / Kuwahara / EdgeDetect / Isoline / RadialBlur / Diffusion（後者 2 つは `maskSpread = 4`）
- RadialBlur は合成だけだと「キャラの残像が背景へ流れる」ため、ブラー本体もマスク対応（RadialBlur.shader Pass 1）。キャラ画素を重み 0 で捨てて正規化することで、キャラの色が背景サンプルに混ざらない
- 未対応: **Grayscale / Blur はゲーム側アセンブリのコンポーネントを流用しており OnRenderImage に介入できない**。対応するには自前実装への置き換えが必要（将来課題）

## 流用価値の評価

### 優先度: 高

| エフェクト | 理由 |
|---|---|
| Grayscale / Sepia / Ramp | 「背景だけモノクロ・セピア、キャラはフルカラー」は演出として定番。合成の最終段で `lerp(effected, original, mask)` するだけで実装コストが最小 |
| Halftone / Kuwahara / EdgeDetect / Isoline | 背景のみスタイライズして被写体を際立たせる。特に Halftone は顔に網点がかかると肌が汚く見えるため除外オプションの実用性が高い |
| Blur / RadialBlur / Diffusion（ブラー成分） | 背景ぼかし＋被写体シャープを DoF の距離調整なしで実現できる。RadialBlur は集中線的演出と相性が良い |

**ブラー系の注意点**: キャラ輪郭のすぐ外側に背景ボケが回り込むため、マスク境界を数ピクセル膨張させる（またはマスク自体を軽くブラーする）処理を入れないと縁が硬く見える。

### 優先度: 低（非推奨）

| エフェクト | 理由 |
|---|---|
| GlobalFog / StylisticFog / SunShafts / LightShafts | 深度ベースで物理整合が前提。キャラだけフォグが消えると浮いて見える。「キャラを霧から守る」用途は fog 側の距離パラメータで足りる |
| Bloom 系 | 発光源はキャラ側（肌ハイライト等）にもあるため、除外すると不自然になりやすい |
| Vignette / LetterBox / Glitch / NoiseAndGrain | 画面全体の演出なので部分除外の意味が薄い |

## 設計メモ

- マスク描画は 1 フレーム 1 回で済むよう遅延共有方式（フレーム内で最初に要求したエフェクトが描画）。どのエフェクトも要求しないフレームでは描画せず、除外オフ時のコストはゼロ
- マスクの要求（`CharacterMask.Render`）は各エフェクトの OnPreCull から行う。OnRenderImage 中の `Camera.Render` は非サポートのため
- Obscurance だけは lerp 合成ではなく遮蔽 RT からの消し込み方式を維持している（「キャラは AO を受けないが背景へ AO を落とす」挙動を守るため）
