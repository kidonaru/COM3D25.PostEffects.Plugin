using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// ビネット (ゲーム標準 Vignetting の自前実装)。
    /// ゲーム標準版はブラー用の半解像度バッファを入力と同じ形式 (HDR) で確保するため、
    /// MSAA 解決で生じた数万オーダーの輝点がブラーで塗り広げられて白い矩形になる。
    /// ここでは半解像度バッファを ARGB32 にしてダウンサンプル時に 0..1 へクランプし、それを防ぐ。
    /// シェーダーはゲームが持つ標準シェーダー (Hidden/Vignetting 等) をそのまま使う
    /// </summary>
    public class VignettingEffect : MonoBehaviour
    {
        public enum AberrationMode
        {
            Simple,
            Advanced,
        }

        public AberrationMode mode = AberrationMode.Simple;
        public float intensity = 0.036f;
        public float chromaticAberration = 0.2f;
        public float axialAberration = 0.5f;
        public float blur;
        public float blurSpread = 0.75f;
        public float luminanceDependency = 0.25f;
        public float blurDistance = 2.5f;

        public Shader vignetteShader;
        public Shader separableBlurShader;
        public Shader chromAberrationShader;

        private Material _vignetteMaterial;
        private Material _separableBlurMaterial;
        private Material _chromAberrationMaterial;

        // 半解像度 1px あたりの分離ブラーのオフセット (ゲーム標準版と同じ定数)
        private const float BlurTexelStep = 1f / 512f;

        private void OnDisable()
        {
            DestroyMaterial(ref _vignetteMaterial);
            DestroyMaterial(ref _separableBlurMaterial);
            DestroyMaterial(ref _chromAberrationMaterial);
        }

        private static void DestroyMaterial(ref Material material)
        {
            if (material != null)
            {
                DestroyImmediate(material);
                material = null;
            }
        }

        private static Material GetMaterial(Shader shader, ref Material material)
        {
            if (shader == null || !shader.isSupported)
            {
                return null;
            }
            if (material == null || material.shader != shader)
            {
                DestroyMaterial(ref material);
                material = new Material(shader);
                material.hideFlags = HideFlags.DontSave;
            }
            return material;
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            var vignetteMaterial = GetMaterial(vignetteShader, ref _vignetteMaterial);
            var separableBlurMaterial = GetMaterial(separableBlurShader, ref _separableBlurMaterial);
            var chromAberrationMaterial = GetMaterial(chromAberrationShader, ref _chromAberrationMaterial);
            if (vignetteMaterial == null || separableBlurMaterial == null || chromAberrationMaterial == null)
            {
                Graphics.Blit(source, destination);
                return;
            }

            var width = source.width;
            var height = source.height;
            var useVignette = Mathf.Abs(blur) > 0f || Mathf.Abs(intensity) > 0f;
            var aspect = (float)width / height;

            RenderTexture vignetted = null;
            RenderTexture blurred = null;
            if (useVignette)
            {
                vignetted = RenderTexture.GetTemporary(width, height, 0, source.format);
                if (Mathf.Abs(blur) > 0f)
                {
                    // ARGB32 で確保することで、ダウンサンプル時に HDR の異常値が 0..1 に丸まる
                    blurred = RenderTexture.GetTemporary(width / 2, height / 2, 0, RenderTextureFormat.ARGB32);
                    Graphics.Blit(source, blurred, chromAberrationMaterial, 0);
                    for (var i = 0; i < 2; i++)
                    {
                        var temp = RenderTexture.GetTemporary(width / 2, height / 2, 0, RenderTextureFormat.ARGB32);
                        separableBlurMaterial.SetVector("offsets", new Vector4(0f, blurSpread * BlurTexelStep, 0f, 0f));
                        Graphics.Blit(blurred, temp, separableBlurMaterial);
                        separableBlurMaterial.SetVector("offsets", new Vector4(blurSpread * BlurTexelStep / aspect, 0f, 0f, 0f));
                        Graphics.Blit(temp, blurred, separableBlurMaterial);
                        RenderTexture.ReleaseTemporary(temp);
                    }
                }

                vignetteMaterial.SetFloat("_Intensity", intensity);
                vignetteMaterial.SetFloat("_Blur", blur);
                vignetteMaterial.SetTexture("_VignetteTex", blurred);
                Graphics.Blit(source, vignetted, vignetteMaterial, 0);
            }

            chromAberrationMaterial.SetFloat("_ChromaticAberration", chromaticAberration);
            chromAberrationMaterial.SetFloat("_AxialAberration", axialAberration);
            chromAberrationMaterial.SetVector("_BlurDistance", new Vector2(-blurDistance, blurDistance));
            chromAberrationMaterial.SetFloat("_Luminance", 1f / Mathf.Max(Mathf.Epsilon, luminanceDependency));

            var input = useVignette ? vignetted : source;
            input.wrapMode = TextureWrapMode.Clamp;
            Graphics.Blit(input, destination, chromAberrationMaterial, mode == AberrationMode.Advanced ? 2 : 1);

            if (vignetted != null) RenderTexture.ReleaseTemporary(vignetted);
            if (blurred != null) RenderTexture.ReleaseTemporary(blurred);
        }
    }
}
