using COM3D2.MotionTimelineEditor;

namespace COM3D25.PostEffects.Plugin
{
    public class MaidHideSetting
    {
        public bool enabled = false;
    }

    public class MaidHideController : EffectControllerBase<MaidHideEffect, MaidHideSetting>
    {
        public override string effectName => "メイド非表示";

        protected override MaidHideSetting setting
        {
            get => settings.maidHide;
            set => settings.maidHide = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        // 設定項目を持たず、コンポーネントの有効・無効だけで動く
        protected override void ApplySetting(MaidHideEffect component)
        {
        }

        protected override void Capture(MaidHideEffect component)
        {
            _capturedEnabled = component.enabled;
        }

        protected override void RestoreSetting(MaidHideEffect component)
        {
            component.enabled = _capturedEnabled;
        }

        public override void DrawContent(GUIView view)
        {
            view.DrawLabel("メイド (Charactor / Face レイヤー) を描画対象から外します", -1, 20);
        }
    }
}
