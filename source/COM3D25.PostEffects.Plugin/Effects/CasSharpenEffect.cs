using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// シャープネス (AMD FidelityFX CAS の簡易移植)。アンチエイリアス後の眠い絵を締める。
    /// 本プラグイン自前の実装 (シェーダーは posteffects バンドル)
    /// </summary>
    public class CasSharpenEffect : MonoBehaviour
    {
        public Shader shader;

        [Range(0f, 1f)]
        public float sharpness = 0.5f;

        private Material _material;

        private void OnDisable()
        {
            if (_material != null)
            {
                DestroyImmediate(_material);
                _material = null;
            }
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
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

            _material.SetFloat("_Sharpness", sharpness);
            Graphics.Blit(source, destination, _material, 0);
        }
    }
}
