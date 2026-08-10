using COM3D2.MotionTimelineEditor;

namespace COM3D25.PostEffects.Plugin
{
    public class CinematicBloomSetting
    {
        public bool enabled = false;
        public float threshold = 1.1f;
        public float softKnee = 0.5f;
        public float radius = 1f;
        public float intensity = 2f;
        public float maxIntensity = 2f;
        public bool highQuality = true;
        public bool antiFlicker = false;
        public bool useDirtTexture = false;
        public float dirtIntensity = 0f;
        // 絶対パス、または Config フォルダからの相対パス
        public string dirtTexturePath = "";
    }

    public class CinematicBloomController : EffectControllerBase<CinematicBloomEffect, CinematicBloomSetting>
    {
        public override string effectName => "シネマティックブルーム";

        protected override CinematicBloomSetting setting
        {
            get => settings.cinematicBloom;
            set => settings.cinematicBloom = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        private readonly TextureFileCache _dirtTextureCache = new TextureFileCache(TextureFileCache.SUB_DIR_LENS_DIRT);

        protected override void ApplySetting(CinematicBloomEffect component)
        {
            if (component.shader == null)
            {
                component.shader = EffectShaders.GetShader(EffectShaders.Cinematic, "cinematicbloomshader");
            }

            component.threshold = setting.threshold;
            component.softKnee = setting.softKnee;
            component.radius = setting.radius;
            component.intensity = setting.intensity;
            component.maxIntensity = setting.maxIntensity;
            component.highQuality = setting.highQuality;
            component.antiFlicker = setting.antiFlicker;
            component.useDirtTexture = setting.useDirtTexture;
            component.dirtIntensity = setting.dirtIntensity;
            component.dirtTexture = setting.useDirtTexture
                ? _dirtTextureCache.GetOrLoad(setting.dirtTexturePath)
                : null;
        }

        protected override void Capture(CinematicBloomEffect component)
        {
            var c = _capturedSetting;
            _capturedEnabled = component.enabled;
            c.threshold = component.threshold;
            c.softKnee = component.softKnee;
            c.radius = component.radius;
            c.intensity = component.intensity;
            c.maxIntensity = component.maxIntensity;
            c.highQuality = component.highQuality;
            c.antiFlicker = component.antiFlicker;
            c.useDirtTexture = component.useDirtTexture;
            c.dirtIntensity = component.dirtIntensity;
        }

        protected override void RestoreSetting(CinematicBloomEffect component)
        {
            var c = _capturedSetting;
            component.enabled = _capturedEnabled;
            component.threshold = c.threshold;
            component.softKnee = c.softKnee;
            component.radius = c.radius;
            component.intensity = c.intensity;
            component.maxIntensity = c.maxIntensity;
            component.highQuality = c.highQuality;
            component.antiFlicker = c.antiFlicker;
            component.useDirtTexture = c.useDirtTexture;
            component.dirtIntensity = c.dirtIntensity;
        }

        public override void DrawContent(GUIView view)
        {
            DrawSlider(view, "しきい値", 0f, 3f, 1.1f, setting.threshold, v => setting.threshold = v);
            DrawSlider(view, "ソフトニー", 0f, 4f, 0.5f, setting.softKnee, v => setting.softKnee = v);
            DrawSlider(view, "半径", 0f, 8f, 1f, setting.radius, v => setting.radius = v);
            DrawSlider(view, "強度", 0f, 10f, 2f, setting.intensity, v => setting.intensity = v);
            DrawSlider(view, "最大強度", 0f, 10f, 2f, setting.maxIntensity, v => setting.maxIntensity = v);

            view.DrawToggle("高品質 (フル解像度)", setting.highQuality, 250, 20, value =>
            {
                setting.highQuality = value;
                SetDirty();
            });

            // ちらつき対策は低品質時のプリフィルタでしかサンプル位置を変えないが、
            // 縮小 1 段目のパス選択には品質に関係なく効く
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
        }
    }
}
