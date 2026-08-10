using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// Kuwahara フィルタ (油絵風)。4 象限の分散比較でエッジを保ちながら面を潰す。
    /// 本プラグイン自前の実装 (シェーダーは posteffects バンドル)
    /// </summary>
    public class KuwaharaEffect : CharacterMaskableEffect
    {
        public Shader shader;

        [Range(1, 8)]
        public int radius = 3;

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

            _material.SetInt("_Radius", Mathf.Clamp(radius, 1, 8));
            Graphics.Blit(source, destination, _material, 0);
        }
    }
}
