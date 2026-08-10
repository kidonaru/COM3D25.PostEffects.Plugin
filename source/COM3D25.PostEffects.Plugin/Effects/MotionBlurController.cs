using COM3D2.MotionTimelineEditor;

namespace COM3D25.PostEffects.Plugin
{
    public class MotionBlurSetting
    {
        public bool enabled = false;
        public float blurAmount = 0.8f;
        public bool extraBlur = false;
    }

    // MotionBlur はゲーム側 Assembly-CSharp-firstpass の実装 (残像式) をそのまま使う
    // (シェーダーフィールドは実行時追加では null のため imageeffects バンドルから補う)
    public class MotionBlurController : EffectControllerBase<MotionBlur, MotionBlurSetting>
    {
        public override string effectName => "モーションブラー";

        protected override MotionBlurSetting setting
        {
            get => settings.motionBlur;
            set => settings.motionBlur = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        protected override void ApplySetting(MotionBlur component)
        {
            if (component.shader == null)
            {
                component.shader = EffectShaders.GetShader(EffectShaders.ImageEffects, "motionblur_shader");
            }
            component.blurAmount = setting.blurAmount;
            component.extraBlur = setting.extraBlur;
        }

        protected override void Capture(MotionBlur component)
        {
            var c = _capturedSetting;
            _capturedEnabled = component.enabled;
            c.blurAmount = component.blurAmount;
            c.extraBlur = component.extraBlur;
        }

        protected override void RestoreSetting(MotionBlur component)
        {
            var c = _capturedSetting;
            component.enabled = _capturedEnabled;
            component.blurAmount = c.blurAmount;
            component.extraBlur = c.extraBlur;
        }

        public override void DrawContent(GUIView view)
        {
            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "残像量",
                labelWidth = 100,
                width = -1,
                min = 0f,
                max = 1f,
                step = 0.001f,
                defaultValue = 0.8f,
                value = setting.blurAmount,
                onChanged = value => { setting.blurAmount = value; SetDirty(); },
            });

            view.DrawToggle("追加ブラー", setting.extraBlur, 250, 20, value =>
            {
                setting.extraBlur = value;
                SetDirty();
            });
        }
    }
}
