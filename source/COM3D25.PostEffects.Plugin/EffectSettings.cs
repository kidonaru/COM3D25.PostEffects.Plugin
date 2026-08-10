namespace COM3D25.PostEffects.Plugin
{
    // ランタイム専用のエフェクト設定一式。config.xml には永続化しない
    // (起動時に必ずプリセットで上書きされるため、永続化はプリセットファイルだけが担う)。
    // MTE 等の外部プラグインがタイムライン再生値を毎フレーム書き込む先もここになる
    //
    // エフェクトを追加したときはここと PostEffectsPreset にフィールドを 1 行ずつ足す
    public class EffectSettings
    {
        public static EffectSettings instance { get; } = new EffectSettings();

        // プリセット保存されていない変更があるか。現状は書き込みのみで参照箇所はない
        // (将来の未保存インジケーター表示・MTE 側の変更検知用に意味だけ維持している)
        public bool dirty = false;

        public BloomSetting bloom = new BloomSetting();
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
        public GameEffectSetting gameEffect = new GameEffectSetting();
    }
}
