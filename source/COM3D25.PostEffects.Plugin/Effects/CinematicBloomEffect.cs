using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// ピラミッド状のダウン/アップサンプルで明部を滲ませるブルーム (Cinematic/Bloom)。
    /// 2.5 のゲームアセンブリに型が存在しないため、SceneCapture 同梱実装を自己完結な形で
    /// 移植したもの (シェーダーは cinematic バンドル)。
    /// 移植元にあったメイドマスク (EffectMask 連携) は EffectMask 自体が未移植のため省いている
    /// </summary>
    public class CinematicBloomEffect : MonoBehaviour
    {
        // 縮小段の上限。移植元と同じ (シェーダー側の想定でもある)
        private const int MaxIterations = 16;

        public Shader shader;

        public float threshold = 1.1f;
        public float softKnee = 0.5f;
        public float radius = 1f;
        public float intensity = 2f;
        public float maxIntensity = 2f;
        public bool highQuality = true;
        public bool antiFlicker = false;
        public Texture dirtTexture = null;
        public bool useDirtTexture = true;
        public float dirtIntensity = 0f;

        private Material _material;
        private readonly RenderTexture[] _blurBuffer1 = new RenderTexture[MaxIterations];
        private readonly RenderTexture[] _blurBuffer2 = new RenderTexture[MaxIterations];

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

            // 低品質時は半解像度で処理する
            var width = highQuality ? source.width : source.width / 2;
            var height = highQuality ? source.height : source.height / 2;
            var format = RenderTextureFormat.DefaultHDR;

            // 縮小段数は解像度と半径から決める (小数部はサンプル間隔の補間に使う)
            var logHeight = Mathf.Log(height, 2f) + radius - 8f;
            var logHeightInt = (int)logHeight;
            var iterations = Mathf.Clamp(logHeightInt, 1, MaxIterations);

            // ガンマ空間で指定されたしきい値をリニアへ直してからカーブを作る
            var thresholdLinear = Mathf.GammaToLinearSpace(Mathf.Max(0f, threshold));
            var knee = thresholdLinear * softKnee + 1E-05f;

            _material.SetFloat("_Threshold", thresholdLinear);
            _material.SetVector("_Curve", new Vector3(thresholdLinear - knee, knee * 2f, 0.25f / knee));
            // 半解像度でのちらつき対策はサンプル位置を半ピクセルずらして行う
            _material.SetFloat("_PrefilterOffs", !highQuality && antiFlicker ? -0.5f : 0f);
            _material.SetFloat("_SampleScale", 0.5f + logHeight - logHeightInt);
            _material.SetFloat("_Intensity", Mathf.Max(0f, intensity));
            _material.SetFloat("_MaxIntensity", Mathf.Max(0f, maxIntensity));

            var useDirt = dirtTexture != null && useDirtTexture;
            if (useDirt)
            {
                _material.SetTexture("_DirtTex", dirtTexture);
                _material.SetFloat("_DirtIntensity", dirtIntensity);
            }
            _material.shaderKeywords = null;

            var prefiltered = RenderTexture.GetTemporary(width, height, 0, format);
            Graphics.Blit(source, prefiltered, _material, antiFlicker ? 1 : 0);

            var last = prefiltered;
            for (var i = 0; i < iterations; i++)
            {
                _blurBuffer1[i] = RenderTexture.GetTemporary(last.width / 2, last.height / 2, 0, format);
                Graphics.Blit(last, _blurBuffer1[i], _material, i == 0 ? (antiFlicker ? 3 : 2) : 4);
                last = _blurBuffer1[i];
            }

            for (var i = iterations - 2; i >= 0; i--)
            {
                var high = _blurBuffer1[i];
                _material.SetTexture("_BaseTex", high);
                _blurBuffer2[i] = RenderTexture.GetTemporary(high.width, high.height, 0, format);
                Graphics.Blit(last, _blurBuffer2[i], _material, highQuality ? 6 : 5);
                last = _blurBuffer2[i];
            }

            // 合成パスは「ダートの有無」と「品質」の組み合わせで 4 通りある
            var compositePass = (useDirt ? 9 : 7) + (highQuality ? 1 : 0);
            _material.SetTexture("_BaseTex", source);
            Graphics.Blit(last, destination, _material, compositePass);

            for (var i = 0; i < MaxIterations; i++)
            {
                if (_blurBuffer1[i] != null)
                {
                    RenderTexture.ReleaseTemporary(_blurBuffer1[i]);
                    _blurBuffer1[i] = null;
                }
                if (_blurBuffer2[i] != null)
                {
                    RenderTexture.ReleaseTemporary(_blurBuffer2[i]);
                    _blurBuffer2[i] = null;
                }
            }
            RenderTexture.ReleaseTemporary(prefiltered);
        }
    }
}
