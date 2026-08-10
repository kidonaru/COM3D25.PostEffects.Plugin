using COM3D2.MotionTimelineEditor;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    public class StylisticFogSetting
    {
        public bool enabled = false;

        public bool distanceFogEnabled = true;
        public bool distanceFogSkybox = false;
        public float distanceFogEndDistance = 100f;
        public StylisticFogEffect.ColorSource distanceColorSource = StylisticFogEffect.ColorSource.Gradient;
        public Color distanceFirstColor = new Color(1f, 1f, 1f, 0f);
        public Color distanceLastColor = new Color(1f, 1f, 1f, 1f);
        // 絶対パス、または Config フォルダからの相対パス
        public string distanceRampPath = "";

        public bool heightFogEnabled = false;
        public bool heightFogSkybox = true;
        public float heightFogBaseHeight = 0f;
        public float heightFogBaseDensity = 0.1f;
        public float heightFogDensityFalloff = 0.5f;
        public StylisticFogEffect.ColorSource heightColorSource = StylisticFogEffect.ColorSource.CopyOther;
        public Color heightFirstColor = new Color(1f, 1f, 1f, 0f);
        public Color heightLastColor = new Color(1f, 1f, 1f, 1f);
        public string heightRampPath = "";
    }

    public class StylisticFogController : EffectControllerBase<StylisticFogEffect, StylisticFogSetting>
    {
        public override string effectName => "スタイリッシュフォグ";

        private static readonly Color DefaultFirstColor = new Color(1f, 1f, 1f, 0f);
        private static readonly Color DefaultLastColor = new Color(1f, 1f, 1f, 1f);

        protected override StylisticFogSetting setting
        {
            get => settings.stylisticFog;
            set => settings.stylisticFog = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        private readonly TextureFileCache _distanceRampCache = new TextureFileCache(TextureFileCache.SUB_DIR_RAMP);
        private readonly TextureFileCache _heightRampCache = new TextureFileCache(TextureFileCache.SUB_DIR_RAMP);

        protected override void ApplySetting(StylisticFogEffect component)
        {
            if (component.shader == null)
            {
                component.shader = EffectShaders.GetShader(EffectShaders.Cinematic, "stylisticfogshader");
            }

            component.distanceFogEnabled = setting.distanceFogEnabled;
            component.distanceFogSkybox = setting.distanceFogSkybox;
            component.distanceFogEndDistance = setting.distanceFogEndDistance;
            component.distanceColorSource = setting.distanceColorSource;
            component.distanceFirstColor = setting.distanceFirstColor;
            component.distanceLastColor = setting.distanceLastColor;
            component.distanceColorRamp = setting.distanceColorSource == StylisticFogEffect.ColorSource.TextureRamp
                ? _distanceRampCache.GetOrLoad(setting.distanceRampPath)
                : null;

            component.heightFogEnabled = setting.heightFogEnabled;
            component.heightFogSkybox = setting.heightFogSkybox;
            component.heightFogBaseHeight = setting.heightFogBaseHeight;
            component.heightFogBaseDensity = setting.heightFogBaseDensity;
            component.heightFogDensityFalloff = setting.heightFogDensityFalloff;
            component.heightColorSource = setting.heightColorSource;
            component.heightFirstColor = setting.heightFirstColor;
            component.heightLastColor = setting.heightLastColor;
            component.heightColorRamp = setting.heightColorSource == StylisticFogEffect.ColorSource.TextureRamp
                ? _heightRampCache.GetOrLoad(setting.heightRampPath)
                : null;
        }

        protected override void Capture(StylisticFogEffect component)
        {
            var c = _capturedSetting;
            _capturedEnabled = component.enabled;
            c.distanceFogEnabled = component.distanceFogEnabled;
            c.distanceFogSkybox = component.distanceFogSkybox;
            c.distanceFogEndDistance = component.distanceFogEndDistance;
            c.distanceColorSource = component.distanceColorSource;
            c.distanceFirstColor = component.distanceFirstColor;
            c.distanceLastColor = component.distanceLastColor;
            c.heightFogEnabled = component.heightFogEnabled;
            c.heightFogSkybox = component.heightFogSkybox;
            c.heightFogBaseHeight = component.heightFogBaseHeight;
            c.heightFogBaseDensity = component.heightFogBaseDensity;
            c.heightFogDensityFalloff = component.heightFogDensityFalloff;
            c.heightColorSource = component.heightColorSource;
            c.heightFirstColor = component.heightFirstColor;
            c.heightLastColor = component.heightLastColor;
        }

        protected override void RestoreSetting(StylisticFogEffect component)
        {
            var c = _capturedSetting;
            component.enabled = _capturedEnabled;
            component.distanceFogEnabled = c.distanceFogEnabled;
            component.distanceFogSkybox = c.distanceFogSkybox;
            component.distanceFogEndDistance = c.distanceFogEndDistance;
            component.distanceColorSource = c.distanceColorSource;
            component.distanceFirstColor = c.distanceFirstColor;
            component.distanceLastColor = c.distanceLastColor;
            component.heightFogEnabled = c.heightFogEnabled;
            component.heightFogSkybox = c.heightFogSkybox;
            component.heightFogBaseHeight = c.heightFogBaseHeight;
            component.heightFogBaseDensity = c.heightFogBaseDensity;
            component.heightFogDensityFalloff = c.heightFogDensityFalloff;
            component.heightColorSource = c.heightColorSource;
            component.heightFirstColor = c.heightFirstColor;
            component.heightLastColor = c.heightLastColor;
        }

        private static GUIComboBox<StylisticFogEffect.ColorSource> CreateColorSourceComboBox()
        {
            return new GUIComboBox<StylisticFogEffect.ColorSource>
            {
                items = MTEUtils.GetEnumValues<StylisticFogEffect.ColorSource>(),
                getName = (source, _) => GetColorSourceName(source),
                buttonSize = new Vector2(140, 20),
            };
        }

        private static string GetColorSourceName(StylisticFogEffect.ColorSource source)
        {
            switch (source)
            {
                case StylisticFogEffect.ColorSource.Gradient: return "グラデーション";
                case StylisticFogEffect.ColorSource.CopyOther: return "もう一方と共通";
                case StylisticFogEffect.ColorSource.TextureRamp: return "ランプテクスチャ";
                default: return source.ToString();
            }
        }

        private readonly GUIComboBox<StylisticFogEffect.ColorSource> _distanceSourceComboBox = CreateColorSourceComboBox();
        private readonly GUIComboBox<StylisticFogEffect.ColorSource> _heightSourceComboBox = CreateColorSourceComboBox();

        public override void DrawContent(GUIView view)
        {
            view.DrawToggle("距離フォグ", setting.distanceFogEnabled, 250, 20, value =>
            {
                setting.distanceFogEnabled = value;
                SetDirty();
            });

            if (setting.distanceFogEnabled)
            {
                DrawColorSource(view, _distanceSourceComboBox, setting.distanceColorSource,
                    source => setting.distanceColorSource = source);

                view.DrawToggle("空にも掛ける", setting.distanceFogSkybox, 250, 20, value =>
                {
                    setting.distanceFogSkybox = value;
                    SetDirty();
                });

                DrawColorSourceDetail(view, setting.distanceColorSource, _distanceRampCache,
                    "距離フォグ", setting.distanceRampPath, path => setting.distanceRampPath = path,
                    setting.distanceFirstColor, color => setting.distanceFirstColor = color,
                    setting.distanceLastColor, color => setting.distanceLastColor = color);

                DrawSlider(view, "到達距離", 0f, 200f, 100f,
                    setting.distanceFogEndDistance, v => setting.distanceFogEndDistance = v);
            }

            view.DrawHorizontalLine(Color.gray);

            view.DrawToggle("高さフォグ", setting.heightFogEnabled, 250, 20, value =>
            {
                setting.heightFogEnabled = value;
                SetDirty();
            });

            if (setting.heightFogEnabled)
            {
                DrawColorSource(view, _heightSourceComboBox, setting.heightColorSource,
                    source => setting.heightColorSource = source);

                view.DrawToggle("空にも掛ける", setting.heightFogSkybox, 250, 20, value =>
                {
                    setting.heightFogSkybox = value;
                    SetDirty();
                });

                DrawColorSourceDetail(view, setting.heightColorSource, _heightRampCache,
                    "高さフォグ", setting.heightRampPath, path => setting.heightRampPath = path,
                    setting.heightFirstColor, color => setting.heightFirstColor = color,
                    setting.heightLastColor, color => setting.heightLastColor = color);

                DrawSlider(view, "基準の高さ", -10f, 10f, 0f,
                    setting.heightFogBaseHeight, v => setting.heightFogBaseHeight = v);
                DrawSlider(view, "基準の濃度", 0f, 2f, 0.1f,
                    setting.heightFogBaseDensity, v => setting.heightFogBaseDensity = v);
                DrawSlider(view, "減衰率", 0.001f, 1f, 0.5f,
                    setting.heightFogDensityFalloff, v => setting.heightFogDensityFalloff = v);
            }

            if (setting.distanceColorSource == StylisticFogEffect.ColorSource.CopyOther &&
                setting.heightColorSource == StylisticFogEffect.ColorSource.CopyOther)
            {
                view.DrawLabel("両方を「もう一方と共通」にはできないため、距離側をグラデーションとして扱います", -1, 20, Color.yellow);
            }
        }

        private void DrawColorSource(
            GUIView view,
            GUIComboBox<StylisticFogEffect.ColorSource> comboBox,
            StylisticFogEffect.ColorSource current,
            System.Action<StylisticFogEffect.ColorSource> onChanged)
        {
            view.BeginHorizontal();
            {
                view.DrawLabel("色の指定", 70, 20);
                comboBox.currentIndex = (int)current;
                comboBox.onSelected = (source, _) => { onChanged(source); SetDirty(); };
                comboBox.DrawButton(view);
            }
            view.EndLayout();
        }

        private void DrawColorSourceDetail(
            GUIView view,
            StylisticFogEffect.ColorSource source,
            TextureFileCache rampCache,
            string label,
            string rampPath,
            System.Action<string> onRampPathChanged,
            Color firstColor,
            System.Action<Color> onFirstColorChanged,
            Color lastColor,
            System.Action<Color> onLastColorChanged)
        {
            if (source == StylisticFogEffect.ColorSource.TextureRamp)
            {
                rampCache.DrawPathField(view, label + "のランプテクスチャ", rampPath,
                    value => { onRampPathChanged(value); SetDirty(); });
                return;
            }

            if (source == StylisticFogEffect.ColorSource.CopyOther)
            {
                return;
            }

            view.DrawColor(
                view.GetColorFieldCache("開始色", true),
                firstColor,
                DefaultFirstColor,
                color => { onFirstColorChanged(color); SetDirty(); });

            view.DrawColor(
                view.GetColorFieldCache("終了色", true),
                lastColor,
                DefaultLastColor,
                color => { onLastColorChanged(color); SetDirty(); });
        }
    }
}
