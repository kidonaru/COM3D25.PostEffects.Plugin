using COM3D2.MotionTimelineEditor;

namespace COM3D25.PostEffects.Plugin
{
    public class FisheyeSetting
    {
        public bool enabled = false;
        public float strengthX = 0.05f;
        public float strengthY = 0.05f;
    }

    // Fisheye はゲーム側 Assembly-UnityScript-firstpass の実装をそのまま使う
    // (シェーダーフィールドは実行時追加では null のため imageeffects バンドルから補う)
    public class FisheyeController : EffectControllerBase<Fisheye, FisheyeSetting>
    {
        public override string effectName => "魚眼レンズ";

        protected override FisheyeSetting setting
        {
            get => settings.fisheye;
            set => settings.fisheye = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        protected override void ApplySetting(Fisheye component)
        {
            if (component.fishEyeShader == null)
            {
                component.fishEyeShader = EffectShaders.GetShader(EffectShaders.ImageEffects, "fisheyeshader");
            }
            component.strengthX = setting.strengthX;
            component.strengthY = setting.strengthY;
        }

        protected override void Capture(Fisheye component)
        {
            var c = _capturedSetting;
            _capturedEnabled = component.enabled;
            c.strengthX = component.strengthX;
            c.strengthY = component.strengthY;
        }

        protected override void RestoreSetting(Fisheye component)
        {
            var c = _capturedSetting;
            component.enabled = _capturedEnabled;
            component.strengthX = c.strengthX;
            component.strengthY = c.strengthY;
        }

        public override void DrawContent(GUIView view)
        {
            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "歪み X",
                labelWidth = 100,
                width = -1,
                min = 0f,
                max = 10f,
                step = 0.01f,
                defaultValue = 0.05f,
                value = setting.strengthX,
                onChanged = value => { setting.strengthX = value; SetDirty(); },
            });

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "歪み Y",
                labelWidth = 100,
                width = -1,
                min = 0f,
                max = 10f,
                step = 0.01f,
                defaultValue = 0.05f,
                value = setting.strengthY,
                onChanged = value => { setting.strengthY = value; SetDirty(); },
            });
        }
    }
}
