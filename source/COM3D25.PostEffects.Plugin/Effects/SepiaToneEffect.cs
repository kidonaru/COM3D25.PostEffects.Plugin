using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// セピア調エフェクト。2.5 のゲームアセンブリには SepiaTone 型が存在しないため、
    /// SceneCapture 同梱実装を自己完結な形で移植したもの (シェーダーは imageeffects バンドル)
    /// </summary>
    public class SepiaToneEffect : CharacterMaskableEffect
    {
        public Shader shader;
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

            Graphics.Blit(source, destination, _material);
        }
    }
}
