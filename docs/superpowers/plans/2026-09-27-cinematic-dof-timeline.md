# シネマティック被写界深度のタイムライン対応 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: `superpowers:executing-plans` でタスク単位に実装すること（ワークスペースの CLAUDE.md により subagent-driven-development は使わない）。手順は `- [ ]` チェックボックスで追跡する。

**Goal:** PostEffects のシネマティック被写界深度を、SceneEditor のポストエフェクトレイヤーでキーフレーム制御・編集できるようにする（タイムライン対応を 6 系統から 7 系統へ広げる）。

**Architecture:** ブルーム対応（MTEUtils `dc7ed7a` / PostEffects `3d2b915` / SceneEditor `76e2160`〜`a65cd5f`）と同じ 3 層構成をなぞる。MTEUtils に共有 DTO `CinematicDepthOfFieldData` とクライアント API を足し、PostEffects の `TimelineBridge` が実体設定と相互変換する。SceneEditor では `TransformDataCinematicDepthOfField` が値を持ち、`PostEffectTimelineLayer` が `LerpScratch` で区間補間して適用し、`PostEffectRowDrawer` が編集 UI を描く。単数エフェクトなので、被写界深度・GTToneMap・ブルームと同じ「1 項目固定」の扱いにする。

**Tech Stack:** C#（PostEffects: net35/COM3D2 と net46 系/COM3D25 の 2 構成、SceneEditor も同様）、Unity IMGUI、MSBuild、xUnit（SceneEditor のみ、net48）、MTEUtils サブモジュール経由のリフレクション連携

**Spec:** 本計画に内包する（別途のスペック文書は無い）。追従元の前例はブルーム対応の計画 `COM3D2.SceneEditor.Plugin/docs/superpowers/plans/2026-09-08-bloom-timeline-support.md`。ただしその後に補間が `LerpScratch`（値ごとの補間）へ、色欄が `DrawColorImmediate` へ変わっているので、コードは**現行のブルーム実装**に合わせる。

## 設計判断

| 論点 | 決定 | 理由 |
|---|---|---|
| タイムラインに載せる項目 | `visualizeFocus`（ピント位置の可視化）と `bokehTexturePath`（文字列）以外の全項目 | 可視化はデバッグ表示なのでキーフレームに持たせない。パスは値配列（float）に載らない。載せない項目は ReflectionFieldCopier が触らないため、PostEffects 側 UI の設定がそのまま残る（被写界深度の DX11 項目と同じ扱い） |
| 列挙値 3 種 | DTO では int で持つ。`tweakMode` は 0/1 の 2 値なのでトグル、`filteringQuality` / `apertureShape` は 0〜2 の Int | ブルームの `hdr` / `screenBlendMode` と同じ作法。実体へ戻すときに定義域へ丸める |
| メイド追従 | DTO は実体と同じ `maidFocus` + `maidIndex`。SceneEditor の値配列では被写界深度と同じく `maidSlotNo`（-1 = 追従なし、`CustomValueUIType.MaidSlot`）1 値にまとめる | 追従メイドの補間止め（`cddb5bc`）がそのまま効く。変換は `TransformDataCinematicDepthOfField` のアクセサ内に閉じる |
| DTO の持ち方（SceneEditor 側） | 共有 DTO を直接使う。`DepthOfFieldData` のようなプラグイン内の複製クラスは作らない | ブルーム計画の Global Constraints を踏襲する（複製は追従漏れの温床になる） |
| `PostEffectDataLerp` | 追加しない | 現行の SceneEditor は `LerpScratch` で補間しており、`PostEffectDataLerp` はどこからも呼ばれていない |
| ホスト API が無い場合 | `PostEffectsClient` で**任意メソッド**として解決する（`ShowTimelineMode` と同じ扱い）。旧版 PostEffects でも他 6 系統は動き続ける | ブルームは必須メソッドにしたため、旧版 PostEffects だと全ポストエフェクトが止まった（ブルーム計画の「レビュー却下メモ」）。同じ轍を踏まない |
| 項目順 | ボーン一覧・PostEffects のタイムラインタブとも、ブルームの後（末尾） | 既存項目の位置を動かさない |

## Global Constraints

- コメント・ログメッセージは日本語で書く。
- プラグイン間連携はリフレクション経由（`PostEffectsClient`）のみ。SceneEditor から `COM3D25.PostEffects.Plugin` をコンパイル時参照してはいけない。
- **`TimelineBridge` のメソッド名・引数の個数と型は文字列契約**。`GetCinematicDepthOfField()` / `ApplyCinematicDepthOfField(PEData.CinematicDepthOfFieldData)` の綴りを MTEUtils 側と一致させる。
- **共有 DTO のフィールド名は実体型 `CinematicDepthOfFieldSetting` と 1:1 で一致させる**。ずれた値は例外を出さずに既定値のまま素通りし、`WarnUnmappedFields` のログでしか気付けない。
- `TransformType` は XML へ enum の**名前**でシリアライズされるため、途中へ挿入しても既存 XML は壊れない。アルファベット順を保ち、`CameraShake` と `DepthOfField` の間へ置く。
- `debug.bat` は使わない（ゲーム停止中に実機へ DLL がコピーされるため）。MSBuild を直接叩き、**COM3D2 版と COM3D25 版の両方**をビルドする。
  - MSBuild: `"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"`
  - PostEffects: `source/COM3D25.PostEffects.Plugin/COM3D25.PostEffects.Plugin.csproj`
  - SceneEditor: `source/COM3D2.SceneEditor.Plugin/COM3D2.SceneEditor.Plugin.csproj`
  - 共通引数: `/p:Configuration=Debug /p:GameVersion=<COM3D2|COM3D25> "/p:COM3D2_DIR=W:\COM3D2" "/p:COM3D25_DIR=W:\COM3D2_5"`
- `deploy.bat` / `deploy.ps1` は実行しない。MTEUtils の `git push` もしない（公開はユーザーの操作に限る）。
- git worktree は使わない。

## リポジトリと MTEUtils の受け渡し

3 リポジトリにまたがる。MTEUtils は各リポジトリに別クローンのサブモジュールとしてある。

| クローン | 現在の HEAD |
|---|---|
| `COM3D2.SceneEditor.Plugin/source/COM3D2.SceneEditor.Plugin/MTEUtils` | `f235400`（最新。origin 未 push を含む） |
| `COM3D2.PostEffects.Plugin/source/COM3D25.PostEffects.Plugin/MTEUtils` | `05c2976`（`f235400` の祖先） |

MTEUtils の変更は**最新の SceneEditor 側クローン**でコミットし、PostEffects 側クローンへはローカルパスから fetch して同じコミットを checkout する（push は不要）。このとき PostEffects 側には `2da6959`（スライダー行のラベルを常時ドラッグ可能にし `< >` ボタンを廃止）と `f235400`（`InspectorHostClient.RegisterRows`）も入る。どちらも MTEUtils master の既存コミットで、いずれ取り込むものなので許容する。ただし Task 2 のビルドで PostEffects の UI コードが壊れないことを確認する。

## ファイル構成

| リポジトリ | ファイル | 責務 | 種別 |
|---|---|---|---|
| MTEUtils | `PostEffectData.cs` | 共有 DTO `CinematicDepthOfFieldData` | 変更 |
| MTEUtils | `PostEffectsClient.cs` | 任意メソッドの解決と Get/Apply | 変更 |
| PostEffects | `source/COM3D25.PostEffects.Plugin/MTEUtils` | サブモジュール参照 | 更新 |
| PostEffects | `source/COM3D25.PostEffects.Plugin/TimelineBridge.cs` | 実体 ⇔ DTO の変換、列挙値の丸め | 変更 |
| PostEffects | `source/COM3D25.PostEffects.Plugin/MainWindow.cs` | タイムラインタブへの追加（履歴・自動編集モードもここで効く） | 変更 |
| SceneEditor | `source/COM3D2.SceneEditor.Plugin/MTEUtils` | サブモジュール参照 | 更新 |
| SceneEditor | `Timeline/PostEffectUtils.cs` | `PostEffectType.CinematicDepthOfField` と和名 | 変更 |
| SceneEditor | `Timeline/TransformData/ITransformData.cs` | `TransformType.CinematicDepthOfField` | 変更 |
| SceneEditor | `Timeline/Manager/PostEffectManager.cs` | クライアントへの委譲、全無効化 | 変更 |
| SceneEditor | `Timeline/TransformData/TransformDataCinematicDepthOfField.cs` | キーフレーム値と DTO 変換 | 新規 |
| SceneEditor | `Timeline/TimelineIntegration.cs` | 生成関数の登録 | 変更 |
| SceneEditor | `Timeline/TimelineLayer/PostEffectTimelineLayer_CinematicDepthOfField.cs` | 区間補間して適用 | 新規 |
| SceneEditor | `Timeline/TimelineLayer/PostEffectTimelineLayer.cs` | ボーン一覧・再生・キー記録 | 変更 |
| SceneEditor | `PostEffectRowDrawer.cs` | 編集 UI | 変更 |
| SceneEditor | `Timeline/ItemInspector/PostEffectItemInspector.cs` | メニュー項目 → UI の振り分け | 変更 |
| SceneEditor | `COM3D2.SceneEditor.Plugin.csproj` | 新規 2 ファイルの登録（旧形式 csproj） | 変更 |
| SceneEditor | `source/COM3D2.SceneEditor.Plugin.Tests/TransformDataCinematicDepthOfFieldTests.cs` | DTO 往復・メイド変換・補間の固定 | 新規 |

以下、SceneEditor のパスは `W:/COM3D2_5/work/COM3D2.SceneEditor.Plugin/source/COM3D2.SceneEditor.Plugin/` からの相対で書く。

## Review Focus

1. **旧版 PostEffects との組み合わせ**: `GetCinematicDepthOfField` を持たない PostEffects でも、SceneEditor の他 6 系統は従来どおり動く。シネマティック被写界深度の Get は既定値を返し、Apply は何もせず、Inspector は「PostEffects.Plugin が古い」と表示する → Task 1 の `isCinematicDepthOfFieldAvailable` と Task 6 Step 3 のガードで担保する。実機検証 7 で確認する
2. **タイムライン XML から定義域外の列挙値が来る**（手編集・将来版）: 実体の enum が未定義値にならない → Task 2 の `ClampEnumValue` で担保する
3. **追従メイドの区間補間**: -1（追従なし）→ 1 の区間の途中で 0 番メイドへ追従しない → Task 4 のテスト「補間で列挙値と追従メイドは区間開始値のまま」で担保する
4. **追従なしのときの `maidIndex`**: 実体に `maidFocus=false` + 任意の `maidIndex` が入っていても、SceneEditor へ取り込むと -1 になり、戻すと `maidIndex=0` になる（被写界深度と同じ規約）→ Task 4 のテストで担保する
5. **キーを打たない項目の保持**: `visualizeFocus` と `bokehTexturePath` は、タイムラインの再生中も PostEffects 側 UI の値のまま残る → `ApplyCinematicDepthOfField` が既存インスタンスへ上書きコピーすることで担保する（Task 2）。実機検証 5 で確認する

---

### Task 1: MTEUtils に共有 DTO とクライアント API を追加する

**Files:**
- Modify: `W:/COM3D2_5/work/COM3D2.SceneEditor.Plugin/source/COM3D2.SceneEditor.Plugin/MTEUtils/PostEffectData.cs`（`BloomData` の後、`PostEffectDataLerp` の前）
- Modify: `.../MTEUtils/PostEffectsClient.cs`

**Interfaces:**
- Produces:
  - `COM3D2.MotionTimelineEditor.PostEffects.CinematicDepthOfFieldData`（フィールドは下記）
  - `PostEffectsClient.isCinematicDepthOfFieldAvailable : bool`
  - `PostEffectsClient.GetCinematicDepthOfField() : PEData.CinematicDepthOfFieldData`
  - `PostEffectsClient.ApplyCinematicDepthOfField(PEData.CinematicDepthOfFieldData)`
  - ホストへ要求する契約: `TimelineBridge.GetCinematicDepthOfField()` / `TimelineBridge.ApplyCinematicDepthOfField(<DTO>)`（public static）

- [ ] **Step 1: 共有 DTO を追加する**

`PostEffectData.cs` の `BloomData` クラスの閉じ括弧の後へ追記する。既定値は実体 `CinematicDepthOfFieldSetting` と同じにする。

```csharp
    /// <summary>
    /// シネマティック被写界深度 (Cinematic Image Effects の DepthOfField を移植したもの)。
    /// 実体の enum 3 種は PostEffects 側の型なのでここでは持てず、int で受け渡す
    /// (値の対応は TimelineBridge が変換する)。
    /// ピント位置の可視化 (デバッグ表示) とボケテクスチャのパス (文字列) は持たない
    /// (触らない = PostEffects 側 UI の管轄)
    /// </summary>
    public class CinematicDepthOfFieldData
    {
        public bool enabled = false;
        // CinematicDepthOfFieldEffect.TweakMode: 0=Range (ピント面と範囲), 1=Explicit (近景・遠景を個別)
        public int tweakMode = 1;
        // CinematicDepthOfFieldEffect.QualityPreset: 0=Low, 1=Medium, 2=High
        public int filteringQuality = 2;
        // CinematicDepthOfFieldEffect.ApertureShape: 0=Circular, 1=Hexagonal, 2=Octogonal
        public int apertureShape = 0;
        public float apertureOrientation = 0f;

        public float focusFocusPlane = 20f;
        public float focusRange = 35f;
        public float focusNearPlane = 3f;
        public float focusNearFalloff = 3f;
        public float focusFarPlane = 6f;
        public float focusFarFalloff = 6f;
        public float focusNearBlurRadius = 18f;
        public float focusFarBlurRadius = 20f;

        public bool antiFlicker = false;
        public bool useBokehTexture = false;
        public float bokehScale = 1f;
        public float bokehIntensity = 50f;
        public float bokehThreshold = 2f;
        public float bokehSpawnHeuristic = 0.15f;

        // メイドの頭にピントを合わせる (ピント面と範囲モードのみ有効)
        public bool maidFocus = false;
        // 準備完了メイド一覧の中のインデックス
        public int maidIndex = 0;
    }
```

- [ ] **Step 2: クライアントにフィールドを追加する**

`PostEffectsClient.cs` の `_showTimelineMode` 宣言の直後へ追記する。

```csharp
        // シネマティック被写界深度も任意メソッド。旧版ホストで欠けていても他の系統は止めない
        private static Func<object> _getCinematicDepthOfField;
        private static MethodInfo _applyCinematicDepthOfField;
```

`_bloomArg` 宣言の直後へ追記する。

```csharp
        private static object _cinematicDepthOfFieldArg;
```

- [ ] **Step 3: `Initialize` で任意メソッドとして解決する**

`_showTimelineMode = CreateAction(type, "ShowTimelineMode");` の直後へ追記する（必須判定の `if` には**含めない**）。

```csharp
                _getCinematicDepthOfField = CreateFuncObject(type, "GetCinematicDepthOfField");
                _applyCinematicDepthOfField = type.GetMethod(
                    "ApplyCinematicDepthOfField", BindingFlags.Public | BindingFlags.Static);
```

`WarnUnmappedFields("ブルーム", ...)` の直後（`try` ブロックの末尾）へ追記する。

```csharp
                // 任意メソッドなので、揃っているときだけ引数を用意する
                if (_getCinematicDepthOfField != null && _applyCinematicDepthOfField != null)
                {
                    _cinematicDepthOfFieldArg = Activator.CreateInstance(
                        _applyCinematicDepthOfField.GetParameters()[0].ParameterType);
                    WarnUnmappedFields(
                        "シネマティック被写界深度",
                        typeof(PEData.CinematicDepthOfFieldData),
                        _cinematicDepthOfFieldArg);
                }
                else
                {
                    MTEUtils.LogDebug(
                        "PostEffectsClient: TimelineBridge にシネマティック被写界深度の API がありません (旧版の PostEffects.Plugin)");
                }
```

旧版との組み合わせは想定内なので、警告ではなく `LogDebug`（`MTEUtils.cs:97` に存在）にする。

- [ ] **Step 4: 公開プロパティと Get/Apply を追加する**

`isAvailable` プロパティの直後へ追記する。

```csharp
        /// <summary>
        /// シネマティック被写界深度の API がホストにあるか。
        /// 旧版の PostEffects.Plugin では false になるが、他の系統は isAvailable のまま動く
        /// </summary>
        public static bool isCinematicDepthOfFieldAvailable =>
            isAvailable && _cinematicDepthOfFieldArg != null;
```

ファイル末尾の `ApplyBloom` の後（クラスの閉じ括弧の前）へ追記する。

```csharp
        public static PEData.CinematicDepthOfFieldData GetCinematicDepthOfField()
        {
            var dto = new PEData.CinematicDepthOfFieldData();
            if (!isCinematicDepthOfFieldAvailable)
            {
                return dto;
            }
            try
            {
                ReflectionFieldCopier.Copy(_getCinematicDepthOfField(), dto);
            }
            catch (Exception e)
            {
                LogHostError("GetCinematicDepthOfField", e);
            }
            return dto;
        }

        public static void ApplyCinematicDepthOfField(PEData.CinematicDepthOfFieldData data)
        {
            if (!isCinematicDepthOfFieldAvailable)
            {
                return;
            }
            try
            {
                ReflectionFieldCopier.Copy(data, _cinematicDepthOfFieldArg);
                _args1[0] = _cinematicDepthOfFieldArg;
                _applyCinematicDepthOfField.Invoke(null, _args1);
            }
            catch (Exception e)
            {
                LogHostError("ApplyCinematicDepthOfField", e);
            }
        }
```

- [ ] **Step 5: SceneEditor（COM3D25 構成）でビルドが通ることを確認する**

MTEUtils 単体のビルドは無いので、取り込み先でビルドする。

```bash
cd W:/COM3D2_5/work/COM3D2.SceneEditor.Plugin/source/COM3D2.SceneEditor.Plugin
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" COM3D2.SceneEditor.Plugin.csproj /p:Configuration=Debug /p:GameVersion=COM3D25 "/p:COM3D2_DIR=W:\COM3D2" "/p:COM3D25_DIR=W:\COM3D2_5"
```
Expected: `Build succeeded.` / `0 Error(s)`

- [ ] **Step 6: MTEUtils でコミットする**

```bash
cd W:/COM3D2_5/work/COM3D2.SceneEditor.Plugin/source/COM3D2.SceneEditor.Plugin/MTEUtils
git add PostEffectData.cs PostEffectsClient.cs
git commit -m "feat(mteutils): 共有ポストエフェクト DTO にシネマティック被写界深度を追加する"
git rev-parse HEAD
```
出力されたハッシュを以降 `<MTE_SHA>` と呼ぶ。

---

### Task 2: PostEffects の TimelineBridge とタイムラインタブ

**Files:**
- Modify: `W:/COM3D2_5/work/COM3D2.PostEffects.Plugin/source/COM3D25.PostEffects.Plugin/MTEUtils`（サブモジュール参照）
- Modify: `.../COM3D25.PostEffects.Plugin/TimelineBridge.cs`
- Modify: `.../COM3D25.PostEffects.Plugin/MainWindow.cs:50,375-384`

**Interfaces:**
- Consumes: `PEData.CinematicDepthOfFieldData`（Task 1）、`CinematicDepthOfFieldEffect.TweakMode / QualityPreset / ApertureShape`、既存の `GetMaxEnumValue(Type)` / `ClampEnumValue(int, int)`
- Produces: `TimelineBridge.GetCinematicDepthOfField() : PEData.CinematicDepthOfFieldData`、`TimelineBridge.ApplyCinematicDepthOfField(PEData.CinematicDepthOfFieldData)`

- [ ] **Step 1: PostEffects 側の MTEUtils を `<MTE_SHA>` へ進める**

```bash
cd W:/COM3D2_5/work/COM3D2.PostEffects.Plugin/source/COM3D25.PostEffects.Plugin/MTEUtils
git status --short   # 空であること
git fetch W:/COM3D2_5/work/COM3D2.SceneEditor.Plugin/source/COM3D2.SceneEditor.Plugin/MTEUtils master
git checkout master
git merge --ff-only <MTE_SHA>
git log --oneline -1   # <MTE_SHA> であること
```

- [ ] **Step 2: `TimelineBridge` に using を足す**

ファイル先頭の `#endif` の直後へ追記する。

```csharp
using CinematicDof = COM3D25.PostEffects.Plugin.CinematicDepthOfFieldEffect;
```

- [ ] **Step 3: Get/Apply と変換を追加する**

`ApplyBloom` の後、`CopyBloomSpecialFieldsToDto` の前へ追記する。

```csharp
        public static PEData.CinematicDepthOfFieldData GetCinematicDepthOfField()
        {
            var setting = settings.cinematicDepthOfField;
            var dto = new PEData.CinematicDepthOfFieldData();
            ReflectionFieldCopier.Copy(setting, dto);
            // enum は int と代入互換が無く ReflectionFieldCopier で写らない
            dto.tweakMode = (int)setting.tweakMode;
            dto.filteringQuality = (int)setting.filteringQuality;
            dto.apertureShape = (int)setting.apertureShape;
            return dto;
        }

        public static void ApplyCinematicDepthOfField(PEData.CinematicDepthOfFieldData data)
        {
            // 実体は差し替えず、既存インスタンスへ写す。DTO に無い項目
            // (ピント位置の可視化・ボケテクスチャのパス) は PostEffects 側 UI の値のまま残る
            var setting = settings.cinematicDepthOfField;
            ReflectionFieldCopier.Copy(data, setting);
            // タイムライン XML の手編集やバージョン差で定義域外の値が来ても
            // 未定義の enum 値にならないよう丸める
            setting.tweakMode = (CinematicDof.TweakMode)ClampEnumValue(data.tweakMode, _maxTweakMode);
            setting.filteringQuality = (CinematicDof.QualityPreset)ClampEnumValue(
                data.filteringQuality, _maxQualityPreset);
            setting.apertureShape = (CinematicDof.ApertureShape)ClampEnumValue(
                data.apertureShape, _maxApertureShape);
            settings.dirty = true;
        }
```

`_maxLensFlareMode` の宣言の直後へ追記する。

```csharp
        private static readonly int _maxTweakMode = GetMaxEnumValue(typeof(CinematicDof.TweakMode));
        private static readonly int _maxQualityPreset = GetMaxEnumValue(typeof(CinematicDof.QualityPreset));
        private static readonly int _maxApertureShape = GetMaxEnumValue(typeof(CinematicDof.ApertureShape));
```

`GetMaxEnumValue` の summary を「ブルームとシネマティック被写界深度の enum はどれもこの形」に書き換える。3 つの enum が 0 始まりの連番であることを `Effects/CinematicDepthOfFieldEffect.cs` の定義（TweakMode: Range, Explicit / ApertureShape: Circular, Hexagonal, Octogonal / QualityPreset: Low, Medium, High。いずれも明示値なし）で確認する。

- [ ] **Step 4: クラスの summary を 7 系統へ直す**

```csharp
    /// 対象はタイムライン対応 7 系統
    /// (DoF / GTToneMap / パラフィン / 距離フォグ / リムライト / ブルーム / シネマティック DoF) のみ
```

- [ ] **Step 5: タイムラインタブへ追加する**

`MainWindow.cs` の `EnsureTimelineControllers` で、`BloomController` の行の直後へ追記する。

```csharp
            _timelineControllers.Add(manager.GetController<CinematicDepthOfFieldController>());
```

`_timelineControllers` 宣言のコメントを「タイムライン対応 7 系統」に直す。`grep -n "6 系統" source/COM3D25.PostEffects.Plugin/*.cs` で他に残っていれば同様に直す。

- [ ] **Step 6: 両構成をビルドする**

```bash
cd W:/COM3D2_5/work/COM3D2.PostEffects.Plugin/source/COM3D25.PostEffects.Plugin
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" COM3D25.PostEffects.Plugin.csproj /p:Configuration=Debug /p:GameVersion=COM3D2  "/p:COM3D2_DIR=W:\COM3D2" "/p:COM3D25_DIR=W:\COM3D2_5"
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" COM3D25.PostEffects.Plugin.csproj /p:Configuration=Debug /p:GameVersion=COM3D25 "/p:COM3D2_DIR=W:\COM3D2" "/p:COM3D25_DIR=W:\COM3D2_5"
```
Expected: 両方 `Build succeeded.` / `0 Error(s)`。MTEUtils の `2da6959` / `f235400` 由来のエラーが出たら、そこで止めてユーザーに報告する（本計画の範囲外の追従が要るため）。

- [ ] **Step 7: コミットする**

```bash
cd W:/COM3D2_5/work/COM3D2.PostEffects.Plugin
git add source/COM3D25.PostEffects.Plugin/MTEUtils \
        source/COM3D25.PostEffects.Plugin/TimelineBridge.cs \
        source/COM3D25.PostEffects.Plugin/MainWindow.cs
git commit -m "feat(timeline): シネマティック被写界深度をタイムライン制御の対象に追加する"
```

---

### Task 3: SceneEditor の型追加と実体への委譲

**Files:**
- Modify: `MTEUtils`（サブモジュール参照。作業ツリーは Task 1 で `<MTE_SHA>` 済み）
- Modify: `Timeline/PostEffectUtils.cs:6-28`
- Modify: `Timeline/TransformData/ITransformData.cs`（`TransformType` enum）
- Modify: `Timeline/Manager/PostEffectManager.cs:195-212,300-311`

**Interfaces:**
- Consumes: `PostEffectsClient.GetCinematicDepthOfField / ApplyCinematicDepthOfField / isCinematicDepthOfFieldAvailable`（Task 1）
- Produces:
  - `PostEffectType.CinematicDepthOfField`（名前 `"CinematicDepthOfField"`、和名 `"シネマティックDoF"`）
  - `TransformType.CinematicDepthOfField`
  - `PostEffectManager.GetCinematicDepthOfFieldData() : PEData.CinematicDepthOfFieldData`
  - `PostEffectManager.ApplyCinematicDepthOfField(PEData.CinematicDepthOfFieldData)`
  - `PostEffectManager.isCinematicDepthOfFieldAvailable : bool`

- [ ] **Step 1: `PostEffectType` に足す**

`Bloom,` の後へ `CinematicDepthOfField,` を、和名マップの `{ PostEffectType.Bloom, "ブルーム" },` の後へ次を足す。和名はタイムラインのボーン名欄に収まるよう短くする。

```csharp
            { PostEffectType.CinematicDepthOfField, "シネマティックDoF" },
```

- [ ] **Step 2: `TransformType` に足す**

`Timeline/TransformData/ITransformData.cs` の enum で、アルファベット順を保って `CameraShake` と `DepthOfField` の間へ挿入する。名前でシリアライズされるので、位置は XML 互換に影響しない。

```csharp
        Camera,
        CameraShake,
        CinematicDepthOfField,
        DepthOfField,
```

- [ ] **Step 3: （欠番。Step 2 に統合済み）**

- [ ] **Step 4: `PostEffectManager` へ委譲を足す**

`ApplyBloom` の後（クラス末尾）へ追記する。

```csharp
        /// <summary>旧版の PostEffects.Plugin では false (Get は既定値、Apply は何もしない)</summary>
        public bool isCinematicDepthOfFieldAvailable => PostEffectsClient.isCinematicDepthOfFieldAvailable;

        // シネマティック被写界深度も共有 DTO をそのまま流す。
        // メイド追従のスロット番号との変換は TransformDataCinematicDepthOfField が持つ
        public PEData.CinematicDepthOfFieldData GetCinematicDepthOfFieldData()
        {
            return PostEffectsClient.GetCinematicDepthOfField();
        }

        public void ApplyCinematicDepthOfField(PEData.CinematicDepthOfFieldData data)
        {
            PostEffectsClient.ApplyCinematicDepthOfField(data);
        }
```

- [ ] **Step 5: `DisableAllEffects` の末尾（bloom の後）へ追記する**

```csharp
            var cinematicDof = PostEffectsClient.GetCinematicDepthOfField();
            cinematicDof.enabled = false;
            PostEffectsClient.ApplyCinematicDepthOfField(cinematicDof);
```

旧版ホストでは Get/Apply とも何もしないため、ガードは要らない。

- [ ] **Step 6: 両構成をビルドする**

```bash
cd W:/COM3D2_5/work/COM3D2.SceneEditor.Plugin/source/COM3D2.SceneEditor.Plugin
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" COM3D2.SceneEditor.Plugin.csproj /p:Configuration=Debug /p:GameVersion=COM3D2  "/p:COM3D2_DIR=W:\COM3D2" "/p:COM3D25_DIR=W:\COM3D2_5"
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" COM3D2.SceneEditor.Plugin.csproj /p:Configuration=Debug /p:GameVersion=COM3D25 "/p:COM3D2_DIR=W:\COM3D2" "/p:COM3D25_DIR=W:\COM3D2_5"
```
Expected: 両方 `Build succeeded.` / `0 Error(s)`。`switch` の網羅漏れ警告が出ても、Task 5 で埋めるのでここでは可。

- [ ] **Step 7: コミットする**

```bash
cd W:/COM3D2_5/work/COM3D2.SceneEditor.Plugin
git add source/COM3D2.SceneEditor.Plugin/MTEUtils \
        source/COM3D2.SceneEditor.Plugin/Timeline/PostEffectUtils.cs \
        source/COM3D2.SceneEditor.Plugin/Timeline/TransformData/ITransformData.cs \
        source/COM3D2.SceneEditor.Plugin/Timeline/Manager/PostEffectManager.cs
git commit -m "feat(posteffect): シネマティック被写界深度を実体へ読み書きする経路を追加する"
```

---

### Task 4: `TransformDataCinematicDepthOfField`（テスト先行）

**Files:**
- Create: `source/COM3D2.SceneEditor.Plugin.Tests/TransformDataCinematicDepthOfFieldTests.cs`
- Create: `Timeline/TransformData/TransformDataCinematicDepthOfField.cs`
- Modify: `Timeline/TimelineIntegration.cs`（`RegisterTransform` の並び）
- Modify: `COM3D2.SceneEditor.Plugin.csproj`（`TransformDataBloom.cs` の行の後）

**Interfaces:**
- Consumes: `TransformType.CinematicDepthOfField`（Task 3）、`PEP.CinematicDepthOfFieldData`（Task 1）
- Produces:
  - `TransformDataCinematicDepthOfField.defaultTrans`
  - `TransformDataCinematicDepthOfField.cinematicDepthOfField { get; set; } : PEP.CinematicDepthOfFieldData`
  - 値プロパティ: `tweakMode` `filteringQuality` `apertureShape`（int）、`apertureOrientation` `focusFocusPlane` `focusRange` `focusNearPlane` `focusNearFalloff` `focusFarPlane` `focusFarFalloff` `focusNearBlurRadius` `focusFarBlurRadius` `bokehScale` `bokehIntensity` `bokehThreshold` `bokehSpawnHeuristic`（float）、`antiFlicker` `useBokehTexture`（bool）、`maidSlotNo`（int、-1 = 追従なし）
  - Info アクセサ: 上記の各名前 + `Info`（例: `focusRangeInfo`）
  - `const int TweakModeRange = 0` / `const int TweakModeExplicit = 1`

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using COM3D2.MotionTimelineEditor.Plugin;
using Xunit;
using PEP = COM3D2.MotionTimelineEditor.PostEffects;

namespace COM3D2.SceneEditor.Plugin.Tests
{
    /// <summary>
    /// シネマティック被写界深度のキーフレーム値と共有 DTO の変換、および区間補間を固定する
    /// </summary>
    public class TransformDataCinematicDepthOfFieldTests
    {
        private static TransformDataCinematicDepthOfField Create()
        {
            var trans = new TransformDataCinematicDepthOfField();
            trans.Initialize("CinematicDepthOfField");
            return trans;
        }

        private static TransformDataCinematicDepthOfField Lerp(
            TransformDataCinematicDepthOfField start, TransformDataCinematicDepthOfField end, float t)
        {
            var scratch = (TransformDataCinematicDepthOfField)start.Clone();
            scratch.LerpFrom(start, end, 0f, 1f, t);
            return scratch;
        }

        [Fact]
        public void 初期値は共有DTOの既定値と一致する()
        {
            var expected = new PEP.CinematicDepthOfFieldData();
            var actual = Create().cinematicDepthOfField;

            Assert.Equal(expected.tweakMode, actual.tweakMode);
            Assert.Equal(expected.filteringQuality, actual.filteringQuality);
            Assert.Equal(expected.apertureShape, actual.apertureShape);
            Assert.Equal(expected.apertureOrientation, actual.apertureOrientation, 4);
            Assert.Equal(expected.focusFocusPlane, actual.focusFocusPlane, 4);
            Assert.Equal(expected.focusRange, actual.focusRange, 4);
            Assert.Equal(expected.focusNearPlane, actual.focusNearPlane, 4);
            Assert.Equal(expected.focusNearFalloff, actual.focusNearFalloff, 4);
            Assert.Equal(expected.focusFarPlane, actual.focusFarPlane, 4);
            Assert.Equal(expected.focusFarFalloff, actual.focusFarFalloff, 4);
            Assert.Equal(expected.focusNearBlurRadius, actual.focusNearBlurRadius, 4);
            Assert.Equal(expected.focusFarBlurRadius, actual.focusFarBlurRadius, 4);
            Assert.Equal(expected.antiFlicker, actual.antiFlicker);
            Assert.Equal(expected.useBokehTexture, actual.useBokehTexture);
            Assert.Equal(expected.bokehScale, actual.bokehScale, 4);
            Assert.Equal(expected.bokehIntensity, actual.bokehIntensity, 4);
            Assert.Equal(expected.bokehThreshold, actual.bokehThreshold, 4);
            Assert.Equal(expected.bokehSpawnHeuristic, actual.bokehSpawnHeuristic, 4);
            Assert.Equal(expected.maidFocus, actual.maidFocus);
            Assert.Equal(expected.maidIndex, actual.maidIndex);
        }

        [Fact]
        public void 共有DTOとの往復で値が保たれる()
        {
            var source = new PEP.CinematicDepthOfFieldData
            {
                enabled = true,
                tweakMode = 0,
                filteringQuality = 1,
                apertureShape = 2,
                apertureOrientation = 45f,
                focusFocusPlane = 12f,
                focusRange = 7f,
                focusNearPlane = 1.5f,
                focusNearFalloff = 2.5f,
                focusFarPlane = 9f,
                focusFarFalloff = 11f,
                focusNearBlurRadius = 30f,
                focusFarBlurRadius = 40f,
                antiFlicker = true,
                useBokehTexture = true,
                bokehScale = 3f,
                bokehIntensity = 120f,
                bokehThreshold = 1.2f,
                bokehSpawnHeuristic = 0.5f,
                maidFocus = true,
                maidIndex = 2,
            };

            var trans = Create();
            trans.cinematicDepthOfField = source;
            var actual = trans.cinematicDepthOfField;

            Assert.True(actual.enabled);
            Assert.Equal(0, actual.tweakMode);
            Assert.Equal(1, actual.filteringQuality);
            Assert.Equal(2, actual.apertureShape);
            Assert.Equal(45f, actual.apertureOrientation, 4);
            Assert.Equal(12f, actual.focusFocusPlane, 4);
            Assert.Equal(7f, actual.focusRange, 4);
            Assert.Equal(1.5f, actual.focusNearPlane, 4);
            Assert.Equal(2.5f, actual.focusNearFalloff, 4);
            Assert.Equal(9f, actual.focusFarPlane, 4);
            Assert.Equal(11f, actual.focusFarFalloff, 4);
            Assert.Equal(30f, actual.focusNearBlurRadius, 4);
            Assert.Equal(40f, actual.focusFarBlurRadius, 4);
            Assert.True(actual.antiFlicker);
            Assert.True(actual.useBokehTexture);
            Assert.Equal(3f, actual.bokehScale, 4);
            Assert.Equal(120f, actual.bokehIntensity, 4);
            Assert.Equal(1.2f, actual.bokehThreshold, 4);
            Assert.Equal(0.5f, actual.bokehSpawnHeuristic, 4);
            Assert.True(actual.maidFocus);
            Assert.Equal(2, actual.maidIndex);
        }

        [Fact]
        public void 追従なしはスロット番号マイナス1になりmaidIndexは0へ戻る()
        {
            var trans = Create();
            trans.cinematicDepthOfField = new PEP.CinematicDepthOfFieldData
            {
                maidFocus = false,
                maidIndex = 3,
            };

            Assert.Equal(-1, trans.maidSlotNo);

            var actual = trans.cinematicDepthOfField;
            Assert.False(actual.maidFocus);
            Assert.Equal(0, actual.maidIndex);
        }

        [Fact]
        public void 補間で列挙値と追従メイドは区間開始値のまま()
        {
            var start = Create();
            var end = Create();
            start.tweakMode = TransformDataCinematicDepthOfField.TweakModeRange;
            end.tweakMode = TransformDataCinematicDepthOfField.TweakModeExplicit;
            start.filteringQuality = 0;
            end.filteringQuality = 2;
            start.apertureShape = 0;
            end.apertureShape = 2;
            start.antiFlicker = false;
            end.antiFlicker = true;
            start.maidSlotNo = -1;
            end.maidSlotNo = 1;
            start.focusRange = 10f;
            end.focusRange = 30f;
            start.apertureOrientation = 0f;
            end.apertureOrientation = 90f;

            var mid = Lerp(start, end, 0.5f);

            Assert.Equal(TransformDataCinematicDepthOfField.TweakModeRange, mid.tweakMode);
            Assert.Equal(0, mid.filteringQuality);
            Assert.Equal(0, mid.apertureShape);
            Assert.False(mid.antiFlicker);
            Assert.Equal(-1, mid.maidSlotNo);
            // 連続値は補間される (両端の間に入る)。
            // 絞りの向きは step が 1 だと Int 扱いで補間されなくなるため、ここで固定する
            Assert.InRange(mid.focusRange, 10.01f, 29.99f);
            Assert.InRange(mid.apertureOrientation, 0.01f, 89.99f);
        }
    }
}
```

- [ ] **Step 2: テストが失敗する（コンパイルエラーになる）ことを確認する**

テストは COM3D25 構成のプラグイン出力を参照するので、先にプラグインをビルドしてから実行する。

```bash
cd W:/COM3D2_5/work/COM3D2.SceneEditor.Plugin
dotnet test source/COM3D2.SceneEditor.Plugin.Tests --filter "FullyQualifiedName~TransformDataCinematicDepthOfFieldTests"
```
Expected: `TransformDataCinematicDepthOfField` が見つからずビルド失敗。

- [ ] **Step 3: `TransformDataCinematicDepthOfField.cs` を作る**

値の並びは `Index` enum が唯一の正で、`valueCount` は 21。`TransformDataBase.Reset()` は `GetCustomValueInfoMap()` の既定値を書き戻すため、`Easing` / `Visible` 以外はすべて登録する。bool は min0/max1/step1 のトグルにし（`Toggle` ヘルパ）、int は step1 にする。これで `LerpScratch` が区間開始値を保つ。**逆に `step == 1f` は `CustomValueInfo.type` で Int と判定されて補間されなくなるため、連続値には 1 未満の step を使う**（PostEffects 側 UI で step 1 の「絞りの向き」も 0.1 にしてある）。`maidSlotNo` は `uiType = MaidSlot` で補間から外れる（`cddb5bc`）。

```csharp
using System.Collections.Generic;
using PEP = COM3D2.MotionTimelineEditor.PostEffects;

namespace COM3D2.MotionTimelineEditor.Plugin
{
    /// <summary>
    /// シネマティック被写界深度のキーフレーム値。
    /// メイド追従は被写界深度と同じくスロット番号 1 値 (-1 = 追従なし) で持ち、
    /// 共有 DTO の maidFocus + maidIndex とはアクセサで相互変換する
    /// </summary>
    public class TransformDataCinematicDepthOfField : TransformDataBase
    {
        public enum Index
        {
            Easing = 0,
            Visible = 1,

            TweakMode = 2,
            FilteringQuality = 3,
            ApertureShape = 4,
            ApertureOrientation = 5,

            FocusFocusPlane = 6,
            FocusRange = 7,
            FocusNearPlane = 8,
            FocusNearFalloff = 9,
            FocusFarPlane = 10,
            FocusFarFalloff = 11,
            FocusNearBlurRadius = 12,
            FocusFarBlurRadius = 13,

            AntiFlicker = 14,
            UseBokehTexture = 15,
            BokehScale = 16,
            BokehIntensity = 17,
            BokehThreshold = 18,
            BokehSpawnHeuristic = 19,

            MaidSlotNo = 20,
        }

        // CinematicDepthOfFieldEffect.TweakMode の値
        public const int TweakModeRange = 0;
        public const int TweakModeExplicit = 1;

        public static TransformDataCinematicDepthOfField defaultTrans = new TransformDataCinematicDepthOfField();

        public override TransformType type => TransformType.CinematicDepthOfField;

        public override int valueCount => 21;

        public override bool hasVisible => true;
        // Tangent 統一により easing 補間は廃止 (easingValue は XML 互換のためだけに残す)
        public override bool hasTangent => true;
        public override ValueData[] tangentValues => values;

        public override ValueData visibleValue => values[(int)Index.Visible];
        public override ValueData easingValue => values[(int)Index.Easing];

        public TransformDataCinematicDepthOfField()
        {
        }

        private static CustomValueInfo Toggle(Index index, string name, float defaultValue)
        {
            // bool は 0/1 の 2 値スライダーとして持つ (ブルームと同じ作法)
            return new CustomValueInfo
            {
                index = (int)index,
                name = name,
                min = 0f,
                max = 1f,
                step = 1f,
                defaultValue = defaultValue,
            };
        }

        private static CustomValueInfo Value(Index index, string name, float max, float step, float defaultValue)
        {
            return new CustomValueInfo
            {
                index = (int)index,
                name = name,
                min = 0f,
                max = max,
                step = step,
                defaultValue = defaultValue,
            };
        }

        // 範囲と既定値は PostEffects 側 UI (CinematicDepthOfFieldController.DrawContent) に合わせる
        private readonly static Dictionary<string, CustomValueInfo> CustomValueInfoMap = new Dictionary<string, CustomValueInfo>
        {
            // 0=ピント面と範囲 / 1=近景・遠景を個別。2 値なのでトグルとして扱う
            { "tweakMode", Toggle(Index.TweakMode, "近景遠景個別", TweakModeExplicit) },
            // 0=Low / 1=Medium / 2=High
            { "filteringQuality", Value(Index.FilteringQuality, "品質", 2f, 1f, 2f) },
            // 0=円形 / 1=六角形 / 2=八角形
            { "apertureShape", Value(Index.ApertureShape, "絞りの形", 2f, 1f, 0f) },
            { "apertureOrientation", Value(Index.ApertureOrientation, "絞りの向き", 180f, 0.1f, 0f) },

            { "focusFocusPlane", Value(Index.FocusFocusPlane, "ﾋﾟﾝﾄ面", 60f, 0.1f, 20f) },
            { "focusRange", Value(Index.FocusRange, "ﾋﾟﾝﾄ範囲", 50f, 0.1f, 35f) },
            { "focusNearPlane", Value(Index.FocusNearPlane, "近景の境界", 60f, 0.1f, 3f) },
            { "focusNearFalloff", Value(Index.FocusNearFalloff, "近景の減衰", 60f, 0.1f, 3f) },
            { "focusFarPlane", Value(Index.FocusFarPlane, "遠景の境界", 60f, 0.1f, 6f) },
            { "focusFarFalloff", Value(Index.FocusFarFalloff, "遠景の減衰", 60f, 0.1f, 6f) },
            { "focusNearBlurRadius", Value(Index.FocusNearBlurRadius, "近景ぼけ半径", 100f, 0.1f, 18f) },
            { "focusFarBlurRadius", Value(Index.FocusFarBlurRadius, "遠景ぼけ半径", 100f, 0.1f, 20f) },

            { "antiFlicker", Toggle(Index.AntiFlicker, "ちらつき対策", 0f) },
            { "useBokehTexture", Toggle(Index.UseBokehTexture, "ﾃｸｽﾁｬﾎﾞｹ", 0f) },
            { "bokehScale", Value(Index.BokehScale, "ﾎﾞｹの大きさ", 20f, 0.01f, 1f) },
            { "bokehIntensity", Value(Index.BokehIntensity, "ﾎﾞｹの強さ", 400f, 0.1f, 50f) },
            { "bokehThreshold", Value(Index.BokehThreshold, "ﾎﾞｹしきい値", 5f, 0.01f, 2f) },
            { "bokehSpawnHeuristic", Value(Index.BokehSpawnHeuristic, "ﾎﾞｹ発生率", 1f, 0.01f, 0.15f) },

            {
                "maidSlotNo", new CustomValueInfo
                {
                    index = (int)Index.MaidSlotNo,
                    name = "追従",
                    defaultValue = -1f,
                    uiType = CustomValueUIType.MaidSlot,
                }
            },
        };

        public override Dictionary<string, CustomValueInfo> GetCustomValueInfoMap()
        {
            return CustomValueInfoMap;
        }

        // ValueData アクセサ
        private ValueData V(Index index) => values[(int)index];

        // CustomValueInfo アクセサ
        public CustomValueInfo tweakModeInfo => GetCustomValueInfo("tweakMode");
        public CustomValueInfo filteringQualityInfo => GetCustomValueInfo("filteringQuality");
        public CustomValueInfo apertureShapeInfo => GetCustomValueInfo("apertureShape");
        public CustomValueInfo apertureOrientationInfo => GetCustomValueInfo("apertureOrientation");
        public CustomValueInfo focusFocusPlaneInfo => GetCustomValueInfo("focusFocusPlane");
        public CustomValueInfo focusRangeInfo => GetCustomValueInfo("focusRange");
        public CustomValueInfo focusNearPlaneInfo => GetCustomValueInfo("focusNearPlane");
        public CustomValueInfo focusNearFalloffInfo => GetCustomValueInfo("focusNearFalloff");
        public CustomValueInfo focusFarPlaneInfo => GetCustomValueInfo("focusFarPlane");
        public CustomValueInfo focusFarFalloffInfo => GetCustomValueInfo("focusFarFalloff");
        public CustomValueInfo focusNearBlurRadiusInfo => GetCustomValueInfo("focusNearBlurRadius");
        public CustomValueInfo focusFarBlurRadiusInfo => GetCustomValueInfo("focusFarBlurRadius");
        public CustomValueInfo antiFlickerInfo => GetCustomValueInfo("antiFlicker");
        public CustomValueInfo useBokehTextureInfo => GetCustomValueInfo("useBokehTexture");
        public CustomValueInfo bokehScaleInfo => GetCustomValueInfo("bokehScale");
        public CustomValueInfo bokehIntensityInfo => GetCustomValueInfo("bokehIntensity");
        public CustomValueInfo bokehThresholdInfo => GetCustomValueInfo("bokehThreshold");
        public CustomValueInfo bokehSpawnHeuristicInfo => GetCustomValueInfo("bokehSpawnHeuristic");
        public CustomValueInfo maidSlotNoInfo => GetCustomValueInfo("maidSlotNo");

        // 値アクセサ
        public int tweakMode { get => V(Index.TweakMode).intValue; set => V(Index.TweakMode).intValue = value; }
        public int filteringQuality { get => V(Index.FilteringQuality).intValue; set => V(Index.FilteringQuality).intValue = value; }
        public int apertureShape { get => V(Index.ApertureShape).intValue; set => V(Index.ApertureShape).intValue = value; }
        public float apertureOrientation { get => V(Index.ApertureOrientation).value; set => V(Index.ApertureOrientation).value = value; }
        public float focusFocusPlane { get => V(Index.FocusFocusPlane).value; set => V(Index.FocusFocusPlane).value = value; }
        public float focusRange { get => V(Index.FocusRange).value; set => V(Index.FocusRange).value = value; }
        public float focusNearPlane { get => V(Index.FocusNearPlane).value; set => V(Index.FocusNearPlane).value = value; }
        public float focusNearFalloff { get => V(Index.FocusNearFalloff).value; set => V(Index.FocusNearFalloff).value = value; }
        public float focusFarPlane { get => V(Index.FocusFarPlane).value; set => V(Index.FocusFarPlane).value = value; }
        public float focusFarFalloff { get => V(Index.FocusFarFalloff).value; set => V(Index.FocusFarFalloff).value = value; }
        public float focusNearBlurRadius { get => V(Index.FocusNearBlurRadius).value; set => V(Index.FocusNearBlurRadius).value = value; }
        public float focusFarBlurRadius { get => V(Index.FocusFarBlurRadius).value; set => V(Index.FocusFarBlurRadius).value = value; }
        public bool antiFlicker { get => V(Index.AntiFlicker).boolValue; set => V(Index.AntiFlicker).boolValue = value; }
        public bool useBokehTexture { get => V(Index.UseBokehTexture).boolValue; set => V(Index.UseBokehTexture).boolValue = value; }
        public float bokehScale { get => V(Index.BokehScale).value; set => V(Index.BokehScale).value = value; }
        public float bokehIntensity { get => V(Index.BokehIntensity).value; set => V(Index.BokehIntensity).value = value; }
        public float bokehThreshold { get => V(Index.BokehThreshold).value; set => V(Index.BokehThreshold).value = value; }
        public float bokehSpawnHeuristic { get => V(Index.BokehSpawnHeuristic).value; set => V(Index.BokehSpawnHeuristic).value = value; }
        public int maidSlotNo { get => V(Index.MaidSlotNo).intValue; set => V(Index.MaidSlotNo).intValue = value; }

        public PEP.CinematicDepthOfFieldData cinematicDepthOfField
        {
            get => new PEP.CinematicDepthOfFieldData
            {
                enabled = visible,
                tweakMode = tweakMode,
                filteringQuality = filteringQuality,
                apertureShape = apertureShape,
                apertureOrientation = apertureOrientation,
                focusFocusPlane = focusFocusPlane,
                focusRange = focusRange,
                focusNearPlane = focusNearPlane,
                focusNearFalloff = focusNearFalloff,
                focusFarPlane = focusFarPlane,
                focusFarFalloff = focusFarFalloff,
                focusNearBlurRadius = focusNearBlurRadius,
                focusFarBlurRadius = focusFarBlurRadius,
                antiFlicker = antiFlicker,
                useBokehTexture = useBokehTexture,
                bokehScale = bokehScale,
                bokehIntensity = bokehIntensity,
                bokehThreshold = bokehThreshold,
                bokehSpawnHeuristic = bokehSpawnHeuristic,
                // 被写界深度 (PostEffectManager.ApplyDepthOfField) と同じ規約で分ける
                maidFocus = maidSlotNo >= 0,
                maidIndex = maidSlotNo >= 0 ? maidSlotNo : 0,
            };
            set
            {
                visible = value.enabled;
                tweakMode = value.tweakMode;
                filteringQuality = value.filteringQuality;
                apertureShape = value.apertureShape;
                apertureOrientation = value.apertureOrientation;
                focusFocusPlane = value.focusFocusPlane;
                focusRange = value.focusRange;
                focusNearPlane = value.focusNearPlane;
                focusNearFalloff = value.focusNearFalloff;
                focusFarPlane = value.focusFarPlane;
                focusFarFalloff = value.focusFarFalloff;
                focusNearBlurRadius = value.focusNearBlurRadius;
                focusFarBlurRadius = value.focusFarBlurRadius;
                antiFlicker = value.antiFlicker;
                useBokehTexture = value.useBokehTexture;
                bokehScale = value.bokehScale;
                bokehIntensity = value.bokehIntensity;
                bokehThreshold = value.bokehThreshold;
                bokehSpawnHeuristic = value.bokehSpawnHeuristic;
                maidSlotNo = value.maidFocus ? value.maidIndex : -1;
            }
        }
    }
}
```

注意: `TransformDataBloom` は 1 プロパティ 4 行の書式で書いている。上の 1 行書式が既存ファイル群の書式と合わないと判断したら、`TransformDataBloom` と同じ 4 行書式（`...Value` アクセサ + get/set ブロック）に揃える。`GetCustomValueInfo(string)` が `TransformDataBase` にあることは `TransformDataBloom` の使用で確認済み。

- [ ] **Step 4: csproj と TimelineIntegration へ登録する**

`COM3D2.SceneEditor.Plugin.csproj` の `<Compile Include="Timeline\TransformData\TransformDataBloom.cs" />` の直後へ追記する。

```xml
    <Compile Include="Timeline\TransformData\TransformDataCinematicDepthOfField.cs" />
```

`Timeline/TimelineIntegration.cs` の `TransformType.Bloom` の `RegisterTransform` の直後へ追記する（アルファベット順）。

```csharp
                timelineManager.RegisterTransform(
                    MTEP.TransformType.CinematicDepthOfField,
                    MTEP.TimelineManager.CreateTransform<MTEP.TransformDataCinematicDepthOfField>);
```

- [ ] **Step 5: ビルドしてテストが通ることを確認する**

```bash
cd W:/COM3D2_5/work/COM3D2.SceneEditor.Plugin/source/COM3D2.SceneEditor.Plugin
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" COM3D2.SceneEditor.Plugin.csproj /p:Configuration=Debug /p:GameVersion=COM3D2  "/p:COM3D2_DIR=W:\COM3D2" "/p:COM3D25_DIR=W:\COM3D2_5"
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" COM3D2.SceneEditor.Plugin.csproj /p:Configuration=Debug /p:GameVersion=COM3D25 "/p:COM3D2_DIR=W:\COM3D2" "/p:COM3D25_DIR=W:\COM3D2_5"
cd W:/COM3D2_5/work/COM3D2.SceneEditor.Plugin
dotnet test source/COM3D2.SceneEditor.Plugin.Tests
```
Expected: ビルド 2 本成功。新規 4 テストを含め全テスト PASS（既存テストの退行なし）。

「補間で列挙値と追従メイドは区間開始値のまま」で `filteringQuality` / `apertureShape` が補間されてしまう場合は、`LerpScratch` の Int 判定条件を `TransformDataBase` で確認し（`cddb5bc` の差分付近）、判定に合う `CustomValueInfo` の形へ直す。テストの期待値は仕様そのもの（列挙値は区間開始値を保つ）なので変えない。直すのは実装側（`CustomValueInfo` の min/max/step）である。

- [ ] **Step 6: コミットする**

```bash
cd W:/COM3D2_5/work/COM3D2.SceneEditor.Plugin
git add source/COM3D2.SceneEditor.Plugin/Timeline/TransformData/TransformDataCinematicDepthOfField.cs \
        source/COM3D2.SceneEditor.Plugin/Timeline/TimelineIntegration.cs \
        source/COM3D2.SceneEditor.Plugin/COM3D2.SceneEditor.Plugin.csproj \
        source/COM3D2.SceneEditor.Plugin.Tests/TransformDataCinematicDepthOfFieldTests.cs
git commit -m "feat(timeline): シネマティック被写界深度の TransformData を追加する"
```

---

### Task 5: レイヤーへの組み込み（キー記録と再生）

**Files:**
- Create: `Timeline/TimelineLayer/PostEffectTimelineLayer_CinematicDepthOfField.cs`
- Modify: `Timeline/TimelineLayer/PostEffectTimelineLayer.cs`
- Modify: `COM3D2.SceneEditor.Plugin.csproj`

**Interfaces:**
- Consumes: `PostEffectManager.GetCinematicDepthOfFieldData / ApplyCinematicDepthOfField`（Task 3）、`TransformDataCinematicDepthOfField.cinematicDepthOfField`（Task 4）
- Produces: ボーン名 `"CinematicDepthOfField"`、`PostEffectTimelineLayer.ApplyCinematicDepthOfField(MotionData, float)`（private）

- [ ] **Step 1: partial ファイルを作る**

```csharp
namespace COM3D2.MotionTimelineEditor.Plugin
{
    public partial class PostEffectTimelineLayer : TimelineLayerBase
    {
        private void ApplyCinematicDepthOfField(MotionData motion, float t)
        {
            var scratch = LerpScratch<TransformDataCinematicDepthOfField>(motion, t);
            postEffectManager.ApplyCinematicDepthOfField(scratch.cinematicDepthOfField);
        }
    }
}
```

csproj の `<Compile Include="Timeline\TimelineLayer\PostEffectTimelineLayer_Bloom.cs" />` の直後へ追記する。

```xml
    <Compile Include="Timeline\TimelineLayer\PostEffectTimelineLayer_CinematicDepthOfField.cs" />
```

- [ ] **Step 2: ボーン一覧に足す（固定エフェクト 3 → 4）**

`allBoneNames` の初期容量と追加:

```csharp
                    _allBoneNames = new List<string>(
                        4 + timeline.paraffinCount + timeline.distanceFogCount + timeline.rimlightCount);
```

`_allBoneNames.Add("Bloom");` の直後へ:

```csharp
                    _allBoneNames.Add("CinematicDepthOfField");
```

- [ ] **Step 3: `Update` の件数判定を 4 にする**

```csharp
            var boneCount = 4
```

ここが 3 のままだとボーン一覧が毎フレーム作り直される。

- [ ] **Step 4: `ApplyPlayData` に足す**

`ApplyPlayDataByType(TransformType.Bloom);` と直後のコメント行の後へ:

```csharp
            ApplyPlayDataByType(TransformType.CinematicDepthOfField);
            //stopwatch.ProcessEnd("  CinematicDepthOfField");
```

- [ ] **Step 5: `ApplyMotion` / `UpdateFrame` / `GetTransformType` に足す**

`ApplyMotion` の `case TransformType.Bloom:` ブロックの後へ:

```csharp
                case TransformType.CinematicDepthOfField:
                    ApplyCinematicDepthOfField(motion, t);
                    break;
```

`UpdateFrame` の `case PostEffectType.Bloom:` ブロックの後へ:

```csharp
                    case PostEffectType.CinematicDepthOfField:
                    {
                        var trans = CreateTransformData<TransformDataCinematicDepthOfField>(effectName);
                        trans.cinematicDepthOfField = postEffectManager.GetCinematicDepthOfFieldData();

                        var bone = frame.CreateBone(trans);
                        frame.UpdateBone(bone);
                        break;
                    }
```

`GetTransformType` の `case PostEffectType.Bloom:` の後へ:

```csharp
                case PostEffectType.CinematicDepthOfField:
                    return TransformType.CinematicDepthOfField;
```

- [ ] **Step 6: 他の固定件数の前提が無いか確認する**

```bash
cd W:/COM3D2_5/work/COM3D2.SceneEditor.Plugin/source
grep -rn "PostEffectType\.\|\"Bloom\"\|TransformType.Bloom" --include=*.cs . | grep -v "/MTEUtils/" | grep -v "Tests/"
```

Task 3〜6 で触るファイル以外に `Bloom` を列挙している箇所（例: エフェクト種別ごとの switch、固定件数の前提、XML 変換）があれば、同じ位置へ `CinematicDepthOfField` を足す。見つけた箇所と対応を作業メモに残す。

- [ ] **Step 7: ビルドとテスト**

Task 4 Step 5 と同じコマンド（ビルド 2 本 + `dotnet test`）。
Expected: すべて成功。

- [ ] **Step 8: コミットする**

```bash
cd W:/COM3D2_5/work/COM3D2.SceneEditor.Plugin
git add source/COM3D2.SceneEditor.Plugin/Timeline/TimelineLayer/PostEffectTimelineLayer_CinematicDepthOfField.cs \
        source/COM3D2.SceneEditor.Plugin/Timeline/TimelineLayer/PostEffectTimelineLayer.cs \
        source/COM3D2.SceneEditor.Plugin/COM3D2.SceneEditor.Plugin.csproj
git commit -m "feat(timeline): シネマティック被写界深度をポストエフェクトレイヤーで再生・記録する"
```

（Step 6 で他ファイルを直した場合は、それらも add する）

---

### Task 6: 編集 UI

**Files:**
- Modify: `PostEffectRowDrawer.cs`（`DrawBloomRows` の後、`DrawGTToneMapCurve` の前）
- Modify: `Timeline/ItemInspector/PostEffectItemInspector.cs`

**Interfaces:**
- Consumes: `TransformDataCinematicDepthOfField.defaultTrans` の各 Info と定数（Task 4）、`PostEffectManager` の Get/Apply/`isCinematicDepthOfFieldAvailable`（Task 3）
- Produces: `PostEffectRowDrawer.DrawCinematicDepthOfFieldRows(GUIView view)`

- [ ] **Step 1: `DrawCinematicDepthOfFieldRows` を追加する**

表示条件は PostEffects 側 UI（`CinematicDepthOfFieldController.DrawContent`）と合わせる。メイド追従行は `DrawDepthOfFieldRows` と同じ作りにする（`_maidComboBox` は Drawer インスタンスごとにあり、Drawer はエフェクトごとに分かれているので共用で問題ない）。テクスチャのパスは PostEffects 側で設定する旨を表示する。

```csharp
        /// <summary>シネマティック被写界深度</summary>
        public void DrawCinematicDepthOfFieldRows(GUIView view)
        {
            if (!postEffectManager.isCinematicDepthOfFieldAvailable)
            {
                view.DrawLabel("PostEffects.Plugin が古いため使用できません", -1, 20, Color.yellow);
                return;
            }

            var dof = postEffectManager.GetCinematicDepthOfFieldData();
            var updateTransform = false;
            var defaultTrans = TransformDataCinematicDepthOfField.defaultTrans;

            view.DrawToggle("有効化", dof.enabled, 80, 20, newValue =>
            {
                dof.enabled = newValue;
                updateTransform = true;
            });

            updateTransform |= view.DrawCustomValueBool(
                defaultTrans.tweakModeInfo,
                dof.tweakMode == TransformDataCinematicDepthOfField.TweakModeExplicit,
                newValue => dof.tweakMode = newValue
                    ? TransformDataCinematicDepthOfField.TweakModeExplicit
                    : TransformDataCinematicDepthOfField.TweakModeRange);

            updateTransform |= view.DrawCustomValueInt(
                defaultTrans.filteringQualityInfo,
                dof.filteringQuality,
                newValue => dof.filteringQuality = newValue,
                labelWidth: CustomLabelWidth,
                sliderWidth: CustomSliderWidth);

            updateTransform |= view.DrawCustomValueInt(
                defaultTrans.apertureShapeInfo,
                dof.apertureShape,
                newValue => dof.apertureShape = newValue,
                labelWidth: CustomLabelWidth,
                sliderWidth: CustomSliderWidth);

            // 絞りの向きは円形以外 (方向性ぼかし) でのみ効く
            if (dof.apertureShape != 0)
            {
                updateTransform |= view.DrawCustomValueFloat(
                    defaultTrans.apertureOrientationInfo,
                    dof.apertureOrientation,
                    newValue => dof.apertureOrientation = newValue,
                    labelWidth: CustomLabelWidth,
                    sliderWidth: CustomSliderWidth);
            }

            var isRange = dof.tweakMode == TransformDataCinematicDepthOfField.TweakModeRange;
            if (isRange)
            {
                // メイド追従はピント面と範囲モードでのみ効く (実体側と同じ条件)
                view.BeginHorizontal();
                {
                    view.DrawLabel("追従メイド", 70, 20);

                    view.DrawToggle("", dof.maidFocus, 20, 20, newValue =>
                    {
                        dof.maidFocus = newValue;
                        dof.maidIndex = newValue ? Mathf.Max(0, _maidComboBox.currentIndex) : 0;
                        updateTransform = true;
                    });

                    _maidComboBox.items = MTEP.MaidManager.instance.maidCaches;
                    _maidComboBox.onSelected = (maidCache, index) =>
                    {
                        dof.maidFocus = true;
                        dof.maidIndex = index;
                        updateTransform = true;
                    };
                    _maidComboBox.DrawButton(view);
                }
                view.EndLayout();

                view.SetEnabled(view.focusedComboBox == null);

                if (!dof.maidFocus)
                {
                    updateTransform |= view.DrawCustomValueFloat(
                        defaultTrans.focusFocusPlaneInfo,
                        dof.focusFocusPlane,
                        newValue => dof.focusFocusPlane = newValue,
                        labelWidth: CustomLabelWidth,
                        sliderWidth: CustomSliderWidth);
                }

                updateTransform |= view.DrawCustomValueFloat(
                    defaultTrans.focusRangeInfo,
                    dof.focusRange,
                    newValue => dof.focusRange = newValue,
                    labelWidth: CustomLabelWidth,
                    sliderWidth: CustomSliderWidth);
            }
            else
            {
                updateTransform |= view.DrawCustomValueFloat(
                    defaultTrans.focusNearPlaneInfo,
                    dof.focusNearPlane,
                    newValue => dof.focusNearPlane = newValue,
                    labelWidth: CustomLabelWidth,
                    sliderWidth: CustomSliderWidth);
            }

            updateTransform |= view.DrawCustomValueFloat(
                defaultTrans.focusNearFalloffInfo,
                dof.focusNearFalloff,
                newValue => dof.focusNearFalloff = newValue,
                labelWidth: CustomLabelWidth,
                sliderWidth: CustomSliderWidth);

            updateTransform |= view.DrawCustomValueFloat(
                defaultTrans.focusNearBlurRadiusInfo,
                dof.focusNearBlurRadius,
                newValue => dof.focusNearBlurRadius = newValue,
                labelWidth: CustomLabelWidth,
                sliderWidth: CustomSliderWidth);

            if (!isRange)
            {
                updateTransform |= view.DrawCustomValueFloat(
                    defaultTrans.focusFarPlaneInfo,
                    dof.focusFarPlane,
                    newValue => dof.focusFarPlane = newValue,
                    labelWidth: CustomLabelWidth,
                    sliderWidth: CustomSliderWidth);
            }

            updateTransform |= view.DrawCustomValueFloat(
                defaultTrans.focusFarFalloffInfo,
                dof.focusFarFalloff,
                newValue => dof.focusFarFalloff = newValue,
                labelWidth: CustomLabelWidth,
                sliderWidth: CustomSliderWidth);

            updateTransform |= view.DrawCustomValueFloat(
                defaultTrans.focusFarBlurRadiusInfo,
                dof.focusFarBlurRadius,
                newValue => dof.focusFarBlurRadius = newValue,
                labelWidth: CustomLabelWidth,
                sliderWidth: CustomSliderWidth);

            updateTransform |= view.DrawCustomValueBool(
                defaultTrans.antiFlickerInfo,
                dof.antiFlicker,
                newValue => dof.antiFlicker = newValue);

            view.DrawHorizontalLine(Color.gray);

            updateTransform |= view.DrawCustomValueBool(
                defaultTrans.useBokehTextureInfo,
                dof.useBokehTexture,
                newValue => dof.useBokehTexture = newValue);

            if (dof.useBokehTexture)
            {
                // 画像のパスは値配列に載らないため PostEffects 側でだけ設定できる。
                // DX11 判定 (CinematicDepthOfFieldEffect.supportsTextureBokeh) は実体側の型でここから
                // 参照できないため、非 DX11 環境では効かない旨を併記する (実体側は描画時にガード済み)
                view.DrawLabel("ボケ画像は PostEffects 側で設定します (DX11 のみ有効)", -1, 20, Color.gray);

                updateTransform |= view.DrawCustomValueFloat(
                    defaultTrans.bokehScaleInfo,
                    dof.bokehScale,
                    newValue => dof.bokehScale = newValue,
                    labelWidth: CustomLabelWidth,
                    sliderWidth: CustomSliderWidth);

                updateTransform |= view.DrawCustomValueFloat(
                    defaultTrans.bokehIntensityInfo,
                    dof.bokehIntensity,
                    newValue => dof.bokehIntensity = newValue,
                    labelWidth: CustomLabelWidth,
                    sliderWidth: CustomSliderWidth);

                updateTransform |= view.DrawCustomValueFloat(
                    defaultTrans.bokehThresholdInfo,
                    dof.bokehThreshold,
                    newValue => dof.bokehThreshold = newValue,
                    labelWidth: CustomLabelWidth,
                    sliderWidth: CustomSliderWidth);

                updateTransform |= view.DrawCustomValueFloat(
                    defaultTrans.bokehSpawnHeuristicInfo,
                    dof.bokehSpawnHeuristic,
                    newValue => dof.bokehSpawnHeuristic = newValue,
                    labelWidth: CustomLabelWidth,
                    sliderWidth: CustomSliderWidth);
            }

            if (updateTransform)
            {
                postEffectManager.ApplyCinematicDepthOfField(dof);
            }
        }
```

`DrawCustomValueBool` / `DrawCustomValueInt` / `DrawCustomValueFloat` のシグネチャ（`labelWidth` / `sliderWidth` の有無）は `DrawBloomRows` の呼び方に合わせてある。コンパイルが通らなければ `DrawBloomRows` の同種の呼び出しと同じ形へ直す。

- [ ] **Step 2: クラスの summary を直す**

`PostEffectRowDrawer.cs` 冒頭の 1 行目:

```csharp
    /// ポストエフェクト 1 つ分のパラメータ行 (被写界深度 / パラフィン / 距離フォグ / リムライト / GTToneMap / ブルーム / シネマティック被写界深度)。
```

- [ ] **Step 3: Inspector の振り分けへ足す**

`PostEffectItemInspector.cs` の `case MTEP.PostEffectType.Bloom:` ブロックの後へ:

```csharp
                case MTEP.PostEffectType.CinematicDepthOfField:
                    drawer.DrawCinematicDepthOfFieldRows(view);
                    return;
```

クラスの summary を直す。

```csharp
    /// 項目は被写界深度・GTToneMap・ブルーム・シネマティック被写界深度が 1 つずつ、
    /// パラフィン・距離フォグ・リムライトがタイムラインの設定数だけ並ぶ。
```

（既存の 2 行目「タイムラインの設定数だけ並ぶ。」と重複しないよう、既存の 2 行を置き換える）

- [ ] **Step 4: ビルドとテスト**

Task 4 Step 5 と同じコマンド。
Expected: すべて成功。

- [ ] **Step 5: コミットする**

```bash
cd W:/COM3D2_5/work/COM3D2.SceneEditor.Plugin
git add source/COM3D2.SceneEditor.Plugin/PostEffectRowDrawer.cs \
        source/COM3D2.SceneEditor.Plugin/Timeline/ItemInspector/PostEffectItemInspector.cs
git commit -m "feat(posteffect): シネマティック被写界深度の編集 UI を追加する"
```

---

## 実機検証（全タスク完了後）

ゲームを停止した状態で両プラグインの DLL を配置する（配置方法はユーザーに確認する。`debug.bat` はゲーム停止中に実機へ反映するので、ユーザーの了承を得てから実行する）。起動後、SceneEditor のタイムラインで以下を確認する。

1. ポストエフェクトレイヤーのボーン一覧の末尾（ブルームの後）に「シネマティックDoF」が出る。
2. PostEffects のタイムラインタブに「シネマティック被写界深度」のタブが出る。そこで値を変えると SceneEditor の編集モードへ移り、Undo できる（`IsTimelineDriven` 経由の既存の仕組み）。
3. SceneEditor の Inspector でピント範囲・ぼけ半径などを変えると、画面へ即座に反映される。
4. 2 か所のフレームへ違うピント範囲でキーを打って再生すると、連続して変化する。品質・絞りの形・追従メイドは区間の開始値のまま切り替わる。
5. PostEffects 側で「ピント位置を可視化」を ON にし、ボケ画像のパスを設定する。その状態でタイムラインを再生しても、両方が保たれる。
6. 保存して読み込み直すと値が戻る。XML に `<Type>CinematicDepthOfField</Type>` のボーンが出ている。シネマティック DoF のキーが無い既存 XML も、エラー無く読み込める。
7. 旧版の PostEffects DLL（`fef9d3a` v2.2.1.0 のリリース zip など）と組み合わせる。ブルーム対応では、この組み合わせで全ポストエフェクトが止まる問題が実際に起きた。そのため省略しない。他 6 系統は動き、Inspector には「PostEffects.Plugin が古いため使用できません」と出る。
8. タイムラインを閉じる（レイヤーがカレントでなくなる）と、シネマティック被写界深度も無効へ戻る。

## 完了後のフロー

CLAUDE.md の標準フローに従い、実装が完了したら **code-review** スキルでレビューしてからユーザーへ提示する。コミットは各リポジトリで行う。MTEUtils の push はユーザーが行う。

## レビュー却下メモ

（plan-review の指摘 3 件はすべて取り込み済み。却下なし）
