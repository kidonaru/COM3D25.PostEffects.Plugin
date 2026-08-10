using COM3D2.MotionTimelineEditor;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    public class DiffusionSetting
    {
        public bool enabled = false;
        public float intensity = 0.5f;
        public float threshold = 0.5f;
        public float blurSize = 3f;
        public DiffusionEffect.BlendMode blendMode = DiffusionEffect.BlendMode.Screen;
        public bool excludeCharacters = false;
    }

    public class DiffusionController : EffectControllerBase<DiffusionEffect, DiffusionSetting>
    {
        public override string effectName => "ディフュージョン";

        protected override DiffusionSetting setting
        {
            get => settings.diffusion;
            set => settings.diffusion = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        protected override void ApplySetting(DiffusionEffect component)
        {
            if (component.shader == null)
            {
                component.shader = EffectShaders.GetShader(EffectShaders.PostEffects, "Diffusion");
            }

            component.intensity = setting.intensity;
            component.threshold = setting.threshold;
            component.blurSize = setting.blurSize;
            component.blendMode = setting.blendMode;
            component.excludeCharacters = setting.excludeCharacters;
        }

        protected override void Capture(DiffusionEffect component)
        {
            var c = _capturedSetting;
            _capturedEnabled = component.enabled;
            c.intensity = component.intensity;
            c.threshold = component.threshold;
            c.blurSize = component.blurSize;
            c.blendMode = component.blendMode;
            c.excludeCharacters = component.excludeCharacters;
        }

        protected override void RestoreSetting(DiffusionEffect component)
        {
            var c = _capturedSetting;
            component.enabled = _capturedEnabled;
            component.intensity = c.intensity;
            component.threshold = c.threshold;
            component.blurSize = c.blurSize;
            component.blendMode = c.blendMode;
            component.excludeCharacters = c.excludeCharacters;
        }

        private readonly GUIComboBox<DiffusionEffect.BlendMode> _blendComboBox = new GUIComboBox<DiffusionEffect.BlendMode>
        {
            items = MTEUtils.GetEnumValues<DiffusionEffect.BlendMode>(),
            getName = (mode, _) => mode.ToString(),
            buttonSize = new Vector2(100, 20),
        };

        public override void DrawContent(GUIView view)
        {
            view.BeginHorizontal();
            {
                view.DrawLabel("合成", 60, 20);
                _blendComboBox.currentIndex = (int)setting.blendMode;
                _blendComboBox.onSelected = (mode, _) => { setting.blendMode = mode; SetDirty(); };
                _blendComboBox.DrawButton(view);
            }
            view.EndLayout();

            DrawSlider(view, "強度", 0f, 1f, 0.5f, setting.intensity, v => setting.intensity = v);
            DrawSlider(view, "しきい値", 0f, 1f, 0.5f, setting.threshold, v => setting.threshold = v);
            DrawSlider(view, "ぼかし半径", 1f, 10f, 3f, setting.blurSize, v => setting.blurSize = v);

            view.DrawToggle("キャラに適用しない", setting.excludeCharacters, 250, 20, value =>
            {
                setting.excludeCharacters = value;
                SetDirty();
            });
        }
    }
}
