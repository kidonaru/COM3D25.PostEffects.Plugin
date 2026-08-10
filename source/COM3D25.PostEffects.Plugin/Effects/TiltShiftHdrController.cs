using COM3D2.MotionTimelineEditor;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    public class TiltShiftHdrSetting
    {
        public bool enabled = false;
        public TiltShiftHdr.TiltShiftMode mode = TiltShiftHdr.TiltShiftMode.TiltShiftMode;
        public TiltShiftHdr.TiltShiftQuality quality = TiltShiftHdr.TiltShiftQuality.Normal;
        public float blurArea = 1f;
        public float maxBlurSize = 5f;
        public int downsample = 0;
    }

    // TiltShiftHdr はゲーム側 Assembly-UnityScript-firstpass の実装をそのまま使う
    // (シェーダーフィールドは実行時追加では null のため imageeffects バンドルから補う)
    public class TiltShiftHdrController : EffectControllerBase<TiltShiftHdr, TiltShiftHdrSetting>
    {
        public override string effectName => "チルトシフト";

        protected override TiltShiftHdrSetting setting
        {
            get => settings.tiltShiftHdr;
            set => settings.tiltShiftHdr = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        protected override void ApplySetting(TiltShiftHdr component)
        {
            if (component.tiltShiftShader == null)
            {
                component.tiltShiftShader = EffectShaders.GetShader(EffectShaders.ImageEffects, "tiltshiftshader");
            }

            component.mode = setting.mode;
            component.quality = setting.quality;
            component.blurArea = setting.blurArea;
            component.maxBlurSize = setting.maxBlurSize;
            component.downsample = setting.downsample;
        }

        protected override void Capture(TiltShiftHdr component)
        {
            var c = _capturedSetting;
            _capturedEnabled = component.enabled;
            c.mode = component.mode;
            c.quality = component.quality;
            c.blurArea = component.blurArea;
            c.maxBlurSize = component.maxBlurSize;
            c.downsample = component.downsample;
        }

        protected override void RestoreSetting(TiltShiftHdr component)
        {
            var c = _capturedSetting;
            component.enabled = _capturedEnabled;
            component.mode = c.mode;
            component.quality = c.quality;
            component.blurArea = c.blurArea;
            component.maxBlurSize = c.maxBlurSize;
            component.downsample = c.downsample;
        }

        private readonly GUIComboBox<TiltShiftHdr.TiltShiftMode> _modeComboBox =
            new GUIComboBox<TiltShiftHdr.TiltShiftMode>
            {
                items = MTEUtils.GetEnumValues<TiltShiftHdr.TiltShiftMode>(),
                getName = (mode, _) => mode == TiltShiftHdr.TiltShiftMode.TiltShiftMode ? "帯状" : "円形",
                buttonSize = new Vector2(100, 20),
            };

        private readonly GUIComboBox<TiltShiftHdr.TiltShiftQuality> _qualityComboBox =
            new GUIComboBox<TiltShiftHdr.TiltShiftQuality>
            {
                items = MTEUtils.GetEnumValues<TiltShiftHdr.TiltShiftQuality>(),
                getName = (quality, _) => quality.ToString(),
                buttonSize = new Vector2(100, 20),
            };

        public override void DrawContent(GUIView view)
        {
            view.BeginHorizontal();
            {
                view.DrawLabel("ぼかし形状", 80, 20);
                _modeComboBox.currentIndex = (int)setting.mode;
                _modeComboBox.onSelected = (mode, _) => { setting.mode = mode; SetDirty(); };
                _modeComboBox.DrawButton(view);
            }
            view.EndLayout();

            view.BeginHorizontal();
            {
                view.DrawLabel("品質", 80, 20);
                _qualityComboBox.currentIndex = (int)setting.quality;
                _qualityComboBox.onSelected = (quality, _) => { setting.quality = quality; SetDirty(); };
                _qualityComboBox.DrawButton(view);
            }
            view.EndLayout();

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "ぼかし範囲",
                labelWidth = 100,
                width = -1,
                min = 0f,
                max = 15f,
                step = 0.01f,
                defaultValue = 1f,
                value = setting.blurArea,
                onChanged = value => { setting.blurArea = value; SetDirty(); },
            });

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "ぼかし最大",
                labelWidth = 100,
                width = -1,
                min = 0f,
                max = 25f,
                step = 0.01f,
                defaultValue = 5f,
                value = setting.maxBlurSize,
                onChanged = value => { setting.maxBlurSize = value; SetDirty(); },
            });

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "縮小率",
                labelWidth = 100,
                width = -1,
                fieldType = FloatFieldType.Int,
                min = 0,
                max = 1,
                step = 1,
                defaultValue = 0,
                value = setting.downsample,
                onChanged = value => { setting.downsample = (int)value; SetDirty(); },
            });
        }
    }
}
