using COM3D2.MotionTimelineEditor;
using UnityEngine;
using CinematicDof = COM3D25.PostEffects.Plugin.CinematicDepthOfFieldEffect;

namespace COM3D25.PostEffects.Plugin
{
    public class CinematicDepthOfFieldSetting
    {
        public bool enabled = false;
        public bool visualizeFocus = false;
        public CinematicDof.TweakMode tweakMode = CinematicDof.TweakMode.Explicit;
        public CinematicDof.QualityPreset filteringQuality = CinematicDof.QualityPreset.High;
        public CinematicDof.ApertureShape apertureShape = CinematicDof.ApertureShape.Circular;
        public float apertureOrientation = 0f;

        public float focusFocusPlane = 20f;
        public float focusRange = 35f;
        public float focusNearPlane = 3f;
        public float focusNearFalloff = 3f;
        public float focusFarPlane = 6f;
        public float focusFarFalloff = 6f;
        public float focusNearBlurRadius = 18f;
        public float focusFarBlurRadius = 20f;

        public bool antiFlicker = false;
        public bool useBokehTexture = false;
        public float bokehScale = 1f;
        public float bokehIntensity = 50f;
        public float bokehThreshold = 2f;
        public float bokehSpawnHeuristic = 0.15f;
        // 絶対パス、または Config フォルダからの相対パス
        public string bokehTexturePath = "";

        // メイドの頭にピントを合わせる (ピント面と範囲モードのみ)
        public bool maidFocus = false;
        public int maidIndex = 0;
    }

    public class CinematicDepthOfFieldController
        : EffectControllerBase<CinematicDepthOfFieldEffect, CinematicDepthOfFieldSetting>
    {
        public override string effectName => "シネマティック被写界深度";

        protected override CinematicDepthOfFieldSetting setting
        {
            get => settings.cinematicDepthOfField;
            set => settings.cinematicDepthOfField = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        private readonly TextureFileCache _bokehTextureCache = new TextureFileCache(TextureFileCache.SUB_DIR_BOKEH);

        protected override void ApplySetting(CinematicDepthOfFieldEffect component)
        {
            if (component.shader == null)
            {
                component.shader = EffectShaders.GetShader(EffectShaders.Cinematic, "cinematicdepthoffieldshader");
            }
            if (component.medianFilterShader == null)
            {
                component.medianFilterShader = EffectShaders.GetShader(EffectShaders.Cinematic, "medianfiltershader");
            }
            if (component.bokehSplattingShader == null)
            {
                component.bokehSplattingShader = EffectShaders.GetShader(EffectShaders.Cinematic, "bokehsplattingshader");
            }

            component.visualizeFocus = setting.visualizeFocus;
            component.tweakMode = setting.tweakMode;
            component.filteringQuality = setting.filteringQuality;
            component.apertureShape = setting.apertureShape;
            component.apertureOrientation = setting.apertureOrientation;
            component.focusFocusPlane = setting.focusFocusPlane;
            component.focusRange = setting.focusRange;
            component.focusNearPlane = setting.focusNearPlane;
            component.focusNearFalloff = setting.focusNearFalloff;
            component.focusFarPlane = setting.focusFarPlane;
            component.focusFarFalloff = setting.focusFarFalloff;
            component.focusNearBlurRadius = setting.focusNearBlurRadius;
            component.focusFarBlurRadius = setting.focusFarBlurRadius;
            component.antiFlicker = setting.antiFlicker;
            component.useBokehTexture = setting.useBokehTexture;
            component.bokehScale = setting.bokehScale;
            component.bokehIntensity = setting.bokehIntensity;
            component.bokehThreshold = setting.bokehThreshold;
            component.bokehSpawnHeuristic = setting.bokehSpawnHeuristic;
            component.bokehTexture = setting.useBokehTexture
                ? _bokehTextureCache.GetOrLoad(setting.bokehTexturePath)
                : null;

            // ピント追従はピント面と範囲で指定するモードでのみ意味がある
            component.focusTransform =
                setting.maidFocus && setting.tweakMode == CinematicDof.TweakMode.Range
                    ? GetMaidHeadTransform()
                    : null;
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

        protected override void Capture(CinematicDepthOfFieldEffect component)
        {
            var c = _capturedSetting;
            _capturedEnabled = component.enabled;
            c.visualizeFocus = component.visualizeFocus;
            c.tweakMode = component.tweakMode;
            c.filteringQuality = component.filteringQuality;
            c.apertureShape = component.apertureShape;
            c.apertureOrientation = component.apertureOrientation;
            c.focusFocusPlane = component.focusFocusPlane;
            c.focusRange = component.focusRange;
            c.focusNearPlane = component.focusNearPlane;
            c.focusNearFalloff = component.focusNearFalloff;
            c.focusFarPlane = component.focusFarPlane;
            c.focusFarFalloff = component.focusFarFalloff;
            c.focusNearBlurRadius = component.focusNearBlurRadius;
            c.focusFarBlurRadius = component.focusFarBlurRadius;
            c.antiFlicker = component.antiFlicker;
            c.useBokehTexture = component.useBokehTexture;
            c.bokehScale = component.bokehScale;
            c.bokehIntensity = component.bokehIntensity;
            c.bokehThreshold = component.bokehThreshold;
            c.bokehSpawnHeuristic = component.bokehSpawnHeuristic;
        }

        protected override void RestoreSetting(CinematicDepthOfFieldEffect component)
        {
            var c = _capturedSetting;
            component.enabled = _capturedEnabled;
            component.visualizeFocus = c.visualizeFocus;
            component.tweakMode = c.tweakMode;
            component.filteringQuality = c.filteringQuality;
            component.apertureShape = c.apertureShape;
            component.apertureOrientation = c.apertureOrientation;
            component.focusFocusPlane = c.focusFocusPlane;
            component.focusRange = c.focusRange;
            component.focusNearPlane = c.focusNearPlane;
            component.focusNearFalloff = c.focusNearFalloff;
            component.focusFarPlane = c.focusFarPlane;
            component.focusFarFalloff = c.focusFarFalloff;
            component.focusNearBlurRadius = c.focusNearBlurRadius;
            component.focusFarBlurRadius = c.focusFarBlurRadius;
            component.antiFlicker = c.antiFlicker;
            component.useBokehTexture = c.useBokehTexture;
            component.bokehScale = c.bokehScale;
            component.bokehIntensity = c.bokehIntensity;
            component.bokehThreshold = c.bokehThreshold;
            component.bokehSpawnHeuristic = c.bokehSpawnHeuristic;
            component.bokehTexture = null;
            component.focusTransform = null;
        }

        private readonly GUIComboBox<CinematicDof.TweakMode> _tweakModeComboBox =
            new GUIComboBox<CinematicDof.TweakMode>
            {
                items = MTEUtils.GetEnumValues<CinematicDof.TweakMode>(),
                getName = (mode, _) => mode == CinematicDof.TweakMode.Range ? "ピント面と範囲" : "近景・遠景を個別",
                buttonSize = new Vector2(140, 20),
            };

        private readonly GUIComboBox<CinematicDof.QualityPreset> _qualityComboBox =
            new GUIComboBox<CinematicDof.QualityPreset>
            {
                items = MTEUtils.GetEnumValues<CinematicDof.QualityPreset>(),
                getName = (quality, _) => quality.ToString(),
                buttonSize = new Vector2(100, 20),
            };

        private readonly GUIComboBox<CinematicDof.ApertureShape> _apertureComboBox =
            new GUIComboBox<CinematicDof.ApertureShape>
            {
                items = MTEUtils.GetEnumValues<CinematicDof.ApertureShape>(),
                getName = (shape, _) => GetApertureShapeName(shape),
                buttonSize = new Vector2(100, 20),
            };

        private static string GetApertureShapeName(CinematicDof.ApertureShape shape)
        {
            switch (shape)
            {
                case CinematicDof.ApertureShape.Circular: return "円形";
                case CinematicDof.ApertureShape.Hexagonal: return "六角形";
                case CinematicDof.ApertureShape.Octogonal: return "八角形";
                default: return shape.ToString();
            }
        }

        private readonly GUIComboBox<Maid> _maidComboBox = new GUIComboBox<Maid>
        {
            getName = (maid, _) => maid == null ? "未選択" : maid.status.fullNameJpStyle,
            buttonSize = new Vector2(180, 20),
        };

        public override void DrawContent(GUIView view)
        {
            view.BeginHorizontal();
            {
                view.DrawLabel("ピント指定", 90, 20);
                _tweakModeComboBox.currentIndex = (int)setting.tweakMode;
                _tweakModeComboBox.onSelected = (mode, _) => { setting.tweakMode = mode; SetDirty(); };
                _tweakModeComboBox.DrawButton(view);
            }
            view.EndLayout();

            view.BeginHorizontal();
            {
                view.DrawLabel("品質", 90, 20);
                _qualityComboBox.currentIndex = (int)setting.filteringQuality;
                _qualityComboBox.onSelected = (quality, _) => { setting.filteringQuality = quality; SetDirty(); };
                _qualityComboBox.DrawButton(view);
            }
            view.EndLayout();

            view.BeginHorizontal();
            {
                view.DrawLabel("絞りの形", 90, 20);
                _apertureComboBox.currentIndex = (int)setting.apertureShape;
                _apertureComboBox.onSelected = (shape, _) => { setting.apertureShape = shape; SetDirty(); };
                _apertureComboBox.DrawButton(view);
            }
            view.EndLayout();

            // 絞りの向きは方向性ぼかしを使う形状でのみ効く
            if (setting.apertureShape != CinematicDof.ApertureShape.Circular)
            {
                DrawSlider(view, "絞りの向き", 0f, 180f, 0f,
                    setting.apertureOrientation, v => setting.apertureOrientation = v, 1f);
            }

            if (setting.tweakMode == CinematicDof.TweakMode.Range)
            {
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
                    DrawSlider(view, "ピント面", 0f, 60f, 20f,
                        setting.focusFocusPlane, v => setting.focusFocusPlane = v);
                }

                DrawSlider(view, "ピント範囲", 0f, 50f, 35f, setting.focusRange, v => setting.focusRange = v);
            }
            else
            {
                DrawSlider(view, "近景の境界", 0f, 60f, 3f,
                    setting.focusNearPlane, v => setting.focusNearPlane = v);
            }

            DrawSlider(view, "近景の減衰", 0f, 60f, 3f,
                setting.focusNearFalloff, v => setting.focusNearFalloff = v);
            DrawSlider(view, "近景のぼけ半径", 0f, 100f, 18f,
                setting.focusNearBlurRadius, v => setting.focusNearBlurRadius = v);

            if (setting.tweakMode == CinematicDof.TweakMode.Explicit)
            {
                DrawSlider(view, "遠景の境界", 0f, 60f, 6f,
                    setting.focusFarPlane, v => setting.focusFarPlane = v);
            }

            DrawSlider(view, "遠景の減衰", 0f, 60f, 6f,
                setting.focusFarFalloff, v => setting.focusFarFalloff = v);
            DrawSlider(view, "遠景のぼけ半径", 0f, 100f, 20f,
                setting.focusFarBlurRadius, v => setting.focusFarBlurRadius = v);

            // ちらつき対策は品質 Medium 以上のときにメディアンフィルタを有効にするスイッチ
            view.DrawToggle("ちらつき対策", setting.antiFlicker, 250, 20, value =>
            {
                setting.antiFlicker = value;
                SetDirty();
            });

            view.DrawToggle("ピント位置を可視化", setting.visualizeFocus, 250, 20, value =>
            {
                setting.visualizeFocus = value;
                SetDirty();
            });

            if (!CinematicDepthOfFieldEffect.supportsTextureBokeh)
            {
                view.DrawLabel("この環境ではテクスチャボケ (DX11) を使用できません", -1, 20, Color.yellow);
                return;
            }

            view.DrawToggle("テクスチャボケ", setting.useBokehTexture, 250, 20, value =>
            {
                setting.useBokehTexture = value;
                SetDirty();
            });

            if (setting.useBokehTexture)
            {
                _bokehTextureCache.DrawPathField(view, "ボケテクスチャパス", setting.bokehTexturePath,
                    value => { setting.bokehTexturePath = value; SetDirty(); });
                DrawSlider(view, "ボケの大きさ", 0f, 20f, 1f, setting.bokehScale, v => setting.bokehScale = v);
                DrawSlider(view, "ボケの強さ", 0f, 400f, 50f, setting.bokehIntensity, v => setting.bokehIntensity = v);
                DrawSlider(view, "しきい値", 0f, 5f, 2f, setting.bokehThreshold, v => setting.bokehThreshold = v);
                DrawSlider(view, "発生しやすさ", 0f, 1f, 0.15f,
                    setting.bokehSpawnHeuristic, v => setting.bokehSpawnHeuristic = v);
            }
        }
    }
}
