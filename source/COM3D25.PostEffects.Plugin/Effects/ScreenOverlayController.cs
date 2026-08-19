using COM3D2.MotionTimelineEditor;
using UnityEngine;
#if COM3D25
using ScreenOverlayEffect = PostEffects_Dummy.ScreenOverlay;
#else
// COM3D2 (2.0) の内蔵エフェクトはグローバル名前空間 (Assembly-UnityScript-firstpass) にある
using ScreenOverlayEffect = global::ScreenOverlay;
#endif

namespace COM3D25.PostEffects.Plugin
{
    public class ScreenOverlaySetting
    {
        public bool enabled = false;
        // 既定値はゲームがメインカメラの ScreenOverlay に設定している値に合わせてある
        public ScreenOverlayEffect.OverlayBlendMode blendMode = ScreenOverlayEffect.OverlayBlendMode.Multiply;
        public float intensity = 1f;
        // 絶対パス、または Config フォルダからの相対パス
        public string texturePath = "";
    }

    public class ScreenOverlayController : EffectControllerBase<ScreenOverlayEffect, ScreenOverlaySetting>
    {
        public override string effectName => "オーバーレイ";

        protected override ScreenOverlaySetting setting
        {
            get => settings.screenOverlay;
            set => settings.screenOverlay = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        private readonly TextureFileCache _textureCache = new TextureFileCache(TextureFileCache.SUB_DIR_OVERLAY);

        protected override void ApplySetting(ScreenOverlayEffect component)
        {
            component.blendMode = setting.blendMode;
            component.intensity = setting.intensity;
            component.texture = _textureCache.GetOrLoad(setting.texturePath);
        }

        private Texture2D _capturedTexture;

        protected override void Capture(ScreenOverlayEffect component)
        {
            var c = _capturedSetting;
            _capturedEnabled = component.enabled;
            c.blendMode = component.blendMode;
            c.intensity = component.intensity;
            _capturedTexture = component.texture;
        }

        protected override void RestoreSetting(ScreenOverlayEffect component)
        {
            var c = _capturedSetting;
            component.enabled = _capturedEnabled;
            component.blendMode = c.blendMode;
            component.intensity = c.intensity;
            component.texture = _capturedTexture;
        }

        private GUIComboBox<ScreenOverlayEffect.OverlayBlendMode> _blendModeComboBox = new GUIComboBox<ScreenOverlayEffect.OverlayBlendMode>
        {
            items = MTEUtils.GetEnumValues<ScreenOverlayEffect.OverlayBlendMode>(),
            getName = (mode, _) => mode.ToString(),
            buttonSize = new Vector2(100, 20),
        };

        public override void DrawContent(GUIView view)
        {
            view.BeginHorizontal();
            {
                view.DrawLabel("ブレンドモード", 100, 20);
                _blendModeComboBox.currentIndex = (int)setting.blendMode;
                _blendModeComboBox.onSelected = (mode, _) => { setting.blendMode = mode; SetDirty(); };
                _blendModeComboBox.DrawButton(view);
            }
            view.EndLayout();

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "強度",
                labelWidth = 80,
                width = -1,
                min = 0f,
                max = 3f,
                step = 0.01f,
                defaultValue = 1f,
                value = setting.intensity,
                onChanged = value => { setting.intensity = value; SetDirty(); },
            });

            _textureCache.DrawPathField(view, "テクスチャパス", setting.texturePath,
                value => { setting.texturePath = value; SetDirty(); });
        }
    }
}
