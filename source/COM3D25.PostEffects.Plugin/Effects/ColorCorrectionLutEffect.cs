using COM3D2.MotionTimelineEditor;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// 3D ルックアップテーブルによる色補正 (Color Correction (3D Lookup Texture))。
    /// 2.5 のゲームアセンブリに型が存在しないため、SceneCapture 同梱実装を自己完結な形で
    /// 移植したもの (シェーダーは imageeffects バンドル)。
    /// LUT は横一列に並んだ 2D ストリップ (幅 = 高さ^2、例: 256x16) を Texture3D へ組み直して使う
    /// </summary>
    public class ColorCorrectionLutEffect : MonoBehaviour
    {
        // LUT 未指定時に生成する無変換テーブルの一辺
        private const int IdentityLutSize = 16;

        public Shader shader;
        public float contribution = 1f;
        public Texture2D lutTexture;

        private Material _material;
        private Texture3D _converted;
        // _converted の生成元。差し替え検知に使う (無変換テーブル時は null)
        private Texture2D _convertedFrom;

        private void OnDisable()
        {
            if (_material != null)
            {
                DestroyImmediate(_material);
                _material = null;
            }
            DestroyConverted();
            _convertedFrom = null;
        }

        private void DestroyConverted()
        {
            if (_converted != null)
            {
                DestroyImmediate(_converted);
                _converted = null;
            }
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (shader == null || !shader.isSupported || !SystemInfo.supports3DTextures)
            {
                Graphics.Blit(source, destination);
                return;
            }

            if (_material == null)
            {
                _material = new Material(shader);
                _material.hideFlags = HideFlags.DontSave;
            }

            // テクスチャが差し替わったとき、および未生成のときだけ 3D LUT を組み直す
            if (_converted == null || _convertedFrom != lutTexture)
            {
                UpdateConverted();
            }

            var size = _converted.width;
            _converted.wrapMode = TextureWrapMode.Clamp;
            // テクセル中心をサンプルするためのスケールとオフセット
            _material.SetFloat("_Scale", (size - 1f) / size);
            _material.SetFloat("_Offset", 1f / (2f * size));
            _material.SetFloat("_Intensity", contribution);
            _material.SetTexture("_ClutTex", _converted);

            Graphics.Blit(source, destination, _material,
                QualitySettings.activeColorSpace == ColorSpace.Linear ? 1 : 0);
        }

        private void UpdateConverted()
        {
            var source = lutTexture;
            // 生成元を記録してから組み立てる。失敗して無変換テーブルに落ちた場合も
            // 同じテクスチャで毎フレーム再試行・ログ出力しないようにする
            _convertedFrom = source;

            if (source == null)
            {
                SetIdentityLut();
                return;
            }

            var size = source.height;
            if (size <= 0 || size != Mathf.FloorToInt(Mathf.Sqrt(source.width)))
            {
                MTEUtils.LogError(
                    "LUT テクスチャのサイズが不正です (幅 = 高さの 2 乗である必要があります): {0}x{1}",
                    source.width, source.height);
                SetIdentityLut();
                return;
            }

            Color[] pixels;
            try
            {
                pixels = source.GetPixels();
            }
            catch (UnityException e)
            {
                MTEUtils.LogException(e);
                MTEUtils.LogError("LUT テクスチャの画素を読み取れませんでした");
                SetIdentityLut();
                return;
            }

            var colors = new Color[size * size * size];
            for (var r = 0; r < size; r++)
            {
                for (var g = 0; g < size; g++)
                {
                    for (var b = 0; b < size; b++)
                    {
                        // ストリップは上下反転で並んでいるので緑成分の行を裏返して読む
                        var row = size - g - 1;
                        colors[r + g * size + b * size * size] = pixels[b * size + r + row * size * size];
                    }
                }
            }

            DestroyConverted();
            _converted = CreateLut(size, colors);
        }

        private void SetIdentityLut()
        {
            var size = IdentityLutSize;
            var colors = new Color[size * size * size];
            var scale = 1f / (size - 1f);
            for (var r = 0; r < size; r++)
            {
                for (var g = 0; g < size; g++)
                {
                    for (var b = 0; b < size; b++)
                    {
                        colors[r + g * size + b * size * size] = new Color(r * scale, g * scale, b * scale, 1f);
                    }
                }
            }

            DestroyConverted();
            _converted = CreateLut(size, colors);
        }

        private static Texture3D CreateLut(int size, Color[] colors)
        {
            var lut = new Texture3D(size, size, size, TextureFormat.ARGB32, false);
            lut.hideFlags = HideFlags.DontSave;
            lut.SetPixels(colors);
            lut.Apply();
            return lut;
        }
    }
}
