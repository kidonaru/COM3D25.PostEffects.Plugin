using System;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// レンズ由来の歪み・色収差・ビネットをまとめて掛ける。
    /// 2.5 のゲームアセンブリに型が存在しないため、SceneCapture 同梱実装を移植したもの
    /// (シェーダーは cinematic バンドルの cinematiclensaberrationsshader)
    /// </summary>
    public class CinematicLensAberrationsEffect : MonoBehaviour
    {
        public Shader shader;

        public bool distortionEnabled = false;
        public float distortionAmount = 0f;
        public float distortionCenterX = 0f;
        public float distortionCenterY = 0f;
        public float distortionAmountX = 1f;
        public float distortionAmountY = 1f;
        public float distortionScale = 1f;

        public bool vignetteEnabled = false;
        public Color vignetteColor = new Color(0f, 0f, 0f, 1f);
        public Vector2 vignetteCenter = new Vector2(0.5f, 0.5f);
        public float vignetteIntensity = 1.4f;
        public float vignetteSmoothness = 0.8f;
        public float vignetteRoundness = 1f;
        public float vignetteBlur = 0f;
        public float vignetteDesaturate = 0f;

        public bool chromaticAberrationEnabled = false;
        public Color chromaticAberrationColor = Color.green;
        public float chromaticAberrationAmount = 0f;

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
            if (shader == null || !shader.isSupported ||
                (!vignetteEnabled && !chromaticAberrationEnabled && !distortionEnabled))
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

            if (distortionEnabled)
            {
                SetupDistortion();
            }

            if (chromaticAberrationEnabled)
            {
                _material.EnableKeyword("CHROMATIC_ABERRATION");
                _material.SetVector("_ChromaticAberration", new Vector4(
                    chromaticAberrationColor.r,
                    chromaticAberrationColor.g,
                    chromaticAberrationColor.b,
                    chromaticAberrationAmount * 0.001f));
            }

            RenderTexture blurred = null;
            if (vignetteEnabled)
            {
                blurred = SetupVignette(source);
            }

            Graphics.Blit(source, destination, _material, GetPass());

            if (blurred != null)
            {
                RenderTexture.ReleaseTemporary(blurred);
            }
        }

        private void SetupDistortion()
        {
            var angle = 0.017453292f * Math.Min(160f, 1.6f * Math.Max(Mathf.Abs(distortionAmount), 1f));
            var centerScale = new Vector4(
                distortionCenterX,
                distortionCenterY,
                Mathf.Max(distortionAmountX, 0.0001f),
                Mathf.Max(distortionAmountY, 0.0001f));
            var amount = new Vector3(
                distortionAmount >= 0f ? angle : 1f / angle,
                2f * Mathf.Tan(angle * 0.5f),
                1f / distortionScale);

            _material.EnableKeyword(distortionAmount >= 0f ? "DISTORT" : "UNDISTORT");
            _material.SetVector("_DistCenterScale", centerScale);
            _material.SetVector("_DistAmount", amount);
        }

        // ぼかしたビネット用テクスチャを作る。呼び出し側が解放する
        private RenderTexture SetupVignette(RenderTexture source)
        {
            _material.SetColor("_VignetteColor", vignetteColor);

            RenderTexture blurred = null;
            if (vignetteBlur > 0f)
            {
                var width = source.width / 2;
                var height = source.height / 2;
                var tempA = RenderTexture.GetTemporary(width, height, 0, source.format);
                blurred = RenderTexture.GetTemporary(width, height, 0, source.format);
                tempA.filterMode = FilterMode.Bilinear;
                blurred.filterMode = FilterMode.Bilinear;

                // 前段のぼかしパスに歪みを掛けると二重に歪むため、1 回目の後だけ無効化する
                _material.SetVector("_BlurPass", new Vector2(1f / width, 0f));
                Graphics.Blit(source, tempA, _material, 0);
                if (distortionEnabled)
                {
                    _material.DisableKeyword("DISTORT");
                    _material.DisableKeyword("UNDISTORT");
                }
                _material.SetVector("_BlurPass", new Vector2(0f, 1f / height));
                Graphics.Blit(tempA, blurred, _material, 0);
                _material.SetVector("_BlurPass", new Vector2(1f / width, 0f));
                Graphics.Blit(blurred, tempA, _material, 0);
                _material.SetVector("_BlurPass", new Vector2(0f, 1f / height));
                Graphics.Blit(tempA, blurred, _material, 0);
                RenderTexture.ReleaseTemporary(tempA);

                _material.SetTexture("_BlurTex", blurred);
                _material.SetFloat("_VignetteBlur", vignetteBlur * 3f);
                _material.EnableKeyword("VIGNETTE_BLUR");
                if (distortionEnabled)
                {
                    _material.EnableKeyword(distortionAmount >= 0f ? "DISTORT" : "UNDISTORT");
                }
            }

            if (vignetteDesaturate > 0f)
            {
                _material.EnableKeyword("VIGNETTE_DESAT");
                _material.SetFloat("_VignetteDesat", 1f - vignetteDesaturate);
            }

            _material.SetVector("_VignetteCenter", vignetteCenter);
            if (Mathf.Approximately(vignetteRoundness, 1f))
            {
                _material.EnableKeyword("VIGNETTE_CLASSIC");
                _material.SetVector("_VignetteSettings", new Vector2(vignetteIntensity, vignetteSmoothness));
            }
            else
            {
                _material.EnableKeyword("VIGNETTE_FILMIC");
                var roundness = (1f - vignetteRoundness) * 6f + vignetteRoundness;
                _material.SetVector("_VignetteSettings",
                    new Vector3(vignetteIntensity, vignetteSmoothness, roundness));
            }

            return blurred;
        }

        // シェーダーは有効な組み合わせごとに専用パスを持つ。番号はシェーダー側の Pass 宣言順に
        // 固定で対応しており、フラグのビット演算では導けない (色収差 1 + 歪み 2 の同時は 3 ではなく 4)
        private int GetPass()
        {
            if (vignetteEnabled && chromaticAberrationEnabled && distortionEnabled) return 7;
            if (vignetteEnabled && chromaticAberrationEnabled) return 5;
            if (vignetteEnabled && distortionEnabled) return 6;
            if (chromaticAberrationEnabled && distortionEnabled) return 4;
            if (vignetteEnabled) return 3;
            if (chromaticAberrationEnabled) return 1;
            if (distortionEnabled) return 2;
            return 0;
        }
    }
}
