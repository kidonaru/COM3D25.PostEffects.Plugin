using System.Xml.Serialization;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// 深度・法線・輝度から輪郭線を抽出する。ゲーム側にも同名の EdgeDetectEffectNormals が居るが、
    /// そちらには線の色・濃さのフィールドが無い (imageeffects バンドルのシェーダーは
    /// _EdgeColor / _EdgePower を持つ) ため、SceneCapture 同梱実装の方を移植している
    /// </summary>
    public class EdgeDetectEffect : CharacterMaskableEffect
    {
        // XmlSerializer は入れ子型も外側の型名を含まない XML 型名で扱うため、一意な名前を明示する
        [XmlType("EdgeDetectMode")]
        public enum EdgeDetectMode
        {
            TriangleDepthNormals,
            RobertsCrossDepthNormals,
            SobelDepth,
            SobelDepthThin,
            TriangleLuminance,
            SobelColor,
            SobelColorThin,
        }

        public Shader shader;

        public EdgeDetectMode mode = EdgeDetectMode.SobelDepthThin;
        public float sensitivityDepth = 1f;
        public float sensitivityNormals = 1f;
        public float lumThreshhold = 0.2f;
        public float edgeExp = 1f;
        public float sampleDist = 1f;
        public float edgesOnly = 0f;
        public float edgePower = 0.5f;
        public Color edgesOnlyBgColor = Color.white;
        public Color edgeColor = Color.black;

        private Material _material;

        private void OnEnable()
        {
            SetCameraFlag();
        }

        private void OnDisable()
        {
            if (_material != null)
            {
                DestroyImmediate(_material);
                _material = null;
            }
        }

        // モードによって必要な深度テクスチャが変わる。設定変更に追従するよう描画時にも呼び直す
        private void SetCameraFlag()
        {
            var camera = GetComponent<Camera>();
            if (camera == null)
            {
                return;
            }

            if (mode == EdgeDetectMode.SobelDepth || mode == EdgeDetectMode.SobelDepthThin)
            {
                camera.depthTextureMode |= DepthTextureMode.Depth;
            }
            else if (mode == EdgeDetectMode.TriangleDepthNormals || mode == EdgeDetectMode.RobertsCrossDepthNormals)
            {
                camera.depthTextureMode |= DepthTextureMode.DepthNormals;
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

            SetCameraFlag();

            // シェーダーは (深度感度, 法線感度, 1, 法線感度) の並びで受け取る
            _material.SetVector("_Sensitivity",
                new Vector4(sensitivityDepth, sensitivityNormals, 1f, sensitivityNormals));
            _material.SetFloat("_BgFade", edgesOnly);
            _material.SetFloat("_SampleDistance", sampleDist);
            _material.SetVector("_BgColor", edgesOnlyBgColor);
            _material.SetVector("_EdgeColor", edgeColor);
            _material.SetFloat("_EdgePower", edgePower);
            _material.SetFloat("_Exponent", edgeExp);
            _material.SetFloat("_Threshold", lumThreshhold);

            Graphics.Blit(source, destination, _material, (int)mode);
        }
    }
}
