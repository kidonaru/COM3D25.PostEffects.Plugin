# SceneCapture 併用時のクラッシュ回避 設計メモ

## 背景

COM3D2 (2.0) で `COM3D2.SceneCapture.Plugin` (0.3.1.30) と本プラグインを同時に入れると、
SceneCapture が初期化に失敗して機能しなくなり、毎フレーム NullReferenceException を吐き続ける。

実ログ (`COM3D2x64_Data/output_log.txt`) の起点:

```
new instances
System.TypeInitializationException: ... CM3D2.SceneCapture.Plugin.SunShaftsDef
  ---> NullReferenceException at SunShaftsDef.InitMemberByInstance (SunShafts)
  at Util.GetComponentVar[SunShafts,SunShaftsDef]
  at EffectManager..ctor → Instances..ctor → SceneCapture.Initialize
```

## 原因

本プラグインは `PostEffectManager.PrepareAll()` で、OnRenderImage の実行順を固定するために
**全エフェクトのコンポーネントを「無効・シェーダー未設定」のままカメラへ先に AddComponent** する
(シェーダーは有効化時の `ApplySetting` で入る)。

一方 SceneCapture の `Util.GetComponentVar<T, T2>()` は、対象コンポーネントが
**カメラに既に存在するかどうかで処理が完全に分岐する**:

| 状態 | SceneCapture の挙動 |
|---|---|
| 存在しない | `AddComponent` し、自前バンドルからシェーダーフィールドを埋め、`InitExtra` を呼ぶ |
| **存在する** | `enabled = true` を**強制**し、`InitMemberByInstance` を呼ぶ |

本プラグインが先にコンポーネントを置くため、SceneCapture は必ず後者を通る。これが 2 つの障害を生む。

1. `SunShaftsDef.InitMemberByInstance` は `SunShaftsDef.texSunColor.SetPixel(...)` を呼ぶが、
   このメソッドは `SunShaftsDef` の**静的コンストラクタ実行中**に (GetComponentVar 経由で) 呼ばれるため
   `texSunColor` はまだ null。→ TypeInitializationException が `EffectManager..ctor` まで伝播し、
   `Instances` の生成が失敗 → `SceneCapture.Initialize()` が中断 → `modeSelectView` / `envView` が
   null のまま残り、以後 `Update` / `LateUpdate` / `OnGUI` が毎フレーム NRE。
2. シェーダー未設定のコンポーネントが `enabled = true` にされるため、
   `AntialiasingAsPostEffect.CheckResources()` が NRE。
   本プラグインの「シェーダーが取れなければ `enabled = false`」ガードは外部からの有効化には効かない。

つまり**カメラへ先にコンポーネントを置くこと自体**が引き金であり、SceneCapture 側のバグ
(cctor 中の未初期化 static 参照 / 既存コンポーネントの強制有効化) に依存している。

## 方針: SceneCapture 互換モード

本プラグインが SceneCapture より**後**にカメラへ触れば、SceneCapture は自分でコンポーネントを追加し
シェーダーも埋めるため、両障害とも起こらない。したがって:

> SceneCapture がインストールされている場合、その初期化 (`Instances.effm` の生成) が完了するまで
> 本プラグインはカメラへ一切触らない。

SceneCapture の `Initialize()` は最初の `Update()` で走るため、待機は通常 1 フレーム未満で解ける。

### 判定

- インストール判定: アセンブリ `COM3D2.SceneCapture.Plugin` の型
  `CM3D2.SceneCapture.Plugin.Instances` が解決できるか
  (CLAUDE.md のプラグイン間連携方針どおりリフレクション経由。見つかるまで毎回探し直す)
- 初期化完了判定: `Instances.effm` (public static プロパティ) が非 null
  — `initEffects()` は `EffectManager` の生成に成功したときだけ代入するため、
  全 `*Def` の静的コンストラクタが通ったことの証明になる

### 待機の打ち切り

SceneCapture が別要因で初期化に失敗すると永久に待つことになり、本プラグインのエフェクトが
一切効かなくなる。待機開始から 30 秒でタイムアウトし、警告ログを出して通常動作へ移行する。

### 適用範囲

待機中は `PrepareAll()` だけでなく**エフェクトの適用 (`Apply` → `GetOrAddComponent`) も止める**。
起動時プリセットで有効なエフェクトがあると、Prepare を止めても Apply 側から
AddComponent されて同じ衝突を起こすため。

### 設定

`config.xml` の `sceneCaptureCompat` (既定 `true`) で無効化できる。UI は持たない。
SceneCapture 未導入の環境では型が見つからず、この機能は完全な no-op になる。

## 非目標

- 両プラグインから**同じエフェクト**を操作できるようにすること。
  互換モードはあくまで「SceneCapture を壊さない」ためのもので、
  同じコンポーネントを両者が毎フレーム書き合う状況は解決しない
  (本プラグインは LateUpdate で毎フレーム書き込むため実質こちらが勝つ)。
  併用時は「同じエフェクトを両方から触らない」運用を README に明記する。
- VR モードでの動作保証。SceneCapture の `Util.GetComponentVar` は VR 時に
  `OvrCamera.m_goCenterEye` / `ViveCamera.m_goCenterEye` など別 GameObject を対象にするのに対し、
  本プラグインの `EffectControllerBase.cameraObject` は常に `GameMain.Instance.MainCamera.gameObject` を返す。
  そもそも衝突しない可能性が高いが未検証で、互換モードは非 VR (MainCamera) 前提とする。
  待機の判定は SceneCapture 全体の初期化完了を見るだけなので、VR でも無害に働く。
- 互換モード時のコンポーネント追加順の保証。
  SceneCapture が先に自分の分を追加するため、`ComponentOrder` は本プラグイン固有のエフェクトにしか効かない。

## 検証

自動テスト基盤がない (`source/Tests` はゲーム非依存のスモークのみ) ため、検証は次の 2 点:

1. 両バージョンでビルドが通ること
2. COM3D2 (2.0) に SceneCapture を入れた状態で起動し、`output_log.txt` に
   `SunShaftsDef` の TypeInitializationException と `SceneCapture.OnGUI` の NRE が出ないこと。
   SceneCapture のウィンドウが開けること
