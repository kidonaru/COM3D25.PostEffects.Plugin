using System.Xml.Serialization;
using UnityEngine;
using COM3D2.MotionTimelineEditor;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// スポット / 平行光源のボリュームライト (光の筋)。カメラではなくライト側に付き、
    /// 自前の深度シャドウマップとエピポーラ座標系のレイマーチで光の散乱を描く。
    /// 2.5 のゲームアセンブリに型が存在しないため、SceneCapture 同梱実装を自己完結な形で
    /// 移植したもの (シェーダーは lightshafts バンドル)。
    /// 移植元にあったサンプル点の可視化 (開発用デバッグ表示) は省いている
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class LightShaftsEffect : MonoBehaviour
    {
        // XmlSerializer は入れ子型も外側の型名を含まない XML 型名で扱うため明示的に名前を付ける
        [XmlType("LightShaftsResolution")]
        public enum Resolution
        {
            Low,
            Medium,
            High,
            VeryHigh,
        }

        [XmlType("LightShaftsShadowmapMode")]
        public enum ShadowmapMode
        {
            // 毎フレーム描き直す
            Dynamic,
            // 一度描いたものを使い回す
            Static,
        }

        public Shader depthShader;
        public Shader colorFilterShader;
        public Shader coordShader;
        public Shader depthBreaksShader;
        public Shader raymarchShader;
        public Shader interpolateAlongRaysShader;
        public Shader finalInterpolationShader;

        public Camera targetCamera;

        public ShadowmapMode shadowmapMode = ShadowmapMode.Dynamic;
        // 平行光源のときの光の箱の大きさ
        public Vector3 size = new Vector3(2f, 2f, 2f);
        // スポットのときの near / far を range に対する比率で指定する
        public float spotNear = 0.1f;
        public float spotFar = 1f;
        public LayerMask cullingMask = -1;
        public LayerMask colorFilterMask = 1 << 20;

        public float brightness = 5f;
        public float brightnessColored = 5f;
        public float extinction = 0.5f;
        public float minDistFromCamera = 0f;
        public bool colored = false;
        public float colorBalance = 1f;

        public Resolution shadowmapResolution = Resolution.VeryHigh;
        public Resolution epipolarSamplesResolution = Resolution.VeryHigh;
        public Resolution epipolarLinesResolution = Resolution.VeryHigh;
        public float depthThreshold = 0.001f;
        public int interpolationStep = 8;

        public bool attenuationCurveEnabled = false;
        public AnimationCurve attenuationCurve;

        private Light _light;
        private Camera _currentCamera;
        private Camera _shadowmapCamera;

        private Material _coordMaterial;
        private Material _depthBreaksMaterial;
        private Material _raymarchMaterial;
        private Material _interpolateAlongRaysMaterial;
        private Material _finalInterpolationMaterial;

        private RenderTexture _shadowmap;
        private RenderTexture _colorFilter;
        private RenderTexture _coordEpi;
        private RenderTexture _depthEpi;
        private RenderTexture _interpolationEpi;
        private RenderTexture _raymarchedLightEpi;
        private RenderTexture _interpolateAlongRaysEpi;

        private Texture2D _attenuationCurveTexture;
        private bool _attenuationCurveDirty = true;

        private Mesh _spotMesh;
        private float _spotMeshNear = -1f, _spotMeshFar = -1f, _spotMeshAngle = -1f, _spotMeshRange = -1f;

        // 毎フレームの描画で使い回す作業用バッファ (確保を繰り返さないためのもの)
        private readonly RenderBuffer[] _coordColorBuffers = new RenderBuffer[2];
        private static readonly Vector2[] ViewportCorners =
        {
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
        };

        private bool _dx11Support;
        private bool _minRequirements;
        private bool _requirementsChecked;
        private ShadowmapMode _shadowmapModeOld = ShadowmapMode.Dynamic;
        private bool _shadowmapDirty = true;

        private Light targetLight
        {
            get
            {
                if (_light == null)
                {
                    _light = GetComponent<Light>();
                }
                return _light;
            }
        }

        private bool directional => targetLight != null && targetLight.type == LightType.Directional;

        /// <summary>減衰カーブを差し替えたあとに呼び、次の描画で焼き直させる</summary>
        public void SetAttenuationCurveDirty()
        {
            _attenuationCurveDirty = true;
        }

        /// <summary>静的シャドウマップを描き直させる</summary>
        public void SetShadowmapDirty()
        {
            _shadowmapDirty = true;
        }

        private void OnEnable()
        {
            _requirementsChecked = false;
            _shadowmapDirty = true;
            if (targetCamera != null)
            {
                targetCamera.depthTextureMode |= DepthTextureMode.Depth;
            }
        }

        private void OnDisable()
        {
            DestroyMaterial(ref _coordMaterial);
            DestroyMaterial(ref _depthBreaksMaterial);
            DestroyMaterial(ref _raymarchMaterial);
            DestroyMaterial(ref _interpolateAlongRaysMaterial);
            DestroyMaterial(ref _finalInterpolationMaterial);

            ReleasePersistent(ref _shadowmap);
            ReleasePersistent(ref _colorFilter);

            if (_shadowmapCamera != null)
            {
                DestroyImmediate(_shadowmapCamera.gameObject);
                _shadowmapCamera = null;
            }
            if (_attenuationCurveTexture != null)
            {
                DestroyImmediate(_attenuationCurveTexture);
                _attenuationCurveTexture = null;
            }
            if (_spotMesh != null)
            {
                DestroyImmediate(_spotMesh);
                _spotMesh = null;
            }
            _attenuationCurveDirty = true;
        }

        private static void DestroyMaterial(ref Material material)
        {
            if (material != null)
            {
                DestroyImmediate(material);
                material = null;
            }
        }

        private static void ReleasePersistent(ref RenderTexture rt)
        {
            if (rt != null)
            {
                rt.Release();
                DestroyImmediate(rt);
                rt = null;
            }
        }

        public static int GetResolutionSize(Resolution resolution)
        {
            switch (resolution)
            {
                case Resolution.Low: return 256;
                case Resolution.Medium: return 512;
                case Resolution.High: return 1024;
                default: return 2048;
            }
        }

        // 光の筋はカメラの後処理ではなくシーン描画の最後に不透明として重ねる
        private void OnRenderObject()
        {
            _currentCamera = Camera.current;
            if (targetCamera == null || _currentCamera != targetCamera)
            {
                return;
            }
            if (!CheckMinRequirements() || !IsVisible())
            {
                return;
            }

            var activeColor = Graphics.activeColorBuffer;
            var activeDepth = Graphics.activeDepthBuffer;

            InitResources();

            var lightPos = GetLightViewportPos();
            SetKeyword(
                lightPos.x >= -1f && lightPos.x <= 1f && lightPos.y >= -1f && lightPos.y <= 1f,
                "LIGHT_ON_SCREEN", "LIGHT_OFF_SCREEN");
            // サンプル点の可視化は移植していないので常に無効
            SetKeyword(false, "SHOW_SAMPLES_ON", "SHOW_SAMPLES_OFF");
            SetKeyword(directional, "DIRECTIONAL_SHAFTS", "SPOT_SHAFTS");

            var width = Screen.width;
            var height = Screen.height;

            UpdateShadowmap();
            RenderCoords(width, height, lightPos);
            RenderInterpolationTexture(lightPos);
            Raymarch(width, height, lightPos);
            InterpolateAlongRays(lightPos);

            SetFrustumRays(_finalInterpolationMaterial);
            _finalInterpolationMaterial.SetTexture("_InterpolationEpi", _interpolationEpi);
            _finalInterpolationMaterial.SetTexture("_DepthEpi", _depthEpi);
            _finalInterpolationMaterial.SetTexture("_Shadowmap", _shadowmap);
            _finalInterpolationMaterial.SetTexture("_Coord", _coordEpi);
            _finalInterpolationMaterial.SetTexture("_RaymarchedLight", _interpolateAlongRaysEpi);
            _finalInterpolationMaterial.SetVector("_CoordTexDim", TexDim(_coordEpi));
            _finalInterpolationMaterial.SetVector("_ScreenTexDim", TexDim(width, height));
            _finalInterpolationMaterial.SetVector("_LightPos", lightPos);
            _finalInterpolationMaterial.SetFloat("_DepthThreshold", GetDepthThresholdAdjusted());

            // 光の箱がニアクリップに掛かっていると錐台メッシュが描けないので全画面クアッドへ切り替える
            var useQuad = directional || IntersectsNearPlane();
            _finalInterpolationMaterial.SetFloat("_ZTest", useQuad ? 8 : 2);
            SetKeyword(useQuad, "QUAD_SHAFTS", "FRUSTUM_SHAFTS");

            Graphics.SetRenderTarget(activeColor, activeDepth);
            _finalInterpolationMaterial.SetPass(0);
            if (useQuad)
            {
                RenderQuad();
            }
            else
            {
                Graphics.DrawMeshNow(_spotMesh, transform.position, transform.rotation);
            }

            ReleaseResources();
        }

        private bool CheckMinRequirements()
        {
            if (_requirementsChecked)
            {
                return _minRequirements;
            }
            _requirementsChecked = true;

            _dx11Support = SystemInfo.graphicsShaderLevel >= 50;
            _minRequirements =
                SystemInfo.graphicsShaderLevel >= 30 &&
                SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGFloat) &&
                SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RFloat) &&
                ShaderCompiles(depthShader) && ShaderCompiles(colorFilterShader) &&
                ShaderCompiles(coordShader) && ShaderCompiles(depthBreaksShader) &&
                ShaderCompiles(raymarchShader) && ShaderCompiles(interpolateAlongRaysShader) &&
                ShaderCompiles(finalInterpolationShader);

            if (!_minRequirements)
            {
                MTEUtils.LogError("光の筋: この環境では動作要件 (シェーダーモデル 3.0 と RGFloat/RFloat) を満たしていません");
            }
            return _minRequirements;
        }

        private static bool ShaderCompiles(Shader shader)
        {
            return shader != null && shader.isSupported;
        }

        private void InitResources()
        {
            InitMaterials();
            InitEpipolarTextures();
            BakeAttenuationCurve();
            InitSpotFrustumMesh();
        }

        private void ReleaseResources()
        {
            if (shadowmapMode != ShadowmapMode.Static)
            {
                RenderTexture.ReleaseTemporary(_shadowmap);
                if (colored)
                {
                    RenderTexture.ReleaseTemporary(_colorFilter);
                }
            }
            RenderTexture.ReleaseTemporary(_coordEpi);
            RenderTexture.ReleaseTemporary(_depthEpi);
            RenderTexture.ReleaseTemporary(_interpolationEpi);
            RenderTexture.ReleaseTemporary(_raymarchedLightEpi);
            RenderTexture.ReleaseTemporary(_interpolateAlongRaysEpi);
        }

        private void InitMaterials()
        {
            InitMaterial(ref _finalInterpolationMaterial, finalInterpolationShader);
            InitMaterial(ref _coordMaterial, coordShader);
            InitMaterial(ref _raymarchMaterial, raymarchShader);
            InitMaterial(ref _depthBreaksMaterial, depthBreaksShader);
            InitMaterial(ref _interpolateAlongRaysMaterial, interpolateAlongRaysShader);
        }

        private static void InitMaterial(ref Material material, Shader shader)
        {
            if (material == null && shader != null)
            {
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
        }

        private static void InitRenderTexture(
            ref RenderTexture rt, int width, int height, int depth, RenderTextureFormat format, bool temporary)
        {
            if (temporary)
            {
                rt = RenderTexture.GetTemporary(width, height, depth, format);
                return;
            }

            if (rt != null)
            {
                if (rt.width == width && rt.height == height && rt.depth == depth && rt.format == format)
                {
                    return;
                }
                rt.Release();
                DestroyImmediate(rt);
            }
            rt = new RenderTexture(width, height, depth, format) { hideFlags = HideFlags.HideAndDontSave };
        }

        private void InitEpipolarTextures()
        {
            var samples = GetResolutionSize(epipolarSamplesResolution);
            var lines = GetResolutionSize(epipolarLinesResolution);

            InitRenderTexture(ref _coordEpi, samples, lines, 0, RenderTextureFormat.RGFloat, true);
            _coordEpi.filterMode = FilterMode.Point;
            InitRenderTexture(ref _depthEpi, samples, lines, 0, RenderTextureFormat.RFloat, true);
            _depthEpi.filterMode = FilterMode.Point;
            InitRenderTexture(ref _interpolationEpi, samples, lines, 0,
                _dx11Support ? RenderTextureFormat.RGInt : RenderTextureFormat.RGFloat, true);
            _interpolationEpi.filterMode = FilterMode.Point;
            InitRenderTexture(ref _raymarchedLightEpi, samples, lines, 24, RenderTextureFormat.ARGBFloat, true);
            _raymarchedLightEpi.filterMode = FilterMode.Point;
            InitRenderTexture(ref _interpolateAlongRaysEpi, samples, lines, 0, RenderTextureFormat.ARGBFloat, true);
            _interpolateAlongRaysEpi.filterMode = FilterMode.Point;
        }

        private void InitShadowmap()
        {
            var temporary = shadowmapMode == ShadowmapMode.Dynamic;
            // 常設から毎フレームへ切り替わったときは、常設側の RT を先に手放す
            if (temporary && shadowmapMode != _shadowmapModeOld)
            {
                ReleasePersistent(ref _shadowmap);
                ReleasePersistent(ref _colorFilter);
            }

            var resolution = GetResolutionSize(shadowmapResolution);
            InitRenderTexture(ref _shadowmap, resolution, resolution, 24, RenderTextureFormat.RFloat, temporary);
            _shadowmap.filterMode = FilterMode.Point;
            _shadowmap.wrapMode = TextureWrapMode.Clamp;
            if (colored)
            {
                InitRenderTexture(ref _colorFilter, resolution, resolution, 0, RenderTextureFormat.ARGB32, temporary);
            }
            _shadowmapModeOld = shadowmapMode;
        }

        // ライト視点の深度 (と色フィルタ) を描く
        private void UpdateShadowmap()
        {
            if (shadowmapMode == ShadowmapMode.Static && !_shadowmapDirty)
            {
                return;
            }

            InitShadowmap();

            if (_shadowmapCamera == null)
            {
                var go = new GameObject("PostEffectsLightShaftsDepthCamera")
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };
                _shadowmapCamera = go.AddComponent<Camera>();
                _shadowmapCamera.enabled = false;
                _shadowmapCamera.clearFlags = CameraClearFlags.Color;
            }

            var cameraTransform = _shadowmapCamera.transform;
            cameraTransform.position = transform.position;
            cameraTransform.rotation = transform.rotation;

            if (directional)
            {
                _shadowmapCamera.orthographic = true;
                _shadowmapCamera.nearClipPlane = 0f;
                _shadowmapCamera.farClipPlane = size.z;
                _shadowmapCamera.orthographicSize = size.y * 0.5f;
                _shadowmapCamera.aspect = size.x / size.y;
            }
            else
            {
                _shadowmapCamera.orthographic = false;
                _shadowmapCamera.nearClipPlane = spotNear * targetLight.range;
                _shadowmapCamera.farClipPlane = spotFar * targetLight.range;
                _shadowmapCamera.fieldOfView = targetLight.spotAngle;
                _shadowmapCamera.aspect = 1f;
            }

            _shadowmapCamera.renderingPath = RenderingPath.Forward;
            _shadowmapCamera.targetTexture = _shadowmap;
            _shadowmapCamera.cullingMask = cullingMask;
            _shadowmapCamera.backgroundColor = Color.white;
            _shadowmapCamera.RenderWithShader(depthShader, "RenderType");

            if (colored)
            {
                _shadowmapCamera.targetTexture = _colorFilter;
                _shadowmapCamera.cullingMask = colorFilterMask;
                _shadowmapCamera.backgroundColor = new Color(colorBalance, colorBalance, colorBalance);
                _shadowmapCamera.RenderWithShader(colorFilterShader, "");
            }

            _shadowmapDirty = false;
        }

        // 画面をエピポーラ線 (光源から放射状に伸びる線) の座標系へ写す
        private void RenderCoords(int width, int height, Vector4 lightPos)
        {
            SetFrustumRays(_coordMaterial);
            _coordColorBuffers[0] = _coordEpi.colorBuffer;
            _coordColorBuffers[1] = _depthEpi.colorBuffer;
            Graphics.SetRenderTarget(_coordColorBuffers, _depthEpi.depthBuffer);
            _coordMaterial.SetVector("_LightPos", lightPos);
            _coordMaterial.SetVector("_CoordTexDim", TexDim(_coordEpi));
            _coordMaterial.SetVector("_ScreenTexDim", TexDim(width, height));
            _coordMaterial.SetPass(0);
            RenderQuad();
        }

        // 深度が急に変わる位置に印を付け、そこだけレイマーチする補間用テクスチャを作る
        private void RenderInterpolationTexture(Vector4 lightPos)
        {
            Graphics.SetRenderTarget(_interpolationEpi.colorBuffer, _raymarchedLightEpi.depthBuffer);
            if (!_dx11Support)
            {
                _depthBreaksMaterial.SetPass(1);
                RenderQuad();
            }
            else
            {
                GL.Clear(true, true, new Color(0f, 0f, 0f, 1f));
            }

            _depthBreaksMaterial.SetFloat("_InterpolationStep", interpolationStep);
            _depthBreaksMaterial.SetFloat("_DepthThreshold", GetDepthThresholdAdjusted());
            _depthBreaksMaterial.SetTexture("_DepthEpi", _depthEpi);
            _depthBreaksMaterial.SetVector("_DepthEpiTexDim", TexDim(_depthEpi));
            _depthBreaksMaterial.SetPass(0);
            RenderQuadSections(lightPos);
        }

        private void Raymarch(int width, int height, Vector4 lightPos)
        {
            SetFrustumRays(_raymarchMaterial);
            Graphics.SetRenderTarget(_raymarchedLightEpi.colorBuffer, _raymarchedLightEpi.depthBuffer);
            GL.Clear(false, true, new Color(0f, 0f, 0f, 1f));

            _raymarchMaterial.SetTexture("_Coord", _coordEpi);
            _raymarchMaterial.SetTexture("_InterpolationEpi", _interpolationEpi);
            _raymarchMaterial.SetTexture("_Shadowmap", _shadowmap);
            var value = (colored ? brightnessColored / colorBalance : brightness) * targetLight.intensity;
            _raymarchMaterial.SetFloat("_Brightness", value);
            _raymarchMaterial.SetFloat("_Extinction", -extinction);
            _raymarchMaterial.SetVector("_ShadowmapDim", TexDim(_shadowmap));
            _raymarchMaterial.SetVector("_ScreenTexDim", TexDim(width, height));
            _raymarchMaterial.SetVector("_LightColor", targetLight.color.linear);
            _raymarchMaterial.SetFloat("_MinDistFromCamera", minDistFromCamera);

            SetKeyword(colored, "COLORED_ON", "COLORED_OFF");
            _raymarchMaterial.SetTexture("_ColorFilter", _colorFilter);
            SetKeyword(attenuationCurveEnabled, "ATTENUATION_CURVE_ON", "ATTENUATION_CURVE_OFF");
            _raymarchMaterial.SetTexture("_AttenuationCurveTex", _attenuationCurveTexture);

            var cookie = targetLight.cookie;
            SetKeyword(cookie != null, "COOKIE_TEX_ON", "COOKIE_TEX_OFF");
            if (cookie != null)
            {
                _raymarchMaterial.SetTexture("_Cookie", cookie);
            }

            _raymarchMaterial.SetPass(0);
            RenderQuadSections(lightPos);
        }

        // レイマーチを間引いた位置の値を、線に沿って補間で埋める
        private void InterpolateAlongRays(Vector4 lightPos)
        {
            Graphics.SetRenderTarget(_interpolateAlongRaysEpi);
            _interpolateAlongRaysMaterial.SetFloat("_InterpolationStep", interpolationStep);
            _interpolateAlongRaysMaterial.SetTexture("_InterpolationEpi", _interpolationEpi);
            _interpolateAlongRaysMaterial.SetTexture("_RaymarchedLightEpi", _raymarchedLightEpi);
            _interpolateAlongRaysMaterial.SetVector("_RaymarchedLightEpiTexDim", TexDim(_raymarchedLightEpi));
            _interpolateAlongRaysMaterial.SetPass(0);
            RenderQuadSections(lightPos);
        }

        private void BakeAttenuationCurve()
        {
            if (_attenuationCurveTexture == null)
            {
                _attenuationCurveTexture = new Texture2D(256, 1, TextureFormat.ARGB32, false, true)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                _attenuationCurveDirty = true;
            }

            if (!_attenuationCurveDirty)
            {
                return;
            }

            for (var i = 0; i < 256; i++)
            {
                var value = attenuationCurve == null
                    ? i / 255f
                    : Mathf.Clamp01(attenuationCurve.Evaluate(i / 255f));
                _attenuationCurveTexture.SetPixel(i, 0, new Color(value, value, value, value));
            }
            _attenuationCurveTexture.Apply();
            _attenuationCurveDirty = false;
        }

        // スポットの光の錐台をメッシュとして持つ (最終合成でこれを描く)
        private void InitSpotFrustumMesh()
        {
            if (_spotMesh == null)
            {
                _spotMesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            }

            var light = targetLight;
            if (_spotMeshNear == spotNear && _spotMeshFar == spotFar &&
                _spotMeshAngle == light.spotAngle && _spotMeshRange == light.range)
            {
                return;
            }

            var far = light.range * spotFar;
            var near = light.range * spotNear;
            var tan = Mathf.Tan(light.spotAngle * Mathf.Deg2Rad * 0.5f);
            var farHalf = far * tan;
            var nearHalf = near * tan;

            _spotMesh.vertices = new[]
            {
                new Vector3(-farHalf, -farHalf, far),
                new Vector3(farHalf, -farHalf, far),
                new Vector3(farHalf, farHalf, far),
                new Vector3(-farHalf, farHalf, far),
                new Vector3(-nearHalf, -nearHalf, near),
                new Vector3(nearHalf, -nearHalf, near),
                new Vector3(nearHalf, nearHalf, near),
                new Vector3(-nearHalf, nearHalf, near),
            };

            if (_spotMesh.triangles == null || _spotMesh.triangles.Length != 36)
            {
                _spotMesh.triangles = new[]
                {
                    0, 1, 2, 0, 2, 3,
                    6, 5, 4, 7, 6, 4,
                    3, 2, 6, 3, 6, 7,
                    2, 1, 5, 2, 5, 6,
                    0, 3, 7, 0, 7, 4,
                    5, 1, 0, 5, 0, 4,
                };
            }

            _spotMeshNear = spotNear;
            _spotMeshFar = spotFar;
            _spotMeshAngle = light.spotAngle;
            _spotMeshRange = light.range;
        }

        private Bounds GetBoundsLocal()
        {
            if (directional)
            {
                return new Bounds(new Vector3(0f, 0f, size.z * 0.5f), size);
            }

            var light = targetLight;
            var center = new Vector3(0f, 0f, light.range * (spotFar + spotNear) * 0.5f);
            var depth = (spotFar - spotNear) * light.range;
            var width = Mathf.Tan(light.spotAngle * Mathf.Deg2Rad * 0.5f) * spotFar * light.range * 2f;
            return new Bounds(center, new Vector3(width, width, depth));
        }

        private Matrix4x4 GetBoundsMatrix()
        {
            var bounds = GetBoundsLocal();
            return Matrix4x4.TRS(
                transform.position + transform.forward * bounds.center.z, transform.rotation, bounds.size);
        }

        private float GetFrustumApex()
        {
            return -spotNear / (spotFar - spotNear) - 0.5f;
        }

        private Vector4 GetLightViewportPos()
        {
            // 平行光源は位置を持たないので、カメラ前方の方向ベクトルから画面上の位置を求める
            var position = directional
                ? _currentCamera.transform.position + transform.forward
                : transform.position;
            var viewport = _currentCamera.WorldToViewportPoint(position);
            return new Vector4(viewport.x * 2f - 1f, viewport.y * 2f - 1f, 0f, 0f);
        }

        private bool IsVisible()
        {
            var matrix = _currentCamera.projectionMatrix * _currentCamera.worldToCameraMatrix *
                transform.localToWorldMatrix;
            return GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(matrix), GetBoundsLocal());
        }

        private bool IntersectsNearPlane()
        {
            var vertices = _spotMesh.vertices;
            var near = _currentCamera.nearClipPlane - 0.001f;
            for (var i = 0; i < vertices.Length; i++)
            {
                if (_currentCamera.WorldToViewportPoint(transform.TransformPoint(vertices[i])).z < near)
                {
                    return true;
                }
            }
            return false;
        }

        private void GetFrustumRays(out Matrix4x4 frustumRays, out Vector3 cameraPosLocal)
        {
            var far = _currentCamera.farClipPlane;
            var position = _currentCamera.transform.position;
            var inverse = GetBoundsMatrix().inverse;

            frustumRays = default(Matrix4x4);
            for (var i = 0; i < ViewportCorners.Length; i++)
            {
                var ray = _currentCamera.ViewportToWorldPoint(
                    new Vector3(ViewportCorners[i].x, ViewportCorners[i].y, far)) - position;
                frustumRays.SetRow(i, inverse.MultiplyVector(ray));
            }
            cameraPosLocal = inverse.MultiplyPoint3x4(position);
        }

        private void SetFrustumRays(Material material)
        {
            Matrix4x4 frustumRays;
            Vector3 cameraPosLocal;
            GetFrustumRays(out frustumRays, out cameraPosLocal);
            material.SetVector("_CameraPosLocal", cameraPosLocal);
            material.SetMatrix("_FrustumRays", frustumRays);
            material.SetFloat("_FrustumApex", GetFrustumApex());
        }

        private float GetDepthThresholdAdjusted()
        {
            return depthThreshold / _currentCamera.farClipPlane;
        }

        // 光源が画面外にある向きの帯は描いても無駄なので飛ばす
        private void RenderQuadSections(Vector4 lightPos)
        {
            for (var i = 0; i < 4; i++)
            {
                var skip =
                    (i == 0 && lightPos.y > 1f) ||
                    (i == 1 && lightPos.x > 1f) ||
                    (i == 2 && lightPos.y < -1f) ||
                    (i == 3 && lightPos.x < -1f);
                if (skip)
                {
                    continue;
                }

                var bottom = i / 2f - 1f;
                var top = bottom + 0.5f;
                GL.Begin(GL.QUADS);
                GL.Vertex3(-1f, bottom, 0f);
                GL.Vertex3(1f, bottom, 0f);
                GL.Vertex3(1f, top, 0f);
                GL.Vertex3(-1f, top, 0f);
                GL.End();
            }
        }

        private static void RenderQuad()
        {
            GL.Begin(GL.QUADS);
            GL.TexCoord2(0f, 0f);
            GL.Vertex3(-1f, -1f, 0f);
            GL.TexCoord2(0f, 1f);
            GL.Vertex3(-1f, 1f, 0f);
            GL.TexCoord2(1f, 1f);
            GL.Vertex3(1f, 1f, 0f);
            GL.TexCoord2(1f, 0f);
            GL.Vertex3(1f, -1f, 0f);
            GL.End();
        }

        private static void SetKeyword(bool firstOn, string firstKeyword, string secondKeyword)
        {
            Shader.EnableKeyword(firstOn ? firstKeyword : secondKeyword);
            Shader.DisableKeyword(firstOn ? secondKeyword : firstKeyword);
        }

        private static Vector4 TexDim(Texture texture)
        {
            return TexDim(texture.width, texture.height);
        }

        private static Vector4 TexDim(int width, int height)
        {
            return new Vector4(width, height, 1f / width, 1f / height);
        }
    }
}
