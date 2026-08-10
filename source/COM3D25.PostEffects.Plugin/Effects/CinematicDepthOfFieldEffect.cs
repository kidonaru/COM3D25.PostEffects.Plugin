using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// 近景・遠景を個別に設定できる被写界深度 (Cinematic/Depth Of Field)。
    /// 絞り形状 (円/六角/八角) の方向性ぼかしと、DX11 のテクスチャボケ (点描画) を持つ。
    /// 2.5 のゲームアセンブリに型が存在しないため、SceneCapture 同梱実装を自己完結な形で
    /// 移植したもの (シェーダーは cinematic バンドル)
    /// </summary>
    public class CinematicDepthOfFieldEffect : MonoBehaviour
    {
        // XmlSerializer は入れ子型も外側の型名を含まない XML 型名で扱うため明示的に名前を付ける
        [XmlType("CinematicDofTweakMode")]
        public enum TweakMode
        {
            // ピント面と範囲で指定する
            Range,
            // 近景側・遠景側の境界を個別に指定する
            Explicit,
        }

        [XmlType("CinematicDofApertureShape")]
        public enum ApertureShape
        {
            Circular,
            Hexagonal,
            Octogonal,
        }

        [XmlType("CinematicDofQualityPreset")]
        public enum QualityPreset
        {
            Low,
            Medium,
            High,
        }

        private enum FilterQuality
        {
            None,
            Normal,
            High,
        }

        // 被写界深度シェーダーのパス番号
        private const int BokehBlurPass = 0;
        private const int BokehPrefilterPass = 1;
        private const int DilateVerticalPass = 2;
        private const int DilateHorizontalPass = 3;
        // 元画から錯乱円 (CoC) 付きの半解像度バッファを作るパス
        private const int CocPass = 4;
        private const int VisualizeCocPass = 5;
        private const int PrefilterBlurPass = 6;
        // ぼかし結果を元画へ戻すパス
        private const int MergePass = 11;

        // メディアンフィルタシェーダーのパス番号 (0 = 1 方向ずつ、1 = 3x3 一括)
        private const int MedianSeparablePass = 0;
        private const int Median3x3Pass = 1;

        // テクスチャボケシェーダーのパス番号 (0 = 点の描画、1 = 点の収集)
        private const int BokehDrawPass = 0;
        private const int BokehCollectPass = 1;

        public Shader shader;
        public Shader medianFilterShader;
        public Shader bokehSplattingShader;

        public bool visualizeFocus = false;
        public TweakMode tweakMode = TweakMode.Explicit;
        public QualityPreset filteringQuality = QualityPreset.High;
        public ApertureShape apertureShape = ApertureShape.Circular;
        public float apertureOrientation = 0f;

        // Range モードでピントを合わせる対象。null なら focusFocusPlane を使う
        public Transform focusTransform = null;
        public float focusFocusPlane = 20f;
        public float focusRange = 35f;
        public float focusNearPlane = 3f;
        public float focusNearFalloff = 3f;
        public float focusFarPlane = 6f;
        public float focusFarFalloff = 6f;
        public float focusNearBlurRadius = 18f;
        public float focusFarBlurRadius = 20f;

        public bool useBokehTexture = false;
        public bool antiFlicker = false;
        public Texture2D bokehTexture = null;
        public float bokehScale = 1f;
        public float bokehIntensity = 50f;
        public float bokehThreshold = 2f;
        public float bokehSpawnHeuristic = 0.15f;

        private Camera _camera;
        private Material _dofMaterial;
        private Material _medianMaterial;
        private Material _bokehMaterial;
        private ComputeBuffer _drawArgsBuffer;
        private ComputeBuffer _pointsBuffer;

        // 1 フレーム内で使い回す一時 RT。描画の最後にまとめて返す
        private readonly List<RenderTexture> _tempTextures = new List<RenderTexture>();

        // apertureOrientation から作った方向ベクトル群 (角度が変わったときだけ作り直す)
        private Vector4 _hexDirection1, _hexDirection2, _hexDirection3;
        private Vector4 _octDirection1, _octDirection2, _octDirection3, _octDirection4;
        private float _lastApertureOrientation = float.NaN;

        // このフレームで使う実効品質 (filteringQuality と antiFlicker から決まる)
        private bool _prefilterBlur;
        private bool _dilateNearBlur;
        private FilterQuality _medianFilter;

        private Camera targetCamera
        {
            get
            {
                if (_camera == null)
                {
                    _camera = GetComponent<Camera>();
                }
                return _camera;
            }
        }

        // テクスチャボケは DX11 世代の機能 (点の書き出しに ComputeBuffer を使う) に依存する
        public static bool supportsTextureBokeh =>
            SystemInfo.graphicsShaderLevel >= 50 && SystemInfo.supportsComputeShaders;

        private bool shouldPerformBokeh =>
            supportsTextureBokeh && useBokehTexture && bokehTexture != null && _bokehMaterial != null;

        private void OnEnable()
        {
            if (targetCamera != null)
            {
                targetCamera.depthTextureMode |= DepthTextureMode.Depth;
            }
            ComputeBlurDirections(true);
        }

        private void OnDisable()
        {
            ReleaseComputeResources();
            DestroyMaterial(ref _dofMaterial);
            DestroyMaterial(ref _medianMaterial);
            DestroyMaterial(ref _bokehMaterial);
            ReleaseAllTemporary();
        }

        private static void DestroyMaterial(ref Material material)
        {
            if (material != null)
            {
                DestroyImmediate(material);
                material = null;
            }
        }

        private void ReleaseComputeResources()
        {
            if (_drawArgsBuffer != null)
            {
                _drawArgsBuffer.Release();
                _drawArgsBuffer = null;
            }
            if (_pointsBuffer != null)
            {
                _pointsBuffer.Release();
                _pointsBuffer = null;
            }
        }

        private RenderTexture GetTemporary(
            int width, int height, RenderTextureFormat format, FilterMode filterMode = FilterMode.Bilinear)
        {
            var rt = RenderTexture.GetTemporary(width, height, 0, format);
            rt.filterMode = filterMode;
            _tempTextures.Add(rt);
            return rt;
        }

        private void ReleaseTemporary(RenderTexture rt)
        {
            if (_tempTextures.Remove(rt))
            {
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        private void ReleaseAllTemporary()
        {
            foreach (var rt in _tempTextures)
            {
                RenderTexture.ReleaseTemporary(rt);
            }
            _tempTextures.Clear();
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (shader == null || !shader.isSupported ||
                medianFilterShader == null || !medianFilterShader.isSupported)
            {
                Graphics.Blit(source, destination);
                return;
            }

            if (_dofMaterial == null)
            {
                _dofMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
            }
            if (_medianMaterial == null)
            {
                _medianMaterial = new Material(medianFilterShader) { hideFlags = HideFlags.DontSave };
            }
            if (_bokehMaterial == null && bokehSplattingShader != null && bokehSplattingShader.isSupported)
            {
                _bokehMaterial = new Material(bokehSplattingShader) { hideFlags = HideFlags.DontSave };
            }

            targetCamera.depthTextureMode |= DepthTextureMode.Depth;

            if (visualizeFocus)
            {
                Vector4 blurParams, blurCoe;
                ComputeCocParameters(out blurParams, out blurCoe);
                _dofMaterial.SetVector("_BlurParams", blurParams);
                _dofMaterial.SetVector("_BlurCoe", blurCoe);
                Graphics.Blit(null, destination, _dofMaterial, VisualizeCocPass);
            }
            else
            {
                DoDepthOfField(source, destination);
            }

            ReleaseAllTemporary();
        }

        private void DoDepthOfField(RenderTexture source, RenderTexture destination)
        {
            ApplyQualityPreset();

            // ぼかし半径は 720p を基準に解像度でスケールする
            var scale = source.height / 720f;
            var nearRadius = focusNearBlurRadius * scale;
            var farRadius = focusFarBlurRadius * scale;
            var bokehRadius = Mathf.Max(focusNearBlurRadius, focusFarBlurRadius) * scale * 0.75f;

            var maxRadius = Mathf.Max(nearRadius, farRadius);
            if (apertureShape == ApertureShape.Hexagonal)
            {
                maxRadius *= 1.2f;
            }
            else if (apertureShape == ApertureShape.Octogonal)
            {
                maxRadius *= 1.15f;
            }

            // 半ピクセルにも満たないぼかしは効果が見えないので素通しする
            if (maxRadius < 0.5f)
            {
                Graphics.Blit(source, destination);
                return;
            }

            var width = source.width / 2;
            var height = source.height / 2;
            var halfRadii = new Vector4(nearRadius * 0.5f, farRadius * 0.5f, 0f, 0f);

            var rtA = GetTemporary(width, height, RenderTextureFormat.ARGBHalf);
            var rtB = GetTemporary(width, height, RenderTextureFormat.ARGBHalf);

            Vector4 blurParams, blurCoe;
            ComputeCocParameters(out blurParams, out blurCoe);
            _dofMaterial.SetVector("_BlurParams", blurParams);
            _dofMaterial.SetVector("_BlurCoe", blurCoe);
            Graphics.Blit(source, rtB, _dofMaterial, CocPass);

            var src = rtB;
            var dst = rtA;

            if (shouldPerformBokeh)
            {
                src = CollectBokehPoints(src, ref dst, width, height, scale, bokehRadius);
            }

            _dofMaterial.SetVector("_BlurParams", blurParams);
            _dofMaterial.SetVector("_BlurCoe", halfRadii);

            var blurredNearCoc = _dilateNearBlur ? DilateNearCoc(src, width, height, nearRadius) : null;

            if (_prefilterBlur)
            {
                Graphics.Blit(src, dst, _dofMaterial, PrefilterBlurPass);
                Swap(ref src, ref dst);
            }

            switch (apertureShape)
            {
                case ApertureShape.Circular:
                    DoCircularBlur(blurredNearCoc, ref src, ref dst, maxRadius);
                    break;
                case ApertureShape.Hexagonal:
                    DoHexagonalBlur(blurredNearCoc, ref src, ref dst, maxRadius);
                    break;
                case ApertureShape.Octogonal:
                    DoOctogonalBlur(blurredNearCoc, ref src, ref dst, maxRadius);
                    break;
            }

            if (_medianFilter == FilterQuality.High)
            {
                Graphics.Blit(src, dst, _medianMaterial, Median3x3Pass);
                Swap(ref src, ref dst);
            }
            else if (_medianFilter == FilterQuality.Normal)
            {
                _medianMaterial.SetVector("_Offsets", new Vector4(1f, 0f, 0f, 0f));
                Graphics.Blit(src, dst, _medianMaterial, MedianSeparablePass);
                Swap(ref src, ref dst);
                _medianMaterial.SetVector("_Offsets", new Vector4(0f, 1f, 0f, 0f));
                Graphics.Blit(src, dst, _medianMaterial, MedianSeparablePass);
                Swap(ref src, ref dst);
            }

            _dofMaterial.SetVector("_BlurCoe", halfRadii);
            _dofMaterial.SetVector("_Convolved_TexelSize",
                new Vector4(src.width, src.height, 1f / src.width, 1f / src.height));
            _dofMaterial.SetTexture("_SecondTex", src);

            if (shouldPerformBokeh)
            {
                // 移植元はここで幅と高さを入れ違いに渡していたため縦横比が崩れていた
                var merged = GetTemporary(source.width, source.height, source.format);
                Graphics.Blit(source, merged, _dofMaterial, MergePass);
                DrawBokehPoints(merged, source, bokehRadius);
                Graphics.Blit(merged, destination);
            }
            else
            {
                Graphics.Blit(source, destination, _dofMaterial, MergePass);
            }
        }

        // 品質プリセットから前処理ぼかし・メディアンフィルタ・近景 CoC の拡張を決める
        private void ApplyQualityPreset()
        {
            switch (filteringQuality)
            {
                case QualityPreset.High:
                    _prefilterBlur = true;
                    _medianFilter = FilterQuality.High;
                    _dilateNearBlur = true;
                    break;
                case QualityPreset.Medium:
                    _prefilterBlur = true;
                    _medianFilter = FilterQuality.Normal;
                    _dilateNearBlur = false;
                    break;
                default:
                    _prefilterBlur = false;
                    _medianFilter = FilterQuality.None;
                    _dilateNearBlur = false;
                    break;
            }

            // メディアンフィルタはちらつき対策が目的なので、無効なら掛けない
            if (!antiFlicker)
            {
                _medianFilter = FilterQuality.None;
            }
        }

        // 明部をぼかしの点として ComputeBuffer に書き出す。戻り値は次段の入力
        private RenderTexture CollectBokehPoints(
            RenderTexture src, ref RenderTexture dst, int width, int height, float scale, float bokehRadius)
        {
            if (_drawArgsBuffer == null)
            {
                _drawArgsBuffer = new ComputeBuffer(1, 16, ComputeBufferType.IndirectArguments);
                _drawArgsBuffer.SetData(new[] { 0, 1, 0, 0 });
            }
            if (_pointsBuffer == null)
            {
                _pointsBuffer = new ComputeBuffer(90000, 28, ComputeBufferType.Append);
            }

            var blurred = GetTemporary(width, height, RenderTextureFormat.ARGBHalf);
            Graphics.Blit(src, blurred, _dofMaterial, BokehPrefilterPass);
            _dofMaterial.SetVector("_Offsets", new Vector4(0f, 1.5f, 0f, 1.5f));
            Graphics.Blit(blurred, dst, _dofMaterial, BokehBlurPass);
            _dofMaterial.SetVector("_Offsets", new Vector4(1.5f, 0f, 0f, 1.5f));
            Graphics.Blit(dst, blurred, _dofMaterial, BokehBlurPass);

            _bokehMaterial.SetTexture("_BlurredColor", blurred);
            _bokehMaterial.SetFloat("_SpawnHeuristic", bokehSpawnHeuristic);
            _bokehMaterial.SetVector("_BokehParams",
                new Vector4(bokehScale * scale, bokehIntensity, bokehThreshold, bokehRadius));

            Graphics.SetRandomWriteTarget(1, _pointsBuffer);
            Graphics.Blit(src, dst, _bokehMaterial, BokehCollectPass);
            Graphics.ClearRandomWriteTargets();

            Swap(ref src, ref dst);
            ReleaseTemporary(blurred);
            return src;
        }

        // 収集した点をボケテクスチャとして合成結果へ描き込む
        private void DrawBokehPoints(RenderTexture target, RenderTexture source, float bokehRadius)
        {
            Graphics.SetRenderTarget(target);
            ComputeBuffer.CopyCount(_pointsBuffer, _drawArgsBuffer, 0);
            _bokehMaterial.SetBuffer("pointBuffer", _pointsBuffer);
            _bokehMaterial.SetTexture("_MainTex", bokehTexture);
            _bokehMaterial.SetVector("_Screen",
                new Vector3(1f / source.width, 1f / source.height, bokehRadius));
            _bokehMaterial.SetPass(BokehDrawPass);
            // Unity 2022 では即時描画版が DrawProceduralIndirectNow に改名されている
            Graphics.DrawProceduralIndirectNow(MeshTopology.Points, _drawArgsBuffer, 0);
        }

        // 近景の CoC を上下左右へ広げて、ピント内の被写体へぼけが食い込むのを防ぐ
        private RenderTexture DilateNearCoc(RenderTexture src, int width, int height, float nearRadius)
        {
            var temp = GetTemporary(width, height, RenderTextureFormat.RGHalf);
            var dilated = GetTemporary(width, height, RenderTextureFormat.RGHalf);

            _dofMaterial.SetVector("_Offsets", new Vector4(0f, nearRadius * 0.75f, 0f, 0f));
            Graphics.Blit(src, temp, _dofMaterial, DilateVerticalPass);
            _dofMaterial.SetVector("_Offsets", new Vector4(nearRadius * 0.75f, 0f, 0f, 0f));
            Graphics.Blit(temp, dilated, _dofMaterial, DilateHorizontalPass);

            ReleaseTemporary(temp);
            dilated.filterMode = FilterMode.Point;
            return dilated;
        }

        private void DoCircularBlur(
            RenderTexture blurredNearCoc, ref RenderTexture src, ref RenderTexture dst, float maxRadius)
        {
            int pass;
            if (blurredNearCoc != null)
            {
                _dofMaterial.SetTexture("_SecondTex", blurredNearCoc);
                pass = maxRadius > 10f ? 8 : 10;
            }
            else
            {
                pass = maxRadius > 10f ? 7 : 9;
            }
            Graphics.Blit(src, dst, _dofMaterial, pass);
            Swap(ref src, ref dst);
        }

        private void DoHexagonalBlur(
            RenderTexture blurredNearCoc, ref RenderTexture src, ref RenderTexture dst, float maxRadius)
        {
            ComputeBlurDirections(false);
            int blurPass, blurAndMergePass;
            GetDirectionalBlurPasses(blurredNearCoc, maxRadius, out blurPass, out blurAndMergePass);
            _dofMaterial.SetTexture("_SecondTex", blurredNearCoc);

            var temp = GetTemporary(src.width, src.height, src.format);
            _dofMaterial.SetVector("_Offsets", _hexDirection1);
            Graphics.Blit(src, temp, _dofMaterial, blurPass);
            _dofMaterial.SetVector("_Offsets", _hexDirection2);
            Graphics.Blit(temp, src, _dofMaterial, blurPass);
            _dofMaterial.SetVector("_Offsets", _hexDirection3);
            _dofMaterial.SetTexture("_ThirdTex", src);
            Graphics.Blit(temp, dst, _dofMaterial, blurAndMergePass);

            ReleaseTemporary(temp);
            Swap(ref src, ref dst);
        }

        private void DoOctogonalBlur(
            RenderTexture blurredNearCoc, ref RenderTexture src, ref RenderTexture dst, float maxRadius)
        {
            ComputeBlurDirections(false);
            int blurPass, blurAndMergePass;
            GetDirectionalBlurPasses(blurredNearCoc, maxRadius, out blurPass, out blurAndMergePass);
            _dofMaterial.SetTexture("_SecondTex", blurredNearCoc);

            var temp = GetTemporary(src.width, src.height, src.format);
            _dofMaterial.SetVector("_Offsets", _octDirection1);
            Graphics.Blit(src, temp, _dofMaterial, blurPass);
            _dofMaterial.SetVector("_Offsets", _octDirection2);
            Graphics.Blit(temp, dst, _dofMaterial, blurPass);
            _dofMaterial.SetVector("_Offsets", _octDirection3);
            Graphics.Blit(src, temp, _dofMaterial, blurPass);
            _dofMaterial.SetVector("_Offsets", _octDirection4);
            _dofMaterial.SetTexture("_ThirdTex", dst);
            Graphics.Blit(temp, src, _dofMaterial, blurAndMergePass);

            ReleaseTemporary(temp);
        }

        // 方向性ぼかしのパスは「近景 CoC の有無」×「半径の段階 (小/中/大)」で 6 通りある
        private static void GetDirectionalBlurPasses(
            RenderTexture blurredNearCoc, float maxRadius, out int blurPass, out int blurAndMergePass)
        {
            var hasNearCoc = blurredNearCoc != null;
            if (maxRadius > 10f)
            {
                blurPass = hasNearCoc ? 21 : 20;
                blurAndMergePass = hasNearCoc ? 23 : 22;
            }
            else if (maxRadius > 5f)
            {
                blurPass = hasNearCoc ? 17 : 16;
                blurAndMergePass = hasNearCoc ? 19 : 18;
            }
            else
            {
                blurPass = hasNearCoc ? 13 : 12;
                blurAndMergePass = hasNearCoc ? 15 : 14;
            }
        }

        // 近景・遠景の境界からシェーダーへ渡す錯乱円の係数を求める
        private void ComputeCocParameters(out Vector4 blurParams, out Vector4 blurCoe)
        {
            var camera = targetCamera;
            var nearFalloff = focusNearFalloff * 2f;
            var farFalloff = focusFarFalloff * 2f;
            var nearPlane = focusNearPlane;
            var farPlane = focusFarPlane;

            if (tweakMode == TweakMode.Range)
            {
                var focusPlane = focusTransform != null
                    ? camera.WorldToViewportPoint(focusTransform.position).z
                    : focusFocusPlane;
                var halfRange = focusRange * 0.5f;
                nearPlane = focusPlane - halfRange;
                farPlane = focusPlane + halfRange;
            }

            nearPlane -= nearFalloff * 0.5f;
            farPlane += farFalloff * 0.5f;

            var focusCenter = (nearPlane + farPlane) * 0.5f;
            var focusNorm = focusCenter / camera.farClipPlane;
            var nearNorm = nearPlane / camera.farClipPlane;
            var farNorm = farPlane / camera.farClipPlane;
            var depth = farPlane - nearPlane;
            var depthNorm = farNorm - nearNorm;
            var nearMargin = (1f - nearFalloff / depth) * (depthNorm * 0.5f);
            var farMargin = (1f - farFalloff / depth) * (depthNorm * 0.5f);

            // 各境界が重ならないよう最小幅を確保する
            if (focusNorm <= nearNorm)
            {
                focusNorm = nearNorm + 1E-06f;
            }
            if (focusNorm >= farNorm)
            {
                focusNorm = farNorm - 1E-06f;
            }
            if (focusNorm - nearMargin <= nearNorm)
            {
                nearMargin = focusNorm - nearNorm - 1E-06f;
            }
            if (focusNorm + farMargin >= farNorm)
            {
                farMargin = farNorm - focusNorm - 1E-06f;
            }

            var nearSlope = 1f / (nearNorm - focusNorm + nearMargin);
            var farSlope = 1f / (farNorm - focusNorm - farMargin);
            var nearBias = 1f - nearSlope * nearNorm;
            var farBias = 1f - farSlope * farNorm;

            blurParams = new Vector4(-nearSlope, -nearBias, farSlope, farBias);
            blurCoe = new Vector4(0f, 0f, (farBias - nearBias) / (nearSlope - farSlope), 0f);
        }

        private void ComputeBlurDirections(bool force)
        {
            if (!force && Mathf.Abs(_lastApertureOrientation - apertureOrientation) < float.Epsilon)
            {
                return;
            }
            _lastApertureOrientation = apertureOrientation;

            _octDirection1 = new Vector4(0.5f, 0f, 0f, 0f);
            _octDirection2 = new Vector4(0f, 0.5f, 1f, 0f);
            _octDirection3 = new Vector4(-0.353553f, 0.353553f, 1f, 0f);
            _octDirection4 = new Vector4(0.353553f, 0.353553f, 1f, 0f);
            _hexDirection1 = new Vector4(0.5f, 0f, 0f, 0f);
            _hexDirection2 = new Vector4(0.25f, 0.433013f, 1f, 0f);
            _hexDirection3 = new Vector4(0.25f, -0.433013f, 1f, 0f);

            var radian = apertureOrientation * Mathf.Deg2Rad;
            if (radian <= float.Epsilon)
            {
                return;
            }

            var cos = Mathf.Cos(radian);
            var sin = Mathf.Sin(radian);
            Rotate2D(ref _octDirection1, cos, sin);
            Rotate2D(ref _octDirection2, cos, sin);
            Rotate2D(ref _octDirection3, cos, sin);
            Rotate2D(ref _octDirection4, cos, sin);
            Rotate2D(ref _hexDirection1, cos, sin);
            Rotate2D(ref _hexDirection2, cos, sin);
            Rotate2D(ref _hexDirection3, cos, sin);
        }

        private static void Rotate2D(ref Vector4 direction, float cos, float sin)
        {
            var x = direction.x;
            var y = direction.y;
            direction.x = x * cos - y * sin;
            direction.y = x * sin + y * cos;
        }

        private static void Swap(ref RenderTexture a, ref RenderTexture b)
        {
            var temp = a;
            a = b;
            b = temp;
        }
    }
}
