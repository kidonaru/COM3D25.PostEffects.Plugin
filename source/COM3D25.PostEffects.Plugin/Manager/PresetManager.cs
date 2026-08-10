using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using COM3D2.MotionTimelineEditor;

namespace COM3D25.PostEffects.Plugin
{
    public class PresetManager : ManagerBase
    {
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

                var serializer = new XmlSerializer(typeof(PostEffectsPreset));
                using (var stream = new FileStream(GetPresetPath(name), FileMode.Create))
                {
                    serializer.Serialize(stream, preset);
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
                        var serializer = new XmlSerializer(typeof(PostEffectsPreset));
                        using (var stream = new FileStream(path, FileMode.Open))
                        {
                            preset = (PostEffectsPreset)serializer.Deserialize(stream);
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
