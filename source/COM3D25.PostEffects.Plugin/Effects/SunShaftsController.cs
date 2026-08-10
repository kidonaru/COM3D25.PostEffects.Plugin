using COM3D2.MotionTimelineEditor;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    public class SunShaftsSetting
    {
        public bool enabled = false;
        public SunShaftsResolution resolution = SunShaftsResolution.Normal;
        public ShaftsScreenBlendMode screenBlendMode = ShaftsScreenBlendMode.Screen;
        public bool useDepthTexture = true;
        public Color sunColor = Color.white;
        public float maxRadius = 0.75f;
        public float sunShaftBlurRadius = 2.5f;
        public int radialBlurIterations = 2;
        public float sunShaftIntensity = 1.15f;
        public float useSkyBoxAlpha = 0.75f;

        // 光源の位置。追従時はメインライトの向きとカメラからの距離で決まる
        public bool followMainLight = true;
        public float lightDistance = 50f;
        public float sunPosX = 0f;
        public float sunPosY = 20f;
        public float sunPosZ = 0f;
    }

    // SunShafts はゲーム側 Assembly-UnityScript-firstpass の実装をそのまま使う
    // (シェーダーフィールドは実行時追加では null のため imageeffects バンドルから補う)
    public class SunShaftsController : EffectControllerBase<SunShafts, SunShaftsSetting>
    {
        public override string effectName => "光芒";

        // 光源位置を渡すためだけの空オブジェクト。カメラの子にしてシーン遷移で一緒に破棄させる
        private Transform _sunAnchor;
        // ゲーム側が元々指していた光源 (設定クラスには Transform を持てないので別途保持する)
        private Transform _capturedSunTransform;

        protected override SunShaftsSetting setting
        {
            get => settings.sunShafts;
            set => settings.sunShafts = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        protected override void ApplySetting(SunShafts component)
        {
            if (component.sunShaftsShader == null)
            {
                component.sunShaftsShader = EffectShaders.GetShader(EffectShaders.ImageEffects, "sunshaftsshader");
            }
            if (component.simpleClearShader == null)
            {
                component.simpleClearShader = EffectShaders.GetShader(EffectShaders.ImageEffects, "simpleclearshader");
            }

            component.sunTransform = GetSunAnchor(component);
            component.resolution = setting.resolution;
            component.screenBlendMode = setting.screenBlendMode;
            component.useDepthTexture = setting.useDepthTexture;
            component.sunColor = setting.sunColor;
            component.maxRadius = setting.maxRadius;
            component.sunShaftBlurRadius = setting.sunShaftBlurRadius;
            component.radialBlurIterations = setting.radialBlurIterations;
            component.sunShaftIntensity = setting.sunShaftIntensity;
            component.useSkyBoxAlpha = setting.useSkyBoxAlpha;
        }

        private Transform GetSunAnchor(SunShafts component)
        {
            if (_sunAnchor == null)
            {
                var go = new GameObject("PostEffectsSunAnchor");
                go.hideFlags = HideFlags.HideAndDontSave;
                go.transform.SetParent(component.transform, false);
                _sunAnchor = go.transform;
            }

            var mainLight = setting.followMainLight ? GameMain.Instance.MainLight : null;
            if (mainLight != null)
            {
                // 平行光源なので位置ではなく向きが光源方向。カメラから逆向きに離した点を光源とみなす
                _sunAnchor.position =
                    component.transform.position - mainLight.transform.forward * setting.lightDistance;
            }
            else
            {
                _sunAnchor.position = new Vector3(setting.sunPosX, setting.sunPosY, setting.sunPosZ);
            }
            return _sunAnchor;
        }

        protected override void Capture(SunShafts component)
        {
            var c = _capturedSetting;
            _capturedEnabled = component.enabled;
            _capturedSunTransform = component.sunTransform;
            c.resolution = component.resolution;
            c.screenBlendMode = component.screenBlendMode;
            c.useDepthTexture = component.useDepthTexture;
            c.sunColor = component.sunColor;
            c.maxRadius = component.maxRadius;
            c.sunShaftBlurRadius = component.sunShaftBlurRadius;
            c.radialBlurIterations = component.radialBlurIterations;
            c.sunShaftIntensity = component.sunShaftIntensity;
            c.useSkyBoxAlpha = component.useSkyBoxAlpha;
        }

        // 自前で作った光源オブジェクトは Restore の対象外なので、ここで畳む
        public override void Restore()
        {
            base.Restore();

            if (_sunAnchor != null)
            {
                Object.Destroy(_sunAnchor.gameObject);
                _sunAnchor = null;
            }
        }

        protected override void RestoreSetting(SunShafts component)
        {
            var c = _capturedSetting;
            component.enabled = _capturedEnabled;
            component.sunTransform = _capturedSunTransform;
            component.resolution = c.resolution;
            component.screenBlendMode = c.screenBlendMode;
            component.useDepthTexture = c.useDepthTexture;
            component.sunColor = c.sunColor;
            component.maxRadius = c.maxRadius;
            component.sunShaftBlurRadius = c.sunShaftBlurRadius;
            component.radialBlurIterations = c.radialBlurIterations;
            component.sunShaftIntensity = c.sunShaftIntensity;
            component.useSkyBoxAlpha = c.useSkyBoxAlpha;
        }

        private readonly GUIComboBox<SunShaftsResolution> _resolutionComboBox =
            new GUIComboBox<SunShaftsResolution>
            {
                items = MTEUtils.GetEnumValues<SunShaftsResolution>(),
                getName = (resolution, _) => resolution.ToString(),
                buttonSize = new Vector2(100, 20),
            };

        private readonly GUIComboBox<ShaftsScreenBlendMode> _blendModeComboBox =
            new GUIComboBox<ShaftsScreenBlendMode>
            {
                items = MTEUtils.GetEnumValues<ShaftsScreenBlendMode>(),
                getName = (mode, _) => mode.ToString(),
                buttonSize = new Vector2(100, 20),
            };

        public override void DrawContent(GUIView view)
        {
            // 光芒は遮蔽物のない画素 (空・遠景) からしか出ないため、屋内背景では何も起きない
            view.DrawLabel("※ 空が映っていないと光芒は出ません", -1, 20, Color.yellow);

            view.DrawToggle("メインライトに追従", setting.followMainLight, 250, 20, value =>
            {
                setting.followMainLight = value;
                SetDirty();
            });

            if (setting.followMainLight)
            {
                DrawSlider(view, "光源までの距離", 1f, 200f, 50f, setting.lightDistance,
                    v => setting.lightDistance = v);
            }
            else
            {
                DrawSlider(view, "光源 X", -100f, 100f, 0f, setting.sunPosX, v => setting.sunPosX = v);
                DrawSlider(view, "光源 Y", -100f, 100f, 20f, setting.sunPosY, v => setting.sunPosY = v);
                DrawSlider(view, "光源 Z", -100f, 100f, 0f, setting.sunPosZ, v => setting.sunPosZ = v);
            }

            view.DrawHorizontalLine(Color.gray);

            view.DrawColor(
                view.GetColorFieldCache("光の色", false),
                setting.sunColor,
                Color.white,
                color => { setting.sunColor = color; SetDirty(); });

            DrawSlider(view, "強度", 0f, 20f, 1.15f, setting.sunShaftIntensity, v => setting.sunShaftIntensity = v);
            DrawSlider(view, "広がり半径", 0f, 1f, 0.75f, setting.maxRadius, v => setting.maxRadius = v);
            DrawSlider(view, "ぼかし半径", 0f, 40f, 2.5f, setting.sunShaftBlurRadius,
                v => setting.sunShaftBlurRadius = v);

            // ゲーム側で 1〜4 にクランプされるため UI もその範囲に合わせる
            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "ぼかし回数",
                labelWidth = 100,
                width = -1,
                fieldType = FloatFieldType.Int,
                min = 1,
                max = 4,
                step = 1,
                defaultValue = 2,
                value = setting.radialBlurIterations,
                onChanged = value => { setting.radialBlurIterations = (int)value; SetDirty(); },
            });

            view.BeginHorizontal();
            {
                view.DrawLabel("解像度", 80, 20);
                _resolutionComboBox.currentIndex = (int)setting.resolution;
                _resolutionComboBox.onSelected = (resolution, _) => { setting.resolution = resolution; SetDirty(); };
                _resolutionComboBox.DrawButton(view);
            }
            view.EndLayout();

            view.BeginHorizontal();
            {
                view.DrawLabel("合成方法", 80, 20);
                _blendModeComboBox.currentIndex = (int)setting.screenBlendMode;
                _blendModeComboBox.onSelected = (mode, _) => { setting.screenBlendMode = mode; SetDirty(); };
                _blendModeComboBox.DrawButton(view);
            }
            view.EndLayout();

            // OFF だと遮蔽判定をスカイボックスのアルファで行う。空の無いシーンでは光芒が出ない
            view.DrawToggle("深度で遮蔽判定", setting.useDepthTexture, 250, 20, value =>
            {
                setting.useDepthTexture = value;
                SetDirty();
            });

            if (!setting.useDepthTexture)
            {
                DrawSlider(view, "スカイボックス透過", 0f, 1f, 0.75f, setting.useSkyBoxAlpha,
                    v => setting.useSkyBoxAlpha = v);
            }
        }
    }
}
