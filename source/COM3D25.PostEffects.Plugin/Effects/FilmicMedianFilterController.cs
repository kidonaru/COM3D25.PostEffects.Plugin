using COM3D2.MotionTimelineEditor;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    public class FilmicMedianFilterSetting
    {
        public bool enabled = false;
        public FilmicMedianFilterEffect.FilterQuality quality = FilmicMedianFilterEffect.FilterQuality.High;
    }

    public class FilmicMedianFilterController
        : EffectControllerBase<FilmicMedianFilterEffect, FilmicMedianFilterSetting>
    {
        public override string effectName => "メディアンフィルタ";

        protected override FilmicMedianFilterSetting setting
        {
            get => settings.filmicMedianFilter;
            set => settings.filmicMedianFilter = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        protected override void ApplySetting(FilmicMedianFilterEffect component)
        {
            if (component.shader == null)
            {
                component.shader = EffectShaders.GetShader(EffectShaders.Filmic, "filmicmedianfiltershader");
            }

            component.quality = setting.quality;
        }

        protected override void Capture(FilmicMedianFilterEffect component)
        {
            _capturedEnabled = component.enabled;
            _capturedSetting.quality = component.quality;
        }

        protected override void RestoreSetting(FilmicMedianFilterEffect component)
        {
            component.enabled = _capturedEnabled;
            component.quality = _capturedSetting.quality;
        }

        private readonly GUIComboBox<FilmicMedianFilterEffect.FilterQuality> _qualityComboBox =
            new GUIComboBox<FilmicMedianFilterEffect.FilterQuality>
            {
                items = MTEUtils.GetEnumValues<FilmicMedianFilterEffect.FilterQuality>(),
                getName = (quality, _) =>
                    quality == FilmicMedianFilterEffect.FilterQuality.High ? "高 (3x3)" : "低 (十字)",
                buttonSize = new Vector2(120, 20),
            };

        public override void DrawContent(GUIView view)
        {
            view.BeginHorizontal();
            {
                view.DrawLabel("品質", 80, 20);
                _qualityComboBox.currentIndex = (int)setting.quality;
                _qualityComboBox.onSelected = (quality, _) => { setting.quality = quality; SetDirty(); };
                _qualityComboBox.DrawButton(view);
            }
            view.EndLayout();
        }
    }
}
