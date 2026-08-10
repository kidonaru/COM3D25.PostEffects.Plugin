using COM3D2.MotionTimelineEditor;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    public class AntialiasingSetting
    {
        public bool enabled = false;
        public AAMode mode = AAMode.FXAA3Console;
        public float edgeThresholdMin = 0.05f;
        public float edgeThreshold = 0.2f;
        public float edgeSharpness = 4f;
        public float offsetScale = 0.2f;
        public float blurRadius = 18f;
        public bool showGeneratedNormals = false;
        public bool dlaaSharp = false;
    }

    // AntialiasingAsPostEffect はゲーム側 Assembly-UnityScript-firstpass の実装をそのまま使う
    // (シェーダーフィールドは実行時追加では null のため imageeffects バンドルから補う)
    public class AntialiasingController : EffectControllerBase<AntialiasingAsPostEffect, AntialiasingSetting>
    {
        public override string effectName => "アンチエイリアス";

        protected override AntialiasingSetting setting
        {
            get => settings.antialiasing;
            set => settings.antialiasing = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        protected override void ApplySetting(AntialiasingAsPostEffect component)
        {
            if (component.ssaaShader == null)
            {
                component.ssaaShader = EffectShaders.GetShader(EffectShaders.ImageEffects, "ssaashader");
                component.dlaaShader = EffectShaders.GetShader(EffectShaders.ImageEffects, "dlaashader");
                component.nfaaShader = EffectShaders.GetShader(EffectShaders.ImageEffects, "nfaashader");
                component.shaderFXAAPreset2 = EffectShaders.GetShader(EffectShaders.ImageEffects, "shaderfxaapreset2");
                component.shaderFXAAPreset3 = EffectShaders.GetShader(EffectShaders.ImageEffects, "shaderfxaapreset3");
                component.shaderFXAAII = EffectShaders.GetShader(EffectShaders.ImageEffects, "shaderfxaaii");
                component.shaderFXAAIII = EffectShaders.GetShader(EffectShaders.ImageEffects, "shaderfxaaiii");
            }

            // CheckResources が ssaaShader を無条件に参照するため、取れないまま有効にすると毎フレーム落ちる
            if (component.ssaaShader == null)
            {
                component.enabled = false;
                return;
            }

            component.mode = setting.mode;
            component.edgeThresholdMin = setting.edgeThresholdMin;
            component.edgeThreshold = setting.edgeThreshold;
            component.edgeSharpness = setting.edgeSharpness;
            component.offsetScale = setting.offsetScale;
            component.blurRadius = setting.blurRadius;
            component.showGeneratedNormals = setting.showGeneratedNormals;
            component.dlaaSharp = setting.dlaaSharp;
        }

        protected override void Capture(AntialiasingAsPostEffect component)
        {
            var c = _capturedSetting;
            _capturedEnabled = component.enabled;
            c.mode = component.mode;
            c.edgeThresholdMin = component.edgeThresholdMin;
            c.edgeThreshold = component.edgeThreshold;
            c.edgeSharpness = component.edgeSharpness;
            c.offsetScale = component.offsetScale;
            c.blurRadius = component.blurRadius;
            c.showGeneratedNormals = component.showGeneratedNormals;
            c.dlaaSharp = component.dlaaSharp;
        }

        protected override void RestoreSetting(AntialiasingAsPostEffect component)
        {
            var c = _capturedSetting;
            component.enabled = _capturedEnabled;
            component.mode = c.mode;
            component.edgeThresholdMin = c.edgeThresholdMin;
            component.edgeThreshold = c.edgeThreshold;
            component.edgeSharpness = c.edgeSharpness;
            component.offsetScale = c.offsetScale;
            component.blurRadius = c.blurRadius;
            component.showGeneratedNormals = c.showGeneratedNormals;
            component.dlaaSharp = c.dlaaSharp;
        }

        private readonly GUIComboBox<AAMode> _modeComboBox =
            new GUIComboBox<AAMode>
            {
                items = MTEUtils.GetEnumValues<AAMode>(),
                getName = (mode, _) => mode.ToString(),
                buttonSize = new Vector2(160, 20),
            };

        public override void DrawContent(GUIView view)
        {
            view.BeginHorizontal();
            {
                view.DrawLabel("方式", 80, 20);
                _modeComboBox.currentIndex = (int)setting.mode;
                _modeComboBox.onSelected = (mode, _) => { setting.mode = mode; SetDirty(); };
                _modeComboBox.DrawButton(view);
            }
            view.EndLayout();

            // 方式ごとに参照されるパラメータが違うため、効くものだけ出す
            switch (setting.mode)
            {
                case AAMode.FXAA3Console:
                    DrawSlider(view, "エッジ下限", 0f, 1f, 0.05f, setting.edgeThresholdMin,
                        v => setting.edgeThresholdMin = v);
                    DrawSlider(view, "エッジしきい値", 0f, 10f, 0.2f, setting.edgeThreshold,
                        v => setting.edgeThreshold = v);
                    DrawSlider(view, "エッジ強調", 0f, 10f, 4f, setting.edgeSharpness,
                        v => setting.edgeSharpness = v);
                    break;

                case AAMode.NFAA:
                    DrawSlider(view, "オフセット倍率", 0f, 10f, 0.2f, setting.offsetScale,
                        v => setting.offsetScale = v);
                    DrawSlider(view, "ぼかし半径", 0f, 50f, 18f, setting.blurRadius,
                        v => setting.blurRadius = v);
                    view.DrawToggle("生成法線を表示", setting.showGeneratedNormals, 250, 20, value =>
                    {
                        setting.showGeneratedNormals = value;
                        SetDirty();
                    });
                    break;

                case AAMode.DLAA:
                    view.DrawToggle("シャープ版", setting.dlaaSharp, 250, 20, value =>
                    {
                        setting.dlaaSharp = value;
                        SetDirty();
                    });
                    break;
            }
        }
    }
}
