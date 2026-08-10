using COM3D2.MotionTimelineEditor;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    public class StreakSetting
    {
        public bool enabled = false;
        public float threshold = 1f;
        public float softKnee = 0.5f;
        public float stretch = 0.75f;
        public float intensity = 0.3f;
        public bool streakVertical = false;
        public bool streak2Way = false;
        public Color tint = new Color(0.55f, 0.55f, 0.55f);
        public StreakEffect.BlendMode blendMode = StreakEffect.BlendMode.Screen;
    }

    public class StreakController : EffectControllerBase<StreakEffect, StreakSetting>
    {
        public override string effectName => "光条";

        private static readonly Color DefaultTint = new Color(0.55f, 0.55f, 0.55f);

        protected override StreakSetting setting
        {
            get => settings.streak;
            set => settings.streak = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        protected override void ApplySetting(StreakEffect component)
        {
            if (component.shader == null)
            {
                component.shader = EffectShaders.GetShader(EffectShaders.Kino, "streak");
            }

            component.threshold = setting.threshold;
            component.softKnee = setting.softKnee;
            component.stretch = setting.stretch;
            component.intensity = setting.intensity;
            component.streakVertical = setting.streakVertical;
            component.streak2Way = setting.streak2Way;
            component.tint = setting.tint;
            component.blendMode = setting.blendMode;
        }

        protected override void Capture(StreakEffect component)
        {
            var c = _capturedSetting;
            _capturedEnabled = component.enabled;
            c.threshold = component.threshold;
            c.softKnee = component.softKnee;
            c.stretch = component.stretch;
            c.intensity = component.intensity;
            c.streakVertical = component.streakVertical;
            c.streak2Way = component.streak2Way;
            c.tint = component.tint;
            c.blendMode = component.blendMode;
        }

        protected override void RestoreSetting(StreakEffect component)
        {
            var c = _capturedSetting;
            component.enabled = _capturedEnabled;
            component.threshold = c.threshold;
            component.softKnee = c.softKnee;
            component.stretch = c.stretch;
            component.intensity = c.intensity;
            component.streakVertical = c.streakVertical;
            component.streak2Way = c.streak2Way;
            component.tint = c.tint;
            component.blendMode = c.blendMode;
        }

        private readonly GUIComboBox<StreakEffect.BlendMode> _blendComboBox = new GUIComboBox<StreakEffect.BlendMode>
        {
            items = MTEUtils.GetEnumValues<StreakEffect.BlendMode>(),
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

            DrawSlider(view, "しきい値", 0f, 5f, 1f, setting.threshold, v => setting.threshold = v);
            DrawSlider(view, "ソフトニー", 0f, 4f, 0.5f, setting.softKnee, v => setting.softKnee = v);
            DrawSlider(view, "伸び", 0f, 1f, 0.75f, setting.stretch, v => setting.stretch = v);
            DrawSlider(view, "強度", 0f, 3f, 0.3f, setting.intensity, v => setting.intensity = v);

            view.DrawColor(
                view.GetColorFieldCache("色", false),
                setting.tint,
                DefaultTint,
                color => { setting.tint = color; SetDirty(); });

            view.DrawToggle("縦方向", setting.streakVertical, 250, 20, value =>
            {
                setting.streakVertical = value;
                SetDirty();
            });

            view.DrawToggle("縦横両方向", setting.streak2Way, 250, 20, value =>
            {
                setting.streak2Way = value;
                SetDirty();
            });
        }
    }
}
