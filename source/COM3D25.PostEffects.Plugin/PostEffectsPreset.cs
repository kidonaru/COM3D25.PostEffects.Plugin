using System.Collections.Generic;
using System.Reflection;
using COM3D2.MotionTimelineEditor;

namespace COM3D25.PostEffects.Plugin
{
    // プリセットとして保存するエフェクト設定一式
    // (動作設定・ウィンドウ位置・キーバインドは含めない)
    //
    // エフェクトを追加したときはここにフィールドを 1 行足すだけでよい。
    // EffectSettings との受け渡しは同名・同型のフィールドを突き合わせて行う
    public class PostEffectsPreset
    {
        public BloomSetting bloom = new BloomSetting();
        public GameEffectSetting gameEffect = new GameEffectSetting();
        public VignettingSetting vignetting = new VignettingSetting();
        public DepthOfFieldSetting depthOfField = new DepthOfFieldSetting();
        public GlobalFogSetting globalFog = new GlobalFogSetting();
        public BlurSetting blur = new BlurSetting();
        public ColorCorrectionCurvesSetting colorCorrectionCurves = new ColorCorrectionCurvesSetting();
        public ScreenOverlaySetting screenOverlay = new ScreenOverlaySetting();
        public SepiaSetting sepia = new SepiaSetting();
        public GrayscaleSetting grayscale = new GrayscaleSetting();
        public ContrastSetting contrast = new ContrastSetting();
        public MotionBlurSetting motionBlur = new MotionBlurSetting();
        public FisheyeSetting fisheye = new FisheyeSetting();
        public NoiseAndGrainSetting noiseAndGrain = new NoiseAndGrainSetting();
        public AnalogGlitchSetting analogGlitch = new AnalogGlitchSetting();
        public DigitalGlitchSetting digitalGlitch = new DigitalGlitchSetting();
        public RampSetting ramp = new RampSetting();
        public StreakSetting streak = new StreakSetting();
        public BokehSetting bokeh = new BokehSetting();
        public IsolineSetting isoline = new IsolineSetting();
        public ObscuranceSetting obscurance = new ObscuranceSetting();
        public EdgeDetectSetting edgeDetect = new EdgeDetectSetting();
        public CreaseSetting crease = new CreaseSetting();
        public SunShaftsSetting sunShafts = new SunShaftsSetting();
        public TiltShiftHdrSetting tiltShiftHdr = new TiltShiftHdrSetting();
        public AntialiasingSetting antialiasing = new AntialiasingSetting();
        public FilmicLetterBoxSetting filmicLetterBox = new FilmicLetterBoxSetting();
        public FilmicMedianFilterSetting filmicMedianFilter = new FilmicMedianFilterSetting();
        public CinematicLensAberrationsSetting cinematicLensAberrations = new CinematicLensAberrationsSetting();
        public CinematicBloomSetting cinematicBloom = new CinematicBloomSetting();
        public FilmicBloomSetting filmicBloom = new FilmicBloomSetting();
        public ColorCorrectionLutSetting colorCorrectionLut = new ColorCorrectionLutSetting();
        public StylisticFogSetting stylisticFog = new StylisticFogSetting();
        public FilmicBokehSetting filmicBokeh = new FilmicBokehSetting();
        public MaidHideSetting maidHide = new MaidHideSetting();
        public CinematicDepthOfFieldSetting cinematicDepthOfField = new CinematicDepthOfFieldSetting();
        public TonemappingColorGradingSetting tonemappingColorGrading = new TonemappingColorGradingSetting();
        public LightShaftsSetting lightShafts = new LightShaftsSetting();
        public DiffusionSetting diffusion = new DiffusionSetting();
        public CasSharpenSetting casSharpen = new CasSharpenSetting();
        public HalftoneSetting halftone = new HalftoneSetting();
        public WhiteBalanceSetting whiteBalance = new WhiteBalanceSetting();
        public RadialBlurSetting radialBlur = new RadialBlurSetting();
        public KuwaharaSetting kuwahara = new KuwaharaSetting();
        public ColorParaffinEffectSettings paraffin = new ColorParaffinEffectSettings();
        public DistanceFogEffectSettings distanceFog = new DistanceFogEffectSettings();
        public RimlightEffectSettings rimlight = new RimlightEffectSettings();
        public GTToneMapSetting gtToneMap = new GTToneMapSetting();

        private struct FieldPair
        {
            public FieldInfo presetField;
            public FieldInfo settingsField;
        }

        private static readonly List<FieldPair> _fieldPairs = BuildFieldPairs();

        private static List<FieldPair> BuildFieldPairs()
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
            var pairs = new List<FieldPair>();

            foreach (var presetField in typeof(PostEffectsPreset).GetFields(flags))
            {
                var settingsField = typeof(EffectSettings).GetField(presetField.Name, flags);
                if (settingsField == null || settingsField.FieldType != presetField.FieldType)
                {
                    MTEUtils.LogError("EffectSettings に対応するフィールドがありません: {0}", presetField.Name);
                    continue;
                }
                pairs.Add(new FieldPair { presetField = presetField, settingsField = settingsField });
            }

            return pairs;
        }

        public void CaptureFrom(EffectSettings settings)
        {
            foreach (var pair in _fieldPairs)
            {
                pair.presetField.SetValue(this, pair.settingsField.GetValue(settings));
            }
        }

        public void ApplyTo(EffectSettings settings)
        {
            foreach (var pair in _fieldPairs)
            {
                pair.settingsField.SetValue(settings, pair.presetField.GetValue(this));
            }
        }
    }
}
