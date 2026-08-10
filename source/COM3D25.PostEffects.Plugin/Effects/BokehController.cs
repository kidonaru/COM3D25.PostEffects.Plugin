using COM3D2.MotionTimelineEditor;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    public class BokehSetting
    {
        public bool enabled = false;
        public float focusDistance = 10f;
        public float fNumber = 1.4f;
        public bool useCameraFov = true;
        public float focalLength = 0.05f;
        public float focalRange = 1f;
        public BokehEffect.KernelSize kernelSize = BokehEffect.KernelSize.Medium;
        public bool useARGBHalf = true;
        public int radiusBasePixel = 6;
        public bool visualize = false;

        // メイドの頭にピントを合わせる
        public bool maidFocus = false;
        public int maidIndex = 0;
    }

    public class BokehController : EffectControllerBase<BokehEffect, BokehSetting>
    {
        public override string effectName => "ボケ (物理)";

        protected override BokehSetting setting
        {
            get => settings.bokeh;
            set => settings.bokeh = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        protected override void ApplySetting(BokehEffect component)
        {
            if (component.shader == null)
            {
                component.shader = EffectShaders.GetShader(EffectShaders.Kino, "bokeh");
            }

            component.focusDistance = setting.focusDistance;
            component.fNumber = setting.fNumber;
            component.useCameraFov = setting.useCameraFov;
            component.focalLength = setting.focalLength;
            component.focalRange = setting.focalRange;
            component.kernelSize = setting.kernelSize;
            component.useARGBHalf = setting.useARGBHalf;
            component.radiusBasePixel = setting.radiusBasePixel;
            component.visualize = setting.visualize;
            component.pointOfFocus = setting.maidFocus ? GetMaidHeadTransform() : null;
        }

        private Transform GetMaidHeadTransform()
        {
            var maids = MTEUtils.GetReadyMaidList();
            if (maids.Count == 0)
            {
                return null;
            }

            var maid = maids[Mathf.Clamp(setting.maidIndex, 0, maids.Count - 1)];
            if (maid == null || maid.body0 == null)
            {
                return null;
            }
            return maid.body0.trsHead;
        }

        protected override void Capture(BokehEffect component)
        {
            var c = _capturedSetting;
            _capturedEnabled = component.enabled;
            c.focusDistance = component.focusDistance;
            c.fNumber = component.fNumber;
            c.useCameraFov = component.useCameraFov;
            c.focalLength = component.focalLength;
            c.focalRange = component.focalRange;
            c.kernelSize = component.kernelSize;
            c.useARGBHalf = component.useARGBHalf;
            c.radiusBasePixel = component.radiusBasePixel;
            c.visualize = component.visualize;
        }

        protected override void RestoreSetting(BokehEffect component)
        {
            var c = _capturedSetting;
            component.enabled = _capturedEnabled;
            component.focusDistance = c.focusDistance;
            component.fNumber = c.fNumber;
            component.useCameraFov = c.useCameraFov;
            component.focalLength = c.focalLength;
            component.focalRange = c.focalRange;
            component.kernelSize = c.kernelSize;
            component.useARGBHalf = c.useARGBHalf;
            component.radiusBasePixel = c.radiusBasePixel;
            component.visualize = c.visualize;
            component.pointOfFocus = null;
        }

        private readonly GUIComboBox<BokehEffect.KernelSize> _kernelComboBox = new GUIComboBox<BokehEffect.KernelSize>
        {
            items = MTEUtils.GetEnumValues<BokehEffect.KernelSize>(),
            getName = (size, _) => size.ToString(),
            buttonSize = new Vector2(100, 20),
        };

        private readonly GUIComboBox<Maid> _maidComboBox = new GUIComboBox<Maid>
        {
            getName = (maid, _) => maid == null ? "未選択" : maid.status.fullNameJpStyle,
            buttonSize = new Vector2(180, 20),
        };

        public override void DrawContent(GUIView view)
        {
            view.BeginHorizontal();
            {
                view.DrawLabel("ボケの大きさ", 90, 20);
                _kernelComboBox.currentIndex = (int)setting.kernelSize;
                _kernelComboBox.onSelected = (size, _) => { setting.kernelSize = size; SetDirty(); };
                _kernelComboBox.DrawButton(view);
            }
            view.EndLayout();

            view.DrawToggle("メイドの頭に追従", setting.maidFocus, 200, 20, value =>
            {
                setting.maidFocus = value;
                SetDirty();
            });

            if (setting.maidFocus)
            {
                view.BeginHorizontal();
                {
                    view.DrawLabel("メイド", 60, 20);
                    var maids = MTEUtils.GetReadyMaidList();
                    _maidComboBox.items = maids;
                    _maidComboBox.currentIndex = Mathf.Clamp(setting.maidIndex, 0, Mathf.Max(0, maids.Count - 1));
                    _maidComboBox.onSelected = (maid, index) => { setting.maidIndex = index; SetDirty(); };
                    _maidComboBox.DrawButton(view);
                }
                view.EndLayout();
            }
            else
            {
                view.DrawSliderValue(new GUIView.SliderOption
                {
                    label = "ピント距離",
                    labelWidth = 90,
                    width = -1,
                    min = 0.01f,
                    max = 200f,
                    step = 0.1f,
                    defaultValue = 10f,
                    value = setting.focusDistance,
                    onChanged = value => { setting.focusDistance = value; SetDirty(); },
                });
            }

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "F 値",
                labelWidth = 90,
                width = -1,
                min = 0.1f,
                max = 50f,
                step = 0.01f,
                defaultValue = 1.4f,
                value = setting.fNumber,
                onChanged = value => { setting.fNumber = value; SetDirty(); },
            });

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "ピント範囲",
                labelWidth = 90,
                width = -1,
                min = 0.001f,
                max = 5f,
                step = 0.01f,
                defaultValue = 1f,
                value = setting.focalRange,
                onChanged = value => { setting.focalRange = value; SetDirty(); },
            });

            view.DrawToggle("カメラの画角を使う", setting.useCameraFov, 250, 20, value =>
            {
                setting.useCameraFov = value;
                SetDirty();
            });

            if (!setting.useCameraFov)
            {
                view.DrawSliderValue(new GUIView.SliderOption
                {
                    label = "焦点距離",
                    labelWidth = 90,
                    width = -1,
                    min = 0.001f,
                    max = 10f,
                    step = 0.001f,
                    defaultValue = 0.05f,
                    value = setting.focalLength,
                    onChanged = value => { setting.focalLength = value; SetDirty(); },
                });
            }

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "ボケ半径",
                labelWidth = 90,
                width = -1,
                fieldType = FloatFieldType.Int,
                min = 1,
                max = 60,
                step = 1,
                defaultValue = 6,
                value = setting.radiusBasePixel,
                onChanged = value => { setting.radiusBasePixel = (int)value; SetDirty(); },
            });

            view.DrawToggle("高精度バッファ", setting.useARGBHalf, 250, 20, value =>
            {
                setting.useARGBHalf = value;
                SetDirty();
            });

            view.DrawToggle("ピント位置を可視化", setting.visualize, 250, 20, value =>
            {
                setting.visualize = value;
                SetDirty();
            });
        }
    }
}
