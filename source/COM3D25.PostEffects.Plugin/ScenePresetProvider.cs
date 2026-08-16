using System;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// EditorWindow のシーンプリセットへ相乗りするプロバイダの目印。
    /// EditorWindow 側は型の完全一致ではなく短名 ScenePresetProviderAttribute で
    /// 発見するため、アセンブリ参照を増やさずに自前定義で成立する
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ScenePresetProviderAttribute : Attribute
    {
    }

    /// <summary>
    /// シーンプリセットにポストエフェクト設定を載せるためのプロバイダ。
    /// 契約メンバはすべて public static で、EditorWindow が
    /// アセンブリ走査で発見する (このクラス側での登録処理は不要)
    /// </summary>
    [ScenePresetProvider]
    public static class PostEffectsScenePresetProvider
    {
        // サイドカーのファイル名 (<プリセット名>.<id>.xml) に使われる。
        // 他プラグインと衝突すると先勝ちで一方が無効化されるため、一意な名前にする
        public static string PresetProviderId => "PostEffects";

        public static string PresetProviderDisplayName => "ポストエフェクト (PostEffects)";

        /// <summary>読込トグルなど狭い場所で使う短縮名</summary>
        public static string PresetProviderShortDisplayName => "エフェクト";

        public static string CapturePresetXml() => PresetManager.instance.CapturePresetXml();

        public static bool ApplyPresetXml(string xml) => PresetManager.instance.ApplyPresetXml(xml);

        /// <summary>SceneCapture プリセット XML を適用する。成功可否を返す</summary>
        public static bool ApplySceneCaptureXml(string xml) =>
            PresetManager.instance.ApplySceneCaptureXml(xml);
    }
}
