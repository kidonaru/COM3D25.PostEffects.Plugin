using COM3D2.MotionTimelineEditor;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    public class CinematicLensAberrationsSetting
    {
        public bool enabled = false;

        public bool distortionEnabled = false;
        public float distortionAmount = 0f;
        public float distortionCenterX = 0f;
        public float distortionCenterY = 0f;
        public float distortionAmountX = 1f;
        public float distortionAmountY = 1f;
        public float distortionScale = 1f;

        public bool vignetteEnabled = false;
        public Color vignetteColor = Color.black;
        public float vignetteCenterX = 0.5f;
        public float vignetteCenterY = 0.5f;
        public float vignetteIntensity = 1.4f;
        public float vignetteSmoothness = 0.8f;
        public float vignetteRoundness = 1f;
        public float vignetteBlur = 0f;
        public float vignetteDesaturate = 0f;

        public bool chromaticAberrationEnabled = false;
        public Color chromaticAberrationColor = Color.green;
        public float chromaticAberrationAmount = 0f;
    }

    public class CinematicLensAberrationsController
        : EffectControllerBase<CinematicLensAberrationsEffect, CinematicLensAberrationsSetting>
    {
        public override string effectName => "レンズ収差";

        protected override CinematicLensAberrationsSetting setting
        {
            get => settings.cinematicLensAberrations;
            set => settings.cinematicLensAberrations = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        protected override void ApplySetting(CinematicLensAberrationsEffect component)
        {
            if (component.shader == null)
            {
                component.shader = EffectShaders.GetShader(
                    EffectShaders.Cinematic, "cinematiclensaberrationsshader");
            }

            component.distortionEnabled = setting.distortionEnabled;
            component.distortionAmount = setting.distortionAmount;
            component.distortionCenterX = setting.distortionCenterX;
            component.distortionCenterY = setting.distortionCenterY;
            component.distortionAmountX = setting.distortionAmountX;
            component.distortionAmountY = setting.distortionAmountY;
            component.distortionScale = setting.distortionScale;

            component.vignetteEnabled = setting.vignetteEnabled;
            component.vignetteColor = setting.vignetteColor;
            component.vignetteCenter = new Vector2(setting.vignetteCenterX, setting.vignetteCenterY);
            component.vignetteIntensity = setting.vignetteIntensity;
            component.vignetteSmoothness = setting.vignetteSmoothness;
            component.vignetteRoundness = setting.vignetteRoundness;
            component.vignetteBlur = setting.vignetteBlur;
            component.vignetteDesaturate = setting.vignetteDesaturate;

            component.chromaticAberrationEnabled = setting.chromaticAberrationEnabled;
            component.chromaticAberrationColor = setting.chromaticAberrationColor;
            component.chromaticAberrationAmount = setting.chromaticAberrationAmount;
        }

        protected override void Capture(CinematicLensAberrationsEffect component)
        {
            var c = _capturedSetting;
            _capturedEnabled = component.enabled;
            c.distortionEnabled = component.distortionEnabled;
            c.distortionAmount = component.distortionAmount;
            c.distortionCenterX = component.distortionCenterX;
            c.distortionCenterY = component.distortionCenterY;
            c.distortionAmountX = component.distortionAmountX;
            c.distortionAmountY = component.distortionAmountY;
            c.distortionScale = component.distortionScale;
            c.vignetteEnabled = component.vignetteEnabled;
            c.vignetteColor = component.vignetteColor;
            c.vignetteCenterX = component.vignetteCenter.x;
            c.vignetteCenterY = component.vignetteCenter.y;
            c.vignetteIntensity = component.vignetteIntensity;
            c.vignetteSmoothness = component.vignetteSmoothness;
            c.vignetteRoundness = component.vignetteRoundness;
            c.vignetteBlur = component.vignetteBlur;
            c.vignetteDesaturate = component.vignetteDesaturate;
            c.chromaticAberrationEnabled = component.chromaticAberrationEnabled;
            c.chromaticAberrationColor = component.chromaticAberrationColor;
            c.chromaticAberrationAmount = component.chromaticAberrationAmount;
        }

        protected override void RestoreSetting(CinematicLensAberrationsEffect component)
        {
            var c = _capturedSetting;
            component.enabled = _capturedEnabled;
            component.distortionEnabled = c.distortionEnabled;
            component.distortionAmount = c.distortionAmount;
            component.distortionCenterX = c.distortionCenterX;
            component.distortionCenterY = c.distortionCenterY;
            component.distortionAmountX = c.distortionAmountX;
            component.distortionAmountY = c.distortionAmountY;
            component.distortionScale = c.distortionScale;
            component.vignetteEnabled = c.vignetteEnabled;
            component.vignetteColor = c.vignetteColor;
            component.vignetteCenter = new Vector2(c.vignetteCenterX, c.vignetteCenterY);
            component.vignetteIntensity = c.vignetteIntensity;
            component.vignetteSmoothness = c.vignetteSmoothness;
            component.vignetteRoundness = c.vignetteRoundness;
            component.vignetteBlur = c.vignetteBlur;
            component.vignetteDesaturate = c.vignetteDesaturate;
            component.chromaticAberrationEnabled = c.chromaticAberrationEnabled;
            component.chromaticAberrationColor = c.chromaticAberrationColor;
            component.chromaticAberrationAmount = c.chromaticAberrationAmount;
        }

        public override void DrawContent(GUIView view)
        {
            view.DrawToggle("歪み", setting.distortionEnabled, 250, 20, value =>
            {
                setting.distortionEnabled = value;
                SetDirty();
            });

            if (setting.distortionEnabled)
            {
                DrawSlider(view, "歪み量", -100f, 100f, 0f, setting.distortionAmount,
                    v => setting.distortionAmount = v);
                DrawSlider(view, "中心 X", -1f, 1f, 0f, setting.distortionCenterX,
                    v => setting.distortionCenterX = v);
                DrawSlider(view, "中心 Y", -1f, 1f, 0f, setting.distortionCenterY,
                    v => setting.distortionCenterY = v);
                DrawSlider(view, "X 方向倍率", 0f, 1f, 1f, setting.distortionAmountX,
                    v => setting.distortionAmountX = v);
                DrawSlider(view, "Y 方向倍率", 0f, 1f, 1f, setting.distortionAmountY,
                    v => setting.distortionAmountY = v);
                DrawSlider(view, "画面スケール", 0.01f, 5f, 1f, setting.distortionScale,
                    v => setting.distortionScale = v);
            }

            view.DrawHorizontalLine(Color.gray);

            view.DrawToggle("色収差", setting.chromaticAberrationEnabled, 250, 20, value =>
            {
                setting.chromaticAberrationEnabled = value;
                SetDirty();
            });

            if (setting.chromaticAberrationEnabled)
            {
                view.DrawColor(
                    view.GetColorFieldCache("ずらす色", false),
                    setting.chromaticAberrationColor,
                    Color.green,
                    color => { setting.chromaticAberrationColor = color; SetDirty(); });
                DrawSlider(view, "ずれ量", -50f, 50f, 0f, setting.chromaticAberrationAmount,
                    v => setting.chromaticAberrationAmount = v);
            }

            view.DrawHorizontalLine(Color.gray);

            view.DrawToggle("ビネット", setting.vignetteEnabled, 250, 20, value =>
            {
                setting.vignetteEnabled = value;
                SetDirty();
            });

            if (setting.vignetteEnabled)
            {
                view.DrawColor(
                    view.GetColorFieldCache("ビネット色", true),
                    setting.vignetteColor,
                    Color.black,
                    color => { setting.vignetteColor = color; SetDirty(); });
                DrawSlider(view, "強度", 0f, 3f, 1.4f, setting.vignetteIntensity,
                    v => setting.vignetteIntensity = v);
                DrawSlider(view, "境界のぼかし", 0.01f, 3f, 0.8f, setting.vignetteSmoothness,
                    v => setting.vignetteSmoothness = v);
                // 1 で真円、下げるほど角張る
                DrawSlider(view, "丸み", 0f, 1f, 1f, setting.vignetteRoundness,
                    v => setting.vignetteRoundness = v);
                DrawSlider(view, "周辺ぼかし", 0f, 1f, 0f, setting.vignetteBlur,
                    v => setting.vignetteBlur = v);
                DrawSlider(view, "周辺の彩度低下", 0f, 1f, 0f, setting.vignetteDesaturate,
                    v => setting.vignetteDesaturate = v);
                DrawSlider(view, "中心 X", 0f, 1f, 0.5f, setting.vignetteCenterX,
                    v => setting.vignetteCenterX = v);
                DrawSlider(view, "中心 Y", 0f, 1f, 0.5f, setting.vignetteCenterY,
                    v => setting.vignetteCenterY = v);
            }
        }
    }
}
