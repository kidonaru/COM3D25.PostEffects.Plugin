using COM3D2.MotionTimelineEditor;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    public class HalftoneSetting
    {
        public bool enabled = false;
        public float dotSize = 6f;
        public float angle = 45f;
        public float smoothness = 0.1f;
        public HalftoneEffect.Mode mode = HalftoneEffect.Mode.Mono;
        public bool excludeCharacters = false;
    }

    public class HalftoneController : EffectControllerBase<HalftoneEffect, HalftoneSetting>
    {
        public override string effectName => "ハーフトーン";

        protected override HalftoneSetting setting
        {
            get => settings.halftone;
            set => settings.halftone = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        protected override void ApplySetting(HalftoneEffect component)
        {
            if (component.shader == null)
            {
                component.shader = EffectShaders.GetShader(EffectShaders.PostEffects, "Halftone");
            }

            component.dotSize = setting.dotSize;
            component.angle = setting.angle;
            component.smoothness = setting.smoothness;
            component.mode = setting.mode;
            component.excludeCharacters = setting.excludeCharacters;
        }

        protected override void Capture(HalftoneEffect component)
        {
            var c = _capturedSetting;
            _capturedEnabled = component.enabled;
            c.dotSize = component.dotSize;
            c.angle = component.angle;
            c.smoothness = component.smoothness;
            c.mode = component.mode;
            c.excludeCharacters = component.excludeCharacters;
        }

        protected override void RestoreSetting(HalftoneEffect component)
        {
            var c = _capturedSetting;
            component.enabled = _capturedEnabled;
            component.dotSize = c.dotSize;
            component.angle = c.angle;
            component.smoothness = c.smoothness;
            component.mode = c.mode;
            component.excludeCharacters = c.excludeCharacters;
        }

        private readonly GUIComboBox<HalftoneEffect.Mode> _modeComboBox = new GUIComboBox<HalftoneEffect.Mode>
        {
            items = MTEUtils.GetEnumValues<HalftoneEffect.Mode>(),
            getName = (mode, _) => mode == HalftoneEffect.Mode.Mono ? "モノクロ" : "CMYK カラー",
            buttonSize = new Vector2(100, 20),
        };

        public override void DrawContent(GUIView view)
        {
            view.BeginHorizontal();
            {
                view.DrawLabel("モード", 60, 20);
                _modeComboBox.currentIndex = (int)setting.mode;
                _modeComboBox.onSelected = (mode, _) => { setting.mode = mode; SetDirty(); };
                _modeComboBox.DrawButton(view);
            }
            view.EndLayout();

            DrawSlider(view, "ドットサイズ", 2f, 32f, 6f, setting.dotSize, v => setting.dotSize = v, 0.5f);
            DrawSlider(view, "網角度", 0f, 180f, 45f, setting.angle, v => setting.angle = v, 1f);
            DrawSlider(view, "なめらかさ", 0f, 1f, 0.1f, setting.smoothness, v => setting.smoothness = v);

            view.DrawToggle("キャラに適用しない", setting.excludeCharacters, 250, 20, value =>
            {
                setting.excludeCharacters = value;
                SetDirty();
            });
        }
    }
}
