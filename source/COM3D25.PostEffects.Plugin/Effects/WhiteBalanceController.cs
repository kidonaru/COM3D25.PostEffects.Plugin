using COM3D2.MotionTimelineEditor;

namespace COM3D25.PostEffects.Plugin
{
    public class WhiteBalanceSetting
    {
        public bool enabled = false;
        public float temperature = 0f;
        public float tint = 0f;
    }

    public class WhiteBalanceController : EffectControllerBase<WhiteBalanceEffect, WhiteBalanceSetting>
    {
        public override string effectName => "ホワイトバランス";

        protected override WhiteBalanceSetting setting
        {
            get => settings.whiteBalance;
            set => settings.whiteBalance = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        protected override void ApplySetting(WhiteBalanceEffect component)
        {
            if (component.shader == null)
            {
                component.shader = EffectShaders.GetShader(EffectShaders.PostEffects, "WhiteBalance");
            }

            component.temperature = setting.temperature;
            component.tint = setting.tint;
        }

        protected override void Capture(WhiteBalanceEffect component)
        {
            _capturedEnabled = component.enabled;
            _capturedSetting.temperature = component.temperature;
            _capturedSetting.tint = component.tint;
        }

        protected override void RestoreSetting(WhiteBalanceEffect component)
        {
            component.enabled = _capturedEnabled;
            component.temperature = _capturedSetting.temperature;
            component.tint = _capturedSetting.tint;
        }

        public override void DrawContent(GUIView view)
        {
            DrawSlider(view, "色温度", -100f, 100f, 0f, setting.temperature, v => setting.temperature = v);
            DrawSlider(view, "ティント", -100f, 100f, 0f, setting.tint, v => setting.tint = v);
        }
    }
}
