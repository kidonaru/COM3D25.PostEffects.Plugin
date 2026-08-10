using COM3D2.MotionTimelineEditor;

namespace COM3D25.PostEffects.Plugin
{
    public class CreaseSetting
    {
        public bool enabled = false;
        public float intensity = 0.5f;
        public int softness = 1;
        public float spread = 1f;
    }

    // Crease はゲーム側 Assembly-UnityScript-firstpass の実装をそのまま使う
    // (シェーダーフィールドは実行時追加では null のため imageeffects バンドルから補う)
    public class CreaseController : EffectControllerBase<Crease, CreaseSetting>
    {
        public override string effectName => "折り目";

        protected override CreaseSetting setting
        {
            get => settings.crease;
            set => settings.crease = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        protected override void ApplySetting(Crease component)
        {
            if (component.blurShader == null)
            {
                component.blurShader = EffectShaders.GetShader(EffectShaders.ImageEffects, "separableblurshader");
            }
            if (component.depthFetchShader == null)
            {
                component.depthFetchShader = EffectShaders.GetShader(EffectShaders.ImageEffects, "depthfetchshader");
            }
            if (component.creaseApplyShader == null)
            {
                component.creaseApplyShader = EffectShaders.GetShader(EffectShaders.ImageEffects, "creaseapplyshader");
            }

            component.intensity = setting.intensity;
            component.softness = setting.softness;
            component.spread = setting.spread;
        }

        protected override void Capture(Crease component)
        {
            var c = _capturedSetting;
            _capturedEnabled = component.enabled;
            c.intensity = component.intensity;
            c.softness = component.softness;
            c.spread = component.spread;
        }

        protected override void RestoreSetting(Crease component)
        {
            var c = _capturedSetting;
            component.enabled = _capturedEnabled;
            component.intensity = c.intensity;
            component.softness = c.softness;
            component.spread = c.spread;
        }

        public override void DrawContent(GUIView view)
        {
            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "強度",
                labelWidth = 100,
                width = -1,
                min = -10f,
                max = 10f,
                step = 0.01f,
                defaultValue = 0.5f,
                value = setting.intensity,
                onChanged = value => { setting.intensity = value; SetDirty(); },
            });

            // ぼかし回数がそのまま Blit 回数になるため上限を抑える
            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "ぼかし回数",
                labelWidth = 100,
                width = -1,
                fieldType = FloatFieldType.Int,
                min = 0,
                max = 10,
                step = 1,
                defaultValue = 1,
                value = setting.softness,
                onChanged = value => { setting.softness = (int)value; SetDirty(); },
            });

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "ぼかし拡散",
                labelWidth = 100,
                width = -1,
                min = 0f,
                max = 10f,
                step = 0.01f,
                defaultValue = 1f,
                value = setting.spread,
                onChanged = value => { setting.spread = value; SetDirty(); },
            });
        }
    }
}
