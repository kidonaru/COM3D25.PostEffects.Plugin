using COM3D2.MotionTimelineEditor;
using UnityEngine;
// Assembly-UnityScript-firstpass のグローバル名前空間にも旧 DepthOfFieldScatter が残骸として存在するため、
// ゲームが実際に使う PostEffects_Dummy 側へエイリアスで束縛する
using DepthOfFieldEffect = PostEffects_Dummy.DepthOfFieldScatter;

namespace COM3D25.PostEffects.Plugin
{
    public class DepthOfFieldSetting
    {
        public bool enabled = false;
        public bool visualizeFocus = false;
        public float focalLength = 10f;
        public float focalSize = 0.05f;
        public float aperture = 11.5f;
        public float maxBlurSize = 2f;
        public bool highResolution = false;
        public bool nearBlur = false;
        public float foregroundOverlap = 1f;
        public DepthOfFieldEffect.BlurSampleCount blurSampleCount = DepthOfFieldEffect.BlurSampleCount.High;

        // DX11 ボケ (ボケの粒を点描画で重ねる。DX11 + コンピュートシェーダー対応環境でのみ有効)
        public bool useDX11Bokeh = false;
        public float dx11BokehScale = 1.2f;
        public float dx11BokehIntensity = 2.5f;
        public float dx11BokehThreshold = 0.5f;
        public float dx11SpawnHeuristic = 0.0875f;
        // 絶対パス、または Config フォルダからの相対パス
        public string dx11BokehTexturePath = "";

        // メイドの頭にフォーカスを追従させる (focalLength より優先)
        public bool maidFocus = false;
        // 準備完了メイド一覧の中のインデックス
        public int maidIndex = 0;
    }

    public class DepthOfFieldController : EffectControllerBase<DepthOfFieldEffect, DepthOfFieldSetting>
    {
        public override string effectName => "被写界深度";

        protected override DepthOfFieldSetting setting
        {
            get => settings.depthOfField;
            set => settings.depthOfField = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        private Transform _capturedFocalTransform;
        private Texture2D _capturedBokehTexture;

        private readonly TextureFileCache _bokehTextureCache = new TextureFileCache(TextureFileCache.SUB_DIR_BOKEH);

        protected override void ApplySetting(DepthOfFieldEffect component)
        {
            component.visualizeFocus = setting.visualizeFocus;
            component.focalLength = setting.focalLength;
            component.focalSize = setting.focalSize;
            component.aperture = setting.aperture;
            component.maxBlurSize = setting.maxBlurSize;
            component.highResolution = setting.highResolution;
            component.nearBlur = setting.nearBlur;
            component.foregroundOverlap = setting.foregroundOverlap;
            component.blurSampleCount = setting.blurSampleCount;
            component.focalTransform = setting.maidFocus ? GetMaidHeadTransform() : null;

            // DX11 用シェーダーはエフェクト側の CheckResources が Shader.Find で解決するため、ここでは触らない。
            // 非対応環境ではエフェクト側も通常ボケへ落ちるので、設定値は保ったまま適用だけ揃えておく
            component.blurType = setting.useDX11Bokeh && IsDX11Supported()
                ? DepthOfFieldEffect.BlurType.DX11
                : DepthOfFieldEffect.BlurType.DiscBlur;
            component.dx11BokehScale = setting.dx11BokehScale;
            component.dx11BokehIntensity = setting.dx11BokehIntensity;
            component.dx11BokehThreshhold = setting.dx11BokehThreshold;
            component.dx11SpawnHeuristic = setting.dx11SpawnHeuristic;
            component.dx11BokehTexture = _bokehTextureCache.GetOrLoad(setting.dx11BokehTexturePath);
        }

        private Transform GetMaidHeadTransform()
        {
            var maids = MTEUtils.GetReadyMaidList();
            if (maids.Count == 0)
            {
                return null;
            }

            var index = Mathf.Clamp(setting.maidIndex, 0, maids.Count - 1);
            var maid = maids[index];
            if (maid == null || maid.body0 == null)
            {
                return null;
            }
            return maid.body0.trsHead;
        }

        protected override void Capture(DepthOfFieldEffect component)
        {
            var c = _capturedSetting;
            _capturedEnabled = component.enabled;
            c.visualizeFocus = component.visualizeFocus;
            c.focalLength = component.focalLength;
            c.focalSize = component.focalSize;
            c.aperture = component.aperture;
            c.maxBlurSize = component.maxBlurSize;
            c.highResolution = component.highResolution;
            c.nearBlur = component.nearBlur;
            c.foregroundOverlap = component.foregroundOverlap;
            c.blurSampleCount = component.blurSampleCount;
            c.dx11BokehScale = component.dx11BokehScale;
            c.dx11BokehIntensity = component.dx11BokehIntensity;
            c.dx11BokehThreshold = component.dx11BokehThreshhold;
            c.dx11SpawnHeuristic = component.dx11SpawnHeuristic;
            c.useDX11Bokeh = component.blurType == DepthOfFieldEffect.BlurType.DX11;
            _capturedFocalTransform = component.focalTransform;
            _capturedBokehTexture = component.dx11BokehTexture;
        }

        protected override void RestoreSetting(DepthOfFieldEffect component)
        {
            var c = _capturedSetting;
            component.enabled = _capturedEnabled;
            component.visualizeFocus = c.visualizeFocus;
            component.focalLength = c.focalLength;
            component.focalSize = c.focalSize;
            component.aperture = c.aperture;
            component.maxBlurSize = c.maxBlurSize;
            component.highResolution = c.highResolution;
            component.nearBlur = c.nearBlur;
            component.foregroundOverlap = c.foregroundOverlap;
            component.blurSampleCount = c.blurSampleCount;
            component.focalTransform = _capturedFocalTransform;
            component.blurType = c.useDX11Bokeh
                ? DepthOfFieldEffect.BlurType.DX11
                : DepthOfFieldEffect.BlurType.DiscBlur;
            component.dx11BokehScale = c.dx11BokehScale;
            component.dx11BokehIntensity = c.dx11BokehIntensity;
            component.dx11BokehThreshhold = c.dx11BokehThreshold;
            component.dx11SpawnHeuristic = c.dx11SpawnHeuristic;
            component.dx11BokehTexture = _capturedBokehTexture;
        }

        private GUIComboBox<DepthOfFieldEffect.BlurSampleCount> _sampleComboBox
            = new GUIComboBox<DepthOfFieldEffect.BlurSampleCount>
        {
            items = MTEUtils.GetEnumValues<DepthOfFieldEffect.BlurSampleCount>(),
            getName = (count, _) => count.ToString(),
            buttonSize = new Vector2(100, 20),
        };

        private GUIComboBox<Maid> _maidComboBox = new GUIComboBox<Maid>
        {
            getName = (maid, _) => maid == null ? "未選択" : maid.status.fullNameJpStyle,
            buttonSize = new Vector2(150, 20),
        };

        public override void DrawContent(GUIView view)
        {
            view.DrawToggle("フォーカス位置を表示", setting.visualizeFocus, 200, 20, value =>
            {
                setting.visualizeFocus = value;
                SetDirty();
            });

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
                    label = "焦点距離",
                    labelWidth = 80,
                    width = -1,
                    min = 0f,
                    max = 50f,
                    step = 0.01f,
                    defaultValue = 10f,
                    value = setting.focalLength,
                    onChanged = value => { setting.focalLength = value; SetDirty(); },
                });
            }

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "焦点サイズ",
                labelWidth = 80,
                width = -1,
                min = 0f,
                max = 2f,
                step = 0.001f,
                defaultValue = 0.05f,
                value = setting.focalSize,
                onChanged = value => { setting.focalSize = value; SetDirty(); },
            });

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "絞り",
                labelWidth = 80,
                width = -1,
                min = 0f,
                max = 60f,
                step = 0.01f,
                defaultValue = 11.5f,
                value = setting.aperture,
                onChanged = value => { setting.aperture = value; SetDirty(); },
            });

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "最大ブラー",
                labelWidth = 80,
                width = -1,
                min = 0f,
                max = 20f,
                step = 0.01f,
                defaultValue = 2f,
                value = setting.maxBlurSize,
                onChanged = value => { setting.maxBlurSize = value; SetDirty(); },
            });

            view.BeginHorizontal();
            {
                view.DrawLabel("サンプル数", 80, 20);
                _sampleComboBox.currentIndex = (int)setting.blurSampleCount;
                _sampleComboBox.onSelected = (count, _) => { setting.blurSampleCount = count; SetDirty(); };
                _sampleComboBox.DrawButton(view);
            }
            view.EndLayout();

            view.DrawToggle("高解像度", setting.highResolution, 200, 20, value =>
            {
                setting.highResolution = value;
                SetDirty();
            });

            view.DrawToggle("近景ブラー", setting.nearBlur, 200, 20, value =>
            {
                setting.nearBlur = value;
                SetDirty();
            });

            if (setting.nearBlur)
            {
                view.DrawSliderValue(new GUIView.SliderOption
                {
                    label = "近景の重なり",
                    labelWidth = 80,
                    width = -1,
                    min = 0.1f,
                    max = 5f,
                    step = 0.01f,
                    defaultValue = 1f,
                    value = setting.foregroundOverlap,
                    onChanged = value => { setting.foregroundOverlap = value; SetDirty(); },
                });
            }

            view.DrawHorizontalLine(Color.gray);

            view.DrawToggle("DX11 ボケ", setting.useDX11Bokeh, 200, 20, value =>
            {
                setting.useDX11Bokeh = value;
                SetDirty();
            });

            if (!setting.useDX11Bokeh)
            {
                return;
            }

            if (!IsDX11Supported())
            {
                view.DrawLabel("この環境では DX11 ボケを利用できません", -1, 20, Color.red);
                return;
            }

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "ボケ拡大率",
                labelWidth = 80,
                width = -1,
                min = 0f,
                max = 50f,
                step = 0.01f,
                defaultValue = 1.2f,
                value = setting.dx11BokehScale,
                onChanged = value => { setting.dx11BokehScale = value; SetDirty(); },
            });

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "ボケ強度",
                labelWidth = 80,
                width = -1,
                min = 0f,
                max = 50f,
                step = 0.01f,
                defaultValue = 2.5f,
                value = setting.dx11BokehIntensity,
                onChanged = value => { setting.dx11BokehIntensity = value; SetDirty(); },
            });

            view.DrawSliderValue(new GUIView.SliderOption
            {
                // エフェクト側で 0.005〜4 にクランプされるため、スライダーもその範囲に合わせる
                label = "ボケしきい値",
                labelWidth = 80,
                width = -1,
                min = 0.005f,
                max = 4f,
                step = 0.005f,
                defaultValue = 0.5f,
                value = setting.dx11BokehThreshold,
                onChanged = value => { setting.dx11BokehThreshold = value; SetDirty(); },
            });

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "生成頻度",
                labelWidth = 80,
                width = -1,
                min = 0f,
                max = 1f,
                step = 0.001f,
                defaultValue = 0.0875f,
                value = setting.dx11SpawnHeuristic,
                onChanged = value => { setting.dx11SpawnHeuristic = value; SetDirty(); },
            });

            _bokehTextureCache.DrawPathField(view, "ボケテクスチャパス", setting.dx11BokehTexturePath,
                value => { setting.dx11BokehTexturePath = value; SetDirty(); });
        }

        // エフェクト側の PostEffectsBase.CheckSupport と同じ判定
        private static bool IsDX11Supported()
        {
            return SystemInfo.graphicsShaderLevel >= 50 && SystemInfo.supportsComputeShaders;
        }
    }
}
