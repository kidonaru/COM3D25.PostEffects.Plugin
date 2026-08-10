using COM3D2.MotionTimelineEditor;
using UnityEngine;
// Assembly-UnityScript-firstpass のグローバル名前空間にも旧 GlobalFog が残骸として存在するため、
// ゲームが実際に使う PostEffects_Dummy 側へエイリアスで束縛する
using GlobalFogEffect = PostEffects_Dummy.GlobalFog;

namespace COM3D25.PostEffects.Plugin
{
    public class GlobalFogSetting
    {
        public bool enabled = false;
        // 既定値は GlobalFog エフェクト自身の初期値に合わせてある
        public GlobalFogEffect.FogMode fogMode = GlobalFogEffect.FogMode.AbsoluteYAndDistance;
        public float startDistance = 200f;
        public float globalDensity = 1f;
        public float heightScale = 100f;
        public float height = 0f;
        public Color fogColor = Color.grey;
    }

    public class GlobalFogController : EffectControllerBase<GlobalFogEffect, GlobalFogSetting>
    {
        public override string effectName => "フォグ";

        protected override GlobalFogSetting setting
        {
            get => settings.globalFog;
            set => settings.globalFog = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        protected override void ApplySetting(GlobalFogEffect component)
        {
            component.fogMode = setting.fogMode;
            component.startDistance = setting.startDistance;
            component.globalDensity = setting.globalDensity;
            component.heightScale = setting.heightScale;
            component.height = setting.height;
            component.globalFogColor = setting.fogColor;
        }

        protected override void Capture(GlobalFogEffect component)
        {
            var c = _capturedSetting;
            _capturedEnabled = component.enabled;
            c.fogMode = component.fogMode;
            c.startDistance = component.startDistance;
            c.globalDensity = component.globalDensity;
            c.heightScale = component.heightScale;
            c.height = component.height;
            c.fogColor = component.globalFogColor;
        }

        protected override void RestoreSetting(GlobalFogEffect component)
        {
            var c = _capturedSetting;
            component.enabled = _capturedEnabled;
            component.fogMode = c.fogMode;
            component.startDistance = c.startDistance;
            component.globalDensity = c.globalDensity;
            component.heightScale = c.heightScale;
            component.height = c.height;
            component.globalFogColor = c.fogColor;
        }

        private GUIComboBox<GlobalFogEffect.FogMode> _modeComboBox = new GUIComboBox<GlobalFogEffect.FogMode>
        {
            items = MTEUtils.GetEnumValues<GlobalFogEffect.FogMode>(),
            getName = (mode, _) => mode.ToString(),
            buttonSize = new Vector2(180, 20),
        };

        public override void DrawContent(GUIView view)
        {
            view.BeginHorizontal();
            {
                view.DrawLabel("モード", 60, 20);
                _modeComboBox.currentIndex = (int)setting.fogMode;
                _modeComboBox.onSelected = (mode, _) => { setting.fogMode = mode; SetDirty(); };
                _modeComboBox.DrawButton(view);
            }
            view.EndLayout();

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "開始距離",
                labelWidth = 80,
                width = -1,
                min = 0f,
                max = 200f,
                step = 0.1f,
                defaultValue = 200f,
                value = setting.startDistance,
                onChanged = value => { setting.startDistance = value; SetDirty(); },
            });

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "濃度",
                labelWidth = 80,
                width = -1,
                min = 0f,
                max = 10f,
                step = 0.01f,
                defaultValue = 1f,
                value = setting.globalDensity,
                onChanged = value => { setting.globalDensity = value; SetDirty(); },
            });

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "高さスケール",
                labelWidth = 80,
                width = -1,
                min = 0.1f,
                max = 500f,
                step = 0.1f,
                defaultValue = 100f,
                value = setting.heightScale,
                onChanged = value => { setting.heightScale = value; SetDirty(); },
            });

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "高さ",
                labelWidth = 80,
                width = -1,
                min = -100f,
                max = 100f,
                step = 0.1f,
                defaultValue = 0f,
                value = setting.height,
                onChanged = value => { setting.height = value; SetDirty(); },
            });

            view.DrawColor(
                view.GetColorFieldCache("フォグ色", false),
                setting.fogColor,
                Color.grey,
                color => { setting.fogColor = color; SetDirty(); });
        }
    }
}
