using System;
using System.Collections.Generic;
using COM3D2.MotionTimelineEditor;

namespace COM3D25.PostEffects.Plugin
{
    public class PostEffectManager : ManagerBase
    {
        public List<EffectControllerBase> controllers = new List<EffectControllerBase>();

        // 全エフェクトの一時的な無効化スイッチ。
        // 各エフェクトの有効状態は保ったまま適用だけを止めるため、保存対象にはしない
        public bool effectsEnabled = true;

        // 前フレームで適用していたかをコントローラごとに記録し、無効化されたフレームで復元する
        private HashSet<EffectControllerBase> _appliedControllers = new HashSet<EffectControllerBase>();

        private static PostEffectManager _instance = null;
        public static PostEffectManager instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new PostEffectManager();
                }
                return _instance;
            }
        }

        private PostEffectManager()
        {
        }

        // ID → コントローラの索引。外部連携 (MTE 等) のセーブデータキーにも使う
        private Dictionary<string, EffectControllerBase> _controllerById =
            new Dictionary<string, EffectControllerBase>();

        // 登録順がそのまま UI の表示順になるためカテゴリごとにまとめる
        public override void Init()
        {
            Add(EffectCategory.ColorTone, nameof(EffectSettings.sepia), new SepiaController());
            Add(EffectCategory.ColorTone, nameof(EffectSettings.grayscale), new GrayscaleController());
            Add(EffectCategory.ColorTone, nameof(EffectSettings.contrast), new ContrastController());
            Add(EffectCategory.ColorTone, nameof(EffectSettings.colorCorrectionCurves), new ColorCorrectionCurvesController());
            Add(EffectCategory.ColorTone, nameof(EffectSettings.ramp), new RampController());
            Add(EffectCategory.ColorTone, nameof(EffectSettings.colorCorrectionLut), new ColorCorrectionLutController());
            Add(EffectCategory.ColorTone, nameof(EffectSettings.tonemappingColorGrading), new TonemappingColorGradingController());
            Add(EffectCategory.ColorTone, nameof(EffectSettings.whiteBalance), new WhiteBalanceController());
            Add(EffectCategory.ColorTone, nameof(EffectSettings.paraffin), new ParaffinController());
            Add(EffectCategory.ColorTone, nameof(EffectSettings.gtToneMap), new GTToneMapController());

            Add(EffectCategory.Bloom, nameof(EffectSettings.bloom), new BloomController());
            Add(EffectCategory.Bloom, nameof(EffectSettings.cinematicBloom), new CinematicBloomController());
            Add(EffectCategory.Bloom, nameof(EffectSettings.filmicBloom), new FilmicBloomController());
            Add(EffectCategory.Bloom, nameof(EffectSettings.streak), new StreakController());
            Add(EffectCategory.Bloom, nameof(EffectSettings.sunShafts), new SunShaftsController());
            Add(EffectCategory.Bloom, nameof(EffectSettings.lightShafts), new LightShaftsController());
            Add(EffectCategory.Bloom, nameof(EffectSettings.diffusion), new DiffusionController());

            Add(EffectCategory.Dof, nameof(EffectSettings.depthOfField), new DepthOfFieldController());
            Add(EffectCategory.Dof, nameof(EffectSettings.bokeh), new BokehController());
            Add(EffectCategory.Dof, nameof(EffectSettings.filmicBokeh), new FilmicBokehController());
            Add(EffectCategory.Dof, nameof(EffectSettings.cinematicDepthOfField), new CinematicDepthOfFieldController());
            Add(EffectCategory.Dof, nameof(EffectSettings.blur), new BlurController());
            Add(EffectCategory.Dof, nameof(EffectSettings.tiltShiftHdr), new TiltShiftHdrController());
            Add(EffectCategory.Dof, nameof(EffectSettings.radialBlur), new RadialBlurController());
            Add(EffectCategory.Dof, nameof(EffectSettings.filmicMedianFilter), new FilmicMedianFilterController());

            Add(EffectCategory.EdgeLine, nameof(EffectSettings.edgeDetect), new EdgeDetectController());
            Add(EffectCategory.EdgeLine, nameof(EffectSettings.crease), new CreaseController());
            Add(EffectCategory.EdgeLine, nameof(EffectSettings.isoline), new IsolineController());
            Add(EffectCategory.EdgeLine, nameof(EffectSettings.halftone), new HalftoneController());
            Add(EffectCategory.EdgeLine, nameof(EffectSettings.kuwahara), new KuwaharaController());

            Add(EffectCategory.Noise, nameof(EffectSettings.noiseAndGrain), new NoiseAndGrainController());
            Add(EffectCategory.Noise, nameof(EffectSettings.analogGlitch), new AnalogGlitchController());
            Add(EffectCategory.Noise, nameof(EffectSettings.digitalGlitch), new DigitalGlitchController());

            Add(EffectCategory.Lens, nameof(EffectSettings.fisheye), new FisheyeController());
            Add(EffectCategory.Lens, nameof(EffectSettings.cinematicLensAberrations), new CinematicLensAberrationsController());
            Add(EffectCategory.Lens, nameof(EffectSettings.vignetting), new VignettingController());

            Add(EffectCategory.Fog, nameof(EffectSettings.globalFog), new GlobalFogController());
            Add(EffectCategory.Fog, nameof(EffectSettings.stylisticFog), new StylisticFogController());
            Add(EffectCategory.Fog, nameof(EffectSettings.obscurance), new ObscuranceController());
            Add(EffectCategory.Fog, nameof(EffectSettings.distanceFog), new DistanceFogController());

            Add(EffectCategory.Other, nameof(EffectSettings.gameEffect), new GameEffectController());
            Add(EffectCategory.Other, nameof(EffectSettings.motionBlur), new MotionBlurController());
            Add(EffectCategory.Other, nameof(EffectSettings.antialiasing), new AntialiasingController());
            Add(EffectCategory.Other, nameof(EffectSettings.filmicLetterBox), new FilmicLetterBoxController());
            Add(EffectCategory.Other, nameof(EffectSettings.screenOverlay), new ScreenOverlayController());
            Add(EffectCategory.Other, nameof(EffectSettings.casSharpen), new CasSharpenController());
            Add(EffectCategory.Other, nameof(EffectSettings.maidHide), new MaidHideController());
            Add(EffectCategory.Other, nameof(EffectSettings.rimlight), new RimlightController());
        }

        private void Add(EffectCategory category, string id, EffectControllerBase controller)
        {
            controller.category = category;
            controller.effectId = id;
            controllers.Add(controller);
            _controllerById[id] = controller;
        }

        // 外部連携用: 安定 ID でコントローラを引く。未知の ID は null
        public EffectControllerBase GetController(string id)
        {
            EffectControllerBase controller;
            return _controllerById.TryGetValue(id, out controller) ? controller : null;
        }

        public T GetController<T>() where T : EffectControllerBase
        {
            foreach (var controller in controllers)
            {
                if (controller is T)
                {
                    return (T)controller;
                }
            }
            return null;
        }

        // ゲーム側 (CameraMain.Update 等) の書き込みより後に適用するため LateUpdate で処理する
        public override void LateUpdate()
        {
            foreach (var controller in controllers)
            {
                try
                {
                    if (effectsEnabled && controller.effectEnabled)
                    {
                        controller.Apply();
                        _appliedControllers.Add(controller);
                    }
                    else if (_appliedControllers.Contains(controller))
                    {
                        controller.Restore();
                        _appliedControllers.Remove(controller);
                    }
                }
                catch (Exception e)
                {
                    MTEUtils.LogException(e);
                    MTEUtils.LogError("エフェクトの適用に失敗したため無効化します: {0}", controller.effectName);
                    controller.effectEnabled = false;
                    _appliedControllers.Remove(controller);

                    // 適用途中の書き換え (ゲーム標準エフェクトの抑制等) を放置すると
                    // 以降 Restore が呼ばれるパスがなくなるため、ここで復元も試みる
                    try
                    {
                        controller.Restore();
                    }
                    catch (Exception restoreException)
                    {
                        MTEUtils.LogException(restoreException);
                    }
                }
            }

            // キャラ除外マスクは複数エフェクトで共有するため、解放判定は各エフェクトではなくここで行う
            CharacterMask.Tick();
        }

        public void RestoreAll()
        {
            foreach (var controller in controllers)
            {
                try
                {
                    if (_appliedControllers.Contains(controller))
                    {
                        controller.Restore();
                    }
                }
                catch (Exception e)
                {
                    MTEUtils.LogException(e);
                }
            }
            _appliedControllers.Clear();
            CharacterMask.Release();
        }
    }
}
