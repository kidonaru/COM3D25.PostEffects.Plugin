using System.Xml.Serialization;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// ハーフトーン (網点)。モノクロ網点 / CMYK カラー網点で印刷物風にする。
    /// 本プラグイン自前の実装 (シェーダーは posteffects バンドル)
    /// </summary>
    public class HalftoneEffect : CharacterMaskableEffect
    {
        // XmlSerializer の XML 型名衝突対策 (RampEffect.BlendMode の注記と同じ)
        [XmlType("HalftoneMode")]
        public enum Mode
        {
            Mono,
            Cmyk,
        }

        public Shader shader;

        [Range(2f, 32f)]
        public float dotSize = 6f;
        [Range(0f, 180f)]
        public float angle = 45f;
        [Range(0f, 1f)]
        public float smoothness = 0.1f;
        public Mode mode = Mode.Mono;

        private Material _material;

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

            // Config 手編集等で 0 が入るとシェーダー側で 0 除算になるため下限を保証する
            _material.SetFloat("_DotSize", Mathf.Max(1f, dotSize));
            _material.SetFloat("_Angle", angle * Mathf.Deg2Rad);
            _material.SetFloat("_Smoothness", smoothness);
            Graphics.Blit(source, destination, _material, mode == Mode.Cmyk ? 1 : 0);
        }
    }
}
