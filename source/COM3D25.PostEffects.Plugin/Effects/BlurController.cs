using COM3D2.MotionTimelineEditor;
using UnityEngine;
// Assembly-UnityScript-firstpass のグローバル名前空間にも旧 Blur が残骸として存在するため、
// ゲームが実際に使う PostEffects_Dummy 側へエイリアスで束縛する
#if COM3D25
using BlurFx = PostEffects_Dummy.Blur;
#else
// COM3D2 (2.0) の内蔵エフェクトはグローバル名前空間 (Assembly-UnityScript-firstpass) にある
using BlurFx = global::Blur;
#endif

namespace COM3D25.PostEffects.Plugin
{
    public class BlurSetting
    {
        public bool enabled = false;
        public int downsample = 1;
        public float blurSize = 3f;
        public int blurIterations = 2;
        public bool sgxGauss = false;
    }

    public class BlurController : EffectControllerBase<BlurFx, BlurSetting>
    {
        public override string effectName => "ブラー";

        protected override BlurSetting setting
        {
            get => settings.blur;
            set => settings.blur = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        protected override void ApplySetting(BlurFx component)
        {
            component.downsample = setting.downsample;
            component.blurSize = setting.blurSize;
            component.blurIterations = setting.blurIterations;
            component.blurType = setting.sgxGauss ? BlurFx.BlurType.SgxGauss : BlurFx.BlurType.StandardGauss;
        }

        protected override void Capture(BlurFx component)
        {
            var c = _capturedSetting;
            _capturedEnabled = component.enabled;
            c.downsample = component.downsample;
            c.blurSize = component.blurSize;
            c.blurIterations = component.blurIterations;
            c.sgxGauss = component.blurType == BlurFx.BlurType.SgxGauss;
        }

        protected override void RestoreSetting(BlurFx component)
        {
            var c = _capturedSetting;
            component.enabled = _capturedEnabled;
            component.downsample = c.downsample;
            component.blurSize = c.blurSize;
            component.blurIterations = c.blurIterations;
            component.blurType = c.sgxGauss ? BlurFx.BlurType.SgxGauss : BlurFx.BlurType.StandardGauss;
        }

        public override void DrawContent(GUIView view)
        {
            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "ダウンサンプル",
                labelWidth = 100,
                width = -1,
                fieldType = FloatFieldType.Int,
                min = 0,
                max = 2,
                step = 1,
                defaultValue = 1,
                value = setting.downsample,
                onChanged = value => { setting.downsample = (int)value; SetDirty(); },
            });

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "ブラーサイズ",
                labelWidth = 100,
                width = -1,
                min = 0f,
                max = 10f,
                step = 0.01f,
                defaultValue = 3f,
                value = setting.blurSize,
                onChanged = value => { setting.blurSize = value; SetDirty(); },
            });

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "反復回数",
                labelWidth = 100,
                width = -1,
                fieldType = FloatFieldType.Int,
                min = 1,
                max = 4,
                step = 1,
                defaultValue = 2,
                value = setting.blurIterations,
                onChanged = value => { setting.blurIterations = (int)value; SetDirty(); },
            });

            view.DrawToggle("SGX ガウス (モバイル向け高速版)", setting.sgxGauss, 250, 20, value =>
            {
                setting.sgxGauss = value;
                SetDirty();
            });
        }
    }
}
