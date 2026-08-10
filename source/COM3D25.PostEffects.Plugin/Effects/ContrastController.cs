using COM3D2.MotionTimelineEditor;

namespace COM3D25.PostEffects.Plugin
{
    public class ContrastSetting
    {
        public bool enabled = false;
        public float intensity = 0.5f;
        public float threshhold = 0f;
        public float blurSpread = 1f;
    }

    // ContrastEnhance はゲーム側 Assembly-UnityScript-firstpass の実装をそのまま使う
    // (シェーダーフィールドは実行時追加では null のため imageeffects バンドルから補う)
    public class ContrastController : EffectControllerBase<ContrastEnhance, ContrastSetting>
    {
        public override string effectName => "コントラスト";

        protected override ContrastSetting setting
        {
            get => settings.contrast;
            set => settings.contrast = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        protected override void ApplySetting(ContrastEnhance component)
        {
            if (component.separableBlurShader == null)
            {
                component.separableBlurShader = EffectShaders.GetShader(EffectShaders.ImageEffects, "separableblurshader");
            }
            if (component.contrastCompositeShader == null)
            {
                component.contrastCompositeShader = EffectShaders.GetShader(EffectShaders.ImageEffects, "contrastcompositeshader");
            }
            component.intensity = setting.intensity;
            component.threshhold = setting.threshhold;
            component.blurSpread = setting.blurSpread;
        }

        protected override void Capture(ContrastEnhance component)
        {
            var c = _capturedSetting;
            _capturedEnabled = component.enabled;
            c.intensity = component.intensity;
            c.threshhold = component.threshhold;
            c.blurSpread = component.blurSpread;
        }

        protected override void RestoreSetting(ContrastEnhance component)
        {
            var c = _capturedSetting;
            component.enabled = _capturedEnabled;
            component.intensity = c.intensity;
            component.threshhold = c.threshhold;
            component.blurSpread = c.blurSpread;
        }

        public override void DrawContent(GUIView view)
        {
            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "強度",
                labelWidth = 100,
                width = -1,
                min = -10f,
                max = 10f,
                step = 0.01f,
                defaultValue = 0.5f,
                value = setting.intensity,
                onChanged = value => { setting.intensity = value; SetDirty(); },
            });

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "しきい値",
                labelWidth = 100,
                width = -1,
                min = 0f,
                max = 1f,
                step = 0.001f,
                defaultValue = 0f,
                value = setting.threshhold,
                onChanged = value => { setting.threshhold = value; SetDirty(); },
            });

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "ブラー拡散",
                labelWidth = 100,
                width = -1,
                min = 0f,
                max = 10f,
                step = 0.01f,
                defaultValue = 1f,
                value = setting.blurSpread,
                onChanged = value => { setting.blurSpread = value; SetDirty(); },
            });
        }
    }
}
