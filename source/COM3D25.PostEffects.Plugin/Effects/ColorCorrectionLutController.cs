using COM3D2.MotionTimelineEditor;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    public class ColorCorrectionLutSetting
    {
        public bool enabled = false;
        public float contribution = 1f;
        // 絶対パス、または Config フォルダからの相対パス。未指定なら無変換テーブル
        public string lutTexturePath = "";
    }

    public class ColorCorrectionLutController : EffectControllerBase<ColorCorrectionLutEffect, ColorCorrectionLutSetting>
    {
        public override string effectName => "LUT 色補正";

        protected override ColorCorrectionLutSetting setting
        {
            get => settings.colorCorrectionLut;
            set => settings.colorCorrectionLut = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        // LUT は画素値をそのまま座標として使うためガンマ補正をかけずに読み込む
        private readonly TextureFileCache _lutTextureCache = new TextureFileCache(TextureFileCache.SUB_DIR_LUT, linear: true);

        protected override void ApplySetting(ColorCorrectionLutEffect component)
        {
            if (component.shader == null)
            {
                component.shader = EffectShaders.GetShader(EffectShaders.ImageEffects, "colorcorrectionlutshader");
            }

            component.contribution = setting.contribution;
            component.lutTexture = _lutTextureCache.GetOrLoad(setting.lutTexturePath);
        }

        private Texture2D _capturedLutTexture;

        protected override void Capture(ColorCorrectionLutEffect component)
        {
            var c = _capturedSetting;
            _capturedEnabled = component.enabled;
            c.contribution = component.contribution;
            _capturedLutTexture = component.lutTexture;
        }

        protected override void RestoreSetting(ColorCorrectionLutEffect component)
        {
            var c = _capturedSetting;
            component.enabled = _capturedEnabled;
            component.contribution = c.contribution;
            component.lutTexture = _capturedLutTexture;
        }

        public override void DrawContent(GUIView view)
        {
            if (!SystemInfo.supports3DTextures)
            {
                view.DrawLabel("この環境は 3D テクスチャに非対応のため使用できません", -1, 20, Color.red);
                return;
            }

            DrawSlider(view, "適用量", 0f, 1f, 1f, setting.contribution, v => setting.contribution = v);

            _lutTextureCache.DrawPathField(view, "LUT テクスチャパス", setting.lutTexturePath,
                value => { setting.lutTexturePath = value; SetDirty(); });
            view.DrawLabel("横一列に並んだ LUT 画像 (幅 = 高さの 2 乗。例: 256x16)", -1, 20);
        }
    }
}
