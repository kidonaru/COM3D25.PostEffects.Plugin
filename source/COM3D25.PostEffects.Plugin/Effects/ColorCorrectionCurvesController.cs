using System;
using COM3D2.MotionTimelineEditor;
using UnityEngine;
// Assembly-UnityScript-firstpass のグローバル名前空間にも旧 ColorCorrectionCurves が残骸として存在するため、
// ゲームが実際に使う PostEffects_Dummy 側へエイリアスで束縛する
using ColorCorrectionCurvesEffect = PostEffects_Dummy.ColorCorrectionCurves;

namespace COM3D25.PostEffects.Plugin
{
    public class ColorCorrectionCurvesSetting
    {
        public bool enabled = false;
        public float saturation = 1f;

        public CurveData redCurve = CurveData.Linear();
        public CurveData greenCurve = CurveData.Linear();
        public CurveData blueCurve = CurveData.Linear();

        // 深度補正 (奥行きに応じて通常カーブと深度カーブを zCurve で補間する)
        public bool useDepthCorrection = false;
        public CurveData zCurve = CurveData.Linear();
        public CurveData depthRedCurve = CurveData.Linear();
        public CurveData depthGreenCurve = CurveData.Linear();
        public CurveData depthBlueCurve = CurveData.Linear();

        public bool selectiveCc = false;
        public Color selectiveFromColor = Color.white;
        public Color selectiveToColor = Color.white;
    }

    public class ColorCorrectionCurvesController : EffectControllerBase<ColorCorrectionCurvesEffect, ColorCorrectionCurvesSetting>
    {
        public override string effectName => "色補正";

        protected override ColorCorrectionCurvesSetting setting
        {
            get => settings.colorCorrectionCurves;
            set => settings.colorCorrectionCurves = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        // 設定とコンポーネントで対になるカーブの定義。
        // 設定・適用・捕捉・復元がすべてこの配列を回るので、カーブの並びはここだけが持つ
        private class CurveSlot
        {
            public Func<ColorCorrectionCurvesSetting, CurveData> getSetting;
            public Func<ColorCorrectionCurvesEffect, AnimationCurve> getComponent;
            public Action<ColorCorrectionCurvesEffect, AnimationCurve> setComponent;
        }

        private static readonly CurveSlot[] CurveSlots =
        {
            new CurveSlot { getSetting = s => s.redCurve, getComponent = c => c.redChannel, setComponent = (c, v) => c.redChannel = v },
            new CurveSlot { getSetting = s => s.greenCurve, getComponent = c => c.greenChannel, setComponent = (c, v) => c.greenChannel = v },
            new CurveSlot { getSetting = s => s.blueCurve, getComponent = c => c.blueChannel, setComponent = (c, v) => c.blueChannel = v },
            new CurveSlot { getSetting = s => s.zCurve, getComponent = c => c.zCurve, setComponent = (c, v) => c.zCurve = v },
            new CurveSlot { getSetting = s => s.depthRedCurve, getComponent = c => c.depthRedChannel, setComponent = (c, v) => c.depthRedChannel = v },
            new CurveSlot { getSetting = s => s.depthGreenCurve, getComponent = c => c.depthGreenChannel, setComponent = (c, v) => c.depthGreenChannel = v },
            new CurveSlot { getSetting = s => s.depthBlueCurve, getComponent = c => c.depthBlueChannel, setComponent = (c, v) => c.depthBlueChannel = v },
        };

        // カーブテクスチャの再構築 (UpdateParameters) は重いので、値が変わったフレームだけ実行する。
        // リセットやプリセット読込で CurveData ごと差し替わるケースがあるため、参照とバージョンの両方で比較する
        private readonly CurveData[] _curves = new CurveData[CurveSlots.Length];
        private readonly CurveData[] _lastCurves = new CurveData[CurveSlots.Length];
        private readonly int[] _lastVersions = new int[CurveSlots.Length];

        // Shader.Find は毎フレーム呼ぶには重いのでキャッシュする
        private static Shader _simpleShader;
        private static Shader _curvesShader;
        private static Shader _selectiveShader;

        private static Shader FindShader(ref Shader cache, string name)
        {
            if (cache == null)
            {
                cache = Shader.Find(name);
            }
            return cache;
        }

        private CurveData[] CollectCurves()
        {
            var s = setting;
            for (var i = 0; i < CurveSlots.Length; i++)
            {
                _curves[i] = CurveSlots[i].getSetting(s);
            }
            return _curves;
        }

        /// <summary>次回の ApplySetting でカーブを必ず再構築させる</summary>
        private void InvalidateCurveCache()
        {
            for (var i = 0; i < _lastCurves.Length; i++)
            {
                _lastCurves[i] = null;
            }
        }

        private bool IsCurveDirty()
        {
            var curves = CollectCurves();
            for (var i = 0; i < curves.Length; i++)
            {
                if (_lastCurves[i] != curves[i] || _lastVersions[i] != curves[i].version)
                {
                    return true;
                }
            }
            return false;
        }

        protected override void ApplySetting(ColorCorrectionCurvesEffect component)
        {
            // ゲーム側の ColorCorrectionCurves はシェーダーフォールバックを持たないため、
            // 内蔵シェーダーを明示的に割り当てる (全て実機で存在確認済み)
            component.simpleColorCorrectionCurvesShader = FindShader(ref _simpleShader, "Hidden/ColorCorrectionCurvesSimple");
            component.colorCorrectionCurvesShader = FindShader(ref _curvesShader, "Hidden/ColorCorrectionCurves");

            // ゲーム側の CheckResources は深度補正用マテリアルまで colorCorrectionSelectiveShader から作る実装ミスがあり、
            // そのままでは深度補正が選択的色補正シェーダーで描画されて絵が壊れる。
            // 深度補正中だけ同フィールドへ本来のカーブシェーダーを差し込んで回避する (選択的色補正とは併用不可)
            component.colorCorrectionSelectiveShader = setting.useDepthCorrection
                ? FindShader(ref _curvesShader, "Hidden/ColorCorrectionCurves")
                : FindShader(ref _selectiveShader, "Hidden/ColorCorrectionSelective");

            component.mode = setting.useDepthCorrection
                ? PostEffects_Dummy.ColorCorrectionMode.Advanced
                : PostEffects_Dummy.ColorCorrectionMode.Simple;
            component.useDepthCorrection = setting.useDepthCorrection;
            component.saturation = setting.saturation;
            component.selectiveCc = !setting.useDepthCorrection && setting.selectiveCc;
            component.selectiveFromColor = setting.selectiveFromColor;
            component.selectiveToColor = setting.selectiveToColor;

            if (IsCurveDirty())
            {
                var curves = CollectCurves();
                for (var i = 0; i < CurveSlots.Length; i++)
                {
                    CurveSlots[i].setComponent(component, curves[i].ToAnimationCurve());
                    _lastCurves[i] = curves[i];
                    _lastVersions[i] = curves[i].version;
                }
                component.UpdateParameters();
            }
        }

        private AnimationCurve[] _capturedChannels;
        private PostEffects_Dummy.ColorCorrectionMode _capturedMode;
        private Shader _capturedSimpleShader;
        private Shader _capturedCurvesShader;
        private Shader _capturedSelectiveShader;

        protected override void Capture(ColorCorrectionCurvesEffect component)
        {
            var c = _capturedSetting;
            _capturedEnabled = component.enabled;
            c.saturation = component.saturation;
            c.useDepthCorrection = component.useDepthCorrection;
            c.selectiveCc = component.selectiveCc;
            c.selectiveFromColor = component.selectiveFromColor;
            c.selectiveToColor = component.selectiveToColor;
            _capturedMode = component.mode;
            _capturedSimpleShader = component.simpleColorCorrectionCurvesShader;
            _capturedCurvesShader = component.colorCorrectionCurvesShader;
            _capturedSelectiveShader = component.colorCorrectionSelectiveShader;

            _capturedChannels = new AnimationCurve[CurveSlots.Length];
            for (var i = 0; i < CurveSlots.Length; i++)
            {
                var slot = CurveSlots[i];
                _capturedChannels[i] = slot.getComponent(component);
                if (_capturedChannels[i] != null)
                {
                    // リセットで参照できるよう、CurveData 側にも取り込んでおく
                    slot.getSetting(c).FromAnimationCurve(_capturedChannels[i]);
                }
            }
        }

        protected override void RestoreSetting(ColorCorrectionCurvesEffect component)
        {
            var c = _capturedSetting;
            component.enabled = _capturedEnabled;
            component.saturation = c.saturation;
            component.mode = _capturedMode;
            component.useDepthCorrection = c.useDepthCorrection;
            component.selectiveCc = c.selectiveCc;
            component.selectiveFromColor = c.selectiveFromColor;
            component.selectiveToColor = c.selectiveToColor;
            component.simpleColorCorrectionCurvesShader = _capturedSimpleShader;
            component.colorCorrectionCurvesShader = _capturedCurvesShader;
            component.colorCorrectionSelectiveShader = _capturedSelectiveShader;
            for (var i = 0; i < CurveSlots.Length; i++)
            {
                CurveSlots[i].setComponent(component, _capturedChannels[i]);
            }
            InvalidateCurveCache();
            // UpdateParameters は深度カーブまで無条件に評価するため、1 つでも欠けていたら呼ばない
            if (Array.TrueForAll(_capturedChannels, curve => curve != null))
            {
                component.UpdateParameters();
            }
        }

        public override void ResetSetting()
        {
            base.ResetSetting();
            // 新しい設定のカーブは version が 0 に戻るため、バージョン比較を確実に外す
            InvalidateCurveCache();
        }

        public override void DrawContent(GUIView view)
        {
            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "彩度",
                labelWidth = 80,
                width = -1,
                min = 0f,
                max = 10f,
                step = 0.01f,
                defaultValue = 1f,
                value = setting.saturation,
                onChanged = value => { setting.saturation = value; SetDirty(); },
            });

            view.DrawHorizontalLine(Color.gray);

            view.DrawCurve("赤チャンネル", setting.redCurve, new Color(1f, 0.4f, 0.4f), SetDirty);
            view.DrawCurve("緑チャンネル", setting.greenCurve, new Color(0.4f, 1f, 0.4f), SetDirty);
            view.DrawCurve("青チャンネル", setting.blueCurve, new Color(0.4f, 0.6f, 1f), SetDirty);

            view.DrawHorizontalLine(Color.gray);

            view.DrawToggle("深度補正", setting.useDepthCorrection, 150, 20, value =>
            {
                setting.useDepthCorrection = value;
                SetDirty();
            });

            if (setting.useDepthCorrection)
            {
                view.DrawLabel("奥行きカーブの値で上のカーブと深度カーブを補間する", -1, 20);
                view.DrawCurve("奥行き", setting.zCurve, Color.white, SetDirty);
                view.DrawCurve("深度赤チャンネル", setting.depthRedCurve, new Color(1f, 0.4f, 0.4f), SetDirty);
                view.DrawCurve("深度緑チャンネル", setting.depthGreenCurve, new Color(0.4f, 1f, 0.4f), SetDirty);
                view.DrawCurve("深度青チャンネル", setting.depthBlueCurve, new Color(0.4f, 0.6f, 1f), SetDirty);

                // 深度補正と選択的色補正はゲーム側で同じシェーダーフィールドを取り合うため併用できない
                view.DrawLabel("※ 深度補正中は選択的色補正を使用できません", -1, 20, Color.yellow);
                return;
            }

            view.DrawToggle("選択的色補正", setting.selectiveCc, 150, 20, value =>
            {
                setting.selectiveCc = value;
                SetDirty();
            });

            if (setting.selectiveCc)
            {
                view.DrawColor(
                    view.GetColorFieldCache("補正元色", false),
                    setting.selectiveFromColor,
                    Color.white,
                    color => { setting.selectiveFromColor = color; SetDirty(); });

                view.DrawColor(
                    view.GetColorFieldCache("補正先色", false),
                    setting.selectiveToColor,
                    Color.white,
                    color => { setting.selectiveToColor = color; SetDirty(); });
            }
        }
    }
}
