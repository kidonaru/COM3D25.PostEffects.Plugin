# 追加ポストエフェクト候補（SceneCapture 移植完了後）

SceneCapture 由来の 32 種は全て決着済み（`scenecapture-ui-diff.md` 参照）。
本ドキュメントは、それ以外で Unity 2022（ビルトインパイプライン + `OnRenderImage`）で動作する追加候補をまとめたもの。
自前シェーダーバンドル（`posteffects`、`custom-shader-bundle-pipeline` 参照）のビルド基盤があるため、シェーダー同梱型なら追加可能。

調査日: 2026-08-09（未着手・アイデア段階）

## 1. 移植元があるもの — Keijiro Kino 旧世代（MIT ライセンス）

kino バンドル（AnalogGlitch / DigitalGlitch / Ramp / Streak / Bokeh / Isoline / Obscurance）を移植済みのため、
同作者のビルトイン時代の他作品が最も相性がいい。すべて `OnRenderImage` ベースで Unity 2022 でも動く。

| エフェクト | 内容 | 備考 |
|---|---|---|
| KinoContour | 輪郭抽出 | 移植済み EdgeDetect より線が綺麗。漫画・線画系の撮影用途で人気 |
| KinoBinary | ディザ 2 値化 | レトロ・新聞印刷風。実装が小さく移植コスト最小 |
| KinoMirror | 万華鏡ミラー | 演出系スクショ向け |
| KinoSlitscan / KinoFeedback | スリットスキャン / フィードバック | 実験系。グリッチ系が好みなら |
| KinoDatamosh | データモッシュ | モーションベクター必須（`DepthTextureMode.MotionVectors` で forward でも取れる）。動画撮影しないなら優先度低 |

## 2. 自前シェーダーで書く価値が高いもの（撮影用途での実用度順）

いずれも 1 パス〜数パスの短いシェーダーで、既存の `posteffects` バンドルに同居できる。

1. ~~**ディフュージョン（ソフトフォーカス）**~~ — **実装済み（2026-08-09）**。`Effects/DiffusionEffect.cs`。
   明部抽出 → 1/2 解像度ガウス H/V → Screen/Lighten 合成の 4 パス（`Diffusion.shader`）
2. ~~**シャープネス（CAS 風）**~~ — **実装済み（2026-08-09）**。`Effects/CasSharpenEffect.cs`。
   AMD FidelityFX CAS の簡易移植 1 パス（`CasSharpen.shader`）
3. ~~**ホワイトバランス（色温度 / ティント）**~~ — **実装済み（2026-08-09）**。`Effects/WhiteBalanceEffect.cs`。
   温度/ティント→CIE xy→LMS 比を C# 側で計算し、シェーダーは LMS 1 パス（`WhiteBalance.shader`）
4. ~~**Kuwahara フィルタ（油絵風）**~~ — **実装済み（2026-08-09）**。`Effects/KuwaharaEffect.cs`。
   4 象限の輝度分散比較 1 パス（`Kuwahara.shader`）
5. ~~**ハーフトーン / 漫画トーン**~~ — **実装済み（2026-08-09）**。`Effects/HalftoneEffect.cs`。
   モノクロ網点 / CMYK カラー網点の 2 パス（`Halftone.shader`）
6. **ピクセレート / ポスタライズ** — レトロ演出。数行のシェーダー
7. ~~**ラジアルブラー / ズームブラー**~~ — **実装済み（2026-08-09）**。`Effects/RadialBlurEffect.cs`。
   中心方向へのズームサンプリング 1 パス（`RadialBlur.shader`、強度 1 で最大 10% ズーム）

### 実装時の知見（2026-08-09）

- **UnityCG の `Luminance()` は使わない**。`unity_ColorSpaceLuminance` 依存で、実機の `OnRenderImage` 外
  Blit では白の輝度が 0.5 になる（係数が (0.0397, 0.458, 0.006) のまま）。明示的に
  `dot(c, float3(0.299, 0.587, 0.114))` を書くこと
- 網点のドット半径は「被覆率 = インク量」になるよう πr² = amount で決める。`sqrt(amount) * 0.7071` は
  中間調が大幅に潰れる（グレー 0.5 で被覆率 78%）。円がセル境界に達する amount = π/4 以降は対角へ線形補間
- 実機検証（devbridge・画素値確認済み）: CAS はエッジ両脇のオーバーシュート（0.25/0.75 → 0.14/0.86）と
  平坦部不変、ハーフトーンはグレー 0.5 で被覆率 46%、ディフュージョンは明点周囲の暗部が明るくなることを確認
- 実機検証（2026-08-09 第 2 陣）: ホワイトバランスは温度 +50 でグレー 0.5 → (0.545, 0.497, 0.415)・
  ニュートラルで恒等、ラジアルブラーは白線が外側画素へ流れ strength 0 で恒等、
  Kuwahara はステップエッジ保存（灰化なし）+ 市松ノイズが平均値へ平坦化することを確認

## 3. 避けた方がいいもの

| 候補 | 理由 |
|---|---|
| Post Processing Stack v2 まるごと | 独自の Volume/レンダーチェーンを持ち、既存の `OnRenderImage` チェーンと適用順を制御できない。個別アルゴリズム（CAS・ホワイトバランス等）だけ参考にする |
| SSR（画面空間反射） | PPSv2 実装は deferred 専用。メインカメラは forward のため不可 |
| TAA | モーションベクター + ジッター注入が必要で、カメラを直接制御できないプラグイン構造では画揺れが出やすい。AA は既存の FXAA 系で十分 |

## 4. 推奨着手順

**ディフュージョン → CAS シャープ → ハーフトーン** の順が、実装コストと撮影プラグインとしての効果のバランスが最良。
