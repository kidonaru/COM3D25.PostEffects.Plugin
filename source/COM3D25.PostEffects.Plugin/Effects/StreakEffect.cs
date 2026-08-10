using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// 明部を横 (または縦) に伸ばす光条エフェクト (Kino/Streak)。2.5 のゲームアセンブリに型が存在しないため、
    /// SceneCapture 同梱実装を自己完結な形で移植したもの (シェーダーは kino バンドル)。
    /// 移植元にあったメイドマスク (EffectMask 連携) は EffectMask 自体が未移植のため省いている
    /// </summary>
    public class StreakEffect : MonoBehaviour
    {
        // XmlSerializer は入れ子型も外側の型名を含まない XML 型名で扱うため、
        // 他エフェクトの BlendMode と衝突しないよう明示的に名前を付ける (衝突すると Config 保存が丸ごと失敗する)
        [XmlType("StreakBlendMode")]
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

        [Range(0f, 5f)]
        public float threshold = 1f;
        public float softKnee = 0.5f;
        [Range(0f, 1f)]
        public float stretch = 0.75f;
        [Range(0f, 1f)]
        public float intensity = 0.3f;
        public bool streakVertical = false;
        public bool streak2Way = false;
        public Color tint = new Color(0.55f, 0.55f, 0.55f);
        public BlendMode blendMode = BlendMode.Screen;

        private Material _material;
        private readonly Stack<RenderTexture> _mipStack = new Stack<RenderTexture>();
        private readonly Stack<RenderTexture> _mipVStack = new Stack<RenderTexture>();
        private readonly Stack<RenderTexture> _mipHStack = new Stack<RenderTexture>();

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
            // 縮小段が 1 段も作れない極小解像度では中間バッファの取り回しが破綻するため素通しする
            if (shader == null || !shader.isSupported || StreakBlur.IsTooSmall(source))
            {
                Graphics.Blit(source, destination);
                return;
            }

            if (_material == null)
            {
                _material = new Material(shader);
                _material.hideFlags = HideFlags.DontSave;
            }

            _material.SetFloat("_Threshold", threshold);
            _material.SetFloat("_Stretch", stretch);
            _material.SetFloat("_Intensity", intensity);
            _material.SetColor("_Color", tint);

            // しきい値付近をなだらかにするためのカーブ (knee が 0 にならないよう微小値を足す)
            var knee = threshold * softKnee + 1E-05f;
            _material.SetVector("_Curve", new Vector3(threshold - knee, knee * 2f, 0.25f / knee));

            _material.shaderKeywords = null;
            _material.EnableKeyword(BlendModeKeywords[(int)blendMode]);

            if (streak2Way)
            {
                StreakBlur.RenderTwoWay(_material, source, source, destination, _mipVStack, _mipHStack);
            }
            else
            {
                StreakBlur.RenderOneWay(_material, source, source, destination, _mipStack, streakVertical);
            }
        }
    }
}
