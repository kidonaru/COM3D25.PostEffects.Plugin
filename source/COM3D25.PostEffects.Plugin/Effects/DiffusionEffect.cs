using System.Xml.Serialization;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// ディフュージョン (ソフトフォーカス)。明部だけを淡くにじませるポートレート定番エフェクト。
    /// 本プラグイン自前の実装 (シェーダーは posteffects バンドル)
    /// </summary>
    public class DiffusionEffect : CharacterMaskableEffect
    {
        // XmlSerializer は入れ子型も外側の型名を含まない XML 型名で扱うため、
        // 他エフェクトの BlendMode と衝突しないよう明示的に名前を付ける (衝突すると Config 保存が丸ごと失敗する)
        [XmlType("DiffusionBlendMode")]
        public enum BlendMode
        {
            Screen,
            Lighten,
        }

        public Shader shader;

        [Range(0f, 1f)]
        public float intensity = 0.5f;
        [Range(0f, 1f)]
        public float threshold = 0.5f;
        [Range(1f, 10f)]
        public float blurSize = 3f;
        public BlendMode blendMode = BlendMode.Screen;

        private Material _material;

        // キャラ輪郭に背景のにじみが回り込むためマスク境界を膨張させる
        protected override float maskSpread => 4f;

        private void OnDisable()
        {
            if (_material != null)
            {
                DestroyImmediate(_material);
                _material = null;
            }
        }

        protected override void RenderEffect(RenderTexture source, RenderTexture destination)
        {
            // 縮小バッファが 1px 未満になる極小解像度では素通しする (StreakEffect と同じ方針)
            if (shader == null || !shader.isSupported || source.width < 4 || source.height < 4)
            {
                Graphics.Blit(source, destination);
                return;
            }

            if (_material == null)
            {
                _material = new Material(shader);
                _material.hideFlags = HideFlags.DontSave;
            }

            _material.SetFloat("_Threshold", threshold);
            _material.SetFloat("_BlurSize", blurSize);
            _material.SetFloat("_Intensity", intensity);
            _material.SetFloat("_UseLighten", blendMode == BlendMode.Lighten ? 1f : 0f);

            // 1/2 解像度で明部抽出 → ガウス 横/縦 (rtA/rtB を往復し、最終結果は rtA) → 元解像度で合成
            var w = source.width / 2;
            var h = source.height / 2;
            var rtA = RenderTexture.GetTemporary(w, h, 0, source.format);
            var rtB = RenderTexture.GetTemporary(w, h, 0, source.format);

            Graphics.Blit(source, rtA, _material, 0);
            Graphics.Blit(rtA, rtB, _material, 1);
            Graphics.Blit(rtB, rtA, _material, 2);

            _material.SetTexture("_BlurTex", rtA);
            Graphics.Blit(source, destination, _material, 3);

            RenderTexture.ReleaseTemporary(rtA);
            RenderTexture.ReleaseTemporary(rtB);
        }
    }
}
