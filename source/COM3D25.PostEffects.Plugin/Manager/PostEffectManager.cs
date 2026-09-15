using System;
using System.Collections.Generic;
using COM3D2.MotionTimelineEditor;
using UnityEngine;

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

        // カメラへのコンポーネント追加順 = OnRenderImage の実行順。SceneCapture の EffectManager.Clear() 順を
        // 骨格に、本プラグイン独自のエフェクトを用途の近い位置へ挿入している。
        // ここに無い ID (Hub 系 / LightShafts / GameEffect) はカメラ上の並びに関係しないため先頭で処理する
        private static readonly string[] ComponentOrder =
        {
            nameof(EffectSettings.maidHide),
            nameof(EffectSettings.colorCorrectionCurves),
            nameof(EffectSettings.contrast),
            nameof(EffectSettings.crease),
            nameof(EffectSettings.edgeDetect),
#if COM3D25
            nameof(EffectSettings.outlineColor),
#endif
            nameof(EffectSettings.filmicMedianFilter),
            nameof(EffectSettings.antialiasing),
            nameof(EffectSettings.noiseAndGrain),
            nameof(EffectSettings.motionBlur),
            nameof(EffectSettings.tiltShiftHdr),
            nameof(EffectSettings.sunShafts),
            nameof(EffectSettings.analogGlitch),
            nameof(EffectSettings.digitalGlitch),
            nameof(EffectSettings.isoline),
            nameof(EffectSettings.halftone),
            nameof(EffectSettings.kuwahara),
            nameof(EffectSettings.obscurance),
            nameof(EffectSettings.globalFog),
            nameof(EffectSettings.stylisticFog),
            nameof(EffectSettings.cinematicBloom),
            nameof(EffectSettings.filmicBloom),
            nameof(EffectSettings.streak),
            nameof(EffectSettings.bloom),
            nameof(EffectSettings.diffusion),
            nameof(EffectSettings.depthOfField),
            nameof(EffectSettings.cinematicDepthOfField),
            nameof(EffectSettings.bokeh),
            nameof(EffectSettings.filmicBokeh),
            nameof(EffectSettings.blur),
            nameof(EffectSettings.radialBlur),
            nameof(EffectSettings.ramp),
            nameof(EffectSettings.cinematicLensAberrations),
            nameof(EffectSettings.fisheye),
            nameof(EffectSettings.vignetting),
            nameof(EffectSettings.colorCorrectionLut),
            nameof(EffectSettings.tonemappingColorGrading),
            nameof(EffectSettings.whiteBalance),
            nameof(EffectSettings.gtToneMap),
            nameof(EffectSettings.sepia),
            nameof(EffectSettings.grayscale),
            nameof(EffectSettings.casSharpen),
            nameof(EffectSettings.filmicLetterBox),
            nameof(EffectSettings.screenOverlay),
        };

        // 最後に PrepareAll したカメラ。シーン遷移等で差し替わったら組み直す
        private GameObject _preparedCamera;

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
#if COM3D25
            Add(EffectCategory.EdgeLine, nameof(EffectSettings.outlineColor), new OutlineColorController());
#endif
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

        // 全コントローラのコンポーネントを固定順で無効状態のまま追加する。
        // 有効化した順で AddComponent すると OnRenderImage の順序が操作順に依存してしまうため、
        // プラグイン有効化時とカメラ差し替え時にここで並びを確定させる
        public void PrepareAll()
        {
            var camera = EffectControllerBase.cameraObject;
            if (camera == null)
            {
                return;
            }

            var ordered = new List<EffectControllerBase>();
            foreach (var controller in controllers)
            {
                if (Array.IndexOf(ComponentOrder, controller.effectId) < 0)
                {
                    ordered.Add(controller);
                }
            }
            foreach (var id in ComponentOrder)
            {
                var controller = GetController(id);
                if (controller != null)
                {
                    ordered.Add(controller);
                }
            }

            // AddComponent 直後に各 Effect の OnEnable が走り、depthTextureMode |= Depth が
            // 無効化後も残る (OnDisable で戻す実装がない)。無効なエフェクトの深度パスを
            // 常駐させないため、追加前の値へ戻す
            var unityCamera = camera.GetComponent<Camera>();
            var depthMode = unityCamera != null ? unityCamera.depthTextureMode : DepthTextureMode.None;

            try
            {
                foreach (var controller in ordered)
                {
                    try
                    {
                        controller.Prepare();
                    }
                    catch (Exception e)
                    {
                        MTEUtils.LogException(e);
                        MTEUtils.LogError("エフェクトの事前追加に失敗しました: {0}", controller.effectName);
                    }
                }
            }
            finally
            {
                if (unityCamera != null)
                {
                    unityCamera.depthTextureMode = depthMode;
                }
            }

            _preparedCamera = camera;
        }

        // ゲーム側 (CameraMain.Update 等) の書き込みより後に適用するため LateUpdate で処理する
        public override void LateUpdate()
        {
            // カメラが差し替わった (初回・シーン遷移・VR 切替) フレームで並びを組み直す
            if (EffectControllerBase.cameraObject != _preparedCamera)
            {
                PrepareAll();
            }

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
