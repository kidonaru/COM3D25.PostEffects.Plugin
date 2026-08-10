using COM3D2.MotionTimelineEditor;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    public class KuwaharaSetting
    {
        public bool enabled = false;
        public int radius = 3;
        public bool excludeCharacters = false;
    }

    public class KuwaharaController : EffectControllerBase<KuwaharaEffect, KuwaharaSetting>
    {
        public override string effectName => "油絵風";

        protected override KuwaharaSetting setting
        {
            get => settings.kuwahara;
            set => settings.kuwahara = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        protected override void ApplySetting(KuwaharaEffect component)
        {
            if (component.shader == null)
            {
                component.shader = EffectShaders.GetShader(EffectShaders.PostEffects, "Kuwahara");
            }

            component.radius = setting.radius;
            component.excludeCharacters = setting.excludeCharacters;
        }

        protected override void Capture(KuwaharaEffect component)
        {
            _capturedEnabled = component.enabled;
            _capturedSetting.radius = component.radius;
            _capturedSetting.excludeCharacters = component.excludeCharacters;
        }

        protected override void RestoreSetting(KuwaharaEffect component)
        {
            component.enabled = _capturedEnabled;
            component.radius = _capturedSetting.radius;
            component.excludeCharacters = _capturedSetting.excludeCharacters;
        }

        public override void DrawContent(GUIView view)
        {
            DrawSlider(view, "半径", 1f, 8f, 3f, setting.radius, v => setting.radius = Mathf.RoundToInt(v));

            view.DrawToggle("キャラに適用しない", setting.excludeCharacters, 250, 20, value =>
            {
                setting.excludeCharacters = value;
                SetDirty();
            });
        }
    }
}
