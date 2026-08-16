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
    }
}
