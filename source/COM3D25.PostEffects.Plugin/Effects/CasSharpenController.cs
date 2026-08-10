using COM3D2.MotionTimelineEditor;

namespace COM3D25.PostEffects.Plugin
{
    public class CasSharpenSetting
    {
        public bool enabled = false;
        public float sharpness = 0.5f;
    }

    public class CasSharpenController : EffectControllerBase<CasSharpenEffect, CasSharpenSetting>
    {
        public override string effectName => "シャープネス";

        protected override CasSharpenSetting setting
        {
            get => settings.casSharpen;
            set => settings.casSharpen = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        protected override void ApplySetting(CasSharpenEffect component)
        {
            if (component.shader == null)
            {
                component.shader = EffectShaders.GetShader(EffectShaders.PostEffects, "CasSharpen");
            }

            component.sharpness = setting.sharpness;
        }

        protected override void Capture(CasSharpenEffect component)
        {
            _capturedEnabled = component.enabled;
            _capturedSetting.sharpness = component.sharpness;
        }

        protected override void RestoreSetting(CasSharpenEffect component)
        {
            component.enabled = _capturedEnabled;
            component.sharpness = _capturedSetting.sharpness;
        }

        public override void DrawContent(GUIView view)
        {
            DrawSlider(view, "強度", 0f, 1f, 0.5f, setting.sharpness, v => setting.sharpness = v);
        }
    }
}
