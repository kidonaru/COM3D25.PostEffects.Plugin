using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using COM3D2.MotionTimelineEditor;

namespace COM3D25.PostEffects.Plugin
{
    public class PresetManager : ManagerBase
    {
        // XML 宣言の encoding を UTF-8 にするための StringWriter。
        // 既定の StringWriter は UTF-16 を名乗るため、UTF-8 で書き出される
        // シーンプリセットのサイドカーと食い違ってしまう
        private class Utf8StringWriter : StringWriter
        {
            public override Encoding Encoding
            {
                get { return Encoding.UTF8; }
            }
        }

        private static readonly XmlSerializer _serializer = new XmlSerializer(typeof(PostEffectsPreset));

        public static readonly string PresetDirPath =
            MTEUtils.CombinePaths(PluginUtils.UserDataPath, "PostEffects", "Presets");

        // ファイルを持たない固定プリセット。全エフェクト無効＋プラグイン既定値を表す。
        // 上書き・削除はできず、起動時プリセットの初期値になる
        public const string DefaultPresetName = "デフォルト";

        public static bool IsDefaultPreset(string name)
        {
            return name == DefaultPresetName;
        }

        private static PresetManager _instance = null;
        public static PresetManager instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new PresetManager();
                }
                return _instance;
            }
        }

        // 拡張子なしのプリセット名一覧 (先頭が固定プリセット、以降は自然順ソート)
        public List<string> presetNames = new List<string>();

        private PresetManager()
        {
        }

        public override void Init()
        {
            UpdatePresetNames();

            // 起動時は指定プリセットから始める (前回終了時の編集内容は引き継がない)
            LoadPreset(config.startupPresetName);
        }

        private static string GetPresetPath(string name)
        {
            return MTEUtils.CombinePaths(PresetDirPath, name + ".xml");
        }

        public void UpdatePresetNames()
        {
            presetNames.Clear();
            try
            {
                if (Directory.Exists(PresetDirPath))
                {
                    foreach (var path in Directory.GetFiles(PresetDirPath, "*.xml"))
                    {
                        var name = Path.GetFileNameWithoutExtension(path);
                        if (IsDefaultPreset(name))
                        {
                            // 固定プリセットと同名のファイル (旧バージョンで保存されたもの等) は
                            // 一覧を二重にしないため除外する。存在に気付けるよう警告を出す
                            MTEUtils.LogWarning(
                                "固定プリセットと同名のため一覧に出せません。改名してください: {0}", path);
                            continue;
                        }
                        presetNames.Add(name);
                    }
                    presetNames.Sort(new NaturalStringComparer());
                }
            }
            catch (Exception e)
            {
                MTEUtils.LogException(e);
            }

            presetNames.Insert(0, DefaultPresetName);
        }

        public bool SavePreset(string name)
        {
            if (string.IsNullOrEmpty(name) ||
                name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                MTEUtils.LogWarning("プリセット名が不正です: {0}", name);
                return false;
            }

            if (IsDefaultPreset(name))
            {
                MTEUtils.LogWarning("固定プリセットは上書きできません: {0}", name);
                return false;
            }

            try
            {
                Directory.CreateDirectory(PresetDirPath);

                var preset = new PostEffectsPreset();
                preset.CaptureFrom(EffectSettings.instance);

                using (var stream = new FileStream(GetPresetPath(name), FileMode.Create))
                {
                    _serializer.Serialize(stream, preset);
                }

                UpdatePresetNames();
                EffectSettings.instance.dirty = false;
                return true;
            }
            catch (Exception e)
            {
                MTEUtils.LogException(e);
                MTEUtils.LogError("プリセットの保存に失敗しました: {0}", name);
                return false;
            }
        }

        public bool LoadPreset(string name)
        {
            try
            {
                PostEffectsPreset preset;

                if (IsDefaultPreset(name))
                {
                    // 固定プリセットはファイルを持たず、初期値そのものを表す
                    preset = new PostEffectsPreset();
                }
                else
                {
                    var path = GetPresetPath(name);
                    if (File.Exists(path))
                    {
                        using (var stream = new FileStream(path, FileMode.Open))
                        {
                            preset = (PostEffectsPreset)_serializer.Deserialize(stream);
                        }
                    }
                    else
                    {
                        // 指定プリセットのファイルが見つからない場合は固定プリセットへ落とす
                        MTEUtils.LogWarning("プリセットが見つかりません: {0}", name);
                        preset = new PostEffectsPreset();
                    }
                }

                preset.ApplyTo(EffectSettings.instance);
                EffectSettings.instance.dirty = false;
                return true;
            }
            catch (Exception e)
            {
                MTEUtils.LogException(e);
                MTEUtils.LogError("プリセットの読み込みに失敗しました: {0}", name);
                return false;
            }
        }

        /// <summary>
        /// 現在のエフェクト設定をプリセットと同じ形式の XML 文字列で返す (失敗時は null)。
        /// プリセットファイルと同形式のため、シーンプリセットのサイドカーと相互流用できる
        /// </summary>
        public string CapturePresetXml()
        {
            try
            {
                var preset = new PostEffectsPreset();
                preset.CaptureFrom(EffectSettings.instance);

                using (var writer = new Utf8StringWriter())
                {
                    _serializer.Serialize(writer, preset);
                    return writer.ToString();
                }
            }
            catch (Exception e)
            {
                MTEUtils.LogException(e);
                MTEUtils.LogError("プリセットXMLの生成に失敗しました");
                return null;
            }
        }

        /// <summary>
        /// プリセット形式の XML 文字列を現在のエフェクト設定へ反映する (失敗時は false)
        /// </summary>
        public bool ApplyPresetXml(string xml)
        {
            if (string.IsNullOrEmpty(xml))
            {
                return false;
            }

            // 選択中のプリセットとは別経路で設定が変わるため、未保存扱いにする。
            // 途中で失敗しても一部だけ適用されている可能性があるため先に立てておく
            EffectSettings.instance.dirty = true;

            try
            {
                using (var reader = new StringReader(xml))
                {
                    var preset = (PostEffectsPreset)_serializer.Deserialize(reader);
                    preset.ApplyTo(EffectSettings.instance);
                }
                return true;
            }
            catch (Exception e)
            {
                MTEUtils.LogException(e);
                MTEUtils.LogError("プリセットXMLの適用に失敗しました");
                return false;
            }
        }

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

        public void DeletePreset(string name)
        {
            if (IsDefaultPreset(name))
            {
                MTEUtils.LogWarning("固定プリセットは削除できません: {0}", name);
                return;
            }

            try
            {
                var path = GetPresetPath(name);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                UpdatePresetNames();

                // 起動時プリセットが消えたまま残らないよう固定プリセットへ戻す
                if (config.startupPresetName == name)
                {
                    config.startupPresetName = DefaultPresetName;
                    config.dirty = true;
                }
            }
            catch (Exception e)
            {
                MTEUtils.LogException(e);
                MTEUtils.LogError("プリセットの削除に失敗しました: {0}", name);
            }
        }
    }
}
