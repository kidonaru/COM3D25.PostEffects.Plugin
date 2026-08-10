using System.Xml.Serialization;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// 距離フォグと高さフォグを、色のグラデーション (またはランプテクスチャ) 付きで掛ける後処理。
    /// 2.5 のゲームアセンブリに型が存在しないため、SceneCapture 同梱実装を自己完結な形で
    /// 移植したもの (シェーダーは cinematic バンドル)
    /// </summary>
    public class StylisticFogEffect : MonoBehaviour
    {
        // XmlSerializer は入れ子型も外側の型名を含まない XML 型名で扱うため明示的に名前を付ける
        [XmlType("StylisticFogColorSource")]
        public enum ColorSource
        {
            // 開始色→終了色のグラデーションを焼いて使う
            Gradient,
            // もう一方のフォグの色設定を流用する
            CopyOther,
            // 指定したランプテクスチャをそのまま使う
            TextureRamp,
        }

        // シェーダーのパス番号と 1:1 で対応する
        private enum FogPass
        {
            DistanceOnly = 0,
            HeightOnly = 1,
            BothSharedColor = 2,
            BothSeparateColor = 3,
            None = -1,
        }

        private const int DistanceRampWidth = 1024;
        private const int HeightRampWidth = 256;

        public Shader shader;

        public bool distanceFogEnabled = true;
        public bool distanceFogSkybox = false;
        public float distanceFogEndDistance = 100f;
        public ColorSource distanceColorSource = ColorSource.Gradient;
        public Color distanceFirstColor = new Color(1f, 1f, 1f, 0f);
        public Color distanceLastColor = new Color(1f, 1f, 1f, 1f);
        public Texture2D distanceColorRamp;

        public bool heightFogEnabled = false;
        public bool heightFogSkybox = true;
        public float heightFogBaseHeight = 0f;
        public float heightFogBaseDensity = 0.1f;
        public float heightFogDensityFalloff = 0.5f;
        public ColorSource heightColorSource = ColorSource.CopyOther;
        public Color heightFirstColor = new Color(1f, 1f, 1f, 0f);
        public Color heightLastColor = new Color(1f, 1f, 1f, 1f);
        public Texture2D heightColorRamp;

        private Camera _camera;
        private Material _material;
        private Texture2D _distanceGradientTexture;
        private Texture2D _heightGradientTexture;
        // 焼き済みグラデーションの元になった色。変化したときだけ焼き直す
        private Color _bakedDistanceFirst, _bakedDistanceLast;
        private Color _bakedHeightFirst, _bakedHeightLast;

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
            DestroyBaked(ref _distanceGradientTexture);
            DestroyBaked(ref _heightGradientTexture);
        }

        private static void DestroyBaked(ref Texture2D texture)
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
                _material = new Material(shader);
                _material.hideFlags = HideFlags.DontSave;
            }

            // 深度テクスチャの要求は設定変更やカメラ差し替えに追従するよう描画時にも呼び直す
            targetCamera.depthTextureMode |= DepthTextureMode.Depth;

            var pass = SetMaterialUniforms();
            if (pass == FogPass.None)
            {
                Graphics.Blit(source, destination);
                return;
            }

            Graphics.Blit(source, destination, _material, (int)pass);
        }

        private FogPass SetMaterialUniforms()
        {
            if (!distanceFogEnabled && !heightFogEnabled)
            {
                return FogPass.None;
            }

            // 両方が CopyOther だと参照先が無くなるので距離側をグラデーションに倒す
            var distanceSource = distanceColorSource;
            var heightSource = heightColorSource;
            if (distanceSource == ColorSource.CopyOther && heightSource == ColorSource.CopyOther)
            {
                distanceSource = ColorSource.Gradient;
            }

            var shareColor = distanceSource == ColorSource.CopyOther || heightSource == ColorSource.CopyOther;
            FogPass pass;
            if (distanceFogEnabled && heightFogEnabled)
            {
                pass = shareColor ? FogPass.BothSharedColor : FogPass.BothSeparateColor;
            }
            else
            {
                pass = distanceFogEnabled ? FogPass.DistanceOnly : FogPass.HeightOnly;
            }

            _material.SetMatrix("_InverseViewMatrix", targetCamera.cameraToWorldMatrix);
            _material.SetInt("_ApplyDistToSkybox", distanceFogSkybox ? 1 : 0);
            _material.SetInt("_ApplyHeightToSkybox", heightFogSkybox ? 1 : 0);
            _material.SetFloat("_FogEndDistance", distanceFogEndDistance);
            _material.SetFloat("_Height", heightFogBaseHeight);
            _material.SetFloat("_BaseDensity", heightFogBaseDensity);
            _material.SetFloat("_DensityFalloff", heightFogDensityFalloff);

            if (shareColor)
            {
                // 片方が CopyOther なので、CopyOther でない側の色設定を両方に使う
                var useDistance = distanceSource != ColorSource.CopyOther;
                _material.SetTexture("_FogColorTexture0",
                    useDistance ? GetDistanceColorTexture(distanceSource) : GetHeightColorTexture(heightSource));
                return pass;
            }

            if (distanceFogEnabled)
            {
                _material.SetTexture("_FogColorTexture0", GetDistanceColorTexture(distanceSource));
            }
            if (heightFogEnabled)
            {
                // 高さフォグ単体のときは 0 番、距離フォグと併用なら 1 番のスロットを使う
                _material.SetTexture(pass == FogPass.HeightOnly ? "_FogColorTexture0" : "_FogColorTexture1",
                    GetHeightColorTexture(heightSource));
            }
            return pass;
        }

        private Texture GetDistanceColorTexture(ColorSource source)
        {
            if (source == ColorSource.TextureRamp)
            {
                return distanceColorRamp;
            }
            return BakeGradient(ref _distanceGradientTexture, DistanceRampWidth,
                distanceFirstColor, distanceLastColor, ref _bakedDistanceFirst, ref _bakedDistanceLast);
        }

        private Texture GetHeightColorTexture(ColorSource source)
        {
            if (source == ColorSource.TextureRamp)
            {
                return heightColorRamp;
            }
            return BakeGradient(ref _heightGradientTexture, HeightRampWidth,
                heightFirstColor, heightLastColor, ref _bakedHeightFirst, ref _bakedHeightLast);
        }

        // 開始色→終了色の線形グラデーションを 1 ラインのテクスチャに焼く
        private static Texture2D BakeGradient(
            ref Texture2D texture, int width, Color first, Color last, ref Color bakedFirst, ref Color bakedLast)
        {
            if (texture != null && bakedFirst == first && bakedLast == last)
            {
                return texture;
            }

            if (texture == null)
            {
                texture = new Texture2D(width, 1, TextureFormat.ARGB32, false, false)
                {
                    name = "StylisticFog Ramp",
                    hideFlags = HideFlags.DontSave,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    anisoLevel = 0,
                };
            }

            var colors = new Color[width];
            for (var i = 0; i < width; i++)
            {
                colors[i] = Color.Lerp(first, last, i / (width - 1f));
            }
            texture.SetPixels(colors);
            texture.Apply();

            bakedFirst = first;
            bakedLast = last;
            return texture;
        }
    }
}
