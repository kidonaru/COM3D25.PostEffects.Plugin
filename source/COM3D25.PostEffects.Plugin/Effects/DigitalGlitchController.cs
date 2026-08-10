using COM3D2.MotionTimelineEditor;

namespace COM3D25.PostEffects.Plugin
{
    public class DigitalGlitchSetting
    {
        public bool enabled = false;
        public float intensity = 0f;
    }

    public class DigitalGlitchController : EffectControllerBase<DigitalGlitchEffect, DigitalGlitchSetting>
    {
        public override string effectName => "デジタルノイズ";

        protected override DigitalGlitchSetting setting
        {
            get => settings.digitalGlitch;
            set => settings.digitalGlitch = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        protected override void ApplySetting(DigitalGlitchEffect component)
        {
            if (component.shader == null)
            {
                component.shader = EffectShaders.GetShader(EffectShaders.Kino, "digitalglitch");
            }

            component.intensity = setting.intensity;
        }

        protected override void Capture(DigitalGlitchEffect component)
        {
            _capturedEnabled = component.enabled;
            _capturedSetting.intensity = component.intensity;
        }

        protected override void RestoreSetting(DigitalGlitchEffect component)
        {
            component.enabled = _capturedEnabled;
            component.intensity = _capturedSetting.intensity;
        }

        public override void DrawContent(GUIView view)
        {
            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "強度",
                labelWidth = 100,
                width = -1,
                // シェーダー側が 0〜1 前提の係数で使うため上限は 1 に揃える
                min = 0f,
                max = 1f,
                step = 0.01f,
                defaultValue = 0f,
                value = setting.intensity,
                onChanged = value => { setting.intensity = value; SetDirty(); },
            });
        }
    }
}
