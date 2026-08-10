using COM3D2.MotionTimelineEditor;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    public class EdgeDetectSetting
    {
        public bool enabled = false;
        public EdgeDetectEffect.EdgeDetectMode mode = EdgeDetectEffect.EdgeDetectMode.SobelDepthThin;
        public float sensitivityDepth = 1f;
        public float sensitivityNormals = 1f;
        public float lumThreshhold = 0.2f;
        public float edgeExp = 1f;
        public float sampleDist = 1f;
        public float edgesOnly = 0f;
        public float edgePower = 0.5f;
        public Color edgesOnlyBgColor = Color.white;
        // コンポーネント側の既定は黒だが、移植元 UI の初期値に合わせて灰にしている
        public Color edgeColor = Color.gray;
        public bool excludeCharacters = false;
    }

    public class EdgeDetectController : EffectControllerBase<EdgeDetectEffect, EdgeDetectSetting>
    {
        public override string effectName => "輪郭検出";

        protected override EdgeDetectSetting setting
        {
            get => settings.edgeDetect;
            set => settings.edgeDetect = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        protected override void ApplySetting(EdgeDetectEffect component)
        {
            if (component.shader == null)
            {
                component.shader = EffectShaders.GetShader(EffectShaders.ImageEffects, "edgedetectshader");
            }

            component.mode = setting.mode;
            component.sensitivityDepth = setting.sensitivityDepth;
            component.sensitivityNormals = setting.sensitivityNormals;
            component.lumThreshhold = setting.lumThreshhold;
            component.edgeExp = setting.edgeExp;
            component.sampleDist = setting.sampleDist;
            component.edgesOnly = setting.edgesOnly;
            component.edgePower = setting.edgePower;
            component.edgesOnlyBgColor = setting.edgesOnlyBgColor;
            component.edgeColor = setting.edgeColor;
            component.excludeCharacters = setting.excludeCharacters;
        }

        protected override void Capture(EdgeDetectEffect component)
        {
            var c = _capturedSetting;
            _capturedEnabled = component.enabled;
            c.mode = component.mode;
            c.sensitivityDepth = component.sensitivityDepth;
            c.sensitivityNormals = component.sensitivityNormals;
            c.lumThreshhold = component.lumThreshhold;
            c.edgeExp = component.edgeExp;
            c.sampleDist = component.sampleDist;
            c.edgesOnly = component.edgesOnly;
            c.edgePower = component.edgePower;
            c.edgesOnlyBgColor = component.edgesOnlyBgColor;
            c.edgeColor = component.edgeColor;
            c.excludeCharacters = component.excludeCharacters;
        }

        protected override void RestoreSetting(EdgeDetectEffect component)
        {
            var c = _capturedSetting;
            component.enabled = _capturedEnabled;
            component.mode = c.mode;
            component.sensitivityDepth = c.sensitivityDepth;
            component.sensitivityNormals = c.sensitivityNormals;
            component.lumThreshhold = c.lumThreshhold;
            component.edgeExp = c.edgeExp;
            component.sampleDist = c.sampleDist;
            component.edgesOnly = c.edgesOnly;
            component.edgePower = c.edgePower;
            component.edgesOnlyBgColor = c.edgesOnlyBgColor;
            component.edgeColor = c.edgeColor;
            component.excludeCharacters = c.excludeCharacters;
        }

        private readonly GUIComboBox<EdgeDetectEffect.EdgeDetectMode> _modeComboBox =
            new GUIComboBox<EdgeDetectEffect.EdgeDetectMode>
            {
                items = MTEUtils.GetEnumValues<EdgeDetectEffect.EdgeDetectMode>(),
                getName = (mode, _) => mode.ToString(),
                buttonSize = new Vector2(200, 20),
            };

        public override void DrawContent(GUIView view)
        {
            view.BeginHorizontal();
            {
                view.DrawLabel("検出方法", 80, 20);
                _modeComboBox.currentIndex = (int)setting.mode;
                _modeComboBox.onSelected = (mode, _) => { setting.mode = mode; SetDirty(); };
                _modeComboBox.DrawButton(view);
            }
            view.EndLayout();

            var currentMode = setting.mode;
            var useDepth = currentMode == EdgeDetectEffect.EdgeDetectMode.TriangleDepthNormals ||
                           currentMode == EdgeDetectEffect.EdgeDetectMode.RobertsCrossDepthNormals ||
                           currentMode == EdgeDetectEffect.EdgeDetectMode.SobelDepth ||
                           currentMode == EdgeDetectEffect.EdgeDetectMode.SobelDepthThin;
            var useNormals = currentMode == EdgeDetectEffect.EdgeDetectMode.TriangleDepthNormals ||
                             currentMode == EdgeDetectEffect.EdgeDetectMode.RobertsCrossDepthNormals;
            var useLuminance = currentMode == EdgeDetectEffect.EdgeDetectMode.TriangleLuminance;

            if (useDepth)
            {
                DrawSlider(view, "深度感度", 0f, 10f, 1f, setting.sensitivityDepth, v => setting.sensitivityDepth = v);
            }
            if (useNormals)
            {
                DrawSlider(view, "法線感度", 0f, 10f, 1f, setting.sensitivityNormals, v => setting.sensitivityNormals = v);
            }
            if (useLuminance)
            {
                DrawSlider(view, "輝度しきい値", 0f, 4f, 0.2f, setting.lumThreshhold, v => setting.lumThreshhold = v);
            }

            DrawSlider(view, "サンプル距離", 0f, 10f, 1f, setting.sampleDist, v => setting.sampleDist = v);
            DrawSlider(view, "指数", 0f, 1f, 1f, setting.edgeExp, v => setting.edgeExp = v);
            DrawSlider(view, "線の濃さ", 0f, 1f, 0.5f, setting.edgePower, v => setting.edgePower = v);

            view.DrawColor(
                view.GetColorFieldCache("線の色", false),
                setting.edgeColor,
                Color.gray,
                color => { setting.edgeColor = color; SetDirty(); });

            // 1 で線画のみ (元絵を背景色で塗り潰す)、0 で元絵に線を重ねる
            DrawSlider(view, "線画のみ", 0f, 1f, 0f, setting.edgesOnly, v => setting.edgesOnly = v);

            if (setting.edgesOnly > 0f)
            {
                view.DrawColor(
                    view.GetColorFieldCache("線画の背景色", false),
                    setting.edgesOnlyBgColor,
                    Color.white,
                    color => { setting.edgesOnlyBgColor = color; SetDirty(); });
            }

            view.DrawToggle("キャラに適用しない", setting.excludeCharacters, 250, 20, value =>
            {
                setting.excludeCharacters = value;
                SetDirty();
            });
        }
    }
}
