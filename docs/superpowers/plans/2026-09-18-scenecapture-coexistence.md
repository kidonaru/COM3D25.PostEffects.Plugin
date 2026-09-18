# SceneCapture 互換モード 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** COM3D2.SceneCapture.Plugin と併用しても SceneCapture の初期化が壊れないよう、SceneCapture の初期化完了を確認するまで本プラグインがカメラへ触らないようにする。

**Architecture:** リフレクションで `CM3D2.SceneCapture.Plugin.Instances.effm` を監視する静的クライアント `SceneCaptureCompat` を追加し、`PostEffectManager` の `PrepareAll()` と `LateUpdate()` の入口でゲートする。SceneCapture 未導入時・互換モード無効時は初回の型解決だけで完全に no-op。待機は 30 秒でタイムアウトする。

**Tech Stack:** C# (.NET 3.5 / Unity 5.6 (COM3D2) + Unity 2022.3 (COM3D2.5), UnityInjector), MSBuild

**Spec:** `docs/superpowers/specs/2026-09-18-scenecapture-coexistence-design.md`

## Global Constraints

- コメント・ログメッセージは日本語
- プラグイン間連携はリフレクション経由（コンパイル時参照を追加しない）。SceneCapture は COM3D2 (2.0) 専用だが、コードは両バージョン共通でビルドする
- ビルド確認は `debug.bat all`。**`deploy.bat` / `deploy.ps1` は実行しない**
- 自動テスト基盤がないため、検証は「両バージョンのビルド成功」と「COM3D2 (2.0) 実機ログの確認」

## File Structure

| ファイル | 責務 |
|---|---|
| `source/COM3D25.PostEffects.Plugin/Manager/SceneCaptureCompat.cs` (新規) | SceneCapture の検出と初期化完了判定、待機のタイムアウト。カメラへ触ってよいかの唯一の判断元 |
| `source/COM3D25.PostEffects.Plugin/Config.cs` (変更) | 互換モードの ON/OFF フラグ |
| `source/COM3D25.PostEffects.Plugin/Manager/PostEffectManager.cs` (変更) | `PrepareAll()` / `LateUpdate()` の入口でゲート |
| `README.md` (変更) | 併用時の注意の明文化 |

---

### Task 1: `SceneCaptureCompat` と設定フラグを追加する

**Files:**
- Create: `source/COM3D25.PostEffects.Plugin/Manager/SceneCaptureCompat.cs`
- Modify: `source/COM3D25.PostEffects.Plugin/Config.cs`

**Interfaces:**
- Produces: `public static bool SceneCaptureCompat.CanTouchCamera()`（カメラへコンポーネントを追加してよいなら true）、`Config.sceneCaptureCompat`（既定 `true`）

- [ ] **Step 1: `Config` に互換モードのフラグを追加する**

`Config.cs` の「動作設定」ブロック、`public bool useHSVColor = false;` の直後に追加:

```csharp
        // SceneCapture (COM3D2.SceneCapture.Plugin) 併用時に、SceneCapture の初期化が
        // 終わるまでカメラへ触らずに待つ。SceneCapture 未導入なら何もしない (SceneCaptureCompat)
        public bool sceneCaptureCompat = true;
```

`CurrentVersion` は上げない。既定値付きフィールドの追加なので、旧 `config.xml` に要素が無ければ `true` が入る。

- [ ] **Step 2: `SceneCaptureCompat` を新規作成する**

`source/COM3D25.PostEffects.Plugin/Manager/SceneCaptureCompat.cs`:

```csharp
using System;
using System.Reflection;
using COM3D2.MotionTimelineEditor;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// SceneCapture (COM3D2.SceneCapture.Plugin) 併用時に、
    /// SceneCapture の初期化が終わるまで本プラグインがカメラへ触るのを止めるための判定。
    ///
    /// SceneCapture の Util.GetComponentVar は「対象コンポーネントがカメラに既に居るか」で
    /// 処理を分岐し、居る場合は enabled を強制的に立てたうえで InitMemberByInstance を呼ぶ。
    /// SunShaftsDef ではこれが静的コンストラクタ実行中に呼ばれるため未初期化の static を踏んで落ち、
    /// SceneCapture 全体の初期化 (Instances の生成) が失敗して以後 NRE を吐き続ける。
    /// 本プラグインが PrepareAll でコンポーネントを先置きするとこれを必ず踏むため、
    /// SceneCapture が自分でコンポーネントを追加し終えるまで待つ。
    ///
    /// SceneCapture の Initialize は最初の Update で走るため、待機は通常 1 フレーム未満で解ける。
    /// </summary>
    public static class SceneCaptureCompat
    {
        private const string HostAssemblyName = "COM3D2.SceneCapture.Plugin";
        private const string InstancesTypeName = "CM3D2.SceneCapture.Plugin.Instances";

        // 待機を打ち切るまでの秒数。SceneCapture が別要因で初期化に失敗したときに
        // 本プラグインのエフェクトまで永久に止めないための保険
        private const float WaitTimeoutSeconds = 30f;

        // Instances.effm (EffectManager) の取得元。SceneCapture 不在なら null
        private static PropertyInfo _effmProperty;
        private static bool _resolved;

        // これ以上待つ必要がないと確定したか (不在 / 初期化完了 / タイムアウト)
        private static bool _done;

        // 待機開始時刻。未待機は -1
        private static float _waitStartTime = -1f;

        /// <summary>
        /// カメラへコンポーネントを追加してよいか。
        /// SceneCapture 未導入・互換モード無効・SceneCapture の初期化完了後は true
        /// </summary>
        public static bool CanTouchCamera()
        {
            if (_done || !ConfigManager.instance.config.sceneCaptureCompat)
            {
                return true;
            }

            Resolve();
            if (_effmProperty == null)
            {
                // SceneCapture 不在、または接続できないので待つ理由がない
                _done = true;
                return true;
            }

            bool created;
            if (!TryGetEffectManagerCreated(out created))
            {
                _done = true;
                return true;
            }

            if (created)
            {
                _done = true;
                if (_waitStartTime >= 0f)
                {
                    MTEUtils.Log("SceneCapture の初期化完了を確認しました。エフェクトの適用を開始します");
                }
                return true;
            }

            if (_waitStartTime < 0f)
            {
                _waitStartTime = Time.realtimeSinceStartup;
                MTEUtils.Log("SceneCapture を検出しました。初期化が終わるまでエフェクトの適用を保留します");
                return false;
            }

            if (Time.realtimeSinceStartup - _waitStartTime > WaitTimeoutSeconds)
            {
                _done = true;
                MTEUtils.LogWarning(
                    "SceneCapture の初期化を {0} 秒待っても確認できませんでした。エフェクトの適用を開始します",
                    WaitTimeoutSeconds);
                return true;
            }

            return false;
        }

        // MTEUtils/*Client.cs は「ホスト型が見つかるまで毎回探し直す」作法だが、ここは一度で確定させる。
        // この判定はカメラへ最初に触る瞬間に効いていなければ意味がなく (先置きしてしまえば手遅れ)、
        // 後から見つけて待ちに入っても手遅れなため。SceneCapture 不在と誤判定したときの挙動は
        // 互換モード無効時と同じ (従来どおりの動作) で、ファイル名順でも
        // COM3D2.SceneCapture.Plugin.dll は本プラグインより先にロードされる
        private static void Resolve()
        {
            if (_resolved)
            {
                return;
            }
            _resolved = true;

            var type = FindInstancesType();
            if (type == null)
            {
                return;
            }

            _effmProperty = type.GetProperty("effm", BindingFlags.Public | BindingFlags.Static);
            if (_effmProperty == null)
            {
                MTEUtils.LogWarning(
                    "SceneCapture の Instances.effm が見つかりませんでした。初期化の待機を行いません");
            }
        }

        private static Type FindInstancesType()
        {
            var type = Type.GetType(InstancesTypeName + ", " + HostAssemblyName);
            if (type != null)
            {
                return type;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name != HostAssemblyName)
                {
                    continue;
                }
                return assembly.GetType(InstancesTypeName);
            }
            return null;
        }

        // Instances.effm は EffectManager の生成に成功したときだけ代入されるため、
        // 非 null であることが全 *Def の静的コンストラクタを通過した証明になる
        private static bool TryGetEffectManagerCreated(out bool created)
        {
            created = false;
            try
            {
                created = _effmProperty.GetValue(null, null) != null;
                return true;
            }
            catch (Exception e)
            {
                // 取得できない以上は待っても状態が変わらないため、待機自体を諦める
                MTEUtils.LogException(e);
                MTEUtils.LogWarning("SceneCapture の初期化状態を取得できませんでした。待機せずに続行します");
                return false;
            }
        }
    }
}
```

- [ ] **Step 3: ビルドが通ることを確認する**

Run: `debug.bat all`
Expected: `ビルドに成功しました`（この時点ではまだ誰も `CanTouchCamera` を呼んでいないため、挙動は変わらない）

- [ ] **Step 4: コミット**

```bash
git add source/COM3D25.PostEffects.Plugin/Manager/SceneCaptureCompat.cs source/COM3D25.PostEffects.Plugin/Config.cs
git commit -m "feat(compat): SceneCapture の初期化完了を待つ判定を追加する"
```

---

### Task 2: `PostEffectManager` でカメラ操作をゲートする

**Files:**
- Modify: `source/COM3D25.PostEffects.Plugin/Manager/PostEffectManager.cs`（`PrepareAll()` 冒頭 / `LateUpdate()` 冒頭）

**Interfaces:**
- Consumes: `SceneCaptureCompat.CanTouchCamera()`

- [ ] **Step 1: `PrepareAll()` の冒頭にゲートを入れる**

`PrepareAll()` の `var camera = EffectControllerBase.cameraObject;` の直前に追加:

```csharp
            // SceneCapture 併用時は、SceneCapture がコンポーネントを追加し終えるまで触らない。
            // 空のコンポーネントを先置きすると SceneCapture の初期化が落ちる (SceneCaptureCompat)。
            // _preparedCamera を更新しないため、次フレームの LateUpdate で再試行される
            if (!SceneCaptureCompat.CanTouchCamera())
            {
                return;
            }
```

これでプラグインの `Initialize()` から直接呼ばれる `PrepareAll()`（`COM3D25.PostEffects.Plugin.cs:206`）も保留される。

- [ ] **Step 2: `LateUpdate()` の冒頭にゲートを入れる**

`LateUpdate()` の「カメラが差し替わった ...」コメントより前、メソッド冒頭に追加:

```csharp
            // Prepare だけ止めても、有効なエフェクトの Apply 側 (GetOrAddComponent) から
            // コンポーネントが追加されて同じ衝突を起こすため、適用ごと保留する
            if (!SceneCaptureCompat.CanTouchCamera())
            {
                return;
            }
```

- [ ] **Step 3: ビルドが通ることを確認する**

Run: `debug.bat all`
Expected: `ビルドに成功しました`

- [ ] **Step 4: COM3D2 (2.0) 実機で併用を確認する**

前提: `W:\COM3D2\Sybaris\UnityInjector\` に `COM3D2.SceneCapture.Plugin.dll` と本プラグインの両方が入っていること（`debug.bat all` で本プラグインは配置済み）。ユーザーに COM3D2 (2.0) を起動してもらい、`W:\COM3D2\COM3D2x64_Data\output_log.txt` を確認する。

Expected:
- `TypeInitializationException ... SunShaftsDef` が**出ない**
- `CM3D2.SceneCapture.Plugin.SceneCapture.OnGUI` / `LateUpdate` の NRE が**出ない**
- `AntialiasingAsPostEffect.CheckResources` の NRE が**出ない**
- `COM3D25.PostEffects: SceneCapture を検出しました。...` が出て、その後 `初期化完了を確認しました` が出る
- SceneCapture のウィンドウが開き、本プラグインのウィンドウ（`Alt+P`）でもエフェクトが効く

確認コマンド（ゲーム終了後でも可）:

```bash
grep -n "SunShaftsDef\|SceneCapture.Plugin.SceneCapture\.\|CheckResources\|PostEffects: SceneCapture" /w/COM3D2/COM3D2x64_Data/output_log.txt | tail -20
```

- [ ] **Step 5: コミット**

```bash
git add source/COM3D25.PostEffects.Plugin/Manager/PostEffectManager.cs
git commit -m "fix(posteffect): SceneCapture の初期化完了までカメラへ触らない"
```

---

### Task 3: 併用時の注意をドキュメント化する

**Files:**
- Modify: `README.md`（「インストール方法」節の後ろ、「使い方」節の前）

- [ ] **Step 1: README に併用の節を追加する**

```markdown
## COM3D2.SceneCapture.Plugin との併用について

同じカメラの同じコンポーネントを両プラグインが取り合うため、**併用は推奨しません**。
本プラグインは SceneCapture のエフェクトをほぼ網羅しており、SceneCapture のプリセットは
プリセット画面から取り込めます。

やむを得ず併用する場合、本プラグインは SceneCapture の初期化が終わるまでカメラへ触らずに待ちます
（この待機が無いと SceneCapture の初期化が失敗し、以後エラーを吐き続けます）。
この挙動は `Sybaris\UnityInjector\Config\PostEffects.xml` の `sceneCaptureCompat` を `false` にすると切れます。

併用時の制限:

- **同じエフェクトを両方から操作しないでください。** 本プラグインは毎フレーム値を書き込むため、
  SceneCapture 側の操作が打ち消されます
- SceneCapture が先にコンポーネントを追加するため、本プラグインのエフェクト適用順
  （描画順）の指定は本プラグイン固有のエフェクトにしか効きません
```

- [ ] **Step 2: コミット**

```bash
git add README.md
git commit -m "docs(readme): SceneCapture 併用時の注意を追記する"
```

---

## Self-Review

- **Spec coverage:** 検出 (Task 1) / 初期化完了判定 (Task 1) / タイムアウト (Task 1) / 設定フラグ (Task 1) / Prepare と Apply の両方のゲート (Task 2) / 非目標と運用の明文化 (Task 3) をカバー。
- **Placeholder scan:** なし。全ステップに実コードまたは実行コマンドを記載。
- **Type consistency:** `CanTouchCamera()` の名前と戻り値は Task 1 の定義と Task 2 の呼び出しで一致。`Config.sceneCaptureCompat` は Task 1 の定義と README の記述（`PostEffects.xml`）で一致。

## レビュー却下メモ

- 型未検出時も再試行を続ける（`MTEUtils/*Client.cs` の作法に揃える）— 却下。この判定は「カメラへ最初に触る瞬間」に効かなければ意味がなく、先置きしてしまった後で SceneCapture を見つけても手遅れ。一方で毎フレーム `AppDomain.GetAssemblies()` を走査する固定コストが SceneCapture 未導入の全ユーザーにかかる。誤判定時の縮退先が「従来どおりの動作」であることと合わせ、一度で確定させる。理由はコードコメントに明記した
- `MTEUtils/SceneCaptureClient.cs` へ寄せる — 却下。`MTEUtils/` は MTE 本体からの同期コピー（`Update MTEUtils` コミット）で、独自ファイルを置くと次の同期で失われる。既存の SceneCapture 連携も `Manager/SceneCaptureImporter.cs` に置かれており、`Manager/SceneCaptureCompat.cs` はそれに揃う
