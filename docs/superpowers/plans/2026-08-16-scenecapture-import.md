# SceneCapture プリセット取り込み Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
> （このリポジトリのワークフロー規約により subagent-driven-development は使わない）

**Goal:** SceneEditor から渡される SceneCapture プリセットの `<Effects>` を本プラグインのエフェクト設定へ適用できるようにする。

**Architecture:** `SceneCaptureImporter`（新規）が `<Effects>` XML を読み、既定値の `PostEffectsPreset` を組み立てて返す純粋な変換。`PresetManager.ApplySceneCaptureXml` がそれを `EffectSettings` へ適用し、`PostEffectsScenePresetProvider.ApplySceneCaptureXml` が SceneEditor 向けの契約メソッドとして公開する。Def→Setting の対応は「子要素名の先頭 `_` を落として同名フィールドへリフレクション代入」を既定規則とし、解決しない項目だけを 1 枚の表に持つ。

**Tech Stack:** C# / .NET Framework 4.7.1 / Unity 2022.3（COM3D2.5）/ BepInEx + UnityInjector / `System.Xml.Linq`

**Spec:** `docs/superpowers/specs/2026-08-16-scenecapture-import-design.md`

## Global Constraints

- コードのコメントとログメッセージは日本語で書く
- `git worktree` を使わない。メインの作業ディレクトリで作業する
- ビルドは `source\COM3D25.PostEffects.Plugin\build.bat debug`（リポジトリルートの `debug.bat` でも同じ）。ゲーム起動中は DLL のデプロイに失敗するが続行される
- 単体テスト基盤は無い。検証は「ビルド成功」＋「MCP `com3d25-devbridge` の `eval_csharp` による実機評価」で行う
- devbridge の REPL は Unity 型を完全修飾名で書く（`UnityEngine.Color` など）。短縮するとゲーム側の同名識別子に化ける
- DLL を差し替えたらゲームの再起動が必要。ゲーム起動中はビルド出力のデプロイが失敗するので、検証前に必ずゲームを落としてビルドし直す
- 適用処理から HistoryAPI へ `Register` しない
- 既存の `PostEffectsPreset` / `EffectSettings` / 各 `XxxSetting` のフィールドは変更しない。取り込み側だけで吸収する

---

## File Structure

| ファイル | 責務 |
|---|---|
| `source/COM3D25.PostEffects.Plugin/Manager/SceneCaptureImporter.cs`（新規） | `<Effects>` XML → `PostEffectsPreset` の変換。Def 対応表・例外表・値パーサをここに集約する。`EffectSettings` には触らない |
| `source/COM3D25.PostEffects.Plugin/Manager/PresetManager.cs`（変更） | `ApplySceneCaptureXml(string)` を追加。Importer を呼んで `ApplyTo` し、未解決要素を警告ログに出す |
| `source/COM3D25.PostEffects.Plugin/ScenePresetProvider.cs`（変更） | SceneEditor の契約メソッド `public static bool ApplySceneCaptureXml(string)` を追加 |
| `docs/scenecapture-ui-diff.md`（変更） | 取り込み対応の節を追記。取り込めない項目の一覧を資産として残す |

---

## Task 1: 変換基盤とプロバイダ配線

`<Effects>` を読んで既定値の `PostEffectsPreset` を返す骨組みと、SceneEditor への配線を通す。この時点では Def 対応表は空でよく、「`<Effects>` が無ければ何もしない」「壊れた XML なら `false`」「未知 Def は警告にまとめる」が動くことを確認する。

**Files:**
- Create: `source/COM3D25.PostEffects.Plugin/Manager/SceneCaptureImporter.cs`
- Modify: `source/COM3D25.PostEffects.Plugin/COM3D25.PostEffects.Plugin.csproj`（`System.Xml.Linq` 参照と新規ファイルの `Compile Include` を追加）
- Modify: `source/COM3D25.PostEffects.Plugin/Manager/PresetManager.cs`（末尾、`DeletePreset` の前）
- Modify: `source/COM3D25.PostEffects.Plugin/ScenePresetProvider.cs:31` の直後

**Interfaces:**
- Consumes: `PostEffectsPreset`（`ApplyTo(EffectSettings)`）, `EffectSettings.instance`, `MTEUtils.LogWarning/LogError/LogException`
- Produces:
  - `SceneCaptureImporter.Parse(string xml, out List<string> unresolved)` → `PostEffectsPreset`。`<Effects>` が無い / 子要素 0 件なら `null`（適用不要）。XML が壊れていれば例外を投げる
  - `SceneCaptureImporter.DefMap` / `_defMaps`（Task 2 以降が要素を足す表）
  - `SceneCaptureImporter.TrySetField(object setting, string fieldName, string text)` → `bool`
  - `PresetManager.instance.ApplySceneCaptureXml(string xml)` → `bool`
  - `PostEffectsScenePresetProvider.ApplySceneCaptureXml(string xml)` → `bool`

- [ ] **Step 1: csproj に参照と新規ファイルを足す**

この csproj は SDK スタイルではなく、コンパイル対象を `<Compile Include>` で明示する形式。
新規ファイルを足すときは csproj への追加が必須（忘れると型が見つからずビルドが落ちる）。

`source/COM3D25.PostEffects.Plugin/COM3D25.PostEffects.Plugin.csproj` の
`<Reference Include="System.Xml" />` の直後に追加:

```xml
    <Reference Include="System.Xml.Linq" />
```

`<Compile Include="Manager\PresetManager.cs" />` の直後に追加（`Manager\` 配下は名前順に並んでいる）:

```xml
    <Compile Include="Manager\SceneCaptureImporter.cs" />
```

SceneEditor プラグインも同じ構成（`XDocument` + `System.Xml.Linq` 参照）で SceneCapture プリセットを
実機で読めているため、この参照で問題ないことは確認済み。

- [ ] **Step 2: `SceneCaptureImporter.cs` を作成する**

`source/COM3D25.PostEffects.Plugin/Manager/SceneCaptureImporter.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Xml.Linq;
using COM3D2.MotionTimelineEditor;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// SceneCapture プリセットの Effects セクションを PostEffectsPreset へ変換する。
    /// EffectSettings には触らない純粋な変換で、適用は呼び出し側の責務。
    ///
    /// 対応の既定規則は「子要素名の先頭 _ を落として同名の Setting フィールドへ代入」。
    /// 解決しない項目だけを _defMaps の rename / ignored / custom に持つ
    /// </summary>
    public static class SceneCaptureImporter
    {
        /// <summary>Def 要素 1 つ分の対応定義</summary>
        private class DefMap
        {
            /// <summary>PostEffectsPreset 側のフィールド名</summary>
            public string presetField;

            /// <summary>子要素名 → Setting フィールド名 (既定規則で解決しないもの)</summary>
            public Dictionary<string, string> renames = new Dictionary<string, string>();

            /// <summary>対応する設定が無く、警告を出さずに捨てる子要素名 (先頭 _ を落とした形)</summary>
            public HashSet<string> ignored = new HashSet<string>();

            /// <summary>型変換だけでは済まない子要素の代入処理 (setting, 値文字列)</summary>
            public Dictionary<string, Action<object, string>> custom =
                new Dictionary<string, Action<object, string>>();
        }

        // Def 要素名 → 対応定義。Task 2 以降で中身を足していく
        private static readonly Dictionary<string, DefMap> _defMaps =
            new Dictionary<string, DefMap>();

        // 対応するエフェクトが本プラグインに無く、警告も出さずに捨てる Def。
        // CinematicBloomLayer は移植元の実装が EffectMask の発光 RT に固定されており移植不可
        private static readonly HashSet<string> _ignoredDefs = new HashSet<string>
        {
            "CinematicBloomLayerDef",
        };

        private const BindingFlags FieldFlags = BindingFlags.Public | BindingFlags.Instance;

        /// <summary>
        /// SceneCapture プリセット XML を PostEffectsPreset へ変換する。
        /// Effects セクションが無い / 空なら null を返す (適用不要)。
        /// XML 自体が壊れている場合は例外を投げる
        /// </summary>
        /// <param name="unresolved">解決できなかった Def 名・子要素名 (警告表示用)</param>
        public static PostEffectsPreset Parse(string xml, out List<string> unresolved)
        {
            unresolved = new List<string>();

            var root = XDocument.Parse(xml).Root;
            var effects = root != null ? root.Element("Effects") : null;
            if (effects == null || !effects.HasElements)
            {
                return null;
            }

            // 記載の無いエフェクトは既定値へ戻す。プリセットは全体の状態を表すため
            var preset = new PostEffectsPreset();

            foreach (var defElement in effects.Elements())
            {
                ApplyDef(preset, defElement, unresolved);
            }

            return preset;
        }

        private static void ApplyDef(PostEffectsPreset preset, XElement defElement, List<string> unresolved)
        {
            var defName = defElement.Name.LocalName;

            if (_ignoredDefs.Contains(defName))
            {
                return;
            }

            DefMap map;
            if (!_defMaps.TryGetValue(defName, out map))
            {
                unresolved.Add(defName);
                return;
            }

            var setting = GetPresetSetting(preset, map.presetField);
            if (setting == null)
            {
                // 表の presetField がタイプミス等で解決できないのは実装バグなので即座に分かるようにする
                MTEUtils.LogError("PostEffectsPreset にフィールドがありません: {0}", map.presetField);
                return;
            }

            ApplyDefFields(setting, defElement, map, defName, unresolved);
            TrySetField(setting, "enabled", "True");
        }

        /// <summary>Def の子要素を 1 つずつ setting へ流し込む</summary>
        private static void ApplyDefFields(
            object setting, XElement defElement, DefMap map, string defName, List<string> unresolved)
        {
            foreach (var element in defElement.Elements())
            {
                // SceneCapture 側はコンポーネントのフィールド名をそのまま書き出す。
                // 先頭 _ の有無はエフェクトごとにまちまちなので落として揃える
                var name = element.Name.LocalName.TrimStart('_');
                var text = element.Value;

                if (map.ignored.Contains(name))
                {
                    continue;
                }

                Action<object, string> custom;
                if (map.custom.TryGetValue(name, out custom))
                {
                    custom(setting, text);
                    continue;
                }

                string renamed;
                if (!map.renames.TryGetValue(name, out renamed))
                {
                    renamed = name;
                }

                if (!TrySetField(setting, renamed, text))
                {
                    unresolved.Add(defName + "/" + element.Name.LocalName);
                }
            }
        }

        private static object GetPresetSetting(PostEffectsPreset preset, string presetField)
        {
            var field = typeof(PostEffectsPreset).GetField(presetField, FieldFlags);
            return field != null ? field.GetValue(preset) : null;
        }

        /// <summary>
        /// Setting のフィールドへ SceneCapture の書式で書かれた値を代入する。
        /// フィールドが無い / 書式が不正なときは false を返して代入しない
        /// </summary>
        public static bool TrySetField(object setting, string fieldName, string text)
        {
            var field = setting.GetType().GetField(fieldName, FieldFlags);
            if (field == null)
            {
                return false;
            }

            object value;
            if (!TryConvert(field.FieldType, text, out value))
            {
                return false;
            }

            field.SetValue(setting, value);
            return true;
        }

        private static bool TryConvert(Type type, string text, out object value)
        {
            value = null;

            if (type == typeof(string))
            {
                value = text;
                return true;
            }
            if (type == typeof(float))
            {
                float f;
                if (!TryParseFloat(text, out f)) return false;
                value = f;
                return true;
            }
            if (type == typeof(int))
            {
                int i;
                if (!TryParseInt(text, out i)) return false;
                value = i;
                return true;
            }
            if (type == typeof(bool))
            {
                bool b;
                if (!bool.TryParse(text, out b)) return false;
                value = b;
                return true;
            }
            if (type.IsEnum)
            {
                int i;
                if (!TryParseInt(text, out i)) return false;
                // SceneCapture は enum を int で書き出す。範囲外は代入しない
                if (!Enum.IsDefined(type, i)) return false;
                value = Enum.ToObject(type, i);
                return true;
            }
            if (type == typeof(Color))
            {
                Color c;
                if (!TryParseColor(text, out c)) return false;
                value = c;
                return true;
            }
            if (type == typeof(Vector3))
            {
                Vector3 v;
                if (!TryParseVector3(text, out v)) return false;
                value = v;
                return true;
            }

            return false;
        }

        public static bool TryParseFloat(string text, out float value)
        {
            return float.TryParse(
                text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        public static bool TryParseInt(string text, out int value)
        {
            return int.TryParse(
                text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>SceneCapture は Color を Color32 の "r,g,b,a" (各 0-255) で書き出す</summary>
        public static bool TryParseColor(string text, out Color value)
        {
            value = Color.white;

            var parts = text.Split(',');
            if (parts.Length != 4)
            {
                return false;
            }

            var components = new float[4];
            for (var i = 0; i < 4; i++)
            {
                float f;
                if (!TryParseFloat(parts[i], out f))
                {
                    return false;
                }
                components[i] = f / 255f;
            }

            value = new Color(components[0], components[1], components[2], components[3]);
            return true;
        }

        public static bool TryParseVector3(string text, out Vector3 value)
        {
            value = Vector3.zero;

            var parts = text.Split(',');
            if (parts.Length != 3)
            {
                return false;
            }

            var components = new float[3];
            for (var i = 0; i < 3; i++)
            {
                if (!TryParseFloat(parts[i], out components[i]))
                {
                    return false;
                }
            }

            value = new Vector3(components[0], components[1], components[2]);
            return true;
        }
    }
}
```

- [ ] **Step 3: `PresetManager` に `ApplySceneCaptureXml` を追加する**

`source/COM3D25.PostEffects.Plugin/Manager/PresetManager.cs` の `ApplyPresetXml` の直後（`DeletePreset` の前）に挿入:

```csharp
        /// <summary>
        /// SceneCapture プリセット XML のエフェクト設定を現在の設定へ反映する (失敗時は false)。
        /// Effects セクションが無い / 空なら何もせず true を返す
        /// </summary>
        public bool ApplySceneCaptureXml(string xml)
        {
            if (string.IsNullOrEmpty(xml))
            {
                return false;
            }

            try
            {
                List<string> unresolved;
                var preset = SceneCaptureImporter.Parse(xml, out unresolved);

                if (unresolved.Count > 0)
                {
                    // 要素ごとに出すとログが埋まるため 1 回の適用につき 1 行にまとめる
                    MTEUtils.LogWarning(
                        "SceneCapture プリセットに未対応の項目があります: {0}",
                        string.Join(", ", unresolved.ToArray()));
                }

                if (preset == null)
                {
                    // Models だけを持つプリセットで現在の設定を消さない
                    return true;
                }

                // 選択中のプリセットとは別経路で設定が変わるため未保存扱いにする
                EffectSettings.instance.dirty = true;
                preset.ApplyTo(EffectSettings.instance);
                return true;
            }
            catch (Exception e)
            {
                MTEUtils.LogException(e);
                MTEUtils.LogError("SceneCapture プリセットの適用に失敗しました");
                return false;
            }
        }
```

- [ ] **Step 4: `ScenePresetProvider` に契約メソッドを追加する**

`source/COM3D25.PostEffects.Plugin/ScenePresetProvider.cs` の `ApplyPresetXml` の直後に挿入:

```csharp

        /// <summary>SceneCapture プリセット XML を適用する。成功可否を返す</summary>
        public static bool ApplySceneCaptureXml(string xml) =>
            PresetManager.instance.ApplySceneCaptureXml(xml);
```

- [ ] **Step 5: ビルドする**

ゲームが起動していたら終了させてから実行する。

Run: `source\COM3D25.PostEffects.Plugin\build.bat debug`
Expected: `=== ビルド中 (Debug) ===` のあとエラー無しで終了（exit code 0）

- [ ] **Step 6: 実機で骨組みを検証する**

ゲームを起動し、MCP `com3d25-devbridge` の `eval_csharp` で以下を順に評価する。

```csharp
// (a) Effects が無い → true を返し、設定は変わらない
COM3D25.PostEffects.Plugin.EffectSettings.instance.bloom.enabled = true;
var r1 = COM3D25.PostEffects.Plugin.PresetManager.instance.ApplySceneCaptureXml(
    "<Preset><Models /></Preset>");
$"{r1} {COM3D25.PostEffects.Plugin.EffectSettings.instance.bloom.enabled}"
```
Expected: `True True`（no-op なので bloom.enabled が立ったまま）

```csharp
// (b) 壊れた XML → false
COM3D25.PostEffects.Plugin.PresetManager.instance.ApplySceneCaptureXml("<Preset>")
```
Expected: `False`

```csharp
// (c) 未知 Def → true。BepInEx ログに「未対応の項目」警告が 1 行出る
COM3D25.PostEffects.Plugin.PresetManager.instance.ApplySceneCaptureXml(
    "<Preset><Effects><NoSuchDef><a>1</a></NoSuchDef></Effects></Preset>")
```
Expected: `True`。`tail_log` に `未対応の項目があります: NoSuchDef` を含む警告が 1 行

```csharp
// (d) Effects があれば全リセットされる
COM3D25.PostEffects.Plugin.EffectSettings.instance.bloom.enabled = true;
COM3D25.PostEffects.Plugin.PresetManager.instance.ApplySceneCaptureXml(
    "<Preset><Effects><NoSuchDef /></Effects></Preset>");
COM3D25.PostEffects.Plugin.EffectSettings.instance.bloom.enabled
```
Expected: `False`（記載の無いエフェクトは既定値へ戻る）

- [ ] **Step 7: コミット**

```bash
git add source/COM3D25.PostEffects.Plugin/Manager/SceneCaptureImporter.cs source/COM3D25.PostEffects.Plugin/Manager/PresetManager.cs source/COM3D25.PostEffects.Plugin/ScenePresetProvider.cs
git commit -m "feat(preset): SceneCapture プリセット取り込みの骨組みを追加"
```

---

## Task 2: 既定規則だけで通る Def を登録する

リネームも変換も要らない 18 Def を対応表に載せる。

**Files:**
- Modify: `source/COM3D25.PostEffects.Plugin/Manager/SceneCaptureImporter.cs`（`_defMaps` の初期化）

**Interfaces:**
- Consumes: Task 1 の `DefMap` / `_defMaps` / `TrySetField`
- Produces: `_defMaps` に `MaidHideDef` `SepiaDef` `AnalogGlitchDef` `DigitalGlitchDef` `GrayscaleDef` `ContrastDef` `CreaseDef` `EdgeDetectDef` `EdgeDetect2Def` `MotionBlurDef` `FisheyeDef` `TiltShiftHdrDef` `AntialiasingDef` `FilmicLetterBoxDef` `RampDef` `StreakDef` `CinematicLensAberrationsDef` `CinematicDepthOfFieldDef` のエントリ

- [ ] **Step 1: `_defMaps` の初期化を書き換える**

`SceneCaptureImporter.cs` の

```csharp
        // Def 要素名 → 対応定義。Task 2 以降で中身を足していく
        private static readonly Dictionary<string, DefMap> _defMaps =
            new Dictionary<string, DefMap>();
```

を次に置き換える:

```csharp
        // Def 要素名 → 対応定義
        private static readonly Dictionary<string, DefMap> _defMaps = BuildDefMaps();

        private static Dictionary<string, DefMap> BuildDefMaps()
        {
            var maps = new Dictionary<string, DefMap>();

            // 既定規則 (先頭 _ を落として同名代入) だけで通る Def。
            // MaidHide / Sepia / AnalogGlitch / DigitalGlitch は移植元が
            // パラメータを public プロパティで持つため中身が常に空になり、有効化のみ行う
            maps["MaidHideDef"] = new DefMap { presetField = "maidHide" };
            maps["SepiaDef"] = new DefMap { presetField = "sepia" };
            maps["AnalogGlitchDef"] = new DefMap { presetField = "analogGlitch" };
            maps["DigitalGlitchDef"] = new DefMap { presetField = "digitalGlitch" };
            maps["ContrastDef"] = new DefMap { presetField = "contrast" };
            maps["CreaseDef"] = new DefMap { presetField = "crease" };
            maps["MotionBlurDef"] = new DefMap { presetField = "motionBlur" };
            maps["FisheyeDef"] = new DefMap { presetField = "fisheye" };
            maps["TiltShiftHdrDef"] = new DefMap { presetField = "tiltShiftHdr" };
            maps["AntialiasingDef"] = new DefMap { presetField = "antialiasing" };
            maps["FilmicLetterBoxDef"] = new DefMap { presetField = "filmicLetterBox" };
            maps["CinematicLensAberrationsDef"] =
                new DefMap { presetField = "cinematicLensAberrations" };

            // 移植元の GrayscaleEffect はランプテクスチャを持つが本プラグインは未対応
            maps["GrayscaleDef"] = new DefMap
            {
                presetField = "grayscale",
                ignored = { "textureRamp" },
            };

            // EdgeDetect2 は EdgeDetect と同一実装 (フィールド順以外同じ・シェーダーも共通)。
            // EdgeDetectDef が無いときのフォールバックとして同じ設定へ流す
            maps["EdgeDetectDef"] = new DefMap { presetField = "edgeDetect" };
            maps["EdgeDetect2Def"] = new DefMap { presetField = "edgeDetect" };

            // _debug は移植元でも UI に出ないデバッグ表示
            maps["RampDef"] = new DefMap
            {
                presetField = "ramp",
                ignored = { "debug", "maidMask" },
            };

            // maidMask / enabledTransparentMode は EffectMask 依存で未移植
            maps["StreakDef"] = new DefMap
            {
                presetField = "streak",
                ignored = { "maidMask", "enabledTransparentMode" },
            };

            // prefilterBlur / medianFilter / dilateNearBlur は移植時に省略、
            // focusTransform は移植元もロード時に読み戻さない
            maps["CinematicDepthOfFieldDef"] = new DefMap
            {
                presetField = "cinematicDepthOfField",
                renames = { { "bokehTexture", "bokehTexturePath" } },
                ignored = { "prefilterBlur", "medianFilter", "dilateNearBlur", "focusTransform" },
            };

            return maps;
        }
```

- [ ] **Step 2: `EdgeDetect2Def` のフォールバックを実装する**

`Parse` の Def ループを、`EdgeDetectDef` があるときに `EdgeDetect2Def` を無視するよう書き換える。
`Parse` 内の

```csharp
            foreach (var defElement in effects.Elements())
            {
                ApplyDef(preset, defElement, unresolved);
            }
```

を次に置き換える:

```csharp
            // EdgeDetect2 は EdgeDetect の複製。両方あれば EdgeDetect を優先する
            var hasEdgeDetect = effects.Element("EdgeDetectDef") != null;

            foreach (var defElement in effects.Elements())
            {
                if (hasEdgeDetect && defElement.Name.LocalName == "EdgeDetect2Def")
                {
                    continue;
                }
                ApplyDef(preset, defElement, unresolved);
            }
```

- [ ] **Step 3: ビルドする**

Run: `source\COM3D25.PostEffects.Plugin\build.bat debug`
Expected: エラー無しで終了

- [ ] **Step 4: 実機で検証する**

```csharp
COM3D25.PostEffects.Plugin.PresetManager.instance.ApplySceneCaptureXml(
    "<Preset><Effects>" +
    "<ContrastDef><intensity>0.5</intensity><threshhold>0.25</threshhold><blurSpread>1.5</blurSpread></ContrastDef>" +
    "<EdgeDetectDef><mode>2</mode><edgeColor>255,0,0,255</edgeColor><edgesOnly>1</edgesOnly></EdgeDetectDef>" +
    "<AnalogGlitchDef />" +
    "</Effects></Preset>");
var s = COM3D25.PostEffects.Plugin.EffectSettings.instance;
$"{s.contrast.enabled} {s.contrast.intensity} {s.contrast.threshhold} {s.edgeDetect.enabled} {(int)s.edgeDetect.mode} {s.edgeDetect.edgeColor} {s.analogGlitch.enabled} {s.bloom.enabled}"
```
Expected: `True 0.5 0.25 True 2 RGBA(1.000, 0.000, 0.000, 1.000) True False`

```csharp
// EdgeDetect2Def 単独ならフォールバックで edgeDetect に入る
COM3D25.PostEffects.Plugin.PresetManager.instance.ApplySceneCaptureXml(
    "<Preset><Effects><EdgeDetect2Def><edgesOnly>1</edgesOnly></EdgeDetect2Def></Effects></Preset>");
var s2 = COM3D25.PostEffects.Plugin.EffectSettings.instance;
$"{s2.edgeDetect.enabled} {s2.edgeDetect.edgesOnly}"
```
Expected: `True 1`

- [ ] **Step 5: コミット**

```bash
git add source/COM3D25.PostEffects.Plugin/Manager/SceneCaptureImporter.cs
git commit -m "feat(preset): 既定規則で通る SceneCapture Def を対応表へ登録"
```

---

## Task 3: リネーム・値変換が要る Def を登録する

Bloom 系・ボケ系・フォグ系など、フィールド名の食い違いや値の作り替えが要る Def を追加する。

**Files:**
- Modify: `source/COM3D25.PostEffects.Plugin/Manager/SceneCaptureImporter.cs`（`BuildDefMaps` の `return maps;` の直前）

**Interfaces:**
- Consumes: Task 1 の `DefMap` / `TrySetField` / `TryParseFloat` / `TryParseInt`
- Produces: `_defMaps` に `BloomDef` `DepthOfFieldDef` `BokehDef` `FilmicBokehDef` `ObscuranceDef` `FilmicMedianFilterDef` `ColorCorrectionLutDef` `CinematicBloomDef` `FilmicBloomDef` `StylisticFogDef` のエントリ

- [ ] **Step 1: `BuildDefMaps` に 10 Def を追加する**

`return maps;` の直前へ挿入:

```csharp
            // 移植元は bloomIntensity を 0〜2.85 で扱っていた時期があり、
            // ロード時に 2.86 以上なら 100 で割る互換処理を持つ。同じ扱いにする
            maps["BloomDef"] = new DefMap
            {
                presetField = "bloom",
                renames =
                {
                    { "bloomThreshhold", "threshold" },
                    { "bloomThreshholdColor", "thresholdColor" },
                    { "bloomBlurIterations", "blurIterations" },
                    { "sepBlurSpread", "blurSpread" },
                    { "lensflareMode", "lensFlareMode" },
                    { "lensflareIntensity", "lensFlareIntensity" },
                    { "lensflareThreshhold", "lensFlareThreshold" },
                },
                // tweakMode は移植元でも描画に使われない Inspector 専用フィールド。
                // blurWidth は古いバージョンのプリセットにだけ現れる
                ignored = { "tweakMode", "blurWidth", "lensFlareVignetteMask" },
                custom =
                {
                    { "bloomIntensity", (setting, text) =>
                        {
                            float value;
                            if (!TryParseFloat(text, out value)) return;
                            ((BloomSetting)setting).intensity = value >= 2.86f ? value / 100f : value;
                        }
                    },
                    { "quality", (setting, text) =>
                        {
                            int value;
                            if (!TryParseInt(text, out value)) return;
                            // BloomQuality: 0 = Cheap, 1 = High
                            ((BloomSetting)setting).highQuality = value == 1;
                        }
                    },
                },
            };

            // focalTransform は移植元もロード時に読み戻さない
            maps["DepthOfFieldDef"] = new DefMap
            {
                presetField = "depthOfField",
                renames =
                {
                    // 綴りは移植元のバージョンによって Threshhold / Threshold が混在する
                    { "dx11BokehThreshhold", "dx11BokehThreshold" },
                    { "dx11BokehTexture", "dx11BokehTexturePath" },
                },
                ignored = { "focalTransform" },
                custom =
                {
                    { "blurType", (setting, text) =>
                        {
                            int value;
                            if (!TryParseInt(text, out value)) return;
                            // BlurType: 0 = DiscBlur, 1 = DX11
                            ((DepthOfFieldSetting)setting).useDX11Bokeh = value == 1;
                        }
                    },
                },
            };

            // pointOfFocus は移植元もロード時に読み戻さない
            maps["BokehDef"] = new DefMap
            {
                presetField = "bokeh",
                renames = { { "focalrange", "focalRange" } },
                ignored = { "pointOfFocus" },
            };

            // depthCutoff 系・medianFilter は移植元でもシェーダーへ渡らないデッドコード
            maps["FilmicBokehDef"] = new DefMap
            {
                presetField = "filmicBokeh",
                renames = { { "focalrange", "focalRange" } },
                ignored = { "pointOfFocus", "depthCutoffMode", "depthCutoff", "medianFilter" },
            };

            // ambientOnly は移植元でも機能していないデッドコード
            maps["ObscuranceDef"] = new DefMap
            {
                presetField = "obscurance",
                renames = { { "sampleCountValue", "variableSampleCount" } },
                ignored = { "ambientOnly" },
            };

            maps["FilmicMedianFilterDef"] = new DefMap
            {
                presetField = "filmicMedianFilter",
                renames = { { "medianFilter", "quality" } },
            };

            // 移植元は Texture3D フィールドの保存に converted3DLutFile プロパティの
            // 相対パスを書き出す。本プラグインは 2D ストリップのパスとして受け取る
            maps["ColorCorrectionLutDef"] = new DefMap
            {
                presetField = "colorCorrectionLut",
                renames = { { "converted3DLut", "lutTexturePath" } },
            };

            maps["CinematicBloomDef"] = new DefMap
            {
                presetField = "cinematicBloom",
                renames =
                {
                    { "bDirtTexture", "useDirtTexture" },
                    { "dirtTexture", "dirtTexturePath" },
                },
                ignored = { "maidMask", "enabledTransparentMode" },
            };

            maps["FilmicBloomDef"] = new DefMap
            {
                presetField = "filmicBloom",
                renames =
                {
                    { "bDirtTexture", "useDirtTexture" },
                    { "dirtTexture", "dirtTexturePath" },
                    { "streakthreshold", "streakThreshold" },
                    { "streaksoftKnee", "streakSoftKnee" },
                    { "streakstretch", "streakStretch" },
                    { "streakintensity", "streakIntensity" },
                    { "streaktint", "streakTint" },
                },
                ignored = { "maidMask", "enabledTransparentMode" },
            };

            // 移植先は Gradient を 2 色の線形補間へ整理してある。
            // Gradient 型そのものは移植元が保存しないので届かない
            maps["StylisticFogDef"] = new DefMap
            {
                presetField = "stylisticFog",
                renames =
                {
                    { "distanceGradientFirstColor", "distanceFirstColor" },
                    { "distanceGradientLastColor", "distanceLastColor" },
                    { "heightGradientFirstColor", "heightFirstColor" },
                    { "heightGradientLastColor", "heightLastColor" },
                    { "distanceFogColorSelectionType", "distanceColorSource" },
                    { "heightFogColorSelectionType", "heightColorSource" },
                    { "distanceColorRamp", "distanceRampPath" },
                    { "heightColorRamp", "heightRampPath" },
                },
            };
```

- [ ] **Step 2: ビルドする**

Run: `source\COM3D25.PostEffects.Plugin\build.bat debug`
Expected: エラー無しで終了

- [ ] **Step 3: 実機でリネームと値変換を検証する**

```csharp
COM3D25.PostEffects.Plugin.PresetManager.instance.ApplySceneCaptureXml(
    "<Preset><Effects>" +
    "<BloomDef><bloomIntensity>285</bloomIntensity><bloomThreshhold>0.4</bloomThreshhold>" +
    "<sepBlurSpread>2.5</sepBlurSpread><quality>1</quality><tweakMode>1</tweakMode></BloomDef>" +
    "<DepthOfFieldDef><blurType>1</blurType><dx11BokehThreshhold>0.9</dx11BokehThreshhold>" +
    "<focalLength>12</focalLength></DepthOfFieldDef>" +
    "<BokehDef><_focalrange>3.5</_focalrange></BokehDef>" +
    "</Effects></Preset>");
var s = COM3D25.PostEffects.Plugin.EffectSettings.instance;
$"{s.bloom.intensity} {s.bloom.threshold} {s.bloom.blurSpread} {s.bloom.highQuality} {s.depthOfField.useDX11Bokeh} {s.depthOfField.dx11BokehThreshold} {s.depthOfField.focalLength} {s.bokeh.focalRange}"
```
Expected: `2.85 0.4 2.5 True True 0.9 12 3.5`

```csharp
// 2.86 未満はそのまま
COM3D25.PostEffects.Plugin.PresetManager.instance.ApplySceneCaptureXml(
    "<Preset><Effects><BloomDef><bloomIntensity>2.1</bloomIntensity></BloomDef></Effects></Preset>");
COM3D25.PostEffects.Plugin.EffectSettings.instance.bloom.intensity
```
Expected: `2.1`

- [ ] **Step 4: コミット**

```bash
git add source/COM3D25.PostEffects.Plugin/Manager/SceneCaptureImporter.cs
git commit -m "feat(preset): リネーム・値変換が要る SceneCapture Def を対応表へ登録"
```

---

## Task 4: Vector3 の成分分解と SunShafts の光源位置

`Vector3` を 3 つのスカラーへばらす Def と、位置 `Transform` を持つ `SunShaftsDef` を追加する。

**Files:**
- Modify: `source/COM3D25.PostEffects.Plugin/Manager/SceneCaptureImporter.cs`

**Interfaces:**
- Consumes: Task 1 の `DefMap` / `TryParseVector3`
- Produces: `SceneCaptureImporter.SplitVector3(string suffixLessPrefix...)` に相当するヘルパ `MakeVector3Splitter(string xField, string yField, string zField)` → `Action<object, string>`。`_defMaps` に `IsolineDef` `NoiseAndGrainDef` `SunShaftsDef` のエントリ

- [ ] **Step 1: 成分分解ヘルパを追加する**

`SceneCaptureImporter.cs` の `TryParseVector3` の直後に追加:

```csharp
        /// <summary>
        /// Vector3 の値を 3 つのスカラーフィールドへばらす代入処理を作る。
        /// 移植先は軸や敷き詰め量を成分ごとのスライダーで持つため
        /// </summary>
        private static Action<object, string> MakeVector3Splitter(
            string xField, string yField, string zField)
        {
            return (setting, text) =>
            {
                Vector3 v;
                if (!TryParseVector3(text, out v))
                {
                    return;
                }

                // 表のフィールド名にタイプミスがあっても NullReference で
                // プリセット全体の適用を落とさない (Def 単位のスキップに留める)
                var type = setting.GetType();
                var x = type.GetField(xField, FieldFlags);
                var y = type.GetField(yField, FieldFlags);
                var z = type.GetField(zField, FieldFlags);
                if (x == null || y == null || z == null)
                {
                    MTEUtils.LogError(
                        "{0} に成分フィールドがありません: {1}/{2}/{3}",
                        type.Name, xField, yField, zField);
                    return;
                }

                x.SetValue(setting, v.x);
                y.SetValue(setting, v.y);
                z.SetValue(setting, v.z);
            };
        }
```

- [ ] **Step 2: `BuildDefMaps` に 3 Def を追加する**

`return maps;` の直前へ挿入:

```csharp
            // offset / modulationTime は移植先に対応するパラメータが無い
            maps["IsolineDef"] = new DefMap
            {
                presetField = "isoline",
                ignored = { "offset", "modulationTime" },
                custom =
                {
                    { "axis", MakeVector3Splitter("axisX", "axisY", "axisZ") },
                    { "direction", MakeVector3Splitter("directionX", "directionY", "directionZ") },
                    { "modulationAxis",
                        MakeVector3Splitter("modulationAxisX", "modulationAxisY", "modulationAxisZ") },
                },
            };

            // dx11Grain は未対応、intensities / filterMode / noiseTexture は移植先に対応が無い
            // (ノイズテクスチャは seed 固定のランタイム生成で代替している)
            maps["NoiseAndGrainDef"] = new DefMap
            {
                presetField = "noiseAndGrain",
                ignored = { "dx11Grain", "filterMode", "intensities", "noiseTexture" },
                custom =
                {
                    { "tiling", MakeVector3Splitter("tilingX", "tilingY", "tilingZ") },
                },
            };

            // 移植元がロード時に位置を復元する唯一の Transform フィールド。
            // 移植先はメインライト追従トグル + ワールド座標で持つので追従を切って座標を入れる
            maps["SunShaftsDef"] = new DefMap
            {
                presetField = "sunShafts",
                custom =
                {
                    { "sunTransform", (setting, text) =>
                        {
                            Vector3 v;
                            if (!TryParseVector3(text, out v)) return;
                            var s = (SunShaftsSetting)setting;
                            s.followMainLight = false;
                            s.sunPosX = v.x;
                            s.sunPosY = v.y;
                            s.sunPosZ = v.z;
                        }
                    },
                },
            };
```

- [ ] **Step 3: ビルドする**

Run: `source\COM3D25.PostEffects.Plugin\build.bat debug`
Expected: エラー無しで終了

- [ ] **Step 4: 実機で検証する**

```csharp
COM3D25.PostEffects.Plugin.PresetManager.instance.ApplySceneCaptureXml(
    "<Preset><Effects>" +
    "<IsolineDef><_axis>0,1,0</_axis><_direction>1,0,0</_direction><_interval>0.5</_interval></IsolineDef>" +
    "<NoiseAndGrainDef><tiling>64,32,16</tiling><generalIntensity>0.7</generalIntensity></NoiseAndGrainDef>" +
    "<SunShaftsDef><sunTransform>1.5,2.5,-3.5</sunTransform><sunShaftIntensity>1.2</sunShaftIntensity></SunShaftsDef>" +
    "</Effects></Preset>");
var s = COM3D25.PostEffects.Plugin.EffectSettings.instance;
$"{s.isoline.axisX},{s.isoline.axisY},{s.isoline.axisZ} {s.isoline.directionX} {s.isoline.interval} {s.noiseAndGrain.tilingX},{s.noiseAndGrain.tilingY},{s.noiseAndGrain.tilingZ} {s.sunShafts.followMainLight} {s.sunShafts.sunPosX},{s.sunShafts.sunPosY},{s.sunShafts.sunPosZ}"
```
Expected: `0,1,0 1 0.5 64,32,16 False 1.5,2.5,-3.5`

- [ ] **Step 5: コミット**

```bash
git add source/COM3D25.PostEffects.Plugin/Manager/SceneCaptureImporter.cs
git commit -m "feat(preset): SceneCapture の Vector3 分解と光源位置の取り込みに対応"
```

---

## Task 5: カーブを持つ Def

`ColorCorrectionCurvesDef` と `TonemappingColorGradingDef` を追加する。移植元のカーブ文字列は
`outTangent0,value0,inTangent1,value1` の 4 値で、時刻 0 と 1 の 2 キー固定（`Util.ConvertStringToAnimationCurve`）。

**Files:**
- Modify: `source/COM3D25.PostEffects.Plugin/Manager/SceneCaptureImporter.cs`

**Interfaces:**
- Consumes: Task 1 の `DefMap` / `TryParseFloat`、`COM3D2.MotionTimelineEditor.CurveData` / `CurveKeyData`
- Produces: `MakeCurveSetter(string fieldName)` → `Action<object, string>`。`_defMaps` に `ColorCorrectionCurvesDef` `TonemappingColorGradingDef` のエントリ

- [ ] **Step 1: カーブ代入ヘルパを追加する**

`MakeVector3Splitter` の直後に追加:

```csharp
        /// <summary>
        /// 移植元のカーブ文字列を CurveData へ変換する代入処理を作る。
        /// 書式は "outTangent0,value0,inTangent1,value1" で、時刻 0 と 1 の 2 キー固定
        /// (移植元 Util.ConvertStringToAnimationCurve と同じ組み立て)
        /// </summary>
        private static Action<object, string> MakeCurveSetter(string fieldName)
        {
            return (setting, text) =>
            {
                var parts = text.Split(',');
                if (parts.Length != 4)
                {
                    return;
                }

                var values = new float[4];
                for (var i = 0; i < 4; i++)
                {
                    if (!TryParseFloat(parts[i], out values[i]))
                    {
                        return;
                    }
                }

                var curve = new CurveData
                {
                    keys = new List<CurveKeyData>
                    {
                        new CurveKeyData
                        {
                            time = 0f, value = values[1], inTangent = 0f, outTangent = values[0],
                        },
                        new CurveKeyData
                        {
                            time = 1f, value = values[3], inTangent = values[2], outTangent = 0f,
                        },
                    },
                };

                var field = setting.GetType().GetField(fieldName, FieldFlags);
                if (field != null)
                {
                    field.SetValue(setting, curve);
                }
            };
        }
```

- [ ] **Step 2: `BuildDefMaps` に 2 Def を追加する**

`return maps;` の直前へ挿入:

```csharp
            // mode / updateTextures は移植先が持たない (深度補正の可否は useDepthCorrection 側)
            maps["ColorCorrectionCurvesDef"] = new DefMap
            {
                presetField = "colorCorrectionCurves",
                ignored = { "mode", "updateTextures" },
                custom =
                {
                    { "redChannel", MakeCurveSetter("redCurve") },
                    { "greenChannel", MakeCurveSetter("greenCurve") },
                    { "blueChannel", MakeCurveSetter("blueCurve") },
                    { "depthRedChannel", MakeCurveSetter("depthRedCurve") },
                    { "depthGreenChannel", MakeCurveSetter("depthGreenCurve") },
                    { "depthBlueChannel", MakeCurveSetter("depthBlueCurve") },
                    { "zCurve", MakeCurveSetter("zCurve") },
                },
            };

            // precision は LUT サイズ固定で参照されないデッドコード、
            // ShowDebug 系と minSizePerWheel / maxSizePerWheel / color は UI 描画用
            maps["TonemappingColorGradingDef"] = new DefMap
            {
                presetField = "tonemappingColorGrading",
                renames =
                {
                    { "EyeAdaptationEnabled", "eyeAdaptationEnabled" },
                    { "TonemappingEnabled", "tonemappingEnabled" },
                    { "LUTEnabled", "userLutEnabled" },
                    { "contribution", "userLutContribution" },
                    { "texture", "userLutPath" },
                },
                ignored =
                {
                    "precision", "eyeAdaptationShowDebug", "showDebug",
                    "minSizePerWheel", "maxSizePerWheel", "color",
                },
                custom =
                {
                    { "tonemappingCurve", MakeCurveSetter("tonemappingCurve") },
                    { "masterCurve", MakeCurveSetter("masterCurve") },
                    { "redCurve", MakeCurveSetter("redCurve") },
                    { "greenCurve", MakeCurveSetter("greenCurve") },
                    { "blueCurve", MakeCurveSetter("blueCurve") },
                },
            };
```

- [ ] **Step 3: ビルドする**

Run: `source\COM3D25.PostEffects.Plugin\build.bat debug`
Expected: エラー無しで終了

- [ ] **Step 4: 実機で検証する**

```csharp
COM3D25.PostEffects.Plugin.PresetManager.instance.ApplySceneCaptureXml(
    "<Preset><Effects>" +
    "<ColorCorrectionCurvesDef><redChannel>1.2,0.1,0.8,0.9</redChannel>" +
    "<saturation>1.5</saturation><useDepthCorrection>False</useDepthCorrection></ColorCorrectionCurvesDef>" +
    "<TonemappingColorGradingDef><_LUTEnabled>True</_LUTEnabled><_contribution>0.6</_contribution>" +
    "<_masterCurve>1,0,1,1</_masterCurve><_saturation>1.3</_saturation></TonemappingColorGradingDef>" +
    "</Effects></Preset>");
var s = COM3D25.PostEffects.Plugin.EffectSettings.instance;
var k = s.colorCorrectionCurves.redCurve.keys;
$"{s.colorCorrectionCurves.saturation} {k.Count} {k[0].value},{k[0].outTangent} {k[1].value},{k[1].inTangent} {s.tonemappingColorGrading.userLutEnabled} {s.tonemappingColorGrading.userLutContribution} {s.tonemappingColorGrading.saturation}"
```
Expected: `1.5 2 0.1,1.2 0.9,0.8 True 0.6 1.3`

- [ ] **Step 5: コミット**

```bash
git add source/COM3D25.PostEffects.Plugin/Manager/SceneCaptureImporter.cs
git commit -m "feat(preset): SceneCapture のカーブ付き Def の取り込みに対応"
```

---

## Task 6: enum の値対応を検証する

移植元は enum を int で保存する。移植の過程で並び順が変わっていると、無言で別の値が入る。
宣言を 1 つずつ突き合わせ、ずれていれば変換を入れる。

**Files:**
- Read: `W:\COM3D2_5\work\COM3D2.SceneCapture.Plugin\COM3D2.SceneCapture.Plugin\CM3D2\SceneCapture\Plugin\*.cs`
- Modify（ずれが見つかった場合のみ）: `source/COM3D25.PostEffects.Plugin/Manager/SceneCaptureImporter.cs`
- Modify: `docs/scenecapture-ui-diff.md`

**Interfaces:**
- Consumes: Task 3 で作った `custom` の書き方（int → 値変換）
- Produces: 検証結果の記録。ずれがあれば該当 Def の `custom` エントリ

- [ ] **Step 1: 移植元の enum 宣言を抜き出す**

Run:
```bash
cd /w/COM3D2_5/work/COM3D2.SceneCapture.Plugin && grep -rn "enum " --include=*.cs -A 12 . | grep -vE "^\S+-\s*//" > /tmp/sc-enums.txt; wc -l /tmp/sc-enums.txt
```
Expected: 行数が出力される（数百行）

- [ ] **Step 2: 移植先の enum 宣言を抜き出す**

Run:
```bash
cd /w/COM3D2_5/work/COM3D2.PostEffects.Plugin/source/COM3D25.PostEffects.Plugin && grep -rn "enum " --include=*.cs -A 12 Effects/ > /tmp/pe-enums.txt; wc -l /tmp/pe-enums.txt
```
Expected: 行数が出力される

- [ ] **Step 3: 下記の組を 1 つずつ突き合わせる**

移植先の型と移植元の型（メンバの並び順が同一かを見る。ゲーム側 firstpass の共有型は定義が同一なので確認だけでよい）:

| 移植先 | 移植元 | 備考 |
|---|---|---|
| `AAMode`（`AntialiasingSetting.mode`） | `AntialiasingAsPostEffect.AAMode` | ゲーム側の共有型 |
| `BloomEffect.HDRBloomMode` / `BloomScreenBlendMode` / `LensFlareStyle`（`BloomSetting`） | `BloomSC` の同名 enum | 移植先はゲーム側 `PostEffects_Dummy.Bloom` |
| `TiltShiftHdr.TiltShiftMode` / `TiltShiftQuality` | 同名 | ゲーム側の共有型 |
| `DepthOfFieldEffect.BlurSampleCount`（`DepthOfFieldSetting.blurSampleCount`） | `DepthOfField.BlurSampleCount` | |
| `CinematicDof.TweakMode` / `QualityPreset` / `ApertureShape` | `CinematicDepthOfField` の同名 enum | |
| `BokehEffect.KernelSize` | `Bokeh.KernelSize` / `FilmicBokeh.KernelSize` | `FilmicBokehSetting.kernelSize` も同型か確認 |
| `ObscuranceEffect.SampleCount` / `OcclusionSource` | `Obscurance` の同名 enum | |
| `RampEffect.BlendMode` / `StreakEffect.BlendMode` / `FilmicBloomEffect.BlendMode` | `Ramp` / `Streak` / `FilmicBloom` の `BlendMode` | |
| `IsolineEffect.ModulationMode` | `Isoline.ModulationMode` | |
| `SunShaftsResolution` / `ShaftsScreenBlendMode` | `SunShafts` の同名 enum | ゲーム側の共有型 |
| `EdgeDetectEffect.EdgeDetectMode` | `EdgeDetectEffectNormals.EdgeDetectMode` | 移植先は自前実装なので要注意 |
| `StylisticFogEffect.ColorSource` | `StylisticFog.ColorSelectionType` | 名前が違うので特に要注意 |
| `TonemappingEffect.Tonemapper` | `TonemappingColorGrading.Tonemapper` | |
| `FilmicMedianFilterEffect.FilterQuality` | `FilmicMedianFilter.FilterQuality` | |

- [ ] **Step 4: ずれた enum に変換を入れる**

並び順が違う enum が見つかったら、該当 Def の `custom` に int の読み替えを足す。書き方の雛形（`StylisticFogDef` の `distanceFogColorSelectionType` がずれていた場合の例）:

```csharp
                    { "distanceFogColorSelectionType", (setting, text) =>
                        {
                            int value;
                            if (!TryParseInt(text, out value)) return;
                            // 移植元 ColorSelectionType の並びが移植先 ColorSource と違うため読み替える
                            StylisticFogEffect.ColorSource source;
                            switch (value)
                            {
                                case 0: source = StylisticFogEffect.ColorSource.Gradient; break;
                                case 1: source = StylisticFogEffect.ColorSource.CopyOther; break;
                                default: return;
                            }
                            ((StylisticFogSetting)setting).distanceColorSource = source;
                        }
                    },
```

読み替えを足した場合は、その Def の `renames` から同じ子要素名のエントリを外す（`custom` が優先されるので害は無いが、二重定義は紛らわしい）。

ずれが無ければコード変更は不要。

- [ ] **Step 5: 検証結果を `docs/scenecapture-ui-diff.md` に記録する**

ファイル末尾に節を追加する。`（ここに Step 3 の結果を書く）` の部分は実際の確認結果に置き換えること。

```markdown
## 7. SceneCapture プリセットの取り込み

SceneEditor 経由で SceneCapture プリセットの `<Effects>` を適用できる
（`Manager/SceneCaptureImporter.cs`、契約は `ScenePresetProvider.ApplySceneCaptureXml`）。
対応の詳細は `docs/superpowers/specs/2026-08-16-scenecapture-import-design.md` を参照。

### enum の値対応

移植元は enum を int 値で保存するため、移植先との並び順を全件突き合わせた。

（ここに Step 3 の結果を書く。ずれが無ければ「全 20 種で並び順が一致。読み替えは不要」等）

### 取り込めない項目

- `CinematicBloomLayerDef` — 移植不可（EffectMask 依存）
- `MaidHideDef` / `SepiaDef` / `AnalogGlitchDef` / `DigitalGlitchDef` の各パラメータ —
  移植元がパラメータを public プロパティで持つため、`SaveDef`（public フィールドのみ走査）が
  値を書き出さない。有効化のみ取り込める
- `vignetteCenter`（LensAberrations）/ `center`（FilmicLetterBox）/ Gradient 型（StylisticFog）—
  移植元の `SerializeStatic.ALLOWED_TYPES` に `Vector2` / `Gradient` が無く保存されない
- ピント用 Transform（`focalTransform` / `_pointOfFocus` / `_focusTransform`）—
  移植元もロード時に読み戻さない（`SunShaftsDef.sunTransform` のみ例外的に復元される）
- `_maidMask` / `_enabledTransparentMode` / `_ambientOnly` / `_debug` / `tweakMode` /
  `_prefilterBlur` / `_dilateNearBlur` / `_depthCutoff` 系 / `dx11Grain` / `intensities` —
  移植時に落とした項目、または移植元でもデッドコードの項目
```

- [ ] **Step 6: ビルドしてコミット**

Run: `source\COM3D25.PostEffects.Plugin\build.bat debug`
Expected: エラー無しで終了

```bash
git add source/COM3D25.PostEffects.Plugin/Manager/SceneCaptureImporter.cs docs/scenecapture-ui-diff.md
git commit -m "fix(preset): SceneCapture の enum 値対応を検証して記録"
```

---

## Task 7: 実プリセットでの全件スイープと実機確認

手元の SceneCapture プリセット全件を通し、未解決要素が残らないことと、実際に絵が出ることを確認する。

**Files:**
- Modify（不足が見つかった場合のみ）: `source/COM3D25.PostEffects.Plugin/Manager/SceneCaptureImporter.cs`

**Interfaces:**
- Consumes: `SceneCaptureImporter.Parse(string, out List<string>)`

- [ ] **Step 1: プリセットの所在を確認する**

Run: `ls "/w/COM3D2/Sybaris/UnityInjector/Config/SceneCapture/Presets"`
Expected: `*.xml` が 20 件以上並ぶ

ゲーム側 Config（`%COM3D25_DIR%\Sybaris\UnityInjector\Config\SceneCapture\Presets`）に無い場合は、
上記からコピーしておく（SceneEditor も同じ場所を見る）。

- [ ] **Step 2: 全件スイープを実機で流す**

`eval_csharp` で以下を評価する。

```csharp
var dir = System.IO.Path.Combine(COM3D25.PostEffects.Plugin.PluginUtils.UserDataPath, "SceneCapture\\Presets");
var lines = new System.Collections.Generic.List<string>();
foreach (var path in System.IO.Directory.GetFiles(dir, "*.xml"))
{
    try
    {
        System.Collections.Generic.List<string> unresolved;
        COM3D25.PostEffects.Plugin.SceneCaptureImporter.Parse(System.IO.File.ReadAllText(path), out unresolved);
        if (unresolved.Count > 0)
            lines.Add(System.IO.Path.GetFileName(path) + ": " + string.Join(", ", unresolved.ToArray()));
    }
    catch (System.Exception e) { lines.Add(System.IO.Path.GetFileName(path) + ": EX " + e.Message); }
}
lines.Count + "\n" + string.Join("\n", lines.ToArray())
```
Expected: `0`（未解決ゼロ）

- [ ] **Step 3: 未解決が残っていたら対応表を直す**

出力された `Def名/要素名` を見て、`SceneCaptureImporter.BuildDefMaps` に `renames` か `ignored` を足す。
対応する設定が本プラグインに存在しない場合は `ignored` に入れ、その理由をコメントで書く。
直したらビルドし直して Step 2 をもう一度流し、`0` になるまで繰り返す。

- [ ] **Step 4: 代表プリセットを実機適用して絵を確認する**

Bloom / DepthOfField / TonemappingColorGrading / StylisticFog / Isoline を含むプリセットを選び、
SceneEditor のシーンプリセット一覧から SceneCapture 仮想フォルダ経由で読み込む。
本プラグインのウィンドウで各エフェクトが有効になり、値がプリセットどおりに入っていることを確認する。

`screenshot` で適用前後を撮り、SceneCapture 本家で同じプリセットを読んだときと絵が大きく食い違わないことを見る。
特にカーブ（色補正）・テクスチャ（LUT / レンズダート）・Vector3 分解（Isoline の軸）を重点的に確認する。

- [ ] **Step 5: コミット**

```bash
git add -A source/COM3D25.PostEffects.Plugin docs
git commit -m "fix(preset): 実プリセットのスイープで見つかった SceneCapture 取り込みの漏れを補う"
```

（Step 3 で修正が無ければこのコミットは不要。その場合はスキップする）

---

## 完了後

実装が終わったら `code-review` スキルでレビューし、指摘を取り込んでから最終コミットする。

## レビュー却下メモ

- `FilmicBloomDef` に `streaktint`→`streakTint` のリネームが抜けている — 誤検知。Task 3 の `renames` に実在する
- enum の並び照合（Task 6）を Def 登録タスク（Task 2〜5）へ分散すべき — 却下。移植元ソースの enum 宣言を読む作業が 4 タスクに重複し、突き合わせ表も分断される。Task 7 の実機確認より前に置いた専用ゲートとして Task 6 のまま残す
- 同一 Def が XML 内に重複した場合の挙動が未記載 — 却下。移植元の `SaveDef` は Def ごとに 1 要素しか書き出さないため発生しない。発生しても後勝ちで実害が無い
