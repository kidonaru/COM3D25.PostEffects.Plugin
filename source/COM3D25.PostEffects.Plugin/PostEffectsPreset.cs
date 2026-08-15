using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Xml.Serialization;
using COM3D2.MotionTimelineEditor;

namespace COM3D25.PostEffects.Plugin
{
    // プリセットとして保存するエフェクト設定一式
    // (動作設定・ウィンドウ位置・キーバインドは含めない)
    //
    // エフェクトを追加したときはここに `= new XxxSetting()` 付きのフィールドを 1 行足すだけでよい
    // (初期化子が「既定値」の定義そのものなので省略しないこと)。
    // EffectSettings との受け渡しは同名・同型のフィールドを突き合わせて行う。
    //
    // CaptureFrom 後は既定値のままのフィールドが null になる (XML から省くため)。
    // 値を読み出す用途では ApplyTo を通すこと
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

        private const BindingFlags MemberFlags = BindingFlags.Public | BindingFlags.Instance;

        private static readonly List<FieldPair> _fieldPairs = BuildFieldPairs();

        private static List<FieldPair> BuildFieldPairs()
        {
            var pairs = new List<FieldPair>();

            foreach (var presetField in typeof(PostEffectsPreset).GetFields(MemberFlags))
            {
                var settingsField = typeof(EffectSettings).GetField(presetField.Name, MemberFlags);
                if (settingsField == null || settingsField.FieldType != presetField.FieldType)
                {
                    MTEUtils.LogError("EffectSettings に対応するフィールドがありません: {0}", presetField.Name);
                    continue;
                }
                pairs.Add(new FieldPair { presetField = presetField, settingsField = settingsField });
            }

            return pairs;
        }

        /// <summary>
        /// 既定値のままのエフェクトは null にして XML から丸ごと省く。
        /// 大半のエフェクトは無効＝既定値のままなので、特にシーンプリセットの
        /// サイドカーが肥大化するのを防げる
        /// </summary>
        public void CaptureFrom(EffectSettings settings)
        {
            var defaults = new PostEffectsPreset();

            foreach (var pair in _fieldPairs)
            {
                var value = pair.settingsField.GetValue(settings);
                if (SerializedEquals(value, pair.presetField.GetValue(defaults)))
                {
                    value = null;
                }
                pair.presetField.SetValue(this, value);
            }
        }

        /// <summary>
        /// 省略されたエフェクトは既定値へ戻す。プリセットは全体の状態を表すため、
        /// 記述がないものを「現状維持」にはしない。
        /// 実際に既定値を供給しているのは XML 欠落時に残るフィールド初期化子で、
        /// null 合体は CaptureFrom 済みインスタンスを渡された場合の保険
        /// </summary>
        public void ApplyTo(EffectSettings settings)
        {
            foreach (var pair in _fieldPairs)
            {
                var value = pair.presetField.GetValue(this)
                    ?? Activator.CreateInstance(pair.presetField.FieldType);
                pair.settingsField.SetValue(settings, value);
            }
        }

        /// <summary>
        /// 既定値との一致判定用の再帰比較。各 Setting クラスは Equals を持たない POCO なので、
        /// XmlSerializer が書き出すのと同じ範囲 (公開フィールド・公開プロパティのうち
        /// [XmlIgnore] でないもの) を突き合わせる。
        /// CurveData.version のような直列化されない編集カウンタまで比較してしまうと、
        /// 値を既定へ戻したのに「変更あり」と誤判定されるため範囲を揃えている
        /// </summary>
        private static bool SerializedEquals(object a, object b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }
            if (a == null || b == null)
            {
                return false;
            }

            var type = a.GetType();
            if (type != b.GetType())
            {
                return false;
            }

            // 数値・列挙・string や Color/Vector などの構造体は既定の等価比較で足りる
            if (type.IsValueType || type == typeof(string))
            {
                return a.Equals(b);
            }

            var listA = a as IList;
            if (listA != null)
            {
                var listB = (IList)b;
                if (listA.Count != listB.Count)
                {
                    return false;
                }
                for (int i = 0; i < listA.Count; i++)
                {
                    if (!SerializedEquals(listA[i], listB[i]))
                    {
                        return false;
                    }
                }
                return true;
            }

            var members = GetSerializedMembers(type);
            if (members.Count == 0)
            {
                // 比較材料が無い型 (プロパティのみで状態を持つ等) を「一致」にすると
                // 設定が黙って XML から消える。安全側に倒して省略させない
                return false;
            }

            foreach (var member in members)
            {
                if (!SerializedEquals(GetMemberValue(member, a), GetMemberValue(member, b)))
                {
                    return false;
                }
            }
            return true;
        }

        private static readonly Dictionary<Type, List<MemberInfo>> _serializedMembers =
            new Dictionary<Type, List<MemberInfo>>();

        private static List<MemberInfo> GetSerializedMembers(Type type)
        {
            List<MemberInfo> members;
            if (_serializedMembers.TryGetValue(type, out members))
            {
                return members;
            }

            members = new List<MemberInfo>();
            foreach (var field in type.GetFields(MemberFlags))
            {
                if (!IsXmlIgnored(field))
                {
                    members.Add(field);
                }
            }
            foreach (var property in type.GetProperties(MemberFlags))
            {
                // インデクサと読み書き不揃いのプロパティは XmlSerializer も対象外
                if (property.CanRead && property.CanWrite &&
                    property.GetIndexParameters().Length == 0 && !IsXmlIgnored(property))
                {
                    members.Add(property);
                }
            }

            _serializedMembers[type] = members;
            return members;
        }

        private static bool IsXmlIgnored(MemberInfo member)
        {
            return member.IsDefined(typeof(XmlIgnoreAttribute), true);
        }

        private static object GetMemberValue(MemberInfo member, object target)
        {
            var field = member as FieldInfo;
            if (field != null)
            {
                return field.GetValue(target);
            }
            return ((PropertyInfo)member).GetValue(target, null);
        }
    }
}
