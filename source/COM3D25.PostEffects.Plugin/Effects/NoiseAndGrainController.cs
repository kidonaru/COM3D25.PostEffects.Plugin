using COM3D2.MotionTimelineEditor;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    public class NoiseAndGrainSetting
    {
        public bool enabled = false;
        public float intensityMultiplier = 0.25f;
        public float generalIntensity = 0.5f;
        public float blackIntensity = 1f;
        public float whiteIntensity = 1f;
        public float midGrey = 0.2f;
        public float softness = 0f;
        public bool monochrome = false;
        public float monochromeTiling = 64f;
        public float tilingX = 64f;
        public float tilingY = 64f;
        public float tilingZ = 64f;
    }

    // NoiseAndGrain はゲーム側 Assembly-UnityScript-firstpass の実装をそのまま使う。
    // 元実装が参照するノイズテクスチャはアセットとして入手できないため、ランタイム生成で代替する
    public class NoiseAndGrainController : EffectControllerBase<NoiseAndGrain, NoiseAndGrainSetting>
    {
        public override string effectName => "ノイズ/グレイン";

        private static Texture2D _noiseTexture;

        protected override NoiseAndGrainSetting setting
        {
            get => settings.noiseAndGrain;
            set => settings.noiseAndGrain = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        // ノイズテクスチャが null だとグレインが乗らない (実機確認済み) ため必ず生成して渡す
        private static Texture2D GetOrCreateNoiseTexture()
        {
            if (_noiseTexture != null)
            {
                return _noiseTexture;
            }

            const int size = 64;
            // 起動ごとにノイズパターンが変わらないよう seed を固定する
            var random = new System.Random(0x505345);
            _noiseTexture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            _noiseTexture.hideFlags = HideFlags.DontSave;
            _noiseTexture.wrapMode = TextureWrapMode.Repeat;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var color = new Color(
                        (float)random.NextDouble(),
                        (float)random.NextDouble(),
                        (float)random.NextDouble(),
                        1f);
                    _noiseTexture.SetPixel(x, y, color);
                }
            }
            _noiseTexture.Apply();
            return _noiseTexture;
        }

        protected override void ApplySetting(NoiseAndGrain component)
        {
            if (component.noiseShader == null)
            {
                component.noiseShader = EffectShaders.GetShader(EffectShaders.ImageEffects, "noiseshader");
            }
            if (component.dx11NoiseShader == null)
            {
                component.dx11NoiseShader = EffectShaders.GetShader(EffectShaders.ImageEffects, "dx11noiseshader");
            }
            if (component.noiseTexture == null)
            {
                component.noiseTexture = GetOrCreateNoiseTexture();
            }

            component.intensityMultiplier = setting.intensityMultiplier;
            component.generalIntensity = setting.generalIntensity;
            component.blackIntensity = setting.blackIntensity;
            component.whiteIntensity = setting.whiteIntensity;
            component.midGrey = setting.midGrey;
            component.softness = setting.softness;
            component.monochrome = setting.monochrome;
            component.monochromeTiling = setting.monochromeTiling;
            component.tiling = new Vector3(setting.tilingX, setting.tilingY, setting.tilingZ);
        }

        protected override void Capture(NoiseAndGrain component)
        {
            var c = _capturedSetting;
            _capturedEnabled = component.enabled;
            c.intensityMultiplier = component.intensityMultiplier;
            c.generalIntensity = component.generalIntensity;
            c.blackIntensity = component.blackIntensity;
            c.whiteIntensity = component.whiteIntensity;
            c.midGrey = component.midGrey;
            c.softness = component.softness;
            c.monochrome = component.monochrome;
            c.monochromeTiling = component.monochromeTiling;
            c.tilingX = component.tiling.x;
            c.tilingY = component.tiling.y;
            c.tilingZ = component.tiling.z;
        }

        protected override void RestoreSetting(NoiseAndGrain component)
        {
            var c = _capturedSetting;
            component.enabled = _capturedEnabled;
            component.intensityMultiplier = c.intensityMultiplier;
            component.generalIntensity = c.generalIntensity;
            component.blackIntensity = c.blackIntensity;
            component.whiteIntensity = c.whiteIntensity;
            component.midGrey = c.midGrey;
            component.softness = c.softness;
            component.monochrome = c.monochrome;
            component.monochromeTiling = c.monochromeTiling;
            component.tiling = new Vector3(c.tilingX, c.tilingY, c.tilingZ);
        }

        public override void DrawContent(GUIView view)
        {
            DrawSlider(view, "強度倍率", 0f, 10f, 0.25f, setting.intensityMultiplier, v => setting.intensityMultiplier = v);
            DrawSlider(view, "全体強度", 0f, 10f, 0.5f, setting.generalIntensity, v => setting.generalIntensity = v);
            DrawSlider(view, "暗部強度", 0f, 10f, 1f, setting.blackIntensity, v => setting.blackIntensity = v);
            DrawSlider(view, "明部強度", 0f, 10f, 1f, setting.whiteIntensity, v => setting.whiteIntensity = v);
            DrawSlider(view, "中間グレー", 0f, 1f, 0.2f, setting.midGrey, v => setting.midGrey = v);
            DrawSlider(view, "柔らかさ", 0f, 1f, 0f, setting.softness, v => setting.softness = v);

            view.DrawToggle("モノクロノイズ", setting.monochrome, 250, 20, value =>
            {
                setting.monochrome = value;
                SetDirty();
            });

            if (setting.monochrome)
            {
                DrawSlider(view, "タイリング", 0f, 100f, 64f, setting.monochromeTiling, v => setting.monochromeTiling = v);
            }
            else
            {
                DrawSlider(view, "タイリング R", 0f, 100f, 64f, setting.tilingX, v => setting.tilingX = v);
                DrawSlider(view, "タイリング G", 0f, 100f, 64f, setting.tilingY, v => setting.tilingY = v);
                DrawSlider(view, "タイリング B", 0f, 100f, 64f, setting.tilingZ, v => setting.tilingZ = v);
            }
        }
    }
}
