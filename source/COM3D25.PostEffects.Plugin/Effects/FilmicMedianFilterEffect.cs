using System.Xml.Serialization;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// 近傍画素の中央値を取ってノイズだけを潰す (輪郭は残る)。
    /// 2.5 のゲームアセンブリに型が存在しないため、SceneCapture 同梱実装を移植したもの
    /// (シェーダーは filmic バンドルの filmicmedianfiltershader)
    /// </summary>
    public class FilmicMedianFilterEffect : MonoBehaviour
    {
        // XmlSerializer は入れ子型も外側の型名を含まない XML 型名で扱うため、一意な名前を明示する
        [XmlType("MedianFilterQuality")]
        public enum FilterQuality
        {
            // 水平・垂直の 2 パス (3 タップ)
            Normal,
            // 3x3 を 1 パス
            High,
        }

        public Shader shader;

        public FilterQuality quality = FilterQuality.High;

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

            if (quality == FilterQuality.High)
            {
                Graphics.Blit(source, destination, _material, 1);
                return;
            }

            var temp = RenderTexture.GetTemporary(
                source.width, source.height, 0, RenderTextureFormat.ARGBHalf);
            temp.filterMode = FilterMode.Bilinear;

            _material.SetVector("_Offsets", new Vector4(1f, 0f, 0f, 0f));
            Graphics.Blit(source, temp, _material, 0);
            _material.SetVector("_Offsets", new Vector4(0f, 1f, 0f, 0f));
            Graphics.Blit(temp, destination, _material, 0);

            RenderTexture.ReleaseTemporary(temp);
        }
    }
}
