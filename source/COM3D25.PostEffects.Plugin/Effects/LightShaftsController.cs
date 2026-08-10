using COM3D2.MotionTimelineEditor;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    public class LightShaftsSetting
    {
        public bool enabled = false;

        // ライトの配置と設定 (本プラグインには光源を扱う画面が無いので専用の光源を自前で持つ)
        public bool directional = false;
        public Vector3 position = new Vector3(0f, 4f, 0f);
        public Vector3 eulerAngles = new Vector3(90f, 180f, 0f);
        public Color color = Color.white;
        public float intensity = 0.95f;
        public float range = 10f;
        public float spotAngle = 30f;

        public Vector3 size = new Vector3(2f, 2f, 2f);
        public float spotNear = 0.1f;
        public float spotFar = 1f;

        public float brightness = 5f;
        public float brightnessColored = 5f;
        public float extinction = 0.5f;
        public float minDistFromCamera = 0f;
        public bool colored = false;
        public float colorBalance = 1f;

        public LightShaftsEffect.ShadowmapMode shadowmapMode = LightShaftsEffect.ShadowmapMode.Dynamic;
        public LightShaftsEffect.Resolution shadowmapResolution = LightShaftsEffect.Resolution.High;
        public LightShaftsEffect.Resolution epipolarSamplesResolution = LightShaftsEffect.Resolution.High;
        public LightShaftsEffect.Resolution epipolarLinesResolution = LightShaftsEffect.Resolution.High;
        public float depthThreshold = 0.001f;
        public int interpolationStep = 8;

        public bool attenuationCurveEnabled = false;
        public CurveData attenuationCurve = CurveData.Linear();
    }

    /// <summary>
    /// 光の筋。カメラではなくライトに付くエフェクトなので、カメラ上のコンポーネントを操作する
    /// <see cref="EffectControllerBase{TComponent, TSetting}"/> は使わず、専用の光源ごと自前で管理する
    /// </summary>
    public class LightShaftsController : EffectControllerBase
    {
        public override string effectName => "光の筋";

        private static readonly Color DefaultColor = Color.white;

        private LightShaftsSetting setting
        {
            get => settings.lightShafts;
            set => settings.lightShafts = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        private GameObject _lightObject;
        private Light _light;
        private LightShaftsEffect _effect;

        private CurveData _lastCurve;
        private int _lastCurveVersion;
        // 固定シャドウマップの描き直しが要るかを判断するための、光源配置と描画範囲の要約。
        // 毎フレーム比較するので文字列化はせず値のまま持つ
        private struct ShadowmapSignature
        {
            public bool directional;
            public Vector3 position;
            public Vector3 eulerAngles;
            public Vector3 size;
            public float range;
            public float spotAngle;
            public float spotNear;
            public float spotFar;
            public bool colored;
            public float colorBalance;
            public LightShaftsEffect.Resolution resolution;

            public bool Equals(ShadowmapSignature other)
            {
                return directional == other.directional &&
                    position == other.position &&
                    eulerAngles == other.eulerAngles &&
                    size == other.size &&
                    range == other.range &&
                    spotAngle == other.spotAngle &&
                    spotNear == other.spotNear &&
                    spotFar == other.spotFar &&
                    colored == other.colored &&
                    colorBalance == other.colorBalance &&
                    resolution == other.resolution;
            }
        }

        private ShadowmapSignature _lastShadowmapSignature;
        private bool _hasShadowmapSignature;

        private ShadowmapSignature GetShadowmapSignature()
        {
            var s = setting;
            return new ShadowmapSignature
            {
                directional = s.directional,
                position = s.position,
                eulerAngles = s.eulerAngles,
                size = s.size,
                range = s.range,
                spotAngle = s.spotAngle,
                spotNear = s.spotNear,
                spotFar = s.spotFar,
                colored = s.colored,
                colorBalance = s.colorBalance,
                resolution = s.shadowmapResolution,
            };
        }

        public override void ResetSetting()
        {
            var enabled = effectEnabled;
            setting = new LightShaftsSetting();
            effectEnabled = enabled;
            _lastCurve = null;
            SetDirty();
        }

        public override void Apply()
        {
            var camera = cameraObject != null ? cameraObject.GetComponent<Camera>() : null;
            if (camera == null)
            {
                return;
            }

            // シーン遷移で破棄されていたら作り直す
            if (_lightObject == null)
            {
                _lightObject = new GameObject("PostEffectsLightShafts");
                _light = _lightObject.AddComponent<Light>();
                _effect = _lightObject.AddComponent<LightShaftsEffect>();
            }

            _light.enabled = true;
            _effect.enabled = true;

            _light.type = setting.directional ? LightType.Directional : LightType.Spot;
            _light.color = setting.color;
            _light.intensity = setting.intensity;
            _light.range = setting.range;
            _light.spotAngle = setting.spotAngle;
            // 影は自前のシャドウマップで扱うので、光源自体の影は落とさない
            _light.shadows = LightShadows.None;
            _lightObject.transform.position = setting.position;
            _lightObject.transform.eulerAngles = setting.eulerAngles;

            if (_effect.depthShader == null)
            {
                _effect.depthShader = GetShader("depth");
                _effect.colorFilterShader = GetShader("colorfilter");
                _effect.coordShader = GetShader("coord");
                _effect.depthBreaksShader = GetShader("depthbreaks");
                _effect.raymarchShader = GetShader("raymarch");
                _effect.interpolateAlongRaysShader = GetShader("interpolatealongrays");
                _effect.finalInterpolationShader = GetShader("finalinterpolation");
            }

            _effect.targetCamera = camera;
            camera.depthTextureMode |= DepthTextureMode.Depth;

            _effect.size = setting.size;
            _effect.spotNear = setting.spotNear;
            _effect.spotFar = Mathf.Max(setting.spotFar, setting.spotNear + 0.001f);
            _effect.brightness = setting.brightness;
            _effect.brightnessColored = setting.brightnessColored;
            _effect.extinction = setting.extinction;
            _effect.minDistFromCamera = setting.minDistFromCamera;
            _effect.colored = setting.colored;
            _effect.colorBalance = setting.colorBalance;
            _effect.shadowmapMode = setting.shadowmapMode;
            _effect.shadowmapResolution = setting.shadowmapResolution;
            _effect.epipolarSamplesResolution = setting.epipolarSamplesResolution;
            _effect.epipolarLinesResolution = setting.epipolarLinesResolution;
            _effect.depthThreshold = setting.depthThreshold;
            _effect.interpolationStep = setting.interpolationStep;
            _effect.attenuationCurveEnabled = setting.attenuationCurveEnabled;

            // 固定モードのシャドウマップは自動では描き直されないので、
            // 見た目に影響する設定が変わったフレームだけ明示的に汚す
            var signature = GetShadowmapSignature();
            if (!_hasShadowmapSignature || !_lastShadowmapSignature.Equals(signature))
            {
                _effect.SetShadowmapDirty();
                _lastShadowmapSignature = signature;
                _hasShadowmapSignature = true;
            }

            if (_lastCurve != setting.attenuationCurve || _lastCurveVersion != setting.attenuationCurve.version)
            {
                _effect.attenuationCurve = setting.attenuationCurve.ToAnimationCurve();
                _effect.SetAttenuationCurveDirty();
                _lastCurve = setting.attenuationCurve;
                _lastCurveVersion = setting.attenuationCurve.version;
            }
        }

        private static Shader GetShader(string assetName)
        {
            return EffectShaders.GetShader(EffectShaders.LightShafts, assetName);
        }

        public override void Restore()
        {
            if (_lightObject != null)
            {
                Object.Destroy(_lightObject);
            }
            _lightObject = null;
            _light = null;
            _effect = null;
            _lastCurve = null;
            _hasShadowmapSignature = false;
        }

        private static GUIComboBox<LightShaftsEffect.Resolution> CreateResolutionComboBox()
        {
            return new GUIComboBox<LightShaftsEffect.Resolution>
            {
                items = MTEUtils.GetEnumValues<LightShaftsEffect.Resolution>(),
                getName = (resolution, _) =>
                    string.Format("{0} ({1})", resolution, LightShaftsEffect.GetResolutionSize(resolution)),
                buttonSize = new Vector2(140, 20),
            };
        }

        private readonly GUIComboBox<LightShaftsEffect.Resolution> _shadowmapResComboBox = CreateResolutionComboBox();
        private readonly GUIComboBox<LightShaftsEffect.Resolution> _samplesResComboBox = CreateResolutionComboBox();
        private readonly GUIComboBox<LightShaftsEffect.Resolution> _linesResComboBox = CreateResolutionComboBox();

        private readonly GUIComboBox<LightShaftsEffect.ShadowmapMode> _shadowmapModeComboBox =
            new GUIComboBox<LightShaftsEffect.ShadowmapMode>
            {
                items = MTEUtils.GetEnumValues<LightShaftsEffect.ShadowmapMode>(),
                getName = (mode, _) => mode == LightShaftsEffect.ShadowmapMode.Dynamic ? "毎フレーム" : "固定",
                buttonSize = new Vector2(120, 20),
            };

        public override void DrawContent(GUIView view)
        {
            view.DrawLabel("この効果は専用の光源を 1 つ作って描画します", -1, 20);

            view.DrawToggle("平行光源にする", setting.directional, 250, 20, value =>
            {
                setting.directional = value;
                SetDirty();
            });

            DrawVector3(view, "位置", setting.position, new Vector3(0f, 4f, 0f), -20f, 20f,
                v => setting.position = v);
            DrawVector3(view, "回転", setting.eulerAngles, new Vector3(90f, 180f, 0f), 0f, 360f,
                v => setting.eulerAngles = v);

            view.DrawColor(
                view.GetColorFieldCache("光の色", false),
                setting.color, DefaultColor,
                color => { setting.color = color; SetDirty(); });

            DrawSlider(view, "光の強さ", 0f, 3f, 0.95f, setting.intensity, v => setting.intensity = v);

            if (setting.directional)
            {
                DrawVector3(view, "範囲", setting.size, new Vector3(2f, 2f, 2f), 0.1f, 100f,
                    v => setting.size = v);
            }
            else
            {
                DrawSlider(view, "到達距離", 0.1f, 200f, 10f, setting.range, v => setting.range = v);
                DrawSlider(view, "照射角", 1f, 179f, 30f, setting.spotAngle, v => setting.spotAngle = v);
                DrawSlider(view, "手前の比率", 0.001f, 0.999f, 0.1f, setting.spotNear, v => setting.spotNear = v);
                DrawSlider(view, "奥の比率", 0.001f, 1f, 1f, setting.spotFar, v => setting.spotFar = v);
            }

            view.DrawHorizontalLine(Color.gray);

            DrawSlider(view, "明るさ", 0f, 10f, 5f, setting.brightness, v => setting.brightness = v);
            DrawSlider(view, "減衰 (濃さ)", 0f, 2f, 0.5f, setting.extinction, v => setting.extinction = v);
            DrawSlider(view, "カメラからの距離", 0f, 20f, 0f,
                setting.minDistFromCamera, v => setting.minDistFromCamera = v);

            // 色付きは遮蔽物の色を光に乗せるモードで、明るさの係数が別になる
            view.DrawToggle("遮蔽物の色を乗せる", setting.colored, 250, 20, value =>
            {
                setting.colored = value;
                SetDirty();
            });

            if (setting.colored)
            {
                DrawSlider(view, "明るさ (色付き)", 0f, 10f, 5f,
                    setting.brightnessColored, v => setting.brightnessColored = v);
                DrawSlider(view, "色のバランス", 0.01f, 20f, 1f,
                    setting.colorBalance, v => setting.colorBalance = v);
            }

            view.DrawToggle("減衰カーブを使う", setting.attenuationCurveEnabled, 250, 20, value =>
            {
                setting.attenuationCurveEnabled = value;
                SetDirty();
            });

            if (setting.attenuationCurveEnabled)
            {
                view.DrawCurve("減衰カーブ", setting.attenuationCurve, Color.white, SetDirty);
            }

            view.DrawHorizontalLine(Color.gray);

            DrawResolution(view, "シャドウマップ", _shadowmapResComboBox, setting.shadowmapResolution,
                r => setting.shadowmapResolution = r);
            DrawResolution(view, "サンプル数", _samplesResComboBox, setting.epipolarSamplesResolution,
                r => setting.epipolarSamplesResolution = r);
            DrawResolution(view, "ライン数", _linesResComboBox, setting.epipolarLinesResolution,
                r => setting.epipolarLinesResolution = r);

            view.BeginHorizontal();
            {
                view.DrawLabel("影の更新", 100, 20);
                _shadowmapModeComboBox.currentIndex = (int)setting.shadowmapMode;
                _shadowmapModeComboBox.onSelected = (mode, _) => { setting.shadowmapMode = mode; SetDirty(); };
                _shadowmapModeComboBox.DrawButton(view);
            }
            view.EndLayout();

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "補間間隔",
                labelWidth = 100,
                width = -1,
                fieldType = FloatFieldType.Int,
                min = 1,
                max = 64,
                step = 1,
                defaultValue = 8,
                value = setting.interpolationStep,
                onChanged = value => { setting.interpolationStep = (int)value; SetDirty(); },
            });

            DrawSlider(view, "深度しきい値", 0.001f, 20f, 0.001f,
                setting.depthThreshold, v => setting.depthThreshold = v, 0.001f);
        }

        private void DrawResolution(
            GUIView view,
            string label,
            GUIComboBox<LightShaftsEffect.Resolution> comboBox,
            LightShaftsEffect.Resolution current,
            System.Action<LightShaftsEffect.Resolution> onChanged)
        {
            view.BeginHorizontal();
            {
                view.DrawLabel(label, 100, 20);
                comboBox.currentIndex = (int)current;
                comboBox.onSelected = (resolution, _) => { onChanged(resolution); SetDirty(); };
                comboBox.DrawButton(view);
            }
            view.EndLayout();
        }

        private void DrawVector3(
            GUIView view, string label, Vector3 value, Vector3 defaultValue,
            float min, float max, System.Action<Vector3> onChanged)
        {
            view.DrawLabel(label, -1, 20);
            DrawSlider(view, "  X", min, max, defaultValue.x, value.x,
                v => onChanged(new Vector3(v, value.y, value.z)));
            DrawSlider(view, "  Y", min, max, defaultValue.y, value.y,
                v => onChanged(new Vector3(value.x, v, value.z)));
            DrawSlider(view, "  Z", min, max, defaultValue.z, value.z,
                v => onChanged(new Vector3(value.x, value.y, v)));
        }
    }
}
