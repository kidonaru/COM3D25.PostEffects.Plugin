# エフェクトコンポーネント追加順固定 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** プラグイン有効化時に全エフェクトコンポーネントを固定順（SceneCapture 準拠）でカメラへ無効状態で追加し、以後は enabled の切替だけで制御する。

**Architecture:** `EffectControllerBase` に `Prepare()` を追加し、`PostEffectManager.PrepareAll()` が追加順配列に従って呼ぶ。呼び出しは初期化時とカメラ GameObject が変わったフレーム。既存の遅延 `GetOrAddComponent` はフォールバックとして残す。

**Tech Stack:** C# (.NET 3.5 / Unity 2022.3, UnityInjector), MSBuild, devbridge (実機検証)

**Spec:** `docs/superpowers/specs/2026-09-16-component-order-design.md`

## Global Constraints

- コメント・ログは日本語
- ビルドは `debug.bat com3d25`（ゲーム起動中は DLL コピー失敗の警告のみ）。ゲーム停止中に実機反映させたくない場合は MSBuild を直接叩く
- `deploy.bat` / `deploy.ps1` は実行しない
- 自動テスト基盤はないため、検証は「ビルド成功」と「devbridge での実機確認」

---

### Task 1: `EffectControllerBase` に `Prepare()` を追加

**Files:**
- Modify: `source/COM3D25.PostEffects.Plugin/Effects/EffectControllerBase.cs`

**Interfaces:**
- Produces: `public virtual void Prepare()`（非汎用基底。既定は何もしない）、`internal static GameObject cameraObject`（`protected static` から昇格。PostEffectManager が参照する）

- [ ] **Step 1: 非汎用基底に `Prepare()` を追加**

`public abstract void Apply();` の直前に追加:

```csharp
        // プラグイン有効化時 (とカメラ差し替え時) に PostEffectManager から固定順で呼ばれ、
        // コンポーネントを無効状態でカメラへ追加する。OnRenderImage の実行順はカメラ上の
        // コンポーネント順で決まるため、有効化した順ではなくここで並びを確定させる
        public virtual void Prepare() { }
```

- [ ] **Step 2: `cameraObject` を `internal static` にし、`GameMain.Instance` の null を吸収する**

プラグインの `Start()` 時点では `GameMain` が未生成の可能性があり、そのまま `PrepareAll()` が例外を投げると
`Initialize()` の残り（ウィンドウ登録・ギアメニュー・SceneEditor 連携）が丸ごと飛ぶため、ここで null を返す:

```csharp
        // PostEffectManager.PrepareAll からも参照するため internal。外部連携には公開しない
        internal static GameObject cameraObject
        {
            get
            {
                // プラグインの Start 時点では GameMain 自体が未生成のことがある
                var gameMain = GameMain.Instance;
                var mainCamera = gameMain != null ? gameMain.MainCamera : null;
                return mainCamera != null ? mainCamera.gameObject : null;
            }
        }
```

- [ ] **Step 3: 汎用基底に既定実装を追加**

`EffectControllerBase<TComponent, TSetting>` の `SuppressGameEffect()` の直前に追加:

```csharp
        public override void Prepare()
        {
            var component = GetOrAddComponent();
            // ゲーム標準の既存コンポーネントは取得時の状態を保つ (無効化は Apply/Suppress の責務)
            if (component != null && _wasAdded)
            {
                component.enabled = false;
            }
        }
```

- [ ] **Step 4: ビルド**

Run: `debug.bat com3d25`
Expected: エラー 0

- [ ] **Step 5: コミット**

```bash
git add source/COM3D25.PostEffects.Plugin/Effects/EffectControllerBase.cs
git commit -m "feat(effect): コントローラに Prepare を追加しコンポーネントを無効状態で先行追加できるようにする"
```

---

### Task 2: 特殊コントローラの `Prepare()` 実装

**Files:**
- Modify: `source/COM3D25.PostEffects.Plugin/Effects/SeparatedBloom.cs:27-45`
- Modify: `source/COM3D25.PostEffects.Plugin/Effects/ParaffinController.cs:46-53`
- Modify: `source/COM3D25.PostEffects.Plugin/Effects/DistanceFogController.cs:37-44`
- Modify: `source/COM3D25.PostEffects.Plugin/Effects/RimlightController.cs:46-53`

**Interfaces:**
- Consumes: Task 1 の `Prepare()`

- [ ] **Step 1: `SeparatedBloomController` で分離用コンポーネントも先行追加**

`Apply()` 内の `_separatedEffect` 生成を private メソッドへ抽出し、`Prepare()` から Bloom 本体の直後に追加する:

```csharp
        public override void Prepare()
        {
            base.Prepare();
            if (_component != null)
            {
                EnsureSeparatedEffect(_component);
            }
        }

        public override void Apply()
        {
            if (!separation.enabled)
            {
                StopSeparation();
                base.Apply();
                return;
            }

            var component = GetOrAddComponent();
            if (component == null) return;
            component.enabled = false;
            EnsureSeparatedEffect(component).enabled = true;
        }

        // 分離描画用コンポーネントは本体の直後に並ぶよう、本体追加と同じタイミングで無効状態で追加する
        private SeparatedBloomEffect EnsureSeparatedEffect(TComponent component)
        {
            if (_separatedEffect == null)
            {
                _separatedEffect = component.gameObject.AddComponent<SeparatedBloomEffect>();
                _separatedEffect.renderRegion = RenderRegion;
                _separatedEffect.enabled = false;
            }
            return _separatedEffect;
        }
```

- [ ] **Step 2: Hub 系 3 コントローラに `Prepare()` を追加**

Paraffin / DistanceFog / Rimlight それぞれの `Apply()` の直前に同じ実装を追加する（3 ファイルとも同一）:

```csharp
        public override void Prepare()
        {
            // CommandBuffer 方式のため OnRenderImage の並びとは無関係だが、Hub 自体は先に用意しておく
            var hub = PostEffectHub.GetOrAdd(cameraObject);
            if (hub != null)
            {
                hub.enabled = false;
            }
        }
```

注意: `PostEffectHub.GetOrAdd` は `AddComponent` 直後に `OnEnable` が走る（MonoBehaviour は既定で enabled）。Prepare 時点で `enabled = false` にすると `OnDisable` で `_models` の Dispose が呼ばれるが、モデルは `Init(context)` で再初期化されるため問題ない。実機検証で Paraffin を ON にして描画されることを確認する（Task 5）。

- [ ] **Step 3: ビルド**

Run: `debug.bat com3d25`
Expected: エラー 0

- [ ] **Step 4: コミット**

```bash
git add source/COM3D25.PostEffects.Plugin/Effects/SeparatedBloom.cs source/COM3D25.PostEffects.Plugin/Effects/ParaffinController.cs source/COM3D25.PostEffects.Plugin/Effects/DistanceFogController.cs source/COM3D25.PostEffects.Plugin/Effects/RimlightController.cs
git commit -m "feat(effect): 分離ブルームと Hub 系コントローラに Prepare を実装する"
```

---

### Task 3: `PostEffectManager` に追加順と `PrepareAll()` を実装

**Files:**
- Modify: `source/COM3D25.PostEffects.Plugin/Manager/PostEffectManager.cs`

**Interfaces:**
- Consumes: Task 1 の `Prepare()`、`EffectControllerBase.cameraObject`
- Produces: `public void PrepareAll()`

- [ ] **Step 1: `using UnityEngine;` を追加し、追加順配列とフィールドを定義**

`_controllerById` の宣言の直後に追加:

```csharp
        // カメラへのコンポーネント追加順 = OnRenderImage の実行順。SceneCapture の EffectManager.Clear() 順を
        // 骨格に、本プラグイン独自のエフェクトを用途の近い位置へ挿入している。
        // ここに無い ID (Hub 系 / LightShafts / GameEffect) はカメラ上の並びに関係しないため先頭で処理する
        private static readonly string[] ComponentOrder =
        {
            nameof(EffectSettings.maidHide),
            nameof(EffectSettings.colorCorrectionCurves),
            nameof(EffectSettings.contrast),
            nameof(EffectSettings.crease),
            nameof(EffectSettings.edgeDetect),
            nameof(EffectSettings.outlineColor),
            nameof(EffectSettings.filmicMedianFilter),
            nameof(EffectSettings.antialiasing),
            nameof(EffectSettings.noiseAndGrain),
            nameof(EffectSettings.motionBlur),
            nameof(EffectSettings.tiltShiftHdr),
            nameof(EffectSettings.sunShafts),
            nameof(EffectSettings.analogGlitch),
            nameof(EffectSettings.digitalGlitch),
            nameof(EffectSettings.isoline),
            nameof(EffectSettings.halftone),
            nameof(EffectSettings.kuwahara),
            nameof(EffectSettings.obscurance),
            nameof(EffectSettings.globalFog),
            nameof(EffectSettings.stylisticFog),
            nameof(EffectSettings.cinematicBloom),
            nameof(EffectSettings.filmicBloom),
            nameof(EffectSettings.streak),
            nameof(EffectSettings.bloom),
            nameof(EffectSettings.diffusion),
            nameof(EffectSettings.depthOfField),
            nameof(EffectSettings.cinematicDepthOfField),
            nameof(EffectSettings.bokeh),
            nameof(EffectSettings.filmicBokeh),
            nameof(EffectSettings.blur),
            nameof(EffectSettings.radialBlur),
            nameof(EffectSettings.ramp),
            nameof(EffectSettings.cinematicLensAberrations),
            nameof(EffectSettings.fisheye),
            nameof(EffectSettings.vignetting),
            nameof(EffectSettings.colorCorrectionLut),
            nameof(EffectSettings.tonemappingColorGrading),
            nameof(EffectSettings.whiteBalance),
            nameof(EffectSettings.gtToneMap),
            nameof(EffectSettings.sepia),
            nameof(EffectSettings.grayscale),
            nameof(EffectSettings.casSharpen),
            nameof(EffectSettings.filmicLetterBox),
            nameof(EffectSettings.screenOverlay),
        };

        // 最後に PrepareAll したカメラ。シーン遷移等で差し替わったら組み直す
        private GameObject _preparedCamera;
```

`EffectSettings.outlineColor` が `#if COM3D25` で囲まれている場合は、その行も `#if COM3D25` / `#endif` で囲む（`EffectSettings.cs` を確認すること）。

- [ ] **Step 2: `PrepareAll()` を追加**

`GetController<T>()` の直後に追加:

```csharp
        // 全コントローラのコンポーネントを固定順で無効状態のまま追加する。
        // 有効化した順で AddComponent すると OnRenderImage の順序が操作順に依存してしまうため、
        // プラグイン有効化時とカメラ差し替え時にここで並びを確定させる
        public void PrepareAll()
        {
            var camera = EffectControllerBase.cameraObject;
            if (camera == null)
            {
                return;
            }

            var ordered = new List<EffectControllerBase>();
            foreach (var controller in controllers)
            {
                if (Array.IndexOf(ComponentOrder, controller.effectId) < 0)
                {
                    ordered.Add(controller);
                }
            }
            foreach (var id in ComponentOrder)
            {
                var controller = GetController(id);
                if (controller != null)
                {
                    ordered.Add(controller);
                }
            }

            // AddComponent 直後に各 Effect の OnEnable が走り、depthTextureMode |= Depth が
            // 無効化後も残る (OnDisable で戻す実装がない)。無効なエフェクトの深度パスを
            // 常駐させないため、追加前の値へ戻す
            var unityCamera = camera.GetComponent<Camera>();
            var depthMode = unityCamera != null ? unityCamera.depthTextureMode : DepthTextureMode.None;

            foreach (var controller in ordered)
            {
                try
                {
                    controller.Prepare();
                }
                catch (Exception e)
                {
                    MTEUtils.LogException(e);
                    MTEUtils.LogError("エフェクトの事前追加に失敗しました: {0}", controller.effectName);
                }
            }

            if (unityCamera != null)
            {
                unityCamera.depthTextureMode = depthMode;
            }
            _preparedCamera = camera;
        }
```

補足: Hub 系の `Prepare()` は `GetOrAdd` → `OnEnable`（マテリアル・CommandBuffer 生成）→ `enabled=false` → `OnDisable`（`Dispose` で ComputeBuffer 解放）が同フレームに走るが、各モデルの `OnPreRender` が `_computeBuffer == null` なら再生成する実装のため無害（`ColorParaffinEffectModel.cs:106` 等で確認済み）。

- [ ] **Step 3: `LateUpdate()` 冒頭でカメラ差し替えを検知**

`foreach (var controller in controllers)` の直前に追加:

```csharp
            // カメラが差し替わった (初回・シーン遷移・VR 切替) フレームで並びを組み直す
            if (EffectControllerBase.cameraObject != _preparedCamera)
            {
                PrepareAll();
            }
```

- [ ] **Step 4: ビルド**

Run: `debug.bat com3d25`
Expected: エラー 0

- [ ] **Step 5: コミット**

```bash
git add source/COM3D25.PostEffects.Plugin/Manager/PostEffectManager.cs
git commit -m "feat(manager): エフェクトコンポーネントを SceneCapture 準拠の固定順で先行追加する"
```

---

### Task 4: プラグイン初期化時に `PrepareAll()` を呼ぶ

**Files:**
- Modify: `source/COM3D25.PostEffects.Plugin/COM3D25.PostEffects.Plugin.cs:203-204`

**Interfaces:**
- Consumes: Task 3 の `PrepareAll()`

- [ ] **Step 1: `Initialize()` で `postEffectManager.Init()` 直後に呼ぶ**

```csharp
                // PostEffectManager はウィンドウ非表示中も動かすため registry には登録しない
                postEffectManager.Init();
                // カメラ未生成ならここでは何もせず、LateUpdate のカメラ検知で組み直される
                postEffectManager.PrepareAll();
```

- [ ] **Step 2: 両バージョンをビルド**

Run: `debug.bat all`
Expected: COM3D2 / COM3D25 ともエラー 0

- [ ] **Step 3: コミット**

```bash
git add source/COM3D25.PostEffects.Plugin/COM3D25.PostEffects.Plugin.cs
git commit -m "feat(plugin): 有効化時にエフェクトコンポーネントを先行追加する"
```

---

### Task 5: 実機検証（devbridge）

**Files:** なし（検証のみ）

- [ ] **Step 1: ゲームを再起動してもらう**

DLL が `Sybaris/UnityInjector` へコピーされていることを確認し、ユーザーにゲーム再起動を依頼する（ゲーム起動中は debug.bat のコピーが失敗して警告が出る。その場合は停止後に `debug.bat com3d25` を再実行する）。

- [ ] **Step 2: コンポーネント並びを確認**

`mcp__com3d25-devbridge__eval_csharp`:

```csharp
var sb = new System.Text.StringBuilder();
foreach (var b in GameMain.Instance.MainCamera.GetComponents<UnityEngine.Behaviour>())
    sb.AppendLine(b.GetType().Name + " " + b.enabled);
sb.ToString()
```

Expected: ゲーム側既存コンポーネントの後に、spec の追加順どおりに `PostEffects_Dummy.*` / プラグインの Effect 型が並び、追加分は全て `False`。`SeparatedBloomEffect` が各 Bloom 本体の直後にある。

- [ ] **Step 3: 有効化と描画を確認**

メインウィンドウから Paraffin・Bloom（分離 ON）・Sepia を ON にし、`screenshot` で描画されること、ログにエラーがないこと（`tail_log`）を確認する。OFF に戻して `enabled` が `False` に戻ることも確認する。

- [ ] **Step 4: シーン遷移後の再構築を確認**

エディットからタイトルなど別シーンへ遷移し、Step 2 を再実行して並びが維持されることを確認する。

- [ ] **Step 5: 深度パスが残っていないことを確認**

全エフェクト OFF の状態で `eval_csharp`:

```csharp
GameMain.Instance.MainCamera.GetComponent<UnityEngine.Camera>().depthTextureMode.ToString()
```

Expected: プラグイン導入前と同じ値（Prepare 前の値。`Depth` が立ちっぱなしになっていない）。

## レビュー却下メモ

- LateUpdate のカメラ差し替え検知を try-catch で囲む — `cameraObject` の null ガードと `PrepareAll` 内のコントローラ別 try-catch で例外経路が塞がるため不要
- 全 32 Effect クラスの OnEnable 精査 — grep で OnEnable を持つ 11 クラスを確認済み。副作用は depthTextureMode の |= のみで、PrepareAll の退避/復元で吸収する
