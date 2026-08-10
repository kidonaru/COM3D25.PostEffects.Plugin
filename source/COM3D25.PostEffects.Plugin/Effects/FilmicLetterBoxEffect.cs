using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// 画面の上下 (または左右) を帯で覆うシネスコ風レターボックス。
    /// 2.5 のゲームアセンブリに型が存在しないため、SceneCapture 同梱実装を移植したもの
    /// (シェーダーは filmic バンドルの filmiceletterboxshader)
    /// </summary>
    public class FilmicLetterBoxEffect : MonoBehaviour
    {
        public Shader shader;

        public Color color = new Color(0f, 0f, 0f, 1f);
        public Vector2 center = new Vector2(0.5f, 0.5f);
        public float position = 0.25f;
        public float smoothness = 0.001f;
        // true で左右に帯を出す
        public bool vertical = false;

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

            _material.shaderKeywords = null;
            _material.SetColor("_VignetteColor", color);
            _material.SetVector("_VignetteCenter", center);
            _material.EnableKeyword(vertical ? "VIGNETTE_CLASSIC" : "VIGNETTE_FILMIC");
            _material.SetVector("_VignetteSettings", new Vector2(position, smoothness));

            Graphics.Blit(source, destination, _material, 0);
        }
    }
}
