# MotionTimelineEditor 連携に向けた改善計画

MTE のタイムラインから PostEffects の各エフェクトを操作（キーフレーム補間で毎フレーム値を書き込み）できるようにするための、PostEffects 側の事前改善をまとめる。

## 連携方式（決定事項）

- **DLL 直参照**で連携する。リフレクション Facade は不採用
  - MTE / PostEffects / MTEUtils はすべて同一作者の保守で、リリースを揃えられるため、直参照最大のリスク（バージョンずれによる実行時破損）は管理下にある
  - MTE 側には「PostEffects.dll が無い環境でも本体が動く」オプショナル別 DLL（ブリッジ DLL）の機構が既にあり、そこから直接参照する
- **ブリッジの境界に MTEUtils の型を出さない**（案 1）
  - MTEUtils はサブモジュールとして MTE.dll / PostEffects.dll 双方にソース埋め込みされており、同名 public 型が両アセンブリに存在する。ブリッジが `GUIView` / `CurveData` 等の共有型を参照すると CS0433（曖昧型）になる
  - ブリッジが触ってよいのは `COM3D25.PostEffects.Plugin` 名前空間の型（Setting クラス群・PostEffectManager 等）のみ。これらは PostEffects 側にしか存在しないため曖昧参照にならない
  - 将来、境界で MTEUtils の型を共有したくなった場合は MTEUtils の共有 DLL 化へ移行する

## MTE ポストエフェクトの移植（2026-08-10 実施）

MTE 独自実装のポストエフェクト 4 種のランタイム実装を本リポジトリへ移植し、一本化した。

- 移植済み: パラフィン (`paraffin`) / 距離フォグ (`distanceFog`) / リムライト (`rimlight`) / GTトーンマップ (`gtToneMap`)
  - パラフィン/フォグ/リムライトは MTE の CommandBuffer 実装を `PostEffectHub`（共有 MonoBehaviour）として移植。シェーダーは `posteffects` バンドルに同梱（`PostEffects/PostEffect`、`PostEffects/GTToneMap`）
  - DepthOfField は移植対象外（ゲーム側コンポーネント制御であり、本プラグインの既存 DepthOfFieldController が同等機能を持つ）
  - 移植時の修正: ComputeBuffer のストライドを固定値から `Marshal.SizeOf` 導出へ変更（原典の固定値は実サイズ不一致で Unity 2022.3 では SetData 例外）
- データ型 `ColorParaffinData` / `DistanceFogData` / `RimlightData` / `GTToneMapData`（静的 `Lerp` 付き）は `COM3D25.PostEffects.Plugin` 名前空間に移ったため、ブリッジ境界型として使用可能
- **移植後のデータ構造変更（MTE 原典と非互換、ブリッジ設計時に注意）**:
  - `ColorParaffinData`: `depthMin/depthMax/depthFade` を削除し `maskMode`（0=なし / 1=キャラ除外 / 2=キャラのみ、キャラマスク方式）を追加
  - `RimlightData`: `edgeDepth/edgeRange` を削除（Edge 機能は顔マスクの代用ハックだったため撤去）し、`excludeFace`（頭部マスクによる顔除外）を追加
  - 距離フォグは複数データ同時適用に対応（原典はシェーダーが 1 件目しか読まないバグ）
- MTE 側の今後: `PostEffectController` / `GTToneMapController` を廃止し、タイムライン層はブリッジ経由で `EffectSettings.instance.paraffin` 等へ毎フレーム書き込む

## 現状の構造（前提）

- 適用は pull 型: `PostEffectManager.LateUpdate` が毎フレーム、有効なコントローラの `Apply()` を呼び、`Config` 上の Setting をカメラのコンポーネントへ書き込む（ゲーム本体の毎フレーム上書きへの対抗）
- 各エフェクトの設定値は `Config` のフィールド（`config.bloom` 等）に直置きされ、`config.dirty` が立つと `ConfigManager` がマウスアップ契機等で XML 保存する
- プリセットは `PostEffectsPreset` が Config とリフレクションで自動対応付けして保存・復元する

外部から Setting の値を書けばそのフレームで反映されるため、タイムライン補間との相性は良い。問題は以下の 3 点。

## 改善 1: エフェクト設定を永続 Config から分離（最重要）

**問題**: Setting の実体が永続 Config に直置きのため、MTE がタイムライン再生値を毎フレーム書き込むと、何かの契機で `dirty` が立った際に「アニメーション途中の値」がユーザーの Config XML に保存されてしまう。

**前提の調査結論**: config.xml へのエフェクト設定の永続化は現状すでに無意味になっている。

- `PresetManager.Init()` が起動時に必ず `LoadPreset(config.startupPresetName)` を呼び、プリセットファイルが見つからない場合も固定プリセット（既定値）へフォールバックする。どの経路でも `preset.ApplyTo(config)` でエフェクト設定は上書きされるため、config.xml に保存されたエフェクト値が読み出されて意味を持つことはない
- エフェクト値の永続化は実質プリセットファイルが担っており、config.xml のエフェクト設定部分は完全に冗長（毎起動、無意味な値の書き戻しまで発生している）
- プリセットに上書きされず永続化が本当に必要なのは動作・UI 設定（`pluginEnabled`、キーリピート、`useHSVColor`、ウィンドウ位置サイズ、`startupPresetName`、`windowHoverColor`、キーバインド）だけ

**方針（ランタイム分離方式）**: 保存抑止のようなモード切り替えは導入せず、エフェクト設定を XML 永続化対象から外してランタイム専用のホルダーへ移す。

- Config から全エフェクトの Setting フィールドを分離し、ランタイム専用クラス（例: `EffectSettings`）へ移す。config.xml には動作・UI 設定のみ残す
- MTE がランタイム値を毎フレーム書いてもディスクには何も起きない。エフェクト値の永続化はユーザーがプリセット保存操作をしたときだけ
- 副作用として config.xml が痩せ、起動時の無駄な書き戻しも消える

**影響範囲**:

- 各コントローラの `setting` プロパティ（`config.bloom` 等）の参照先を新ホルダーへ変更
- `PostEffectsPreset` のリフレクション自動対応付けの対象を Config から新ホルダーへ変更
- `SetDirty()`（エフェクト値変更時の dirty）は Config 保存の契機としては不要になる。GUI 上の「未保存変更あり」表示等に使っているなら意味を「プリセット未保存」へ付け替える

## 改善 2: コントローラの名前引き API

**問題**: `PostEffectManager.controllers` は `List<EffectControllerBase>` のみで、外部からの特定は型参照か `effectName`（日本語表示名）の線形検索しかない。日本語表示名は将来変更しうるため、MTE のセーブデータキーには不適。

**方針**:

- 各コントローラに安定した英語 ID（`"bloom"` / `"depthOfField"` 等。Config のフィールド名と揃える）を追加する
- `PostEffectManager` に lookup を追加する
  - `EffectControllerBase GetController(string id)`
  - `T GetController<T>() where T : EffectControllerBase`
- MTE 側はこの ID をタイムラインデータのキーとして使う

## 改善 3: 適用タイミングの保証

**問題**: PostEffects の適用は `PostEffectManager.LateUpdate`。UnityInjector プラグイン間の LateUpdate 実行順は未定義のため、MTE が LateUpdate で値を書くと 1 フレーム遅れやちらつきの原因になる。

**方針**（いずれか）:

- MTE 側ルール: タイムライン再生値の書き込みは Update 段で行う
- PostEffects 側対応: `PostEffectManager` に適用直前コールバック（`event Action OnPreApply` 等）を追加し、MTE はそこで値を書く（順序を構造的に保証したい場合はこちら）

## 作業順序

1. 改善 1（エフェクト設定のランタイム分離）— 連携の前提条件
2. 改善 2（ID + lookup）— ブリッジ API の土台
3. 改善 3 — ブリッジ実装時に MTE 側と合わせて決定
4. MTE リポジトリ側でブリッジ DLL 実装（本リポジトリ外の作業）

## 関連

- ビルド手順: [build.md](build.md)
- プラグイン全体構造・LateUpdate 対抗適用の背景: プロジェクト memory `posteffects-plugin-architecture`
