using System.Collections.Generic;
using System.Xml.Serialization;
using COM3D2.MotionTimelineEditor;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    public enum KeyBindType
    {
        PluginToggle,
    }

    public class Config
    {
        public static readonly int CurrentVersion = 1;

        [XmlAttribute]
        public int version = 0;

        // 動作設定
        public bool pluginEnabled = true;
        public float keyRepeatTimeFirst = 0.15f;
        public float keyRepeatTime = 1f / 30f;
        public bool useHSVColor = false;

        // 表示設定
        public int mainWindowPosX = -1;
        public int mainWindowPosY = -1;
        public int mainWindowWidth = 400;
        public int mainWindowHeight = 600;

        // 起動時に読み込むプリセット名。既定は固定プリセット
        public string startupPresetName = PresetManager.DefaultPresetName;

        // 色設定
        public Color windowHoverColor = new Color(48 / 255f, 48 / 255f, 48 / 255f, 224 / 255f);

        // エフェクト設定は config.xml に永続化しない (EffectSettings が保持し、プリセットが永続化を担う)

        [XmlIgnore]
        public Dictionary<KeyBindType, KeyBind> keyBinds = new Dictionary<KeyBindType, KeyBind>
        {
            { KeyBindType.PluginToggle, new KeyBind("Alt+P") },
        };

        public struct KeyBindPair
        {
            public KeyBindType key;
            public string value;
        }

        [XmlElement("keyBind")]
        public KeyBindPair[] keyBindsXml
        {
            get
            {
                var result = new List<KeyBindPair>(keyBinds.Count);
                foreach (var pair in keyBinds)
                {
                    result.Add(new KeyBindPair { key = pair.Key, value = pair.Value.ToString() });
                }
                return result.ToArray();
            }
            set
            {
                if (value == null)
                {
                    return;
                }

                foreach (var pair in value)
                {
                    keyBinds[pair.key] = new KeyBind(pair.value);
                }
            }
        }

        [XmlIgnore]
        public bool dirty = false;

        public void ConvertVersion()
        {
            version = CurrentVersion;
        }

        public bool GetKey(KeyBindType keyBindType)
        {
            return keyBinds[keyBindType].GetKey();
        }

        public bool GetKeyDown(KeyBindType keyBindType)
        {
            return keyBinds[keyBindType].GetKeyDown();
        }

        public bool GetKeyUp(KeyBindType keyBindType)
        {
            return keyBinds[keyBindType].GetKeyUp();
        }

        public string GetKeyName(KeyBindType keyBindType)
        {
            return keyBinds[keyBindType].ToString();
        }
    }
}
