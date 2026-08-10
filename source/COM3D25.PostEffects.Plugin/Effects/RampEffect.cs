using System.Xml.Serialization;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// 画面全体にグラデーションを合成する (Kino/Ramp)。2.5 のゲームアセンブリに型が存在しないため、
    /// SceneCapture 同梱実装を自己完結な形で移植したもの (シェーダーは kino バンドル)
    /// </summary>
    public class RampEffect : CharacterMaskableEffect
    {
        // XmlSerializer は入れ子型も外側の型名を含まない XML 型名で扱うため、
        // 他エフェクトの BlendMode と衝突しないよう明示的に名前を付ける (衝突すると Config 保存が丸ごと失敗する)
        [XmlType("RampBlendMode")]
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

        public Shader shader;

        public Color color1 = Color.blue;
        public Color color2 = Color.red;
        [Range(-180f, 180f)]
        public float angle = 90f;
        [Range(0f, 1f)]
        public float opacity = 1f;
        public BlendMode blendMode = BlendMode.Overlay;

        private Material _material;

        private void OnDisable()
        {
            if (_material != null)
            {
                DestroyImmediate(_material);
                _material = null;
            }
        }

        protected override void RenderEffect(RenderTexture source, RenderTexture destination)
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

            // 不透明度は「そのブレンドで何もしない色」との補間で表現する
            Color neutral;
            switch (blendMode)
            {
                case BlendMode.Multiply:
                    neutral = Color.white;
                    break;
                case BlendMode.Screen:
                    neutral = Color.black;
                    break;
                default:
                    neutral = Color.gray;
                    break;
            }

            _material.SetColor("_Color1", Color.Lerp(neutral, color1, opacity));
            _material.SetColor("_Color2", Color.Lerp(neutral, color2, opacity));

            var radian = Mathf.Deg2Rad * angle;
            _material.SetVector("_Direction", new Vector2(Mathf.Cos(radian), Mathf.Sin(radian)));

            _material.shaderKeywords = null;
            _material.EnableKeyword(BlendModeKeywords[(int)blendMode]);
            if (QualitySettings.activeColorSpace == ColorSpace.Linear)
            {
                _material.EnableKeyword("_LINEAR");
            }

            Graphics.Blit(source, destination, _material, 0);
        }
    }
}
