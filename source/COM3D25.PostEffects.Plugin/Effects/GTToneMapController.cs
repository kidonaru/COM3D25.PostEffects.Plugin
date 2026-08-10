using COM3D2.MotionTimelineEditor;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    public class GTToneMapSetting
    {
        public bool enabled = false;
        public float maxBrightness = 1f;
        public float contrast = 1f;
        public float linearStart = 0.22f;
        public float linearLength = 0.4f;
        public float blackTightness = 1.33f;
        public float blackOffset = 0f;
    }

    /// <summary>
    /// GT トーンマップ (グランツーリスモ方式の HDR→LDR 変換)。MTE 由来の実装
    /// </summary>
    public class GTToneMapController : EffectControllerBase<GTToneMapEffect, GTToneMapSetting>
    {
        public override string effectName => "GTトーンマップ";

        protected override GTToneMapSetting setting
        {
            get => settings.gtToneMap;
            set => settings.gtToneMap = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        protected override void ApplySetting(GTToneMapEffect component)
        {
            component.ApplyData(new GTToneMapData
            {
                enabled = true,
                maxBrightness = setting.maxBrightness,
                contrast = setting.contrast,
                linearStart = setting.linearStart,
                linearLength = setting.linearLength,
                blackTightness = setting.blackTightness,
                blackOffset = setting.blackOffset,
            });
        }

        protected override void Capture(GTToneMapEffect component)
        {
            var c = _capturedSetting;
            _capturedEnabled = component.enabled;
            c.maxBrightness = component.data.maxBrightness;
            c.contrast = component.data.contrast;
            c.linearStart = component.data.linearStart;
            c.linearLength = component.data.linearLength;
            c.blackTightness = component.data.blackTightness;
            c.blackOffset = component.data.blackOffset;
        }

        protected override void RestoreSetting(GTToneMapEffect component)
        {
            var c = _capturedSetting;
            component.enabled = _capturedEnabled;
            component.ApplyData(new GTToneMapData
            {
                enabled = _capturedEnabled,
                maxBrightness = c.maxBrightness,
                contrast = c.contrast,
                linearStart = c.linearStart,
                linearLength = c.linearLength,
                blackTightness = c.blackTightness,
                blackOffset = c.blackOffset,
            });
        }

        // トーンカーブのプレビュー (パラメータ変更時のみ再描画)
        private Texture2D _curveTexture;
        private readonly GTToneMapSetting _curveCache = new GTToneMapSetting();

        private const int CurveWidth = 128;
        private const int CurveHeight = 48;

        // GTToneMap.shader の GTToneMap() と同式のスカラー版
        private static float EvaluateToneMap(GTToneMapSetting s, float x)
        {
            float P = s.maxBrightness;
            float a = s.contrast;
            float m = Mathf.Max(s.linearStart, 1e-4f);
            float l = Mathf.Min(s.linearLength, 0.999f); // 1 だと C2 の分母 (P-S1) が 0 になる
            float c = s.blackTightness;
            float b = s.blackOffset;

            float l0 = (P - m) * l / a;
            float Lx = m + a * (x - m);
            float Tx = m * Mathf.Pow(x / m, c) + b;
            float S1 = m + a * l0;
            float C2 = a * P / (P - S1);
            float Sx = P - (P - S1) * Mathf.Exp(-(C2 * (x - (m + l0)) / P));
            float w0 = 1f - SmoothW(x, 0f, m);
            float w2 = x >= m + l0 ? 1f : 0f;
            float w1 = 1f - w0 - w2;
            return Tx * w0 + Lx * w1 + Sx * w2;
        }

        private static float SmoothW(float x, float e0, float e1)
        {
            if (x <= e0) return 0f;
            if (x >= e1) return 1f;
            float t = (x - e0) / (e1 - e0);
            return t * t * (3f - 2f * t);
        }

        private bool IsCurveDirty()
        {
            var s = setting;
            var c = _curveCache;
            return _curveTexture == null ||
                   c.maxBrightness != s.maxBrightness ||
                   c.contrast != s.contrast ||
                   c.linearStart != s.linearStart ||
                   c.linearLength != s.linearLength ||
                   c.blackTightness != s.blackTightness ||
                   c.blackOffset != s.blackOffset;
        }

        private void UpdateCurveTexture()
        {
            if (!IsCurveDirty())
            {
                return;
            }

            var s = setting;
            var c = _curveCache;
            c.maxBrightness = s.maxBrightness;
            c.contrast = s.contrast;
            c.linearStart = s.linearStart;
            c.linearLength = s.linearLength;
            c.blackTightness = s.blackTightness;
            c.blackOffset = s.blackOffset;

            if (_curveTexture == null)
            {
                _curveTexture = new Texture2D(CurveWidth, CurveHeight, TextureFormat.ARGB32, false);
                _curveTexture.hideFlags = HideFlags.HideAndDontSave;
            }

            var background = new Color(0.1f, 0.1f, 0.1f, 1f);
            var gridColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            var curveColor = new Color(0.4f, 0.9f, 0.4f, 1f);

            var pixels = new Color32[CurveWidth * CurveHeight];
            for (int y = 0; y < CurveHeight; y++)
            {
                for (int x = 0; x < CurveWidth; x++)
                {
                    // 0.25 刻みのグリッド
                    bool grid = (x % (CurveWidth / 4)) == 0 || (y % (CurveHeight / 4)) == 0;
                    pixels[y * CurveWidth + x] = grid ? (Color32)gridColor : (Color32)background;
                }
            }

            // 入力 0..1、出力は 0..max(1, P) で正規化して描画
            float outputMax = Mathf.Max(1f, s.maxBrightness);
            int prevY = -1;
            for (int x = 0; x < CurveWidth; x++)
            {
                float input = (float)x / (CurveWidth - 1);
                float output = Mathf.Clamp01(EvaluateToneMap(s, input) / outputMax);
                int py = Mathf.Clamp(Mathf.RoundToInt(output * (CurveHeight - 1)), 0, CurveHeight - 1);

                // 縦方向の飛びを線で繋ぐ
                int y0 = prevY < 0 ? py : Mathf.Min(prevY, py);
                int y1 = prevY < 0 ? py : Mathf.Max(prevY, py);
                for (int y = y0; y <= y1; y++)
                {
                    pixels[y * CurveWidth + x] = (Color32)curveColor;
                }
                prevY = py;
            }

            _curveTexture.SetPixels32(pixels);
            _curveTexture.Apply(false);
        }

        public override void DrawContent(GUIView view)
        {
            UpdateCurveTexture();
            view.DrawTexture(_curveTexture, CurveWidth, CurveHeight);

            DrawSlider(view, "最大輝度", 1f, 100f, 1f, setting.maxBrightness, v => setting.maxBrightness = v);
            DrawSlider(view, "コントラスト", 0f, 5f, 1f, setting.contrast, v => setting.contrast = v);
            DrawSlider(view, "線形開始", 0f, 1f, 0.22f, setting.linearStart, v => setting.linearStart = v);
            DrawSlider(view, "線形区間", 0f, 1f, 0.4f, setting.linearLength, v => setting.linearLength = v);
            DrawSlider(view, "黒の締まり", 1f, 3f, 1.33f, setting.blackTightness, v => setting.blackTightness = v);
            DrawSlider(view, "黒オフセット", 0f, 1f, 0f, setting.blackOffset, v => setting.blackOffset = v);
        }
    }
}
