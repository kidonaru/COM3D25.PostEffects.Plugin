using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// アナログ映像風グリッチ (Kino/Glitch)。2.5 のゲームアセンブリに型が存在しないため、
    /// SceneCapture 同梱実装を自己完結な形で移植したもの (シェーダーは kino バンドル)
    /// </summary>
    public class AnalogGlitchEffect : MonoBehaviour
    {
        public Shader shader;

        [Range(0f, 1f)]
        public float scanLineJitter = 0f;
        [Range(0f, 1f)]
        public float verticalJump = 0f;
        [Range(0f, 1f)]
        public float horizontalShake = 0f;
        [Range(0f, 1f)]
        public float colorDrift = 0f;

        private Material _material;
        // 縦揺れの位相。verticalJump の強さに比例して進める
        private float _verticalJumpTime;

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

            _verticalJumpTime += Time.deltaTime * verticalJump * 11.3f;

            var jitterThreshold = Mathf.Clamp01(1f - scanLineJitter * 1.2f);
            var jitterDisplacement = 0.002f + Mathf.Pow(scanLineJitter, 3f) * 0.05f;
            _material.SetVector("_ScanLineJitter", new Vector2(jitterDisplacement, jitterThreshold));
            _material.SetVector("_VerticalJump", new Vector2(verticalJump, _verticalJumpTime));
            _material.SetFloat("_HorizontalShake", horizontalShake * 0.2f);
            _material.SetVector("_ColorDrift", new Vector2(colorDrift * 0.04f, Time.time * 606.11f));

            Graphics.Blit(source, destination, _material);
        }
    }
}
