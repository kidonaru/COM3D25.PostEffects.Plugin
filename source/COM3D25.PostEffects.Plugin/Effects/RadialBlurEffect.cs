using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// ラジアルブラー (ズームブラー)。中心から放射状に流して集中線的な演出をする。
    /// 本プラグイン自前の実装 (シェーダーは posteffects バンドル)
    /// </summary>
    public class RadialBlurEffect : CharacterMaskableEffect
    {
        public Shader shader;

        [Range(0f, 1f)]
        public float strength = 0.3f;
        [Range(0f, 1f)]
        public float centerX = 0.5f;
        [Range(0f, 1f)]
        public float centerY = 0.5f;
        [Range(4, 32)]
        public int sampleCount = 16;

        private Material _material;

        // キャラ輪郭に背景ボケが回り込むためマスク境界を膨張させる
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
            if (shader == null || !shader.isSupported)
            {
                Graphics.Blit(source, destination);
                return;
            }

            if (_material == null)
            {
                _material = new Material(shader);
                _material.hideFlags = HideFlags.DontSave;
            }

            _material.SetVector("_Center", new Vector4(centerX, centerY, 0f, 0f));
            _material.SetFloat("_Strength", strength);
            // Config 手編集等の異常値対策。下限は 0 除算防止、上限は GPU ループ暴走防止
            _material.SetInt("_SampleCount", Mathf.Clamp(sampleCount, 2, 32));

            // キャラ除外時はキャラ画素をサンプルから捨てるパスを使い、キャラの残像が背景へ流れないようにする
            var maskRT = excludeCharacters ? CharacterMask.texture : null;
            if (maskRT != null)
            {
                _material.SetTexture("_MaskTex", maskRT);
                Graphics.Blit(source, destination, _material, 1);
                _material.SetTexture("_MaskTex", null);
                return;
            }
            Graphics.Blit(source, destination, _material, 0);
        }
    }
}
