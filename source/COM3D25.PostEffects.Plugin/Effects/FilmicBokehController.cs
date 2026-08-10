using COM3D2.MotionTimelineEditor;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    public class FilmicBokehSetting
    {
        public bool enabled = false;
        public float focusDistance = 10f;
        public float fNumber = 1.4f;
        public bool useCameraFov = true;
        public float focalLength = 0.05f;
        public float focalRange = 1f;
        public FilmicBokehEffect.KernelSize kernelSize = FilmicBokehEffect.KernelSize.Medium;
        public bool useARGBHalf = true;
        public int radiusBasePixel = 6;
        public bool useHexBokeh = false;
        public float angle = 0f;
        public bool visualize = false;

        // メイドの頭にピントを合わせる
        public bool maidFocus = false;
        public int maidIndex = 0;
    }

    public class FilmicBokehController : EffectControllerBase<FilmicBokehEffect, FilmicBokehSetting>
    {
        public override string effectName => "フィルミックボケ";

        protected override FilmicBokehSetting setting
        {
            get => settings.filmicBokeh;
            set => settings.filmicBokeh = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        protected override void ApplySetting(FilmicBokehEffect component)
        {
            if (component.shader == null)
            {
                component.shader = EffectShaders.GetShader(EffectShaders.Filmic, "filmicbokehshader");
            }
            if (component.depthShader == null)
            {
                component.depthShader = EffectShaders.GetShader(EffectShaders.Cinematic, "renderdepthcutoutshader");
            }

            component.focusDistance = setting.focusDistance;
            component.fNumber = setting.fNumber;
            component.useCameraFov = setting.useCameraFov;
            component.focalLength = setting.focalLength;
            component.focalRange = setting.focalRange;
            component.kernelSize = setting.kernelSize;
            component.useARGBHalf = setting.useARGBHalf;
            component.radiusBasePixel = setting.radiusBasePixel;
            component.useHexBokeh = setting.useHexBokeh;
            component.angle = setting.angle;
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

        protected override void Capture(FilmicBokehEffect component)
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
            c.useHexBokeh = component.useHexBokeh;
            c.angle = component.angle;
            c.visualize = component.visualize;
        }

        protected override void RestoreSetting(FilmicBokehEffect component)
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
            component.useHexBokeh = c.useHexBokeh;
            component.angle = c.angle;
            component.visualize = c.visualize;
            component.pointOfFocus = null;
        }

        private readonly GUIComboBox<FilmicBokehEffect.KernelSize> _kernelComboBox =
            new GUIComboBox<FilmicBokehEffect.KernelSize>
            {
                items = MTEUtils.GetEnumValues<FilmicBokehEffect.KernelSize>(),
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
                DrawSlider(view, "ピント距離", 0.01f, 200f, 10f,
                    setting.focusDistance, v => setting.focusDistance = v, 0.1f);
            }

            DrawSlider(view, "F 値", 0.1f, 50f, 1.4f, setting.fNumber, v => setting.fNumber = v);
            DrawSlider(view, "ピント範囲", 0.001f, 5f, 1f, setting.focalRange, v => setting.focalRange = v);

            view.DrawToggle("カメラの画角を使う", setting.useCameraFov, 250, 20, value =>
            {
                setting.useCameraFov = value;
                SetDirty();
            });

            if (!setting.useCameraFov)
            {
                DrawSlider(view, "焦点距離", 0.001f, 10f, 0.05f,
                    setting.focalLength, v => setting.focalLength = v, 0.001f);
            }

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "ボケ半径",
                labelWidth = 100,
                width = -1,
                fieldType = FloatFieldType.Int,
                min = 1,
                max = 60,
                step = 1,
                defaultValue = 6,
                value = setting.radiusBasePixel,
                onChanged = value => { setting.radiusBasePixel = (int)value; SetDirty(); },
            });

            view.DrawToggle("六角形ボケ", setting.useHexBokeh, 250, 20, value =>
            {
                setting.useHexBokeh = value;
                SetDirty();
            });

            if (setting.useHexBokeh)
            {
                DrawSlider(view, "回転", 0f, 360f, 0f, setting.angle, v => setting.angle = v, 1f);
            }

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
