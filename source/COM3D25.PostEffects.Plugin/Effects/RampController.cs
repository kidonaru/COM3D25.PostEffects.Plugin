using COM3D2.MotionTimelineEditor;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    public class RampSetting
    {
        public bool enabled = false;
        public Color color1 = Color.blue;
        public Color color2 = Color.red;
        public float angle = 90f;
        public float opacity = 1f;
        public RampEffect.BlendMode blendMode = RampEffect.BlendMode.Overlay;
        public bool excludeCharacters = false;
    }

    public class RampController : EffectControllerBase<RampEffect, RampSetting>
    {
        public override string effectName => "グラデーション";

        protected override RampSetting setting
        {
            get => settings.ramp;
            set => settings.ramp = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        protected override void ApplySetting(RampEffect component)
        {
            if (component.shader == null)
            {
                component.shader = EffectShaders.GetShader(EffectShaders.Kino, "ramp");
            }

            component.color1 = setting.color1;
            component.color2 = setting.color2;
            component.angle = setting.angle;
            component.opacity = setting.opacity;
            component.blendMode = setting.blendMode;
            component.excludeCharacters = setting.excludeCharacters;
        }

        protected override void Capture(RampEffect component)
        {
            var c = _capturedSetting;
            _capturedEnabled = component.enabled;
            c.color1 = component.color1;
            c.color2 = component.color2;
            c.angle = component.angle;
            c.opacity = component.opacity;
            c.blendMode = component.blendMode;
            c.excludeCharacters = component.excludeCharacters;
        }

        protected override void RestoreSetting(RampEffect component)
        {
            var c = _capturedSetting;
            component.enabled = _capturedEnabled;
            component.color1 = c.color1;
            component.color2 = c.color2;
            component.angle = c.angle;
            component.opacity = c.opacity;
            component.blendMode = c.blendMode;
            component.excludeCharacters = c.excludeCharacters;
        }

        private readonly GUIComboBox<RampEffect.BlendMode> _blendComboBox = new GUIComboBox<RampEffect.BlendMode>
        {
            items = MTEUtils.GetEnumValues<RampEffect.BlendMode>(),
            getName = (mode, _) => mode.ToString(),
            buttonSize = new Vector2(100, 20),
        };

        public override void DrawContent(GUIView view)
        {
            view.BeginHorizontal();
            {
                view.DrawLabel("ブレンド", 60, 20);
                _blendComboBox.currentIndex = (int)setting.blendMode;
                _blendComboBox.onSelected = (mode, _) => { setting.blendMode = mode; SetDirty(); };
                _blendComboBox.DrawButton(view);
            }
            view.EndLayout();

            view.DrawColor(
                view.GetColorFieldCache("色 1", false),
                setting.color1,
                Color.blue,
                color => { setting.color1 = color; SetDirty(); });

            view.DrawColor(
                view.GetColorFieldCache("色 2", false),
                setting.color2,
                Color.red,
                color => { setting.color2 = color; SetDirty(); });

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "角度",
                labelWidth = 80,
                width = -1,
                min = -180f,
                max = 180f,
                step = 1f,
                defaultValue = 90f,
                value = setting.angle,
                onChanged = value => { setting.angle = value; SetDirty(); },
            });

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "不透明度",
                labelWidth = 80,
                width = -1,
                min = 0f,
                max = 1f,
                step = 0.01f,
                defaultValue = 1f,
                value = setting.opacity,
                onChanged = value => { setting.opacity = value; SetDirty(); },
            });

            view.DrawToggle("キャラに適用しない", setting.excludeCharacters, 250, 20, value =>
            {
                setting.excludeCharacters = value;
                SetDirty();
            });
        }
    }
}
