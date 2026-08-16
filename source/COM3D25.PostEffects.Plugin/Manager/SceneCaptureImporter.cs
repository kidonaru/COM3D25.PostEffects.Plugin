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
