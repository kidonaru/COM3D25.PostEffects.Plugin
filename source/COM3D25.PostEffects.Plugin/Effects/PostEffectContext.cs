using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    // モデル群へ渡すコンテキスト。設定の実体は EffectSettings が持つ
    // (ResetSetting でインスタンスが差し替わるため、参照保持ではなくプロパティ委譲にする)
    public class PostEffectContext
    {
        public Camera camera;
        public ColorParaffinEffectSettings paraffinSettings => EffectSettings.instance.paraffin;
        public DistanceFogEffectSettings fogSettings => EffectSettings.instance.distanceFog;
        public RimlightEffectSettings rimlightSettings => EffectSettings.instance.rimlight;
    }
}
