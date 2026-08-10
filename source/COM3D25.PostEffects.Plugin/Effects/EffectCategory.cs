namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// エフェクト一覧の絞り込み用カテゴリ。
    /// </summary>
    public enum EffectCategory
    {
        ColorTone,
        Bloom,
        Dof,
        EdgeLine,
        Noise,
        Lens,
        Fog,
        Other,
    }

    public static class EffectCategoryExtensions
    {
        public static string GetName(this EffectCategory category)
        {
            switch (category)
            {
                case EffectCategory.ColorTone: return "色調";
                case EffectCategory.Bloom: return "ブルーム・光";
                case EffectCategory.Dof: return "ボケ・被写界深度";
                case EffectCategory.EdgeLine: return "輪郭・線画";
                case EffectCategory.Noise: return "ノイズ・グリッチ";
                case EffectCategory.Lens: return "レンズ・歪み";
                case EffectCategory.Fog: return "フォグ・遮蔽";
                case EffectCategory.Other: return "その他";
                default: return category.ToString();
            }
        }
    }
}
