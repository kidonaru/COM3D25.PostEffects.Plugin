using System.Xml.Serialization;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// 物理カメラ風の被写界深度 (Kino/Bokeh)。2.5 のゲームアセンブリに型が存在しないため、
    /// SceneCapture 同梱実装を自己完結な形で移植したもの (シェーダーは kino バンドル)
    /// </summary>
    public class BokehEffect : MonoBehaviour
    {
        // XmlSerializer は入れ子型も外側の型名を含まない XML 型名で扱うため、一意な名前を明示する
        [XmlType("BokehKernelSize")]
        public enum KernelSize
        {
            Small,
            Medium,
            Large,
            VeryLarge,
        }

        // 35mm フィルムの想定高さ (m)。錯乱円の計算に使う
        private const float FilmHeight = 0.024f;

        public Shader shader;

        // 指定するとこの Transform までの距離にピントが合う (未指定なら focusDistance を使う)
        public Transform pointOfFocus;
        public float focusDistance = 10f;
        public float fNumber = 1.4f;
        public bool useCameraFov = true;
        public float focalLength = 0.05f;
        public float focalRange = 1f;
        public KernelSize kernelSize = KernelSize.Medium;
        public bool useARGBHalf = true;
        public int radiusBasePixel = 6;
        // ピント位置を可視化するデバッグ表示
        public bool visualize = false;

        private Material _material;

        private Camera targetCamera => GetComponent<Camera>();

        private void OnEnable()
        {
            targetCamera.depthTextureMode |= DepthTextureMode.Depth;
        }

        private void OnDisable()
        {
            if (_material != null)
            {
                DestroyImmediate(_material);
                _material = null;
            }
        }

        private void Update()
        {
            // 0 除算やピント距離の反転を避けるための下限
            focusDistance = Mathf.Max(focusDistance, 0.01f);
            fNumber = Mathf.Max(fNumber, 0.01f);
        }

        private float CalculateFocusDistance()
        {
            if (pointOfFocus == null)
            {
                return focusDistance;
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

            var fovRadian = targetCamera.fieldOfView * Mathf.Deg2Rad;
            return 0.5f * FilmHeight / Mathf.Tan(0.5f * fovRadian);
        }

        // 錯乱円の最大半径 (画面高に対する割合)
        private float CalculateMaxCoCRadius(int screenHeight)
        {
            var pixels = (float)kernelSize * 4f + radiusBasePixel;
            pixels *= Mathf.Max(screenHeight / 1080, 1f);
            return Mathf.Min(0.05f, Mathf.Max(pixels / screenHeight, 0.0001f));
        }

        private void SetUpShaderParameters(RenderTexture source)
        {
            var distance = CalculateFocusDistance();
            var focal = CalculateFocalLength();
            distance = Mathf.Max(distance, focal);

            _material.SetFloat("_Distance", distance);
            _material.SetFloat("_Range", focalRange);
            _material.SetFloat("_LensCoeff", focal * focal / (fNumber * (distance - focal) * FilmHeight * 2f));

            var maxCoC = CalculateMaxCoCRadius(source.height);
            _material.SetFloat("_MaxCoC", maxCoC);
            _material.SetFloat("_RcpMaxCoC", 1f / maxCoC);
            _material.SetFloat("_RcpAspect", (float)source.height / source.width);
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

            SetUpShaderParameters(source);

            if (visualize)
            {
                Graphics.Blit(source, destination, _material, 7);
                return;
            }

            var format = useARGBHalf ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.Default;
            var halfWidth = source.width / 2;
            var halfHeight = source.height / 2;

            var rt1 = RenderTexture.GetTemporary(halfWidth, halfHeight, 0, format);
            var rt2 = RenderTexture.GetTemporary(halfWidth, halfHeight, 0, format);

            // 半解像度で錯乱円を作り、カーネルぼかし → 2 回の後処理で滑らかにしてから合成する
            source.filterMode = FilterMode.Point;
            Graphics.Blit(source, rt1, _material, 0);
            rt1.filterMode = FilterMode.Bilinear;
            Graphics.Blit(rt1, rt2, _material, 1 + (int)kernelSize);
            rt2.filterMode = FilterMode.Bilinear;
            Graphics.Blit(rt2, rt1, _material, 5);
            rt1.filterMode = FilterMode.Bilinear;
            Graphics.Blit(rt1, rt2, _material, 5);

            _material.SetTexture("_BlurTex", rt2);
            Graphics.Blit(source, destination, _material, 6);

            RenderTexture.ReleaseTemporary(rt1);
            RenderTexture.ReleaseTemporary(rt2);
        }
    }
}
