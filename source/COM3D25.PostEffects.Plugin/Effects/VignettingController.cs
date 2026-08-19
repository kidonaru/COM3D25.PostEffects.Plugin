using COM3D2.MotionTimelineEditor;
using UnityEngine;
// Assembly-UnityScript-firstpass のグローバル名前空間にも旧 Vignetting が残骸として存在するため、
// ゲームが実際に使う PostEffects_Dummy 側へエイリアスで束縛する
#if COM3D25
using VignettingEffect = PostEffects_Dummy.Vignetting;
#else
// COM3D2 (2.0) の内蔵エフェクトはグローバル名前空間 (Assembly-UnityScript-firstpass) にある
using VignettingEffect = global::Vignetting;
#endif

namespace COM3D25.PostEffects.Plugin
{
    public class VignettingSetting
    {
        public bool enabled = false;
        // ゲーム標準のビネット (CameraMain がシーンに応じて有効化する) を強制無効化する
        public bool gameEffectDisabled = false;
        // 既定値はゲームがメインカメラの Vignetting に設定している値に合わせてある
        public VignettingEffect.AberrationMode mode = VignettingEffect.AberrationMode.Simple;
        public float intensity = -3.98f;
        public float blur = 0.82f;
        public float blurSpread = 4.19f;
        public float chromaticAberration = -0.75f;
        public float axialAberration = 1.18f;
        public float luminanceDependency = 0.494f;
        public float blurDistance = 1.71f;
    }

    public class VignettingController : EffectControllerBase<VignettingEffect, VignettingSetting>
    {
        public override string effectName => "ビネット";

        protected override VignettingSetting setting
        {
            get => settings.vignetting;
            set => settings.vignetting = value;
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

        protected override void ApplySetting(VignettingEffect component)
        {
            component.mode = setting.mode;
            component.intensity = setting.intensity;
            component.blur = setting.blur;
            component.blurSpread = setting.blurSpread;
            component.chromaticAberration = setting.chromaticAberration;
            component.axialAberration = setting.axialAberration;
            component.luminanceDependency = setting.luminanceDependency;
            component.blurDistance = setting.blurDistance;
        }

        protected override void Capture(VignettingEffect component)
        {
            var c = _capturedSetting;
            _capturedEnabled = component.enabled;
            c.mode = component.mode;
            c.intensity = component.intensity;
            c.blur = component.blur;
            c.blurSpread = component.blurSpread;
            c.chromaticAberration = component.chromaticAberration;
            c.axialAberration = component.axialAberration;
            c.luminanceDependency = component.luminanceDependency;
            c.blurDistance = component.blurDistance;
        }

        protected override void RestoreSetting(VignettingEffect component)
        {
            var c = _capturedSetting;
            component.enabled = _capturedEnabled;
            component.mode = c.mode;
            component.intensity = c.intensity;
            component.blur = c.blur;
            component.blurSpread = c.blurSpread;
            component.chromaticAberration = c.chromaticAberration;
            component.axialAberration = c.axialAberration;
            component.luminanceDependency = c.luminanceDependency;
            component.blurDistance = c.blurDistance;
        }

        private GUIComboBox<VignettingEffect.AberrationMode> _modeComboBox = new GUIComboBox<VignettingEffect.AberrationMode>
        {
            items = MTEUtils.GetEnumValues<VignettingEffect.AberrationMode>(),
            getName = (mode, _) => mode.ToString(),
            buttonSize = new Vector2(100, 20),
        };

        public override void DrawContent(GUIView view)
        {
            view.BeginHorizontal();
            {
                view.DrawLabel("収差モード", 80, 20);
                _modeComboBox.currentIndex = (int)setting.mode;
                _modeComboBox.onSelected = (mode, _) => { setting.mode = mode; SetDirty(); };
                _modeComboBox.DrawButton(view);
            }
            view.EndLayout();

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "強度",
                labelWidth = 80,
                width = -1,
                min = -5f,
                max = 5f,
                step = 0.01f,
                defaultValue = -3.98f,
                value = setting.intensity,
                onChanged = value => { setting.intensity = value; SetDirty(); },
            });

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "ブラー",
                labelWidth = 80,
                width = -1,
                min = 0f,
                max = 5f,
                step = 0.01f,
                defaultValue = 0.82f,
                value = setting.blur,
                onChanged = value => { setting.blur = value; SetDirty(); },
            });

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "ブラー広がり",
                labelWidth = 80,
                width = -1,
                min = 0f,
                max = 10f,
                step = 0.01f,
                defaultValue = 4.19f,
                value = setting.blurSpread,
                onChanged = value => { setting.blurSpread = value; SetDirty(); },
            });

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "色収差",
                labelWidth = 80,
                width = -1,
                min = -5f,
                max = 5f,
                step = 0.01f,
                defaultValue = -0.75f,
                value = setting.chromaticAberration,
                onChanged = value => { setting.chromaticAberration = value; SetDirty(); },
            });

            if (setting.mode == VignettingEffect.AberrationMode.Advanced)
            {
                view.DrawSliderValue(new GUIView.SliderOption
                {
                    label = "軸上色収差",
                    labelWidth = 80,
                    width = -1,
                    min = 0f,
                    max = 5f,
                    step = 0.01f,
                    defaultValue = 1.18f,
                    value = setting.axialAberration,
                    onChanged = value => { setting.axialAberration = value; SetDirty(); },
                });

                view.DrawSliderValue(new GUIView.SliderOption
                {
                    label = "輝度依存",
                    labelWidth = 80,
                    width = -1,
                    min = 0f,
                    max = 2f,
                    step = 0.01f,
                    defaultValue = 0.494f,
                    value = setting.luminanceDependency,
                    onChanged = value => { setting.luminanceDependency = value; SetDirty(); },
                });

                view.DrawSliderValue(new GUIView.SliderOption
                {
                    label = "ブラー距離",
                    labelWidth = 80,
                    width = -1,
                    min = 0f,
                    max = 10f,
                    step = 0.01f,
                    defaultValue = 1.71f,
                    value = setting.blurDistance,
                    onChanged = value => { setting.blurDistance = value; SetDirty(); },
                });
            }
        }
    }
}
