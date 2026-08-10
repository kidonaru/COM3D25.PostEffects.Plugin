using COM3D2.MotionTimelineEditor;

namespace COM3D25.PostEffects.Plugin
{
    public class AnalogGlitchSetting
    {
        public bool enabled = false;
        public float scanLineJitter = 0f;
        public float verticalJump = 0f;
        public float horizontalShake = 0f;
        public float colorDrift = 0f;
    }

    public class AnalogGlitchController : EffectControllerBase<AnalogGlitchEffect, AnalogGlitchSetting>
    {
        public override string effectName => "アナログノイズ";

        protected override AnalogGlitchSetting setting
        {
            get => settings.analogGlitch;
            set => settings.analogGlitch = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        protected override void ApplySetting(AnalogGlitchEffect component)
        {
            if (component.shader == null)
            {
                component.shader = EffectShaders.GetShader(EffectShaders.Kino, "analogglitch");
            }

            component.scanLineJitter = setting.scanLineJitter;
            component.verticalJump = setting.verticalJump;
            component.horizontalShake = setting.horizontalShake;
            component.colorDrift = setting.colorDrift;
        }

        protected override void Capture(AnalogGlitchEffect component)
        {
            var c = _capturedSetting;
            _capturedEnabled = component.enabled;
            c.scanLineJitter = component.scanLineJitter;
            c.verticalJump = component.verticalJump;
            c.horizontalShake = component.horizontalShake;
            c.colorDrift = component.colorDrift;
        }

        protected override void RestoreSetting(AnalogGlitchEffect component)
        {
            var c = _capturedSetting;
            component.enabled = _capturedEnabled;
            component.scanLineJitter = c.scanLineJitter;
            component.verticalJump = c.verticalJump;
            component.horizontalShake = c.horizontalShake;
            component.colorDrift = c.colorDrift;
        }

        // シェーダー側が 0〜1 前提の係数で使うため、この画面のスライダーは全て 0〜1 に揃える
        private void DrawUnitSlider(GUIView view, string label, float value, System.Action<float> onChanged)
        {
            DrawSlider(view, label, 0f, 1f, 0f, value, onChanged);
        }

        public override void DrawContent(GUIView view)
        {
            DrawUnitSlider(view, "走査線ジッター", setting.scanLineJitter, v => setting.scanLineJitter = v);
            DrawUnitSlider(view, "縦揺れ", setting.verticalJump, v => setting.verticalJump = v);
            DrawUnitSlider(view, "横揺れ", setting.horizontalShake, v => setting.horizontalShake = v);
            DrawUnitSlider(view, "色ずれ", setting.colorDrift, v => setting.colorDrift = v);
        }
    }
}
