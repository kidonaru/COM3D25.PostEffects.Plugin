using System.Xml.Serialization;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// 錯乱円 (CoC) を計算して円形・六角形のボケを作る被写界深度 (Filmic/Bokeh)。
    /// 2.5 のゲームアセンブリに型が存在しないため、SceneCapture 同梱実装を自己完結な形で
    /// 移植したもの (シェーダーは filmic バンドル)。
    /// 移植元は透過込みの深度を EffectMask から受け取っていたが、EffectMask 自体は未移植のため
    /// 必要な部分 (カットオフ深度の描画) だけをこのコンポーネント内に持たせている
    /// </summary>
    public class FilmicBokehEffect : MonoBehaviour
    {
        // XmlSerializer は入れ子型も外側の型名を含まない XML 型名で扱うため明示的に名前を付ける
        [XmlType("FilmicBokehKernelSize")]
        public enum KernelSize
        {
            Small,
            Medium,
            Large,
            VeryLarge,
        }

        // シェーダーのパス番号
        private const int PrefilterPass = 14;
        // ぼかしは PrefilterPass の次から KernelSize の順に 4 本並んでいる
        private const int BlurPassBase = PrefilterPass + 1;
        private const int HexVerticalPass = 27;
        private const int HexDiagonalPass = 28;
        private const int PostBlurPass = 19;
        private const int CompositePass = 20;
        private const int VisualizePass = 26;

        public Shader shader;
        // 透過 (カットオフ) を含む深度を描くための置き換えシェーダー
        public Shader depthShader;

        // ピント合わせの対象。null なら focusDistance を使う
        public Transform pointOfFocus;
        public float focusDistance = 10f;
        public float fNumber = 1.4f;
        public bool useCameraFov = true;
        public float focalLength = 0.05f;
        public float focalRange = 1f;
        public KernelSize kernelSize = KernelSize.Medium;
        public bool useARGBHalf = true;
        public int radiusBasePixel = 6;
        public bool useHexBokeh = false;
        public float angle = 0f;
        public bool visualize = false;

        private Camera _camera;
        private Material _material;
        private Camera _depthCamera;
        private RenderTexture _depthRT;

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

        private void OnEnable()
        {
            if (targetCamera != null)
            {
                targetCamera.depthTextureMode |= DepthTextureMode.Depth;
            }
        }

        private void OnDisable()
        {
            if (_material != null)
            {
                DestroyImmediate(_material);
                _material = null;
            }
            ReleaseDepthResources();
        }

        private void ReleaseDepthResources()
        {
            if (_depthRT != null)
            {
                _depthRT.Release();
                DestroyImmediate(_depthRT);
                _depthRT = null;
            }
            if (_depthCamera != null)
            {
                DestroyImmediate(_depthCamera.gameObject);
                _depthCamera = null;
            }
        }

        // 透過込みの深度は OnRenderImage の時点では描けないため、カリング前に自前カメラで描いておく
        private void OnPreCull()
        {
            if (shader == null || depthShader == null)
            {
                return;
            }

            var format = useARGBHalf ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.Default;
            if (_depthRT != null &&
                (_depthRT.width != Screen.width || _depthRT.height != Screen.height || _depthRT.format != format))
            {
                _depthRT.Release();
                DestroyImmediate(_depthRT);
                _depthRT = null;
            }

            if (_depthRT == null)
            {
                _depthRT = new RenderTexture(Screen.width, Screen.height, 32, format)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Point,
                };
            }

            if (_depthCamera == null)
            {
                var go = new GameObject("PostEffectsFilmicBokehDepthCamera")
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };
                _depthCamera = go.AddComponent<Camera>();
                // 自前で Render を呼ぶので通常の描画順には乗せない
                _depthCamera.enabled = false;
            }

            _depthCamera.CopyFrom(targetCamera);
            _depthCamera.rect = new Rect(0f, 0f, 1f, 1f);
            _depthCamera.clearFlags = CameraClearFlags.Color;
            _depthCamera.backgroundColor = Color.black;
            _depthCamera.allowMSAA = false;
            _depthCamera.allowHDR = false;
            _depthCamera.depthTextureMode = DepthTextureMode.None;
            _depthCamera.targetTexture = _depthRT;
            _depthCamera.RenderWithShader(depthShader, "RenderType");

            if (_material != null)
            {
                _material.SetTexture("_DepthTex", _depthRT);
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
                if (_depthRT != null)
                {
                    _material.SetTexture("_DepthTex", _depthRT);
                }
            }

            targetCamera.depthTextureMode |= DepthTextureMode.Depth;
            SetUpShaderParameters(source);

            if (visualize)
            {
                Graphics.Blit(source, destination, _material, VisualizePass);
                return;
            }

            var width = source.width / 2;
            var height = source.height / 2;
            var format = useARGBHalf ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.Default;

            var rt1 = RenderTexture.GetTemporary(width, height, 0, format);
            var rt2 = RenderTexture.GetTemporary(width, height, 0, format);

            // CoC の計算はテクセルを混ぜたくないので等倍サンプルで前処理する
            source.filterMode = FilterMode.Point;
            Graphics.Blit(source, rt1, _material, PrefilterPass);
            rt1.filterMode = FilterMode.Bilinear;

            if (useHexBokeh)
            {
                // 縦方向と斜め方向のぼかしを個別に作ってから合成する
                Graphics.Blit(rt1, rt2, _material, HexVerticalPass);
                _material.SetTexture("_verticalBlurTexture", rt2);
                Graphics.Blit(rt1, rt2, _material, HexDiagonalPass);
                _material.SetTexture("_diagonalBlurTexture", rt2);
            }
            else
            {
                Graphics.Blit(rt1, rt2, _material, BlurPassBase + (int)kernelSize);
            }
            Swap(ref rt1, ref rt2);

            rt2.filterMode = FilterMode.Bilinear;
            Graphics.Blit(rt1, rt2, _material, PostBlurPass);
            Swap(ref rt1, ref rt2);
            Graphics.Blit(rt1, rt2, _material, PostBlurPass);

            _material.SetTexture("_BlurTex", rt2);
            Graphics.Blit(source, destination, _material, CompositePass);

            RenderTexture.ReleaseTemporary(rt1);
            RenderTexture.ReleaseTemporary(rt2);
        }

        private void SetUpShaderParameters(RenderTexture source)
        {
            var focalLengthValue = CalculateFocalLength();
            var distance = Mathf.Max(CalculateFocusDistance(), focalLengthValue);
            var maxCoC = CalculateMaxCoCRadius(source.height);

            _material.SetFloat("_Distance", distance);
            _material.SetFloat("_Range", focalRange);
            _material.SetFloat("_Angle", angle);
            _material.SetFloat("_viewWidth", source.width);
            _material.SetFloat("_viewHeight", source.height);
            // 焦点距離と F 値からレンズの錯乱円係数を求める (0.024 = 35mm フィルムの縦幅)。
            // ピント位置が焦点距離と一致すると 0 除算になるため下限を入れている (移植元は未対策)
            var focusOffset = Mathf.Max(distance - focalLengthValue, 1E-04f);
            _material.SetFloat("_LensCoeff",
                focalLengthValue * focalLengthValue / (fNumber * focusOffset * 0.024f * 2f));
            _material.SetFloat("_MaxCoC", maxCoC);
            _material.SetFloat("_RcpMaxCoC", 1f / maxCoC);
            _material.SetFloat("_RcpAspect", (float)source.height / source.width);
        }

        private float CalculateFocusDistance()
        {
            if (pointOfFocus == null)
            {
                return Mathf.Max(focusDistance, 0.001f);
            }

            var cameraTransform = targetCamera.transform;
            return Vector3.Dot(pointOfFocus.position - cameraTransform.position, cameraTransform.forward);
        }

        private float CalculateFocalLength()
        {
            if (!useCameraFov)
            {
                return focalLength;
            }
            return 0.012f / Mathf.Tan(0.5f * targetCamera.fieldOfView * Mathf.Deg2Rad);
        }

        // ぼけ半径の上限。解像度が上がるほど画素数ベースの半径も広げる
        private float CalculateMaxCoCRadius(int screenHeight)
        {
            var radius = (int)kernelSize * 4f + radiusBasePixel;
            radius *= Mathf.Max(screenHeight / 1080, 1f);
            return Mathf.Min(0.05f, Mathf.Max(radius / screenHeight, 0.0001f));
        }

        private static void Swap(ref RenderTexture a, ref RenderTexture b)
        {
            var temp = a;
            a = b;
            b = temp;
        }
    }
}
