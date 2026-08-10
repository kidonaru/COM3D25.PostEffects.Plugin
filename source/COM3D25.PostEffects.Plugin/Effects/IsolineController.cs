using COM3D2.MotionTimelineEditor;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    public class IsolineSetting
    {
        public bool enabled = false;
        public Color lineColor = Color.white;
        public Color backgroundColor = Color.black;
        public float luminanceBlending = 1f;
        public float fallOffDepth = 40f;
        public float interval = 0.25f;

        // 等高線の向き
        public float axisX = 0.577f;
        public float axisY = 0.577f;
        public float axisZ = 0.577f;

        // 流れる向きと速さ
        public float directionX = 0.577f;
        public float directionY = 0.577f;
        public float directionZ = 0.577f;
        public float speed = 0.2f;

        // 歪み
        public float distortionFrequency = 1f;
        public float distortionAmount = 0f;

        // 明滅
        public IsolineEffect.ModulationMode modulationMode = IsolineEffect.ModulationMode.None;
        public float modulationAxisX = 0f;
        public float modulationAxisY = 0f;
        public float modulationAxisZ = 1f;
        public float modulationFrequency = 0.2f;
        public float modulationSpeed = 1f;
        public float modulationExponent = 24f;

        public bool excludeCharacters = false;
    }

    public class IsolineController : EffectControllerBase<IsolineEffect, IsolineSetting>
    {
        public override string effectName => "等高線";

        protected override IsolineSetting setting
        {
            get => settings.isoline;
            set => settings.isoline = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        protected override void ApplySetting(IsolineEffect component)
        {
            if (component.shader == null)
            {
                component.shader = EffectShaders.GetShader(EffectShaders.Kino, "isoline");
            }

            component.lineColor = setting.lineColor;
            component.backgroundColor = setting.backgroundColor;
            component.luminanceBlending = setting.luminanceBlending;
            component.fallOffDepth = setting.fallOffDepth;
            component.interval = setting.interval;
            component.axis = new Vector3(setting.axisX, setting.axisY, setting.axisZ);
            component.direction = new Vector3(setting.directionX, setting.directionY, setting.directionZ);
            component.speed = setting.speed;
            component.distortionFrequency = setting.distortionFrequency;
            component.distortionAmount = setting.distortionAmount;
            component.modulationMode = setting.modulationMode;
            component.modulationAxis =
                new Vector3(setting.modulationAxisX, setting.modulationAxisY, setting.modulationAxisZ);
            component.modulationFrequency = setting.modulationFrequency;
            component.modulationSpeed = setting.modulationSpeed;
            component.modulationExponent = setting.modulationExponent;
            component.excludeCharacters = setting.excludeCharacters;
        }

        protected override void Capture(IsolineEffect component)
        {
            var c = _capturedSetting;
            _capturedEnabled = component.enabled;
            c.lineColor = component.lineColor;
            c.backgroundColor = component.backgroundColor;
            c.luminanceBlending = component.luminanceBlending;
            c.fallOffDepth = component.fallOffDepth;
            c.interval = component.interval;
            c.axisX = component.axis.x;
            c.axisY = component.axis.y;
            c.axisZ = component.axis.z;
            c.directionX = component.direction.x;
            c.directionY = component.direction.y;
            c.directionZ = component.direction.z;
            c.speed = component.speed;
            c.distortionFrequency = component.distortionFrequency;
            c.distortionAmount = component.distortionAmount;
            c.modulationMode = component.modulationMode;
            c.modulationAxisX = component.modulationAxis.x;
            c.modulationAxisY = component.modulationAxis.y;
            c.modulationAxisZ = component.modulationAxis.z;
            c.modulationFrequency = component.modulationFrequency;
            c.modulationSpeed = component.modulationSpeed;
            c.modulationExponent = component.modulationExponent;
            c.excludeCharacters = component.excludeCharacters;
        }

        protected override void RestoreSetting(IsolineEffect component)
        {
            var c = _capturedSetting;
            component.enabled = _capturedEnabled;
            component.lineColor = c.lineColor;
            component.backgroundColor = c.backgroundColor;
            component.luminanceBlending = c.luminanceBlending;
            component.fallOffDepth = c.fallOffDepth;
            component.interval = c.interval;
            component.axis = new Vector3(c.axisX, c.axisY, c.axisZ);
            component.direction = new Vector3(c.directionX, c.directionY, c.directionZ);
            component.speed = c.speed;
            component.distortionFrequency = c.distortionFrequency;
            component.distortionAmount = c.distortionAmount;
            component.modulationMode = c.modulationMode;
            component.modulationAxis = new Vector3(c.modulationAxisX, c.modulationAxisY, c.modulationAxisZ);
            component.modulationFrequency = c.modulationFrequency;
            component.modulationSpeed = c.modulationSpeed;
            component.modulationExponent = c.modulationExponent;
            component.excludeCharacters = c.excludeCharacters;
        }

        private readonly GUIComboBox<IsolineEffect.ModulationMode> _modulationComboBox =
            new GUIComboBox<IsolineEffect.ModulationMode>
            {
                items = MTEUtils.GetEnumValues<IsolineEffect.ModulationMode>(),
                getName = (mode, _) => mode.ToString(),
                buttonSize = new Vector2(100, 20),
            };

        public override void DrawContent(GUIView view)
        {
            view.DrawColor(
                view.GetColorFieldCache("線の色", false),
                setting.lineColor,
                Color.white,
                color => { setting.lineColor = color; SetDirty(); });

            view.DrawColor(
                view.GetColorFieldCache("背景色", false),
                setting.backgroundColor,
                Color.black,
                color => { setting.backgroundColor = color; SetDirty(); });

            DrawSlider(view, "元絵の混合", 0f, 1f, 1f, setting.luminanceBlending, v => setting.luminanceBlending = v);
            DrawSlider(view, "減衰距離", 0f, 200f, 40f, setting.fallOffDepth, v => setting.fallOffDepth = v);
            // 0 だと密度 (1/interval) が発散するため下限を設ける
            DrawSlider(view, "線の間隔", 0.01f, 10f, 0.25f, setting.interval, v => setting.interval = v);

            view.DrawToggle("キャラに適用しない", setting.excludeCharacters, 250, 20, value =>
            {
                setting.excludeCharacters = value;
                SetDirty();
            });

            view.DrawHorizontalLine(Color.gray);
            view.DrawLabel("等高線の向き", 120, 20);
            DrawSlider(view, "軸 X", 0f, 1f, 0.577f, setting.axisX, v => setting.axisX = v);
            DrawSlider(view, "軸 Y", 0f, 1f, 0.577f, setting.axisY, v => setting.axisY = v);
            DrawSlider(view, "軸 Z", 0f, 1f, 0.577f, setting.axisZ, v => setting.axisZ = v);

            view.DrawHorizontalLine(Color.gray);
            view.DrawLabel("流れ", 120, 20);
            DrawSlider(view, "方向 X", 0f, 1f, 0.577f, setting.directionX, v => setting.directionX = v);
            DrawSlider(view, "方向 Y", 0f, 1f, 0.577f, setting.directionY, v => setting.directionY = v);
            DrawSlider(view, "方向 Z", 0f, 1f, 0.577f, setting.directionZ, v => setting.directionZ = v);
            DrawSlider(view, "速さ", 0f, 20f, 0.2f, setting.speed, v => setting.speed = v);

            view.DrawHorizontalLine(Color.gray);
            view.DrawLabel("歪み", 120, 20);
            DrawSlider(view, "周波数", 0f, 20f, 1f, setting.distortionFrequency, v => setting.distortionFrequency = v);
            DrawSlider(view, "強さ", 0f, 5f, 0f, setting.distortionAmount, v => setting.distortionAmount = v);

            view.DrawHorizontalLine(Color.gray);
            view.BeginHorizontal();
            {
                view.DrawLabel("明滅", 60, 20);
                _modulationComboBox.currentIndex = (int)setting.modulationMode;
                _modulationComboBox.onSelected = (mode, _) => { setting.modulationMode = mode; SetDirty(); };
                _modulationComboBox.DrawButton(view);
            }
            view.EndLayout();

            if (setting.modulationMode != IsolineEffect.ModulationMode.None)
            {
                DrawSlider(view, "明滅軸 X", 0f, 1f, 0f, setting.modulationAxisX, v => setting.modulationAxisX = v);
                DrawSlider(view, "明滅軸 Y", 0f, 1f, 0f, setting.modulationAxisY, v => setting.modulationAxisY = v);
                DrawSlider(view, "明滅軸 Z", 0f, 1f, 1f, setting.modulationAxisZ, v => setting.modulationAxisZ = v);
                DrawSlider(view, "明滅周波数", 0f, 10f, 0.2f, setting.modulationFrequency,
                    v => setting.modulationFrequency = v);
                DrawSlider(view, "明滅速度", 0f, 25f, 1f, setting.modulationSpeed, v => setting.modulationSpeed = v);
                DrawSlider(view, "明滅の鋭さ", 0f, 50f, 24f, setting.modulationExponent,
                    v => setting.modulationExponent = v);
            }
        }
    }
}
