using COM3D2.MotionTimelineEditor;
using UnityEngine;
using TonemappingEffect = COM3D25.PostEffects.Plugin.TonemappingColorGradingEffect;

namespace COM3D25.PostEffects.Plugin
{
    public class TonemappingColorGradingSetting
    {
        public bool enabled = false;

        public bool eyeAdaptationEnabled = false;
        public float eyeAdaptationMiddleGrey = 0.5f;
        public float eyeAdaptationMin = -0.1f;
        public float eyeAdaptationMax = 0.1f;
        public float eyeAdaptationSpeed = 1.5f;

        public bool tonemappingEnabled = false;
        public TonemappingEffect.Tonemapper tonemapper = TonemappingEffect.Tonemapper.Neutral;
        public float tonemappingExposure = 1f;
        public CurveData tonemappingCurve = CurveData.Linear();
        public float neutralBlackIn = 0.02f;
        public float neutralWhiteIn = 10f;
        public float neutralBlackOut = 0f;
        public float neutralWhiteOut = 10f;
        public float neutralWhiteLevel = 5.3f;
        public float neutralWhiteClip = 10f;

        public bool colorGradingEnabled = false;
        public Color shadows = Color.white;
        public Color midtones = Color.white;
        public Color highlights = Color.white;
        public float temperatureShift = 0f;
        public float tint = 0f;
        public float hue = 0f;
        public float saturation = 1f;
        public float vibrance = 0f;
        public float colorGradingValue = 1f;
        public float colorGradingContrast = 1f;
        public float colorGradingGain = 1f;
        public float colorGradingGamma = 1f;
        public bool useDithering = false;

        public bool channelMixerEnabled = false;
        public Vector3 channelMixerRed = new Vector3(1f, 0f, 0f);
        public Vector3 channelMixerGreen = new Vector3(0f, 1f, 0f);
        public Vector3 channelMixerBlue = new Vector3(0f, 0f, 1f);

        public bool curvesEnabled = false;
        public CurveData masterCurve = CurveData.Linear();
        public CurveData redCurve = CurveData.Linear();
        public CurveData greenCurve = CurveData.Linear();
        public CurveData blueCurve = CurveData.Linear();

        public bool userLutEnabled = false;
        public float userLutContribution = 0f;
        // 絶対パス、または Config フォルダからの相対パス
        public string userLutPath = "";
    }

    public class TonemappingColorGradingController
        : EffectControllerBase<TonemappingColorGradingEffect, TonemappingColorGradingSetting>
    {
        public override string effectName => "トーンマッピング";

        private static readonly Vector3 IdentityMixerRed = new Vector3(1f, 0f, 0f);
        private static readonly Vector3 IdentityMixerGreen = new Vector3(0f, 1f, 0f);
        private static readonly Vector3 IdentityMixerBlue = new Vector3(0f, 0f, 1f);

        protected override TonemappingColorGradingSetting setting
        {
            get => settings.tonemappingColorGrading;
            set => settings.tonemappingColorGrading = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        // LUT は画素値をそのまま座標として使うためガンマ補正をかけずに読み込む
        private readonly TextureFileCache _userLutCache = new TextureFileCache(TextureFileCache.SUB_DIR_LUT, linear: true);

        // カーブの本数 (全体 / 赤 / 緑 / 青 / トーンカーブ)
        private const int CurveCount = 5;

        // カーブテクスチャの焼き直しは重いので、値が変わったフレームだけ知らせる。
        // ApplySetting は毎フレーム走るため、比較用の配列は使い回して確保を避ける
        private readonly CurveData[] _curves = new CurveData[CurveCount];
        private readonly CurveData[] _lastCurves = new CurveData[CurveCount];
        private readonly int[] _lastVersions = new int[CurveCount];

        private CurveData[] CollectCurves()
        {
            var s = setting;
            _curves[0] = s.masterCurve;
            _curves[1] = s.redCurve;
            _curves[2] = s.greenCurve;
            _curves[3] = s.blueCurve;
            _curves[4] = s.tonemappingCurve;
            return _curves;
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

        private void InvalidateCurveCache()
        {
            for (var i = 0; i < _lastCurves.Length; i++)
            {
                _lastCurves[i] = null;
            }
        }

        public override void ResetSetting()
        {
            base.ResetSetting();
            // 新しい設定のカーブは version が 0 に戻るため、バージョン比較を確実に外す
            InvalidateCurveCache();
        }

        protected override void ApplySetting(TonemappingColorGradingEffect component)
        {
            if (component.shader == null)
            {
                component.shader = EffectShaders.GetShader(EffectShaders.Cinematic, "tonemappingcolorgradingshader");
            }

            component.eyeAdaptationEnabled = setting.eyeAdaptationEnabled;
            component.eyeAdaptationMiddleGrey = setting.eyeAdaptationMiddleGrey;
            component.eyeAdaptationMin = setting.eyeAdaptationMin;
            component.eyeAdaptationMax = setting.eyeAdaptationMax;
            component.eyeAdaptationSpeed = setting.eyeAdaptationSpeed;

            component.tonemappingEnabled = setting.tonemappingEnabled;
            component.tonemapper = setting.tonemapper;
            component.tonemappingExposure = setting.tonemappingExposure;
            component.neutralBlackIn = setting.neutralBlackIn;
            component.neutralWhiteIn = setting.neutralWhiteIn;
            component.neutralBlackOut = setting.neutralBlackOut;
            component.neutralWhiteOut = setting.neutralWhiteOut;
            component.neutralWhiteLevel = setting.neutralWhiteLevel;
            component.neutralWhiteClip = setting.neutralWhiteClip;

            component.colorGradingEnabled = setting.colorGradingEnabled;
            component.shadows = setting.shadows;
            component.midtones = setting.midtones;
            component.highlights = setting.highlights;
            component.temperatureShift = setting.temperatureShift;
            component.tint = setting.tint;
            component.hue = setting.hue;
            component.saturation = setting.saturation;
            component.vibrance = setting.vibrance;
            component.colorGradingValue = setting.colorGradingValue;
            component.colorGradingContrast = setting.colorGradingContrast;
            component.colorGradingGain = setting.colorGradingGain;
            component.colorGradingGamma = setting.colorGradingGamma;
            component.useDithering = setting.useDithering;

            // 個別のトグルが off のときは、その機能が何もしない値を流し込む
            component.channelMixerRed = setting.channelMixerEnabled ? setting.channelMixerRed : IdentityMixerRed;
            component.channelMixerGreen = setting.channelMixerEnabled ? setting.channelMixerGreen : IdentityMixerGreen;
            component.channelMixerBlue = setting.channelMixerEnabled ? setting.channelMixerBlue : IdentityMixerBlue;

            component.userLutEnabled = setting.userLutEnabled;
            component.userLutContribution = setting.userLutContribution;
            component.userLut = setting.userLutEnabled ? _userLutCache.GetOrLoad(setting.userLutPath) : null;

            if (IsCurveDirty())
            {
                var curves = CollectCurves();
                component.masterCurve = setting.curvesEnabled ? curves[0].ToAnimationCurve() : null;
                component.redCurve = setting.curvesEnabled ? curves[1].ToAnimationCurve() : null;
                component.greenCurve = setting.curvesEnabled ? curves[2].ToAnimationCurve() : null;
                component.blueCurve = setting.curvesEnabled ? curves[3].ToAnimationCurve() : null;
                component.tonemappingCurve = curves[4].ToAnimationCurve();
                component.SetCurvesDirty();

                for (var i = 0; i < curves.Length; i++)
                {
                    _lastCurves[i] = curves[i];
                    _lastVersions[i] = curves[i].version;
                }
            }
        }

        // このエフェクトの型はゲーム側に存在せず、常に自分で AddComponent するため
        // Capture / RestoreSetting は実際には到達しない。有効・無効の別と主要な切替だけを保持する
        protected override void Capture(TonemappingColorGradingEffect component)
        {
            var c = _capturedSetting;
            _capturedEnabled = component.enabled;
            c.eyeAdaptationEnabled = component.eyeAdaptationEnabled;
            c.tonemappingEnabled = component.tonemappingEnabled;
            c.tonemapper = component.tonemapper;
            c.tonemappingExposure = component.tonemappingExposure;
            c.colorGradingEnabled = component.colorGradingEnabled;
            c.userLutEnabled = component.userLutEnabled;
        }

        protected override void RestoreSetting(TonemappingColorGradingEffect component)
        {
            var c = _capturedSetting;
            component.enabled = _capturedEnabled;
            component.eyeAdaptationEnabled = c.eyeAdaptationEnabled;
            component.tonemappingEnabled = c.tonemappingEnabled;
            component.tonemapper = c.tonemapper;
            component.tonemappingExposure = c.tonemappingExposure;
            component.colorGradingEnabled = c.colorGradingEnabled;
            component.userLutEnabled = c.userLutEnabled;
            component.userLut = null;
            InvalidateCurveCache();
        }

        private readonly GUIComboBox<TonemappingEffect.Tonemapper> _tonemapperComboBox =
            new GUIComboBox<TonemappingEffect.Tonemapper>
            {
                items = MTEUtils.GetEnumValues<TonemappingEffect.Tonemapper>(),
                getName = (mapper, _) => mapper.ToString(),
                buttonSize = new Vector2(120, 20),
            };

        public override void DrawContent(GUIView view)
        {
            DrawTonemappingSection(view);
            view.DrawHorizontalLine(Color.gray);
            DrawColorGradingSection(view);
            view.DrawHorizontalLine(Color.gray);
            DrawEyeAdaptationSection(view);
            view.DrawHorizontalLine(Color.gray);
            DrawUserLutSection(view);
        }

        private void DrawTonemappingSection(GUIView view)
        {
            view.DrawToggle("トーンマッピング", setting.tonemappingEnabled, 250, 20, value =>
            {
                setting.tonemappingEnabled = value;
                SetDirty();
            });

            if (!setting.tonemappingEnabled)
            {
                return;
            }

            view.BeginHorizontal();
            {
                view.DrawLabel("方式", 70, 20);
                _tonemapperComboBox.currentIndex = (int)setting.tonemapper;
                _tonemapperComboBox.onSelected = (mapper, _) => { setting.tonemapper = mapper; SetDirty(); };
                _tonemapperComboBox.DrawButton(view);
            }
            view.EndLayout();

            DrawSlider(view, "露出", 0f, 10f, 1f,
                setting.tonemappingExposure, v => setting.tonemappingExposure = v);

            if (setting.tonemapper == TonemappingEffect.Tonemapper.Curve)
            {
                view.DrawCurve("トーンカーブ", setting.tonemappingCurve, Color.white, SetDirty);
            }
            else if (setting.tonemapper == TonemappingEffect.Tonemapper.Neutral)
            {
                DrawSlider(view, "黒レベル入力", -0.1f, 0.1f, 0.02f,
                    setting.neutralBlackIn, v => setting.neutralBlackIn = v, 0.001f);
                DrawSlider(view, "白レベル入力", 1f, 20f, 10f,
                    setting.neutralWhiteIn, v => setting.neutralWhiteIn = v);
                DrawSlider(view, "黒レベル出力", -0.09f, 0.1f, 0f,
                    setting.neutralBlackOut, v => setting.neutralBlackOut = v, 0.001f);
                DrawSlider(view, "白レベル出力", 1f, 19f, 10f,
                    setting.neutralWhiteOut, v => setting.neutralWhiteOut = v);
                DrawSlider(view, "白の強さ", 0.1f, 20f, 5.3f,
                    setting.neutralWhiteLevel, v => setting.neutralWhiteLevel = v);
                DrawSlider(view, "白のクリップ", 1f, 10f, 10f,
                    setting.neutralWhiteClip, v => setting.neutralWhiteClip = v);
            }
        }

        private void DrawColorGradingSection(GUIView view)
        {
            view.DrawToggle("カラーグレーディング", setting.colorGradingEnabled, 250, 20, value =>
            {
                setting.colorGradingEnabled = value;
                SetDirty();
            });

            if (!setting.colorGradingEnabled)
            {
                return;
            }

            DrawSlider(view, "色温度", -1f, 1f, 0f, setting.temperatureShift, v => setting.temperatureShift = v);
            DrawSlider(view, "色偏差", -1f, 1f, 0f, setting.tint, v => setting.tint = v);
            DrawSlider(view, "色相", -0.5f, 0.5f, 0f, setting.hue, v => setting.hue = v);
            DrawSlider(view, "彩度", 0f, 3f, 1f, setting.saturation, v => setting.saturation = v);
            DrawSlider(view, "自然な彩度", -1f, 1f, 0f, setting.vibrance, v => setting.vibrance = v);
            DrawSlider(view, "明度", 0f, 3f, 1f, setting.colorGradingValue, v => setting.colorGradingValue = v);
            DrawSlider(view, "コントラスト", 0f, 3f, 1f,
                setting.colorGradingContrast, v => setting.colorGradingContrast = v);
            DrawSlider(view, "ゲイン", 0f, 3f, 1f, setting.colorGradingGain, v => setting.colorGradingGain = v);
            DrawSlider(view, "ガンマ", 0.01f, 3f, 1f, setting.colorGradingGamma, v => setting.colorGradingGamma = v);

            view.DrawColor(
                view.GetColorFieldCache("シャドウ", false),
                setting.shadows, Color.white,
                color => { setting.shadows = color; SetDirty(); });
            view.DrawColor(
                view.GetColorFieldCache("ミッドトーン", false),
                setting.midtones, Color.white,
                color => { setting.midtones = color; SetDirty(); });
            view.DrawColor(
                view.GetColorFieldCache("ハイライト", false),
                setting.highlights, Color.white,
                color => { setting.highlights = color; SetDirty(); });

            view.DrawToggle("バンディング低減 (ディザ)", setting.useDithering, 250, 20, value =>
            {
                setting.useDithering = value;
                SetDirty();
            });

            view.DrawToggle("チャンネルミキサー", setting.channelMixerEnabled, 250, 20, value =>
            {
                setting.channelMixerEnabled = value;
                SetDirty();
            });

            if (setting.channelMixerEnabled)
            {
                DrawChannelMixer(view, "赤の出力", setting.channelMixerRed, IdentityMixerRed,
                    v => setting.channelMixerRed = v);
                DrawChannelMixer(view, "緑の出力", setting.channelMixerGreen, IdentityMixerGreen,
                    v => setting.channelMixerGreen = v);
                DrawChannelMixer(view, "青の出力", setting.channelMixerBlue, IdentityMixerBlue,
                    v => setting.channelMixerBlue = v);
            }

            view.DrawToggle("カーブ", setting.curvesEnabled, 250, 20, value =>
            {
                setting.curvesEnabled = value;
                // カーブの有効・無効はコンポーネントへの受け渡し方を変えるので焼き直しが要る
                InvalidateCurveCache();
                SetDirty();
            });

            if (setting.curvesEnabled)
            {
                view.DrawCurve("全体", setting.masterCurve, Color.white, SetDirty);
                view.DrawCurve("赤チャンネル", setting.redCurve, new Color(1f, 0.4f, 0.4f), SetDirty);
                view.DrawCurve("緑チャンネル", setting.greenCurve, new Color(0.4f, 1f, 0.4f), SetDirty);
                view.DrawCurve("青チャンネル", setting.blueCurve, new Color(0.4f, 0.6f, 1f), SetDirty);
            }
        }

        // 出力チャンネル 1 本ぶんの R/G/B 配合比
        private void DrawChannelMixer(
            GUIView view, string label, Vector3 value, Vector3 defaultValue, System.Action<Vector3> onChanged)
        {
            view.DrawLabel(label, -1, 20);
            DrawSlider(view, "  R", -2f, 2f, defaultValue.x, value.x,
                v => onChanged(new Vector3(v, value.y, value.z)));
            DrawSlider(view, "  G", -2f, 2f, defaultValue.y, value.y,
                v => onChanged(new Vector3(value.x, v, value.z)));
            DrawSlider(view, "  B", -2f, 2f, defaultValue.z, value.z,
                v => onChanged(new Vector3(value.x, value.y, v)));
        }

        private void DrawEyeAdaptationSection(GUIView view)
        {
            view.DrawToggle("目の順応", setting.eyeAdaptationEnabled, 250, 20, value =>
            {
                setting.eyeAdaptationEnabled = value;
                SetDirty();
            });

            if (!setting.eyeAdaptationEnabled)
            {
                return;
            }

            DrawSlider(view, "中間グレー", 0.001f, 1f, 0.5f,
                setting.eyeAdaptationMiddleGrey, v => setting.eyeAdaptationMiddleGrey = v);
            DrawSlider(view, "最小輝度 (EV)", -8f, 8f, -0.1f,
                setting.eyeAdaptationMin, v => setting.eyeAdaptationMin = v);
            DrawSlider(view, "最大輝度 (EV)", -8f, 8f, 0.1f,
                setting.eyeAdaptationMax, v => setting.eyeAdaptationMax = v);
            DrawSlider(view, "順応速度", 0.001f, 10f, 1.5f,
                setting.eyeAdaptationSpeed, v => setting.eyeAdaptationSpeed = v);
        }

        private void DrawUserLutSection(GUIView view)
        {
            view.DrawToggle("ユーザー LUT", setting.userLutEnabled, 250, 20, value =>
            {
                setting.userLutEnabled = value;
                SetDirty();
            });

            if (!setting.userLutEnabled)
            {
                return;
            }

            _userLutCache.DrawPathField(view, "LUT テクスチャパス", setting.userLutPath,
                value => { setting.userLutPath = value; SetDirty(); });
            view.DrawLabel("横一列に並んだ LUT 画像 (幅 = 高さの 2 乗。例: 1024x32)", -1, 20);
            DrawSlider(view, "適用量", 0f, 1f, 0f,
                setting.userLutContribution, v => setting.userLutContribution = v);
        }
    }
}
