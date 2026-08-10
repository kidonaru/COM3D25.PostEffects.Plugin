using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// ブルーム (filmicbloomshader) の結果をさらに光条 (filmicstreakshader) に通して合成する 2 段構えのエフェクト。
    /// 2.5 のゲームアセンブリに型が存在しないため、SceneCapture 同梱実装を自己完結な形で移植したもの
    /// (シェーダーは filmic バンドル)。
    /// 移植元にあったメイドマスク (EffectMask 連携) は EffectMask 自体が未移植のため省いている
    /// </summary>
    public class FilmicBloomEffect : MonoBehaviour
    {
        // XmlSerializer は入れ子型も外側の型名を含まない XML 型名で扱うため、
        // 他エフェクトの BlendMode と衝突しないよう明示的に名前を付ける (衝突すると Config 保存が丸ごと失敗する)
        [XmlType("FilmicBloomBlendMode")]
        public enum BlendMode
        {
            Multiply,
            Screen,
            Overlay,
            HardLight,
            SoftLight,
        }

        // BlendMode の並びと 1:1 で対応するシェーダーキーワード
        private static readonly string[] BlendModeKeywords =
        {
            "_MULTIPLY",
            "_SCREEN",
            "_OVERLAY",
            "_HARDLIGHT",
            "_SOFTLIGHT",
        };

        private const int MaxIterations = 16;

        public Shader bloomShader;
        public Shader streakShader;

        public float threshold = 1.1f;
        public float softKnee = 0.5f;
        public float radius = 1f;
        public float intensity = 2f;
        public bool highQuality = true;
        public bool antiFlicker = false;
        public Texture dirtTexture = null;
        public bool useDirtTexture = false;
        public float dirtIntensity = 0f;

        public float streakThreshold = 1f;
        public float streakSoftKnee = 0.5f;
        public float streakStretch = 0.75f;
        public float streakIntensity = 0.3f;
        public bool streakVertical = false;
        public bool streak2Way = false;
        public Color streakTint = new Color(0.55f, 0.55f, 0.55f);
        public BlendMode blendMode = BlendMode.Screen;

        private Material _bloomMaterial;
        private Material _streakMaterial;
        private readonly RenderTexture[] _blurBuffer1 = new RenderTexture[MaxIterations];
        private readonly RenderTexture[] _blurBuffer2 = new RenderTexture[MaxIterations];
        private readonly Stack<RenderTexture> _mipStack = new Stack<RenderTexture>();
        private readonly Stack<RenderTexture> _mipVStack = new Stack<RenderTexture>();
        private readonly Stack<RenderTexture> _mipHStack = new Stack<RenderTexture>();

        private void OnDisable()
        {
            if (_bloomMaterial != null)
            {
                DestroyImmediate(_bloomMaterial);
                _bloomMaterial = null;
            }
            if (_streakMaterial != null)
            {
                DestroyImmediate(_streakMaterial);
                _streakMaterial = null;
            }
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            // 光条段で縮小段が 1 段も作れない極小解像度では中間バッファの取り回しが破綻するため素通しする
            if (bloomShader == null || !bloomShader.isSupported ||
                streakShader == null || !streakShader.isSupported ||
                StreakBlur.IsTooSmall(source))
            {
                Graphics.Blit(source, destination);
                return;
            }

            if (_bloomMaterial == null)
            {
                _bloomMaterial = new Material(bloomShader);
                _bloomMaterial.hideFlags = HideFlags.DontSave;
            }
            if (_streakMaterial == null)
            {
                _streakMaterial = new Material(streakShader);
                _streakMaterial.hideFlags = HideFlags.DontSave;
            }

            var bloomed = RenderBloom(source);
            RenderStreak(source, destination, bloomed);
        }

        // ブルーム段。合成結果を一時 RT に書いて返す (呼び出し側で解放する)
        private RenderTexture RenderBloom(RenderTexture source)
        {
            var width = highQuality ? source.width : source.width / 2;
            var height = highQuality ? source.height : source.height / 2;
            var format = RenderTextureFormat.DefaultHDR;

            var logHeight = Mathf.Log(height, 2f) + radius - 8f;
            var logHeightInt = (int)logHeight;
            var iterations = Mathf.Clamp(logHeightInt, 1, MaxIterations);

            var thresholdLinear = Mathf.GammaToLinearSpace(Mathf.Max(0f, threshold));
            var knee = thresholdLinear * softKnee + 1E-05f;

            _bloomMaterial.SetFloat("_Threshold", thresholdLinear);
            _bloomMaterial.SetVector("_Curve", new Vector3(thresholdLinear - knee, knee * 2f, 0.25f / knee));
            _bloomMaterial.SetFloat("_PrefilterOffs", !highQuality && antiFlicker ? -0.5f : 0f);
            _bloomMaterial.SetFloat("_SampleScale", 0.5f + logHeight - logHeightInt);
            _bloomMaterial.SetFloat("_Intensity", Mathf.Max(0f, intensity));

            var useDirt = dirtTexture != null && useDirtTexture;
            if (useDirt)
            {
                _bloomMaterial.SetTexture("_DirtTex", dirtTexture);
                _bloomMaterial.SetFloat("_DirtIntensity", dirtIntensity);
            }
            _bloomMaterial.shaderKeywords = null;

            var bloomed = RenderTexture.GetTemporary(width, height, 0, format);
            Graphics.Blit(source, bloomed, _bloomMaterial, antiFlicker ? 1 : 0);

            var last = bloomed;
            for (var i = 0; i < iterations; i++)
            {
                _blurBuffer1[i] = RenderTexture.GetTemporary(last.width / 2, last.height / 2, 0, format);
                Graphics.Blit(last, _blurBuffer1[i], _bloomMaterial, i == 0 ? (antiFlicker ? 3 : 2) : 4);
                last = _blurBuffer1[i];
            }

            for (var i = iterations - 2; i >= 0; i--)
            {
                var high = _blurBuffer1[i];
                _bloomMaterial.SetTexture("_BaseTex", high);
                _blurBuffer2[i] = RenderTexture.GetTemporary(high.width, high.height, 0, format);
                Graphics.Blit(last, _blurBuffer2[i], _bloomMaterial, highQuality ? 6 : 5);
                last = _blurBuffer2[i];
            }

            // プリフィルタに使った RT を合成先として使い回す (移植元と同じ)
            var compositePass = (useDirt ? 9 : 7) + (highQuality ? 1 : 0);
            _bloomMaterial.SetTexture("_BaseTex", source);
            Graphics.Blit(last, bloomed, _bloomMaterial, compositePass);

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
            return bloomed;
        }

        // 光条段。ブルーム結果を横 (または縦) に伸ばして元画へ合成する
        private void RenderStreak(RenderTexture source, RenderTexture destination, RenderTexture bloomed)
        {
            var knee = streakThreshold * streakSoftKnee + 1E-05f;
            _streakMaterial.SetFloat("_Threshold", streakThreshold);
            _streakMaterial.SetVector("_Curve", new Vector3(streakThreshold - knee, knee * 2f, 0.25f / knee));
            _streakMaterial.SetFloat("_Stretch", streakStretch);
            _streakMaterial.SetFloat("_Intensity", streakIntensity);
            _streakMaterial.SetColor("_Color", streakTint);
            _streakMaterial.shaderKeywords = null;
            _streakMaterial.EnableKeyword(BlendModeKeywords[(int)blendMode]);

            if (streak2Way)
            {
                StreakBlur.RenderTwoWay(_streakMaterial, bloomed, source, destination, _mipVStack, _mipHStack);
            }
            else
            {
                StreakBlur.RenderOneWay(
                    _streakMaterial, bloomed, source, destination, _mipStack, streakVertical);
            }
            RenderTexture.ReleaseTemporary(bloomed);
        }
    }
}
