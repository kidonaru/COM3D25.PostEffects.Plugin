using COM3D2.MotionTimelineEditor;

namespace COM3D25.PostEffects.Plugin
{
    public class GrayscaleSetting
    {
        public bool enabled = false;
        public float rampOffset = 0f;
    }

    // GrayscaleEffect はゲーム側 Assembly-CSharp-firstpass の実装をそのまま使う
    // (シェーダーフィールドは実行時追加では null のため imageeffects バンドルから補う)
    public class GrayscaleController : EffectControllerBase<GrayscaleEffect, GrayscaleSetting>
    {
        public override string effectName => "グレースケール";

        protected override GrayscaleSetting setting
        {
            get => settings.grayscale;
            set => settings.grayscale = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        protected override void ApplySetting(GrayscaleEffect component)
        {
            if (component.shader == null)
            {
                component.shader = EffectShaders.GetShader(EffectShaders.ImageEffects, "grayscaleeffect_shader");
            }
            component.rampOffset = setting.rampOffset;
        }

        protected override void Capture(GrayscaleEffect component)
        {
            _capturedEnabled = component.enabled;
            _capturedSetting.rampOffset = component.rampOffset;
        }

        protected override void RestoreSetting(GrayscaleEffect component)
        {
            component.enabled = _capturedEnabled;
            component.rampOffset = _capturedSetting.rampOffset;
        }

        public override void DrawContent(GUIView view)
        {
            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "ランプオフセット",
                labelWidth = 100,
                width = -1,
                min = -1f,
                max = 1f,
                step = 0.01f,
                defaultValue = 0f,
                value = setting.rampOffset,
                onChanged = value => { setting.rampOffset = value; SetDirty(); },
            });
        }
    }
}
