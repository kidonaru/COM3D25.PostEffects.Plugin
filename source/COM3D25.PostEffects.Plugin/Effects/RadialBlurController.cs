using COM3D2.MotionTimelineEditor;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    public class RadialBlurSetting
    {
        public bool enabled = false;
        public float strength = 0.3f;
        public float centerX = 0.5f;
        public float centerY = 0.5f;
        public int sampleCount = 16;
        public bool excludeCharacters = false;
    }

    public class RadialBlurController : EffectControllerBase<RadialBlurEffect, RadialBlurSetting>
    {
        public override string effectName => "ラジアルブラー";

        protected override RadialBlurSetting setting
        {
            get => settings.radialBlur;
            set => settings.radialBlur = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        protected override void ApplySetting(RadialBlurEffect component)
        {
            if (component.shader == null)
            {
                component.shader = EffectShaders.GetShader(EffectShaders.PostEffects, "RadialBlur");
            }

            component.strength = setting.strength;
            component.centerX = setting.centerX;
            component.centerY = setting.centerY;
            component.sampleCount = setting.sampleCount;
            component.excludeCharacters = setting.excludeCharacters;
        }

        protected override void Capture(RadialBlurEffect component)
        {
            var c = _capturedSetting;
            _capturedEnabled = component.enabled;
            c.strength = component.strength;
            c.centerX = component.centerX;
            c.centerY = component.centerY;
            c.sampleCount = component.sampleCount;
            c.excludeCharacters = component.excludeCharacters;
        }

        protected override void RestoreSetting(RadialBlurEffect component)
        {
            var c = _capturedSetting;
            component.enabled = _capturedEnabled;
            component.strength = c.strength;
            component.centerX = c.centerX;
            component.centerY = c.centerY;
            component.sampleCount = c.sampleCount;
            component.excludeCharacters = c.excludeCharacters;
        }

        public override void DrawContent(GUIView view)
        {
            DrawSlider(view, "強度", 0f, 1f, 0.3f, setting.strength, v => setting.strength = v);
            DrawSlider(view, "中心X", 0f, 1f, 0.5f, setting.centerX, v => setting.centerX = v);
            DrawSlider(view, "中心Y", 0f, 1f, 0.5f, setting.centerY, v => setting.centerY = v);
            DrawSlider(view, "サンプル数", 4f, 32f, 16f, setting.sampleCount, v => setting.sampleCount = Mathf.RoundToInt(v));

            view.DrawToggle("キャラに適用しない", setting.excludeCharacters, 250, 20, value =>
            {
                setting.excludeCharacters = value;
                SetDirty();
            });
        }
    }
}
