using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace COM3D25.PostEffects.Plugin
{
    // 元の OUTLINE パスを同じ深度へ再描画し、アルファに残る線の被覆率を使う。
    // 輪郭の頂点処理・テクスチャ・線幅は元の材質に任せ、既存材質は変更しない。
    [RequireComponent(typeof(Camera))]
    public class OutlineColorEffect : MonoBehaviour
    {
        public Shader shader;
        public float intensity = 0.6f;
        public float sampleRadius = 2f;
        public float saturation = 1f;
        public Color tint = Color.white;
        public float tintStrength = 0f;
        public bool showMask = false;

        private const CameraEvent MaskEvent = CameraEvent.AfterForwardOpaque;
        // 着替え・モデル追加に追従しつつ、シーン全体の検索を毎フレーム行わない。
        private const float RendererRefreshInterval = 0.5f;
        private Camera _camera;
        private CommandBuffer _commands;
        private RenderTexture _mask;
        private Material _material;
        private Renderer[] _renderers;
        private float _nextRendererRefresh;
        private int _preparedFrame = -1;
        private int _characterLayer;
        private int _drawCount;
        private bool _reportedFailure;

        private static readonly int MaskTex = Shader.PropertyToID("_MaskTex");
        private static readonly int Intensity = Shader.PropertyToID("_Intensity");
        private static readonly int SampleRadius = Shader.PropertyToID("_SampleRadius");
        private static readonly int Saturation = Shader.PropertyToID("_Saturation");
        private static readonly int Tint = Shader.PropertyToID("_Tint");
        private static readonly int TintStrength = Shader.PropertyToID("_TintStrength");
        private static readonly int OutlineWidth = Shader.PropertyToID("_OutlineWidth");

        private void OnEnable()
        {
            _camera = GetComponent<Camera>();
            _characterLayer = LayerMask.NameToLayer("Charactor");
            _renderers = null;
            _reportedFailure = false;
        }

        private int CameraSamples()
        {
            if (_camera.targetTexture != null)
                return _camera.targetTexture.antiAliasing;
            return _camera.allowMSAA && _camera.actualRenderingPath == RenderingPath.Forward
                ? Mathf.Max(1, QualitySettings.antiAliasing) : 1;
        }

        private void OnPreCull()
        {
            _preparedFrame = -1;
            if (_commands != null) _commands.Clear();
            // ステレオの左右別深度には未対応。古いマスクを使わず素通しにする。
            if (_camera == null || _camera.stereoEnabled || _characterLayer < 0 ||
                shader == null || !shader.isSupported || (!showMask && intensity <= 0f))
                return;

            try
            {
                if (!PrepareTargets()) return;
                if (_renderers == null || Time.unscaledTime >= _nextRendererRefresh)
                {
                    _renderers = FindObjectsOfType<Renderer>();
                    _nextRendererRefresh = Time.unscaledTime + RendererRefreshInterval;
                }

                _commands.SetRenderTarget(new RenderTargetIdentifier(_mask),
                    new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget));
                // 共有するカメラ深度を消してはいけない。輪郭色だけを透明に初期化する。
                _commands.ClearRenderTarget(false, true, Color.clear);
                _drawCount = 0;
                foreach (var renderer in _renderers)
                {
                    if (renderer == null || !renderer.enabled || IsRenderingOff(renderer) ||
                        renderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly ||
                        !renderer.gameObject.activeInHierarchy ||
                        renderer.gameObject.layer != _characterLayer ||
                        (_camera.cullingMask & (1 << renderer.gameObject.layer)) == 0)
                        continue;
                    // sharedMaterials は読むだけ。着替えや材質交換は毎回反映する。
                    var materials = renderer.sharedMaterials;
                    for (var submesh = 0; submesh < materials.Length; submesh++)
                    {
                        var material = materials[submesh];
                        if (!SupportsOutline(material)) continue;
                        var pass = material.FindPass("OUTLINE");
                        if (pass < 0) continue;
                        _commands.DrawRenderer(renderer, material, submesh, pass);
                        _drawCount++;
                    }
                }
                _commands.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
                _preparedFrame = Time.frameCount;
            }
            catch (Exception exception)
            {
                if (_commands != null) _commands.Clear();
                if (!_reportedFailure)
                {
                    Debug.LogError("輪郭の色にじみ: マスクの準備に失敗しました。" + exception);
                    _reportedFailure = true;
                }
            }
        }

        // Renderer.forceRenderingOff は Unity 2019.3 以降。COM3D2 (2.0) の Unity 5.6 には
        // プロパティも設定側の API も無いため、常に描画対象として扱う
        private static bool IsRenderingOff(Renderer renderer)
        {
#if COM3D25
            return renderer.forceRenderingOff;
#else
            return false;
#endif
        }

        private static bool SupportsOutline(Material material)
        {
            // 任意のシェーダーのアルファは被覆率とは限らないため、確認済みの標準系に限定する。
            return material != null && material.shader != null && material.shader.isSupported &&
                material.renderQueue <= (int)RenderQueue.GeometryLast &&
                material.shader.name.StartsWith("CM3D2/Toony_Lighted", StringComparison.Ordinal) &&
                material.HasProperty(OutlineWidth) && material.GetFloat(OutlineWidth) > 0f;
        }

        private bool PrepareTargets()
        {
            var width = _camera.pixelWidth;
            var height = _camera.pixelHeight;
            if (width <= 0 || height <= 0) return false;
            var samples = CameraSamples();
            if (_mask != null && (_mask.width != width || _mask.height != height ||
                _mask.antiAliasing != samples || !_mask.IsCreated()))
                ReleaseMask();
            if (_mask == null)
            {
                _mask = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.Linear)
                {
                    name = "PostEffectsOutlineMask",
                    hideFlags = HideFlags.DontSave,
                    antiAliasing = samples,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };
                if (!_mask.Create())
                {
                    ReleaseMask();
                    return false;
                }
            }
            if (_commands == null)
            {
                _commands = new CommandBuffer { name = "輪郭の色にじみ・輪郭マスク" };
                _camera.AddCommandBuffer(MaskEvent, _commands);
            }
            return true;
        }

        // マスクと同じ不透明描画段階で合成し、その後の半透明物は通常どおり上に描く。
        [ImageEffectOpaque]
        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (_preparedFrame != Time.frameCount || _mask == null || shader == null ||
                !shader.isSupported || (!showMask && (_drawCount == 0 || intensity <= 0f)))
            {
                Graphics.Blit(source, destination);
                return;
            }
            if (_material != null && _material.shader != shader)
            {
                DestroyImmediate(_material);
                _material = null;
            }
            if (_material == null)
                _material = new Material(shader) { hideFlags = HideFlags.DontSave };
            _material.SetTexture(MaskTex, _mask);
            _material.SetFloat(Intensity, Mathf.Clamp01(intensity));
            _material.SetFloat(SampleRadius, Mathf.Max(0f, sampleRadius));
            _material.SetFloat(Saturation, Mathf.Max(0f, saturation));
            _material.SetColor(Tint, tint);
            _material.SetFloat(TintStrength, Mathf.Clamp01(tintStrength));
            Graphics.Blit(source, destination, _material, showMask ? 1 : 0);
            _material.SetTexture(MaskTex, null);
        }

        private void ReleaseMask()
        {
            if (_mask == null) return;
            _mask.Release();
            DestroyImmediate(_mask);
            _mask = null;
        }

        private void OnDisable()
        {
            _preparedFrame = -1;
            if (_commands != null)
            {
                if (_camera != null) _camera.RemoveCommandBuffer(MaskEvent, _commands);
                _commands.Release();
                _commands = null;
            }
            ReleaseMask();
            if (_material != null) DestroyImmediate(_material);
            _material = null;
            _renderers = null;
        }
    }
}
