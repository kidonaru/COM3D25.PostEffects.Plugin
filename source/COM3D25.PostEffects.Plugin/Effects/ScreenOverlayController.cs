using System.Reflection;
using COM3D2.MotionTimelineEditor;
using UnityEngine;
#if COM3D25
using ScreenOverlayEffect = PostEffects_Dummy.ScreenOverlay;
#else
// COM3D2 (2.0) の内蔵エフェクトはグローバル名前空間 (Assembly-UnityScript-firstpass) にある
using ScreenOverlayEffect = global::ScreenOverlay;
#endif

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>オーバーレイに重ねる画のソース。値は TimelineBridge で int として受け渡す</summary>
    public enum ScreenOverlaySource
    {
        Texture = 0,
        Color = 1,
    }

    public class ScreenOverlaySetting
    {
        public bool enabled = false;
        // 既定値はゲームがメインカメラの ScreenOverlay に設定している値に合わせてある
        public ScreenOverlayEffect.OverlayBlendMode blendMode = ScreenOverlayEffect.OverlayBlendMode.Multiply;
        public float intensity = 1f;
        // 既存プリセットの見た目を変えないよう、既定はテクスチャ
        public ScreenOverlaySource source = ScreenOverlaySource.Texture;
        // カラーソースで重ねる色。不透明の黒なら AlphaBlend + 強度 0→1 がそのまま暗転になる
        public Color color = Color.black;
        // 絶対パス、または Config フォルダからの相対パス
        public string texturePath = "";
    }

    public class ScreenOverlayController : EffectControllerBase<ScreenOverlayEffect, ScreenOverlaySetting>
    {
        public override string effectName => "オーバーレイ";

        protected override ScreenOverlaySetting setting
        {
            get => settings.screenOverlay;
            set => settings.screenOverlay = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        private readonly TextureFileCache _textureCache = new TextureFileCache(TextureFileCache.SUB_DIR_OVERLAY);

        // カラーソース用の 1x1 単色テクスチャ。全テクセル同色なので UV 変換やフィルタの影響を受けない。
        // コントローラはプロセス寿命の間 1 個だけなので、破棄せず使い回す
        private Texture2D _solidTexture;
        private Color32 _solidColor;

        protected override void ApplySetting(ScreenOverlayEffect component)
        {
            component.blendMode = setting.blendMode;
            component.intensity = setting.intensity;
            component.texture = setting.source == ScreenOverlaySource.Color
                ? GetSolidTexture(GetOverlayColor(setting))
                : _textureCache.GetOrLoad(setting.texturePath);
        }

        /// <summary>
        /// カラーソースで渡す色。AlphaBlend はシェーダーが強度を見ず α だけで混ぜるため、
        /// 「強度 0→1 で色へフェード」の操作にそろうよう α に強度を掛けて渡す
        /// </summary>
        private static Color GetOverlayColor(ScreenOverlaySetting s)
        {
            var color = s.color;
            if (s.blendMode == ScreenOverlayEffect.OverlayBlendMode.AlphaBlend)
            {
                color.a = Mathf.Clamp01(color.a * s.intensity);
            }
            return color;
        }

        // ApplySetting は有効中に毎フレーム呼ばれるため、色が変わったときだけ書き直す。
        // テクスチャは RGBA32 なので、比較も同じ 8bit 精度 (Color32) で行う
        private Texture2D GetSolidTexture(Color32 color)
        {
            if (_solidTexture == null)
            {
                _solidTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Point,
                    name = "PostEffects ScreenOverlay Solid",
                };
            }
            else if (_solidColor.r == color.r && _solidColor.g == color.g &&
                _solidColor.b == color.b && _solidColor.a == color.a)
            {
                return _solidTexture;
            }

            _solidColor = color;
            _solidTexture.SetPixels32(new[] { color });
            _solidTexture.Apply(false);
            return _solidTexture;
        }

        private Texture2D _capturedTexture;

        // メインカメラの ScreenOverlay はゲームのフェード (CameraMain.m_FadeMyCamera) と共用。
        // 暗転中に捕捉すると「有効・強度 0 (真っ黒)」を掴むため、有効状態と強度は捕捉せず復元時のフェード状態から決める
        protected override void Capture(ScreenOverlayEffect component)
        {
            _capturedSetting.blendMode = component.blendMode;
            _capturedTexture = component.texture;
        }

        protected override void RestoreSetting(ScreenOverlayEffect component)
        {
            component.blendMode = _capturedSetting.blendMode;
            component.texture = _capturedTexture;

            // Restore は LateUpdate で走り、フェードのコルーチンが同じフレームで書き直さない。
            // フェードアウト途中は明るい画が 1 フレーム挟まらないよう暗転側へ倒し、翌フレームからコルーチンに任せる。
            // それ以外はフェードイン完了時 (CameraMain.FadeInNoUI) と同じ無効・強度 1 へ戻す
            var darkened = IsFadingOutOrOut(component.GetComponent<CameraMain>());
            component.enabled = darkened;
            component.intensity = darkened ? 0f : 1f;
        }

        private static FieldInfo _myFadeStateField;
        private static bool _myFadeStateFieldResolved;

        // m_eMyFadeState は protected。公開メソッド (IsFadeProcNoUI) はフェードアウト途中 (ProcOut) と
        // 暗転の保持 (Out) を区別できないためリフレクションで読む
        private static bool IsFadingOutOrOut(CameraMain cameraMain)
        {
            if (cameraMain == null)
            {
                return false;
            }

            if (!_myFadeStateFieldResolved)
            {
                _myFadeStateFieldResolved = true;
                _myFadeStateField = typeof(CameraMain).GetField("m_eMyFadeState",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (_myFadeStateField == null)
                {
                    MTEUtils.LogWarning("CameraMain のフェード状態が見つかりません。オーバーレイは無効状態へ戻します");
                }
            }
            if (_myFadeStateField == null)
            {
                return false;
            }

            var state = (CameraMain.FadeState)_myFadeStateField.GetValue(cameraMain);
            return state == CameraMain.FadeState.ProcOut || state == CameraMain.FadeState.Out;
        }

        private GUIComboBox<ScreenOverlayEffect.OverlayBlendMode> _blendModeComboBox = new GUIComboBox<ScreenOverlayEffect.OverlayBlendMode>
        {
            items = MTEUtils.GetEnumValues<ScreenOverlayEffect.OverlayBlendMode>(),
            getName = (mode, _) => mode.ToString(),
            buttonSize = new Vector2(100, 20),
        };

        private static readonly string[] SourceNames = { "テクスチャ", "カラー" };

        private GUIComboBox<ScreenOverlaySource> _sourceComboBox = new GUIComboBox<ScreenOverlaySource>
        {
            items = MTEUtils.GetEnumValues<ScreenOverlaySource>(),
            getName = (source, _) => SourceNames[(int)source],
            buttonSize = new Vector2(100, 20),
        };

        public override void DrawContent(GUIView view)
        {
            view.BeginHorizontal();
            {
                view.DrawLabel("ブレンドモード", 100, 20);
                _blendModeComboBox.currentIndex = (int)setting.blendMode;
                _blendModeComboBox.onSelected = (mode, _) => { setting.blendMode = mode; SetDirty(); };
                _blendModeComboBox.DrawButton(view);
            }
            view.EndLayout();

            view.BeginHorizontal();
            {
                view.DrawLabel("ソース", 100, 20);
                _sourceComboBox.currentIndex = (int)setting.source;
                _sourceComboBox.onSelected = (source, _) => { setting.source = source; SetDirty(); };
                _sourceComboBox.DrawButton(view);
            }
            view.EndLayout();

            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = "強度",
                labelWidth = 80,
                width = -1,
                min = 0f,
                max = 3f,
                step = 0.01f,
                defaultValue = 1f,
                value = setting.intensity,
                onChanged = value => { setting.intensity = value; SetDirty(); },
            });

            var isAlphaBlend = setting.blendMode == ScreenOverlayEffect.OverlayBlendMode.AlphaBlend;
            if (setting.source == ScreenOverlaySource.Color)
            {
                // ラベルはカラーピッカーの同定キーを兼ねるため、他エフェクトの「色」と衝突しない名前にする
                view.DrawColor(view.GetColorFieldCache("オーバーレイ色", true), setting.color, Color.black,
                    value => { setting.color = value; SetDirty(); });
                view.DrawLabel(isAlphaBlend
                        ? "不透明度は 色のアルファ × 強度 (1 で打ち止め)"
                        : "色のアルファは AlphaBlend でのみ効きます",
                    -1, 20, textColor: Color.gray);
            }
            else
            {
                _textureCache.DrawPathField(view, "テクスチャパス", setting.texturePath,
                    value => { setting.texturePath = value; SetDirty(); });
                if (isAlphaBlend)
                {
                    view.DrawLabel("AlphaBlend では強度は効かず、画像のアルファで混ざります", -1, 20,
                        textColor: Color.gray);
                }
            }
        }
    }
}
