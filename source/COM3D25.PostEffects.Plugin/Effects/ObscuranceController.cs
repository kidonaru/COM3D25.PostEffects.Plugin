using COM3D2.MotionTimelineEditor;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    public class ObscuranceSetting
    {
        public bool enabled = false;
        public float intensity = 0.5f;
        public float radius = 0.1f;
        public Color tint = Color.gray;
        public ObscuranceEffect.SampleCount sampleCount = ObscuranceEffect.SampleCount.Medium;
        public int variableSampleCount = 24;
        public bool downsampling = false;
        public ObscuranceEffect.OcclusionSource occlusionSource = ObscuranceEffect.OcclusionSource.DepthTexture;
        public bool colorMode = false;
        public Color subTint = Color.gray;
        public Color subTint2 = Color.gray;
        public float blend = 0f;
        public bool excludeCharacters = true;
    }

    public class ObscuranceController : EffectControllerBase<ObscuranceEffect, ObscuranceSetting>
    {
        public override string effectName => "環境遮蔽";

        protected override ObscuranceSetting setting
        {
            get => settings.obscurance;
            set => settings.obscurance = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        protected override void ApplySetting(ObscuranceEffect component)
        {
            if (component.shader == null)
            {
                component.shader = EffectShaders.GetShader(EffectShaders.Kino, "obscurance");
            }

            component.intensity = setting.intensity;
            component.radius = setting.radius;
            component.tint = setting.tint;
            component.sampleCount = setting.sampleCount;
            component.variableSampleCount = setting.variableSampleCount;
            component.downsampling = setting.downsampling;
            component.occlusionSource = setting.occlusionSource;
            component.colorMode = setting.colorMode;
            component.subTint = setting.subTint;
            component.subTint2 = setting.subTint2;
            component.blend = setting.blend;
            component.excludeCharacters = setting.excludeCharacters;
        }

        protected override void Capture(ObscuranceEffect component)
        {
            var c = _capturedSetting;
            _capturedEnabled = component.enabled;
            c.intensity = component.intensity;
            c.radius = component.radius;
            c.tint = component.tint;
            c.sampleCount = component.sampleCount;
            c.variableSampleCount = component.variableSampleCount;
            c.downsampling = component.downsampling;
            c.occlusionSource = component.occlusionSource;
            c.colorMode = component.colorMode;
            c.subTint = component.subTint;
            c.subTint2 = component.subTint2;
            c.blend = component.blend;
            c.excludeCharacters = component.excludeCharacters;
        }

        protected override void RestoreSetting(ObscuranceEffect component)
        {
            var c = _capturedSetting;
            component.enabled = _capturedEnabled;
            component.intensity = c.intensity;
            component.radius = c.radius;
            component.tint = c.tint;
            component.sampleCount = c.sampleCount;
            component.variableSampleCount = c.variableSampleCount;
            component.downsampling = c.downsampling;
            component.occlusionSource = c.occlusionSource;
            component.colorMode = c.colorMode;
            component.subTint = c.subTint;
            component.subTint2 = c.subTint2;
            component.blend = c.blend;
            component.excludeCharacters = c.excludeCharacters;
        }

        private readonly GUIComboBox<ObscuranceEffect.SampleCount> _sampleComboBox =
            new GUIComboBox<ObscuranceEffect.SampleCount>
            {
                items = MTEUtils.GetEnumValues<ObscuranceEffect.SampleCount>(),
                getName = (count, _) => count.ToString(),
                buttonSize = new Vector2(100, 20),
            };

        private readonly GUIComboBox<ObscuranceEffect.OcclusionSource> _sourceComboBox =
            new GUIComboBox<ObscuranceEffect.OcclusionSource>
            {
                items = MTEUtils.GetEnumValues<ObscuranceEffect.OcclusionSource>(),
                getName = (source, _) => source.ToString(),
                buttonSize = new Vector2(160, 20),
            };

        public override void DrawContent(GUIView view)
        {
            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "強度",
                labelWidth = 100,
                width = -1,
                min = 0f,
                max = 10f,
                step = 0.01f,
                defaultValue = 0.5f,
                value = setting.intensity,
                onChanged = value => { setting.intensity = value; SetDirty(); },
            });

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "半径",
                labelWidth = 100,
                width = -1,
                min = 0f,
                max = 2f,
                step = 0.01f,
                defaultValue = 0.1f,
                value = setting.radius,
                onChanged = value => { setting.radius = value; SetDirty(); },
            });

            view.DrawColor(
                view.GetColorFieldCache("遮蔽色", false),
                setting.tint,
                Color.gray,
                color => { setting.tint = color; SetDirty(); });

            view.BeginHorizontal();
            {
                view.DrawLabel("サンプル数", 80, 20);
                _sampleComboBox.currentIndex = (int)setting.sampleCount;
                _sampleComboBox.onSelected = (count, _) => { setting.sampleCount = count; SetDirty(); };
                _sampleComboBox.DrawButton(view);
            }
            view.EndLayout();

            if (setting.sampleCount == ObscuranceEffect.SampleCount.Variable)
            {
                view.DrawSliderValue(new GUIView.SliderOption
                {
                    label = "サンプル数",
                    labelWidth = 100,
                    width = -1,
                    fieldType = FloatFieldType.Int,
                    min = 1,
                    max = 50,
                    step = 1,
                    defaultValue = 24,
                    value = setting.variableSampleCount,
                    onChanged = value => { setting.variableSampleCount = (int)value; SetDirty(); },
                });
            }

            view.BeginHorizontal();
            {
                view.DrawLabel("遮蔽の元", 80, 20);
                _sourceComboBox.currentIndex = (int)setting.occlusionSource;
                _sourceComboBox.onSelected = (source, _) => { setting.occlusionSource = source; SetDirty(); };
                _sourceComboBox.DrawButton(view);
            }
            view.EndLayout();

            view.DrawToggle("低解像度で計算", setting.downsampling, 250, 20, value =>
            {
                setting.downsampling = value;
                SetDirty();
            });

            view.DrawToggle("キャラに適用しない", setting.excludeCharacters, 250, 20, value =>
            {
                setting.excludeCharacters = value;
                SetDirty();
            });

            view.DrawToggle("カラーモード", setting.colorMode, 250, 20, value =>
            {
                setting.colorMode = value;
                SetDirty();
            });

            if (setting.colorMode)
            {
                view.DrawColor(
                    view.GetColorFieldCache("遮蔽色 2", false),
                    setting.subTint,
                    Color.gray,
                    color => { setting.subTint = color; SetDirty(); });

                view.DrawColor(
                    view.GetColorFieldCache("遮蔽色 3", false),
                    setting.subTint2,
                    Color.gray,
                    color => { setting.subTint2 = color; SetDirty(); });

                view.DrawSliderValue(new GUIView.SliderOption
                {
                    label = "混合",
                    labelWidth = 100,
                    width = -1,
                    min = 0f,
                    max = 1f,
                    step = 0.01f,
                    defaultValue = 0f,
                    value = setting.blend,
                    onChanged = value => { setting.blend = value; SetDirty(); },
                });
            }
        }
    }
}
