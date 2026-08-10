using System.Xml.Serialization;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// 深度に沿って等高線を描く (Kino/Isoline)。2.5 のゲームアセンブリに型が存在しないため、
    /// SceneCapture 同梱実装を自己完結な形で移植したもの (シェーダーは kino バンドル)
    /// </summary>
    public class IsolineEffect : CharacterMaskableEffect
    {
        // XmlSerializer は入れ子型も外側の型名を含まない XML 型名で扱うため、一意な名前を明示する
        [XmlType("IsolineModulationMode")]
        public enum ModulationMode
        {
            None,
            Frac,
            Sin,
            Noise,
        }

        public Shader shader;

        public Color lineColor = Color.white;
        public float luminanceBlending = 1f;
        public float fallOffDepth = 40f;
        public Color backgroundColor = Color.black;
        public Vector3 axis = Vector3.one * 0.577f;
        public float interval = 0.25f;
        public Vector3 offset = Vector3.zero;
        public float distortionFrequency = 1f;
        public float distortionAmount = 0f;
        public ModulationMode modulationMode = ModulationMode.None;
        public Vector3 modulationAxis = Vector3.forward;
        public float modulationFrequency = 0.2f;
        public float modulationSpeed = 1f;
        public float modulationExponent = 24f;
        // 等高線を流す向きと速さ (offset を毎フレーム動かす)
        public Vector3 direction = Vector3.one * 0.577f;
        public float speed = 0.2f;

        private Material _material;
        private float _modulationTime;

        private void OnEnable()
        {
            GetComponent<Camera>().depthTextureMode |= DepthTextureMode.Depth;
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
            _modulationTime += Time.deltaTime * modulationSpeed;
            offset += direction.normalized * speed * Time.deltaTime;
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

            _material.SetMatrix("_InverseView", GetComponent<Camera>().cameraToWorldMatrix);
            _material.SetColor("_Color", lineColor);
            _material.SetFloat("_FallOffDepth", fallOffDepth);
            _material.SetFloat("_Blend", luminanceBlending);
            _material.SetColor("_BgColor", backgroundColor);
            _material.SetVector("_Axis", axis.normalized);
            // シェーダーは線の密度で受け取るので間隔の逆数を渡す
            _material.SetFloat("_Density", 1f / interval);
            _material.SetVector("_Offset", offset);
            _material.SetFloat("_DistFreq", distortionFrequency);
            _material.SetFloat("_DistAmp", distortionAmount);

            SetKeyword("DISTORTION", distortionAmount > 0f);
            SetKeyword("MODULATION_FRAC", modulationMode == ModulationMode.Frac);
            SetKeyword("MODULATION_SIN", modulationMode == ModulationMode.Sin);
            SetKeyword("MODULATION_NOISE", modulationMode == ModulationMode.Noise);

            // Sin だけは 1 周期を 2π として扱う
            var modFrequency = modulationMode == ModulationMode.Sin
                ? modulationFrequency * 2f * Mathf.PI
                : modulationFrequency;
            _material.SetVector("_ModAxis", modulationAxis.normalized);
            _material.SetFloat("_ModFreq", modFrequency);
            _material.SetFloat("_ModTime", _modulationTime);
            _material.SetFloat("_ModExp", modulationExponent);

            Graphics.Blit(source, destination, _material);
        }

        private void SetKeyword(string keyword, bool enabled)
        {
            if (enabled)
            {
                _material.EnableKeyword(keyword);
            }
            else
            {
                _material.DisableKeyword(keyword);
            }
        }
    }
}
