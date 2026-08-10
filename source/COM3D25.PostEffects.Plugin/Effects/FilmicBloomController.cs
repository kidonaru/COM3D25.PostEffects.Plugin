using COM3D2.MotionTimelineEditor;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    public class FilmicBloomSetting
    {
        public bool enabled = false;
        public float threshold = 1.1f;
        public float softKnee = 0.5f;
        public float radius = 1f;
        public float intensity = 2f;
        public bool highQuality = true;
        public bool antiFlicker = false;
        public bool useDirtTexture = false;
        public float dirtIntensity = 0f;
        // 絶対パス、または Config フォルダからの相対パス
        public string dirtTexturePath = "";

        public float streakThreshold = 1f;
        public float streakSoftKnee = 0.5f;
        public float streakStretch = 0.75f;
        public float streakIntensity = 0.3f;
        public bool streakVertical = false;
        public bool streak2Way = false;
        public Color streakTint = new Color(0.55f, 0.55f, 0.55f);
        public FilmicBloomEffect.BlendMode blendMode = FilmicBloomEffect.BlendMode.Screen;
    }

    public class FilmicBloomController : EffectControllerBase<FilmicBloomEffect, FilmicBloomSetting>
    {
        public override string effectName => "フィルミックブルーム";

        private static readonly Color DefaultStreakTint = new Color(0.55f, 0.55f, 0.55f);

        protected override FilmicBloomSetting setting
        {
            get => settings.filmicBloom;
            set => settings.filmicBloom = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        private readonly TextureFileCache _dirtTextureCache = new TextureFileCache(TextureFileCache.SUB_DIR_LENS_DIRT);

        protected override void ApplySetting(FilmicBloomEffect component)
        {
            if (component.bloomShader == null)
            {
                component.bloomShader = EffectShaders.GetShader(EffectShaders.Filmic, "filmicbloomshader");
            }
            if (component.streakShader == null)
            {
                component.streakShader = EffectShaders.GetShader(EffectShaders.Filmic, "filmicstreakshader");
            }

            component.threshold = setting.threshold;
            component.softKnee = setting.softKnee;
            component.radius = setting.radius;
            component.intensity = setting.intensity;
            component.highQuality = setting.highQuality;
            component.antiFlicker = setting.antiFlicker;
            component.useDirtTexture = setting.useDirtTexture;
            component.dirtIntensity = setting.dirtIntensity;
            component.dirtTexture = setting.useDirtTexture
                ? _dirtTextureCache.GetOrLoad(setting.dirtTexturePath)
                : null;

            component.streakThreshold = setting.streakThreshold;
            component.streakSoftKnee = setting.streakSoftKnee;
            component.streakStretch = setting.streakStretch;
            component.streakIntensity = setting.streakIntensity;
            component.streakVertical = setting.streakVertical;
            component.streak2Way = setting.streak2Way;
            component.streakTint = setting.streakTint;
            component.blendMode = setting.blendMode;
        }

        protected override void Capture(FilmicBloomEffect component)
        {
            var c = _capturedSetting;
            _capturedEnabled = component.enabled;
            c.threshold = component.threshold;
            c.softKnee = component.softKnee;
            c.radius = component.radius;
            c.intensity = component.intensity;
            c.highQuality = component.highQuality;
            c.antiFlicker = component.antiFlicker;
            c.useDirtTexture = component.useDirtTexture;
            c.dirtIntensity = component.dirtIntensity;
            c.streakThreshold = component.streakThreshold;
            c.streakSoftKnee = component.streakSoftKnee;
            c.streakStretch = component.streakStretch;
            c.streakIntensity = component.streakIntensity;
            c.streakVertical = component.streakVertical;
            c.streak2Way = component.streak2Way;
            c.streakTint = component.streakTint;
            c.blendMode = component.blendMode;
        }

        protected override void RestoreSetting(FilmicBloomEffect component)
        {
            var c = _capturedSetting;
            component.enabled = _capturedEnabled;
            component.threshold = c.threshold;
            component.softKnee = c.softKnee;
            component.radius = c.radius;
            component.intensity = c.intensity;
            component.highQuality = c.highQuality;
            component.antiFlicker = c.antiFlicker;
            component.useDirtTexture = c.useDirtTexture;
            component.dirtIntensity = c.dirtIntensity;
            component.streakThreshold = c.streakThreshold;
            component.streakSoftKnee = c.streakSoftKnee;
            component.streakStretch = c.streakStretch;
            component.streakIntensity = c.streakIntensity;
            component.streakVertical = c.streakVertical;
            component.streak2Way = c.streak2Way;
            component.streakTint = c.streakTint;
            component.blendMode = c.blendMode;
        }

        private readonly GUIComboBox<FilmicBloomEffect.BlendMode> _blendComboBox =
            new GUIComboBox<FilmicBloomEffect.BlendMode>
            {
                items = MTEUtils.GetEnumValues<FilmicBloomEffect.BlendMode>(),
                getName = (mode, _) => mode.ToString(),
                buttonSize = new Vector2(100, 20),
            };

        public override void DrawContent(GUIView view)
        {
            view.DrawLabel("ブルーム", -1, 20);
            DrawSlider(view, "しきい値", 0f, 3f, 1.1f, setting.threshold, v => setting.threshold = v);
            DrawSlider(view, "ソフトニー", 0f, 4f, 0.5f, setting.softKnee, v => setting.softKnee = v);
            DrawSlider(view, "半径", 0f, 8f, 1f, setting.radius, v => setting.radius = v);
            DrawSlider(view, "強度", 0f, 10f, 2f, setting.intensity, v => setting.intensity = v);

            view.DrawToggle("高品質 (フル解像度)", setting.highQuality, 250, 20, value =>
            {
                setting.highQuality = value;
                SetDirty();
            });

            view.DrawToggle("ちらつき対策", setting.antiFlicker, 250, 20, value =>
            {
                setting.antiFlicker = value;
                SetDirty();
            });

            view.DrawToggle("レンズダートを使う", setting.useDirtTexture, 250, 20, value =>
            {
                setting.useDirtTexture = value;
                SetDirty();
            });

            if (setting.useDirtTexture)
            {
                _dirtTextureCache.DrawPathField(view, "ダートテクスチャパス", setting.dirtTexturePath,
                    value => { setting.dirtTexturePath = value; SetDirty(); });
                DrawSlider(view, "ダート強度", 0f, 20f, 0f, setting.dirtIntensity, v => setting.dirtIntensity = v);
            }

            view.DrawHorizontalLine(Color.gray);
            view.DrawLabel("光条", -1, 20);

            view.BeginHorizontal();
            {
                view.DrawLabel("ブレンド", 60, 20);
                _blendComboBox.currentIndex = (int)setting.blendMode;
                _blendComboBox.onSelected = (mode, _) => { setting.blendMode = mode; SetDirty(); };
                _blendComboBox.DrawButton(view);
            }
            view.EndLayout();

            DrawSlider(view, "しきい値", 0f, 5f, 1f, setting.streakThreshold, v => setting.streakThreshold = v);
            DrawSlider(view, "ソフトニー", 0f, 4f, 0.5f, setting.streakSoftKnee, v => setting.streakSoftKnee = v);
            DrawSlider(view, "伸び", 0f, 1f, 0.75f, setting.streakStretch, v => setting.streakStretch = v);
            DrawSlider(view, "強度", 0f, 3f, 0.3f, setting.streakIntensity, v => setting.streakIntensity = v);

            view.DrawColor(
                view.GetColorFieldCache("色", false),
                setting.streakTint,
                DefaultStreakTint,
                color => { setting.streakTint = color; SetDirty(); });

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
