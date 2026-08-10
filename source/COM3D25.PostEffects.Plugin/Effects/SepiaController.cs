using COM3D2.MotionTimelineEditor;

namespace COM3D25.PostEffects.Plugin
{
    public class SepiaSetting
    {
        public bool enabled = false;
        public bool excludeCharacters = false;
    }

    public class SepiaController : EffectControllerBase<SepiaToneEffect, SepiaSetting>
    {
        public override string effectName => "セピア";

        protected override SepiaSetting setting
        {
            get => settings.sepia;
            set => settings.sepia = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        protected override void ApplySetting(SepiaToneEffect component)
        {
            if (component.shader == null)
            {
                component.shader = EffectShaders.GetShader(EffectShaders.ImageEffects, "sepiatoneshader");
            }
            component.excludeCharacters = setting.excludeCharacters;
        }

        protected override void Capture(SepiaToneEffect component)
        {
            _capturedEnabled = component.enabled;
            _capturedSetting.excludeCharacters = component.excludeCharacters;
        }

        protected override void RestoreSetting(SepiaToneEffect component)
        {
            component.enabled = _capturedEnabled;
            component.excludeCharacters = _capturedSetting.excludeCharacters;
        }

        public override void DrawContent(GUIView view)
        {
            view.DrawToggle("キャラに適用しない", setting.excludeCharacters, 250, 20, value =>
            {
                setting.excludeCharacters = value;
                SetDirty();
            });
        }
    }
}
