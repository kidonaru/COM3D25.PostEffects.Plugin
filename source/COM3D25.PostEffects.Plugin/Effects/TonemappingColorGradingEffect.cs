using System.Xml.Serialization;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// トーンマッピング・カラーグレーディング・目の順応をまとめて行う後処理。
    /// 2.5 のゲームアセンブリに型が存在しないため、SceneCapture 同梱実装を自己完結な形で
    /// 移植したもの (シェーダーは cinematic バンドル)。
    /// 移植元にあった開発用のデバッグ表示 (LUT / 順応輝度の画面焼き込み) は省いている
    /// </summary>
    public class TonemappingColorGradingEffect : MonoBehaviour
    {
        // XmlSerializer は入れ子型も外側の型名を含まない XML 型名で扱うため明示的に名前を付ける
        [XmlType("TonemappingTonemapper")]
        public enum Tonemapper
        {
            ACES,
            Curve,
            Hable,
            HejlDawson,
            Photographic,
            Reinhard,
            Neutral,
        }

        // シェーダーのパス番号
        private const int LutGenPass = 0;
        private const int AdaptationLogPass = 1;
        private const int AdaptationExpBlendPass = 2;
        private const int AdaptationExpPass = 3;
        // トーンマッピング無しの基準パス。Tonemapper の並び順に +1 ずつ続く
        private const int BasePass = 4;

        // 内部 LUT の一辺。移植元も 32 固定
        private const int LutSize = 32;
        private const int CurveTextureWidth = 256;

        public Shader shader;

        public bool eyeAdaptationEnabled = false;
        public float eyeAdaptationMiddleGrey = 0.5f;
        public float eyeAdaptationMin = -0.1f;
        public float eyeAdaptationMax = 0.1f;
        public float eyeAdaptationSpeed = 1.5f;

        public bool tonemappingEnabled = false;
        public Tonemapper tonemapper = Tonemapper.Neutral;
        public float tonemappingExposure = 1f;
        public AnimationCurve tonemappingCurve;
        public float neutralBlackIn = 0.02f;
        public float neutralWhiteIn = 10f;
        public float neutralBlackOut = 0f;
        public float neutralWhiteOut = 10f;
        public float neutralWhiteLevel = 5.3f;
        public float neutralWhiteClip = 10f;

        public bool colorGradingEnabled = false;
        public Color shadows = Color.white;
        public Color midtones = Color.white;
        public Color highlights = Color.white;
        public float temperatureShift = 0f;
        public float tint = 0f;
        public float hue = 0f;
        public float saturation = 1f;
        public float vibrance = 0f;
        public float colorGradingValue = 1f;
        public float colorGradingContrast = 1f;
        public float colorGradingGain = 1f;
        public float colorGradingGamma = 1f;
        public Vector3 channelMixerRed = new Vector3(1f, 0f, 0f);
        public Vector3 channelMixerGreen = new Vector3(0f, 1f, 0f);
        public Vector3 channelMixerBlue = new Vector3(0f, 0f, 1f);
        public AnimationCurve masterCurve;
        public AnimationCurve redCurve;
        public AnimationCurve greenCurve;
        public AnimationCurve blueCurve;
        public bool useDithering = false;

        public bool userLutEnabled = false;
        public Texture2D userLut;
        public float userLutContribution = 0f;

        private Material _material;
        private Texture2D _identityLut;
        private RenderTexture _internalLut;
        private Texture2D _curveTexture;
        private Texture2D _tonemapperCurveTexture;
        private float _tonemapperCurveRange = 1f;
        private RenderTexture _adaptiveRt;
        private RenderTextureFormat _adaptiveRtFormat = RenderTextureFormat.ARGBHalf;
        private RenderTexture[] _adaptMips;

        // カーブテクスチャの焼き直しは 256 点評価するので、変更があったフレームだけ行う。
        // グレーディング用とトーンカーブ用は片方だけ使う構成があるため、フラグを分けて持つ
        private bool _gradingCurvesDirty = true;
        private bool _toneCurveDirty = true;

        /// <summary>カーブを差し替えたあとに呼び、次の描画で焼き直させる</summary>
        public void SetCurvesDirty()
        {
            _gradingCurvesDirty = true;
            _toneCurveDirty = true;
        }

        private void OnDisable()
        {
            if (_material != null)
            {
                DestroyImmediate(_material);
                _material = null;
            }
            DestroyTexture(ref _identityLut);
            DestroyTexture(ref _curveTexture);
            DestroyTexture(ref _tonemapperCurveTexture);
            if (_internalLut != null)
            {
                DestroyImmediate(_internalLut);
                _internalLut = null;
            }
            if (_adaptiveRt != null)
            {
                DestroyImmediate(_adaptiveRt);
                _adaptiveRt = null;
            }
            SetCurvesDirty();
        }

        private static void DestroyTexture(ref Texture2D texture)
        {
            if (texture != null)
            {
                DestroyImmediate(texture);
                texture = null;
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
                _material = new Material(shader) { hideFlags = HideFlags.DontSave };
            }
            _material.shaderKeywords = null;

            RenderTexture adaptationSource = null;
            if (eyeAdaptationEnabled)
            {
                adaptationSource = UpdateEyeAdaptation(source);
            }

            var pass = BasePass;
            if (tonemappingEnabled)
            {
                SetUpTonemapping();
                pass = BasePass + (int)tonemapper + 1;
            }

            if (colorGradingEnabled)
            {
                SetUpColorGrading();
            }

            if (userLutEnabled && userLut != null && IsValidLut(userLut))
            {
                _material.SetTexture("_UserLutTex", userLut);
                _material.SetVector("_UserLutParams",
                    new Vector4(1f / userLut.width, 1f / userLut.height, userLut.height - 1f, userLutContribution));
                _material.EnableKeyword("ENABLE_USER_LUT");
            }

            Graphics.Blit(source, destination, _material, pass);

            if (adaptationSource != null)
            {
                foreach (var rt in _adaptMips)
                {
                    RenderTexture.ReleaseTemporary(rt);
                }
                RenderTexture.ReleaseTemporary(adaptationSource);
            }
        }

        // 画面を 2 の冪へ落としてから 1x1 まで縮小し、平均輝度を時間方向に馴染ませる
        private RenderTexture UpdateEyeAdaptation(RenderTexture source)
        {
            var justCreated = false;
            if (_adaptiveRt == null)
            {
                _adaptiveRtFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGHalf)
                    ? RenderTextureFormat.RGHalf
                    : RenderTextureFormat.ARGBHalf;
                _adaptiveRt = new RenderTexture(1, 1, 0, _adaptiveRtFormat) { hideFlags = HideFlags.DontSave };
                justCreated = true;
            }

            var size = LargestPowerOfTwoAtMost(Mathf.Min(source.width, source.height));
            var squared = RenderTexture.GetTemporary(size, size, 0, _adaptiveRtFormat);
            Graphics.Blit(source, squared);

            var mipCount = (int)Mathf.Log(squared.width, 2f);
            if (_adaptMips == null || _adaptMips.Length != mipCount)
            {
                _adaptMips = new RenderTexture[mipCount];
            }

            var divisor = 2;
            for (var i = 0; i < mipCount; i++)
            {
                _adaptMips[i] = RenderTexture.GetTemporary(
                    squared.width / divisor, squared.width / divisor, 0, _adaptiveRtFormat);
                divisor <<= 1;
            }

            Graphics.Blit(squared, _adaptMips[0], _material, AdaptationLogPass);
            for (var i = 0; i < mipCount - 1; i++)
            {
                Graphics.Blit(_adaptMips[i], _adaptMips[i + 1]);
            }

            _material.SetFloat("_AdaptationSpeed", Mathf.Max(eyeAdaptationSpeed, 0.001f));
            // 初回は前フレームの輝度が無いので、混ぜずにそのまま書き込むパスを使う
            Graphics.Blit(_adaptMips[mipCount - 1], _adaptiveRt, _material,
                justCreated ? AdaptationExpPass : AdaptationExpBlendPass);

            _material.SetFloat("_MiddleGrey", eyeAdaptationMiddleGrey);
            _material.SetFloat("_AdaptationMin", Mathf.Pow(2f, eyeAdaptationMin));
            _material.SetFloat("_AdaptationMax", Mathf.Pow(2f, eyeAdaptationMax));
            _material.SetTexture("_LumTex", _adaptiveRt);
            _material.EnableKeyword("ENABLE_EYE_ADAPTATION");
            return squared;
        }

        private static int LargestPowerOfTwoAtMost(int value)
        {
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            return value - (value >> 1);
        }

        private void SetUpTonemapping()
        {
            if (tonemapper == Tonemapper.Curve)
            {
                BakeTonemapperCurve();
                _material.SetFloat("_ToneCurveRange", _tonemapperCurveRange);
                _material.SetTexture("_ToneCurve", _tonemapperCurveTexture);
            }
            else if (tonemapper == Tonemapper.Neutral)
            {
                var blackIn = neutralBlackIn * 20f + 1f;
                var blackOut = neutralBlackOut * 10f + 1f;
                var whiteIn = neutralWhiteIn / 20f;
                var whiteOut = 1f - neutralWhiteOut / 20f;
                var blackRatio = blackIn / blackOut;
                var whiteRatio = whiteIn / whiteOut;

                _material.SetVector("_NeutralTonemapperParams1", new Vector4(
                    0.2f,
                    Mathf.Max(0f, Mathf.LerpUnclamped(0.57f, 0.37f, blackRatio)),
                    Mathf.LerpUnclamped(0.01f, 0.24f, whiteRatio),
                    Mathf.Max(0f, Mathf.LerpUnclamped(0.02f, 0.2f, blackRatio))));
                _material.SetVector("_NeutralTonemapperParams2",
                    new Vector4(0.02f, 0.3f, neutralWhiteLevel, neutralWhiteClip / 10f));
            }

            _material.SetFloat("_Exposure", tonemappingExposure);
        }

        private void SetUpColorGrading()
        {
            Color lift, gamma, gain;
            GenerateLiftGammaGain(out lift, out gamma, out gain);
            BakeCurveTexture();

            _material.SetVector("_WhiteBalance", GetWhiteBalance());
            _material.SetVector("_Lift", lift);
            _material.SetVector("_Gamma", gamma);
            _material.SetVector("_Gain", gain);
            _material.SetVector("_ContrastGainGamma",
                new Vector3(colorGradingContrast, colorGradingGain, 1f / colorGradingGamma));
            _material.SetFloat("_Vibrance", vibrance);
            _material.SetVector("_HSV", new Vector4(hue, saturation, colorGradingValue));
            _material.SetVector("_ChannelMixerRed", channelMixerRed);
            _material.SetVector("_ChannelMixerGreen", channelMixerGreen);
            _material.SetVector("_ChannelMixerBlue", channelMixerBlue);
            _material.SetTexture("_CurveTex", _curveTexture);

            var lut = GetInternalLut();
            Graphics.Blit(GetIdentityLut(), lut, _material, LutGenPass);

            _material.EnableKeyword("ENABLE_COLOR_GRADING");
            if (useDithering)
            {
                _material.EnableKeyword("ENABLE_DITHERING");
            }
            _material.SetTexture("_InternalLutTex", lut);
            _material.SetVector("_InternalLutParams",
                new Vector3(1f / lut.width, 1f / lut.height, lut.height - 1f));
        }

        private RenderTexture GetInternalLut()
        {
            if (_internalLut == null || !_internalLut.IsCreated())
            {
                if (_internalLut != null)
                {
                    DestroyImmediate(_internalLut);
                }
                _internalLut = new RenderTexture(LutSize * LutSize, LutSize, 0, RenderTextureFormat.ARGB32)
                {
                    name = "Internal LUT",
                    filterMode = FilterMode.Bilinear,
                    anisoLevel = 0,
                    hideFlags = HideFlags.DontSave,
                };
            }
            return _internalLut;
        }

        private Texture2D GetIdentityLut()
        {
            if (_identityLut != null)
            {
                return _identityLut;
            }

            var colors = new Color[LutSize * LutSize * LutSize];
            var scale = 1f / (LutSize - 1f);
            for (var r = 0; r < LutSize; r++)
            {
                for (var g = 0; g < LutSize; g++)
                {
                    for (var b = 0; b < LutSize; b++)
                    {
                        // このシェーダーの内部 LUT は緑と青を入れ替えて格納する (移植元と同じ並び)
                        colors[r + g * LutSize + b * LutSize * LutSize] =
                            new Color(r * scale, b * scale, g * scale, 1f);
                    }
                }
            }

            _identityLut = new Texture2D(LutSize * LutSize, LutSize, TextureFormat.RGB24, false, true)
            {
                name = "Identity LUT",
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0,
                hideFlags = HideFlags.DontSave,
            };
            _identityLut.SetPixels(colors);
            _identityLut.Apply();
            return _identityLut;
        }

        private void BakeCurveTexture()
        {
            if (_curveTexture != null && !_gradingCurvesDirty)
            {
                return;
            }

            if (_curveTexture == null)
            {
                _curveTexture = new Texture2D(CurveTextureWidth, 1, TextureFormat.ARGB32, false, true)
                {
                    name = "Curve texture",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    anisoLevel = 0,
                    hideFlags = HideFlags.DontSave,
                };
            }

            var colors = new Color[CurveTextureWidth];
            for (var i = 0; i < CurveTextureWidth; i++)
            {
                var t = i / (CurveTextureWidth - 1f);
                colors[i] = new Color(
                    Evaluate(redCurve, t),
                    Evaluate(greenCurve, t),
                    Evaluate(blueCurve, t),
                    Evaluate(masterCurve, t));
            }
            _curveTexture.SetPixels(colors);
            _curveTexture.Apply();
            _gradingCurvesDirty = false;
        }

        private void BakeTonemapperCurve()
        {
            if (_tonemapperCurveTexture == null)
            {
                var format = TextureFormat.RGB24;
                if (SystemInfo.SupportsTextureFormat(TextureFormat.RFloat))
                {
                    format = TextureFormat.RFloat;
                }
                else if (SystemInfo.SupportsTextureFormat(TextureFormat.RHalf))
                {
                    format = TextureFormat.RHalf;
                }

                _tonemapperCurveTexture = new Texture2D(CurveTextureWidth, 1, format, false, true)
                {
                    name = "Tonemapper curve texture",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    anisoLevel = 0,
                    hideFlags = HideFlags.DontSave,
                };
            }
            else if (!_toneCurveDirty)
            {
                return;
            }
            _toneCurveDirty = false;

            if (tonemappingCurve == null || tonemappingCurve.length == 0)
            {
                _tonemapperCurveRange = 1f;
                return;
            }

            // カーブの終端時刻を 1 に正規化して引けるよう、逆数をシェーダーへ渡す
            var range = tonemappingCurve[tonemappingCurve.length - 1].time;
            for (var i = 0; i < CurveTextureWidth; i++)
            {
                var t = i / (CurveTextureWidth - 1f);
                var value = tonemappingCurve.Evaluate(t * range);
                _tonemapperCurveTexture.SetPixel(i, 0, new Color(value, value, value));
            }
            _tonemapperCurveTexture.Apply();
            _tonemapperCurveRange = 1f / range;
        }

        private static float Evaluate(AnimationCurve curve, float time)
        {
            return curve == null ? time : Mathf.Clamp01(curve.Evaluate(time));
        }

        // ユーザー LUT は横一列に並んだストリップ (幅 = 高さの 2 乗) である必要がある
        private static bool IsValidLut(Texture2D lut)
        {
            return lut.height == (int)Mathf.Sqrt(lut.width);
        }

        private static Color NormalizeColor(Color color)
        {
            var average = (color.r + color.g + color.b) / 3f;
            if (Mathf.Approximately(average, 0f))
            {
                return Color.white;
            }
            return new Color(color.r / average, color.g / average, color.b / average, 1f);
        }

        // シャドウ / ミッドトーン / ハイライトの色被りを lift-gamma-gain に変換する
        private void GenerateLiftGammaGain(out Color lift, out Color gamma, out Color gain)
        {
            var s = NormalizeColor(shadows);
            var m = NormalizeColor(midtones);
            var h = NormalizeColor(highlights);
            var sAverage = (s.r + s.g + s.b) / 3f;
            var mAverage = (m.r + m.g + m.b) / 3f;
            var hAverage = (h.r + h.g + h.b) / 3f;

            lift = new Color((s.r - sAverage) * 0.1f, (s.g - sAverage) * 0.1f, (s.b - sAverage) * 0.1f);
            gamma = new Color(
                1f / Mathf.Max(0.01f, Mathf.Pow(2f, (m.r - mAverage) * 0.5f)),
                1f / Mathf.Max(0.01f, Mathf.Pow(2f, (m.g - mAverage) * 0.5f)),
                1f / Mathf.Max(0.01f, Mathf.Pow(2f, (m.b - mAverage) * 0.5f)));
            gain = new Color(
                Mathf.Pow(2f, (h.r - hAverage) * 0.5f),
                Mathf.Pow(2f, (h.g - hAverage) * 0.5f),
                Mathf.Pow(2f, (h.b - hAverage) * 0.5f));
        }

        // 色温度・色偏差を LMS 空間のスケールへ変換する
        private Vector3 GetWhiteBalance()
        {
            var x = 0.31271f - temperatureShift * (temperatureShift < 0f ? 0.1f : 0.05f);
            var y = StandardIlluminantY(x) + tint * 0.05f;
            var white = new Vector3(0.949237f, 1.03542f, 1.08728f);
            var lms = CIExyToLMS(x, y);
            return new Vector3(white.x / lms.x, white.y / lms.y, white.z / lms.z);
        }

        private static float StandardIlluminantY(float x)
        {
            return 2.87f * x - 3f * x * x - 0.27509508f;
        }

        private static Vector3 CIExyToLMS(float x, float y)
        {
            var bigX = x / y;
            var bigZ = (1f - x - y) / y;
            return new Vector3(
                0.7328f * bigX + 0.4296f - 0.1624f * bigZ,
                -0.7036f * bigX + 1.6975f + 0.0061f * bigZ,
                0.003f * bigX + 0.0136f + 0.9834f * bigZ);
        }
    }
}
