using COM3D2.MotionTimelineEditor;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    public class OutlineColorSetting
    {
        public bool enabled = false;
        public float intensity = 0.6f;
        public float sampleRadius = 2f;
        public float saturation = 1f;
        public Color tint = Color.white;
        public float tintStrength = 0f;
        public bool showMask = false;
    }

    public class OutlineColorController : EffectControllerBase<OutlineColorEffect, OutlineColorSetting>
    {
        public override string effectName => "輪郭の色にじみ";
        protected override OutlineColorSetting setting
        {
            get => settings.outlineColor;
            set => settings.outlineColor = value;
        }
        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        protected override void ApplySetting(OutlineColorEffect component)
        {
            if (component.shader == null)
                component.shader = EffectShaders.GetShader(EffectShaders.PostEffects, "OutlineColor");
            CopyToComponent(setting, component);
        }

        private static void CopyToComponent(OutlineColorSetting value, OutlineColorEffect component)
        {
            component.intensity = value.intensity;
            component.sampleRadius = value.sampleRadius;
            component.saturation = value.saturation;
            component.tint = value.tint;
            component.tintStrength = value.tintStrength;
            component.showMask = value.showMask;
        }

        protected override void Capture(OutlineColorEffect component)
        {
            _capturedEnabled = component.enabled;
            _capturedSetting.intensity = component.intensity;
            _capturedSetting.sampleRadius = component.sampleRadius;
            _capturedSetting.saturation = component.saturation;
            _capturedSetting.tint = component.tint;
            _capturedSetting.tintStrength = component.tintStrength;
            _capturedSetting.showMask = component.showMask;
        }

        protected override void RestoreSetting(OutlineColorEffect component)
        {
            CopyToComponent(_capturedSetting, component);
            component.enabled = _capturedEnabled;
        }

        public override void DrawContent(GUIView view)
        {
            DrawSlider(view, "混ぜ具合", 0f, 1f, 0.6f, setting.intensity, v => setting.intensity = v);
            DrawSlider(view, "参照距離 (px)", 0f, 10f, 2f, setting.sampleRadius, v => setting.sampleRadius = v);
            DrawSlider(view, "彩度", 0f, 2f, 1f, setting.saturation, v => setting.saturation = v);
            DrawSlider(view, "指定色の割合", 0f, 1f, 0f, setting.tintStrength, v => setting.tintStrength = v);
            view.DrawColor(view.GetColorFieldCache("指定色", false), setting.tint, Color.white,
                value => { setting.tint = value; SetDirty(); });
            view.DrawToggle("対象の線を表示", setting.showMask, 250, 20,
                value => { setting.showMask = value; SetDirty(); });
            view.DrawLabel("標準トゥーンのキャラ輪郭に適用", -1, 20);
        }
    }
}
