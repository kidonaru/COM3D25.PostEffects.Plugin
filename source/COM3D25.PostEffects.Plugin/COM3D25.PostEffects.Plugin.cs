using System;
using COM3D2.MotionTimelineEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityInjector;
using UnityInjector.Attributes;

namespace COM3D25.PostEffects.Plugin
{
    public class GUIOption : GUIOptionBase
    {
        public override float keyRepeatTimeFirst => config.keyRepeatTimeFirst;
        public override float keyRepeatTime => config.keyRepeatTime;
        public override bool useHSVColor
        {
            get => config.useHSVColor;
            set
            {
                config.useHSVColor = value;
                config.dirty = true;
            }
        }
        public override Color windowHoverColor => config.windowHoverColor;
        // UI のアクセントカラー（トグル・ボタン等の有効状態）をシアンにする
        public override Color accentColor => Color.cyan;
        public override Texture2D changeIcon => PluginResources.changeIcon;
        public override Texture2D favoriteOffIcon => null;
        public override Texture2D favoriteOnIcon => null;

        private static Config config => ConfigManager.instance.config;
    }

    [
        PluginFilter("COM3D2x64"),
        PluginName(PluginInfo.PluginFullName),
        PluginVersion(PluginInfo.PluginVersion)
    ]
    public class PostEffectsPlugin : PluginBase
    {
        private bool _isEnable = false;
        public bool isEnable
        {
            get => _isEnable;
            set
            {
                if (_isEnable == value)
                {
                    return;
                }

                _isEnable = value;
                UpdateGearMenu();

                if (value)
                {
                    OnPluginEnable();
                }
                else
                {
                    OnPluginDisable();
                }
            }
        }

        public static PostEffectsPlugin instance { get; private set; }

        private static ManagerRegistry managerRegistry => ManagerRegistry.instance;
        private static WindowManager windowManager => WindowManager.instance;
        private static ConfigManager configManager => ConfigManager.instance;
        private static Config config => ConfigManager.instance.config;
        private static PostEffectManager postEffectManager => PostEffectManager.instance;

        public PostEffectsPlugin()
        {
        }

        public void Awake()
        {
            GameObject.DontDestroyOnLoad(this);
            instance = this;
        }

        public void Start()
        {
            try
            {
                Initialize();
            }
            catch (Exception e)
            {
                MTEUtils.LogException(e);
            }
        }

        public void Update()
        {
            try
            {
                if (!config.pluginEnabled)
                {
                    return;
                }

                if (config.GetKeyDown(KeyBindType.PluginToggle))
                {
                    isEnable = !isEnable;
                }

                if (isEnable)
                {
                    managerRegistry.Update();
                }
            }
            catch (Exception e)
            {
                MTEUtils.LogException(e);
            }
        }

        public void LateUpdate()
        {
            try
            {
                if (!config.pluginEnabled)
                {
                    return;
                }

                // エフェクトはウィンドウ非表示 (isEnable=false) 中でも適用し続ける。
                // ゲーム側がカメラ設定を毎フレーム上書きするため LateUpdate で対抗する
                postEffectManager.LateUpdate();

                if (isEnable)
                {
                    managerRegistry.LateUpdate();
                }
            }
            catch (Exception e)
            {
                MTEUtils.LogException(e);
            }
        }

        public void OnChangedSceneLevel(Scene scene, LoadSceneMode sceneMode)
        {
            try
            {
                if (!config.pluginEnabled)
                {
                    return;
                }

                if (scene.name == "SceneTitle")
                {
                    this.isEnable = false;
                }

                // ギアメニューアイコンが未追加または破棄済みなら再追加する
                // （Unity の == オーバーロードにより破棄済みオブジェクトも null 扱いになる）
                if (gearMenuIcon == null)
                {
                    AddGearMenu();
                }

                managerRegistry.OnChangedSceneLevel(scene, sceneMode);
            }
            catch (Exception e)
            {
                MTEUtils.LogException(e);
            }
        }

        void OnApplicationQuit()
        {
            configManager.SaveConfigXml();
        }

        private void Initialize()
        {
            try
            {
                MTEUtils.Log("初期化中...");
                MTEUtils.LogDebug("Unity Version: " + Application.unityVersion);

                configManager.Init();

                GUIView.option = new GUIOption();

                if (!config.pluginEnabled)
                {
                    MTEUtils.Log("プラグインが無効になっています");
                    return;
                }

                SceneManager.sceneLoaded += OnChangedSceneLevel;

                // PostEffectManager はウィンドウ非表示中も動かすため registry には登録しない
                postEffectManager.Init();

                managerRegistry.RegisterManager(WindowManager.instance);
                managerRegistry.RegisterManager(ConfigManager.instance);
                managerRegistry.RegisterManager(PresetManager.instance);

                AddGearMenu();
            }
            catch (Exception e)
            {
                MTEUtils.LogException(e);
            }
        }

        GameObject gearMenuIcon = null;

        public void AddGearMenu()
        {
            // SysShortcut 生成前に呼ばれた場合は何もしない（シーンロード時に再試行される）
            if (!GearMenu.Buttons.IsReady)
            {
                return;
            }

            gearMenuIcon = GearMenu.Buttons.Add(
                PluginInfo.PluginName,
                PluginInfo.PluginName,
                PluginInfo.Icon,
                (go) =>
                {
                    isEnable = !isEnable;
                });
        }

        public void RemoveGearMenu()
        {
            if (gearMenuIcon != null)
            {
                GearMenu.Buttons.Remove(gearMenuIcon);
                gearMenuIcon = null;
            }
        }

        private void UpdateGearMenu()
        {
            if (gearMenuIcon != null)
            {
                GearMenu.Buttons.SetFrameColor(gearMenuIcon, isEnable ? Color.blue : Color.white);
            }
        }

        public void OnGUI()
        {
            try
            {
                if (isEnable)
                {
                    windowManager.OnGUI();
                }
            }
            catch (Exception e)
            {
                MTEUtils.LogException(e);
            }
        }

        public void OnLoad()
        {
            MTEUtils.LogDebug("PostEffectsPlugin.OnLoad");
            managerRegistry.OnLoad();
        }

        private void OnPluginEnable()
        {
            MTEUtils.Log("プラグインが有効になりました");
            OnLoad();
        }

        private void OnPluginDisable()
        {
            MTEUtils.Log("プラグインが無効になりました");
            managerRegistry.OnPluginDisable();
        }
    }
}
