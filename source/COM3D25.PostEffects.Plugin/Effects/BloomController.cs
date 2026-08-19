using COM3D2.MotionTimelineEditor;
using UnityEngine;
// Assembly-UnityScript-firstpass のグローバル名前空間にも旧 Bloom が残骸として存在するため、
// ゲームが実際に使う PostEffects_Dummy 側へエイリアスで束縛する
#if COM3D25
using BloomEffect = PostEffects_Dummy.Bloom;
#else
// COM3D2 (2.0) の内蔵エフェクトはグローバル名前空間 (Assembly-UnityScript-firstpass) にある
using BloomEffect = global::Bloom;
#endif

namespace COM3D25.PostEffects.Plugin
{
    public class BloomSetting
    {
        public bool enabled = false;
        // ゲーム標準のブルーム (CameraMain が毎フレーム有効化する) を強制無効化する
        public bool gameEffectDisabled = false;
        public BloomEffect.HDRBloomMode hdr = BloomEffect.HDRBloomMode.Auto;
        // 既定値はゲームがメインカメラの Bloom に設定している値に合わせてある
        public BloomEffect.BloomScreenBlendMode screenBlendMode = BloomEffect.BloomScreenBlendMode.Screen;
        public bool highQuality = true;
        public float intensity = 2.1375f;
        public float threshold = 0.7f;
        public Color thresholdColor = Color.white;
        public int blurIterations = 3;
        public float blurSpread = 3.48f;

        // レンズフレア
        public BloomEffect.LensFlareStyle lensFlareMode = BloomEffect.LensFlareStyle.Anamorphic;
        public float lensFlareIntensity = 0f;
        public float lensFlareSaturation = 0.75f;
        public float lensFlareThreshold = 0.3f;
        public float flareRotation = 0f;
        public float hollyStretchWidth = 2.5f;
        public int hollywoodFlareBlurIterations = 2;
        public Color flareColorA = new Color(0.4f, 0.4f, 0.8f, 0.75f);
        public Color flareColorB = new Color(0.4f, 0.8f, 0.8f, 0.75f);
        public Color flareColorC = new Color(0.8f, 0.4f, 0.8f, 0.75f);
        public Color flareColorD = new Color(0.8f, 0.4f, 0f, 0.75f);
    }

    public class BloomController : EffectControllerBase<BloomEffect, BloomSetting>
    {
        public override string effectName => "ブルーム";

        protected override BloomSetting setting
        {
            get => settings.bloom;
            set => settings.bloom = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        public override bool canDisableGameEffect => true;

        public override bool gameEffectDisabled
        {
            get => setting.gameEffectDisabled;
            set => setting.gameEffectDisabled = value;
        }

        protected override void ApplySetting(BloomEffect component)
        {
            component.hdr = setting.hdr;
            component.screenBlendMode = setting.screenBlendMode;
            component.quality = setting.highQuality ? BloomEffect.BloomQuality.High : BloomEffect.BloomQuality.Cheap;
            component.bloomIntensity = setting.intensity;
            component.bloomThreshhold = setting.threshold;
            component.bloomThreshholdColor = setting.thresholdColor;
            component.bloomBlurIterations = setting.blurIterations;
            component.sepBlurSpread = setting.blurSpread;

            component.lensflareMode = setting.lensFlareMode;
            component.lensflareIntensity = setting.lensFlareIntensity;
            component.lensFlareSaturation = setting.lensFlareSaturation;
            component.lensflareThreshhold = setting.lensFlareThreshold;
            component.flareRotation = setting.flareRotation;
            component.hollyStretchWidth = setting.hollyStretchWidth;
            component.hollywoodFlareBlurIterations = setting.hollywoodFlareBlurIterations;
            component.flareColorA = setting.flareColorA;
            component.flareColorB = setting.flareColorB;
            component.flareColorC = setting.flareColorC;
            component.flareColorD = setting.flareColorD;
        }

        protected override void Capture(BloomEffect component)
        {
            var c = _capturedSetting;
            _capturedEnabled = component.enabled;
            c.hdr = component.hdr;
            c.screenBlendMode = component.screenBlendMode;
            c.highQuality = component.quality == BloomEffect.BloomQuality.High;
            c.intensity = component.bloomIntensity;
            c.threshold = component.bloomThreshhold;
            c.thresholdColor = component.bloomThreshholdColor;
            c.blurIterations = component.bloomBlurIterations;
            c.blurSpread = component.sepBlurSpread;
            c.lensFlareMode = component.lensflareMode;
            c.lensFlareIntensity = component.lensflareIntensity;
            c.lensFlareSaturation = component.lensFlareSaturation;
            c.lensFlareThreshold = component.lensflareThreshhold;
            c.flareRotation = component.flareRotation;
            c.hollyStretchWidth = component.hollyStretchWidth;
            c.hollywoodFlareBlurIterations = component.hollywoodFlareBlurIterations;
            c.flareColorA = component.flareColorA;
            c.flareColorB = component.flareColorB;
            c.flareColorC = component.flareColorC;
            c.flareColorD = component.flareColorD;
        }

        protected override void RestoreSetting(BloomEffect component)
        {
            var c = _capturedSetting;
            // enabled と bloomIntensity は CameraMain.Update が毎フレーム
            // ゲーム設定値で上書きするため、ここで書き戻さなくても自然に復帰する
            component.enabled = _capturedEnabled;
            component.hdr = c.hdr;
            component.screenBlendMode = c.screenBlendMode;
            component.quality = c.highQuality ? BloomEffect.BloomQuality.High : BloomEffect.BloomQuality.Cheap;
            component.bloomIntensity = c.intensity;
            component.bloomThreshhold = c.threshold;
            component.bloomThreshholdColor = c.thresholdColor;
            component.bloomBlurIterations = c.blurIterations;
            component.sepBlurSpread = c.blurSpread;
            component.lensflareMode = c.lensFlareMode;
            component.lensflareIntensity = c.lensFlareIntensity;
            component.lensFlareSaturation = c.lensFlareSaturation;
            component.lensflareThreshhold = c.lensFlareThreshold;
            component.flareRotation = c.flareRotation;
            component.hollyStretchWidth = c.hollyStretchWidth;
            component.hollywoodFlareBlurIterations = c.hollywoodFlareBlurIterations;
            component.flareColorA = c.flareColorA;
            component.flareColorB = c.flareColorB;
            component.flareColorC = c.flareColorC;
            component.flareColorD = c.flareColorD;
        }

        private GUIComboBox<BloomEffect.HDRBloomMode> _hdrComboBox = new GUIComboBox<BloomEffect.HDRBloomMode>
        {
            items = MTEUtils.GetEnumValues<BloomEffect.HDRBloomMode>(),
            getName = (mode, _) => mode.ToString(),
            buttonSize = new Vector2(100, 20),
        };

        private GUIComboBox<BloomEffect.BloomScreenBlendMode> _blendComboBox = new GUIComboBox<BloomEffect.BloomScreenBlendMode>
        {
            items = MTEUtils.GetEnumValues<BloomEffect.BloomScreenBlendMode>(),
            getName = (mode, _) => mode.ToString(),
            buttonSize = new Vector2(100, 20),
        };

        private GUIComboBox<BloomEffect.LensFlareStyle> _flareComboBox = new GUIComboBox<BloomEffect.LensFlareStyle>
        {
            items = MTEUtils.GetEnumValues<BloomEffect.LensFlareStyle>(),
            getName = (mode, _) => mode.ToString(),
            buttonSize = new Vector2(100, 20),
        };

        public override void DrawContent(GUIView view)
        {
            view.BeginHorizontal();
            {
                view.DrawLabel("HDR", 60, 20);
                _hdrComboBox.currentIndex = (int)setting.hdr;
                _hdrComboBox.onSelected = (mode, _) => { setting.hdr = mode; SetDirty(); };
                _hdrComboBox.DrawButton(view);

                view.DrawLabel("ブレンド", 60, 20);
                _blendComboBox.currentIndex = (int)setting.screenBlendMode;
                _blendComboBox.onSelected = (mode, _) => { setting.screenBlendMode = mode; SetDirty(); };
                _blendComboBox.DrawButton(view);
            }
            view.EndLayout();

            view.DrawToggle("高品質", setting.highQuality, 200, 20, value =>
            {
                setting.highQuality = value;
                SetDirty();
            });

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "強度",
                labelWidth = 80,
                width = -1,
                min = 0f,
                max = 5f,
                step = 0.01f,
                defaultValue = 2.1375f,
                value = setting.intensity,
                onChanged = value => { setting.intensity = value; SetDirty(); },
            });

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "しきい値",
                labelWidth = 80,
                width = -1,
                min = 0f,
                max = 2f,
                step = 0.01f,
                defaultValue = 0.7f,
                value = setting.threshold,
                onChanged = value => { setting.threshold = value; SetDirty(); },
            });

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "ブラー回数",
                labelWidth = 80,
                width = -1,
                fieldType = FloatFieldType.Int,
                min = 1,
                max = 10,
                step = 1,
                defaultValue = 3,
                value = setting.blurIterations,
                onChanged = value => { setting.blurIterations = (int)value; SetDirty(); },
            });

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "ブラー広がり",
                labelWidth = 80,
                width = -1,
                min = 0f,
                max = 10f,
                step = 0.01f,
                defaultValue = 3.48f,
                value = setting.blurSpread,
                onChanged = value => { setting.blurSpread = value; SetDirty(); },
            });

            view.DrawColor(
                view.GetColorFieldCache("しきい値色", false),
                setting.thresholdColor,
                Color.white,
                color => { setting.thresholdColor = color; SetDirty(); });

            view.DrawHorizontalLine(Color.gray);

            view.DrawLabel("レンズフレア", 120, 20);

            view.BeginHorizontal();
            {
                view.DrawLabel("スタイル", 60, 20);
                _flareComboBox.currentIndex = (int)setting.lensFlareMode;
                _flareComboBox.onSelected = (mode, _) => { setting.lensFlareMode = mode; SetDirty(); };
                _flareComboBox.DrawButton(view);
            }
            view.EndLayout();

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "強度",
                labelWidth = 80,
                width = -1,
                min = 0f,
                max = 10f,
                step = 0.01f,
                defaultValue = 0f,
                value = setting.lensFlareIntensity,
                onChanged = value => { setting.lensFlareIntensity = value; SetDirty(); },
            });

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "彩度",
                labelWidth = 80,
                width = -1,
                min = 0f,
                max = 1f,
                step = 0.01f,
                defaultValue = 0.75f,
                value = setting.lensFlareSaturation,
                onChanged = value => { setting.lensFlareSaturation = value; SetDirty(); },
            });

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "しきい値",
                labelWidth = 80,
                width = -1,
                min = 0f,
                max = 2f,
                step = 0.01f,
                defaultValue = 0.3f,
                value = setting.lensFlareThreshold,
                onChanged = value => { setting.lensFlareThreshold = value; SetDirty(); },
            });

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "回転",
                labelWidth = 80,
                width = -1,
                min = 0f,
                max = 6.28f,
                step = 0.01f,
                defaultValue = 0f,
                value = setting.flareRotation,
                onChanged = value => { setting.flareRotation = value; SetDirty(); },
            });

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "伸縮幅",
                labelWidth = 80,
                width = -1,
                min = 0f,
                max = 10f,
                step = 0.01f,
                defaultValue = 2.5f,
                value = setting.hollyStretchWidth,
                onChanged = value => { setting.hollyStretchWidth = value; SetDirty(); },
            });

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "ブラー回数",
                labelWidth = 80,
                width = -1,
                fieldType = FloatFieldType.Int,
                min = 0,
                max = 10,
                step = 1,
                defaultValue = 2,
                value = setting.hollywoodFlareBlurIterations,
                onChanged = value => { setting.hollywoodFlareBlurIterations = (int)value; SetDirty(); },
            });

            view.DrawColor(
                view.GetColorFieldCache("フレア色 A", true),
                setting.flareColorA,
                new Color(0.4f, 0.4f, 0.8f, 0.75f),
                color => { setting.flareColorA = color; SetDirty(); });

            view.DrawColor(
                view.GetColorFieldCache("フレア色 B", true),
                setting.flareColorB,
                new Color(0.4f, 0.8f, 0.8f, 0.75f),
                color => { setting.flareColorB = color; SetDirty(); });

            view.DrawColor(
                view.GetColorFieldCache("フレア色 C", true),
                setting.flareColorC,
                new Color(0.8f, 0.4f, 0.8f, 0.75f),
                color => { setting.flareColorC = color; SetDirty(); });

            view.DrawColor(
                view.GetColorFieldCache("フレア色 D", true),
                setting.flareColorD,
                new Color(0.8f, 0.4f, 0f, 0.75f),
                color => { setting.flareColorD = color; SetDirty(); });
        }
    }
}
