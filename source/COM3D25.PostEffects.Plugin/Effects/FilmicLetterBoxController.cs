using COM3D2.MotionTimelineEditor;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    public class FilmicLetterBoxSetting
    {
        public bool enabled = false;
        public Color color = Color.black;
        public float centerX = 0.5f;
        public float centerY = 0.5f;
        public float position = 0.25f;
        public float smoothness = 0.001f;
        public bool vertical = false;
    }

    public class FilmicLetterBoxController : EffectControllerBase<FilmicLetterBoxEffect, FilmicLetterBoxSetting>
    {
        public override string effectName => "レターボックス";

        protected override FilmicLetterBoxSetting setting
        {
            get => settings.filmicLetterBox;
            set => settings.filmicLetterBox = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        protected override void ApplySetting(FilmicLetterBoxEffect component)
        {
            if (component.shader == null)
            {
                // "filmice..." はバンドル側のアセット名がそう綴られているため。タイポではない
                component.shader = EffectShaders.GetShader(EffectShaders.Filmic, "filmiceletterboxshader");
            }

            component.color = setting.color;
            component.center = new Vector2(setting.centerX, setting.centerY);
            component.position = setting.position;
            component.smoothness = setting.smoothness;
            component.vertical = setting.vertical;
        }

        protected override void Capture(FilmicLetterBoxEffect component)
        {
            var c = _capturedSetting;
            _capturedEnabled = component.enabled;
            c.color = component.color;
            c.centerX = component.center.x;
            c.centerY = component.center.y;
            c.position = component.position;
            c.smoothness = component.smoothness;
            c.vertical = component.vertical;
        }

        protected override void RestoreSetting(FilmicLetterBoxEffect component)
        {
            var c = _capturedSetting;
            component.enabled = _capturedEnabled;
            component.color = c.color;
            component.center = new Vector2(c.centerX, c.centerY);
            component.position = c.position;
            component.smoothness = c.smoothness;
            component.vertical = c.vertical;
        }

        public override void DrawContent(GUIView view)
        {
            view.DrawColor(
                view.GetColorFieldCache("帯の色", true),
                setting.color,
                Color.black,
                color => { setting.color = color; SetDirty(); });

            // 境界のぼかしは既定値が 0.001 と細かいため、この画面のスライダーは刻みを 1 桁細かくする
            const float step = 0.001f;
            DrawSlider(view, "帯の幅", 0f, 3f, 0.25f, setting.position, v => setting.position = v, step);
            DrawSlider(view, "境界のぼかし", step, 3f, step, setting.smoothness, v => setting.smoothness = v, step);
            DrawSlider(view, "中心 X", 0f, 1f, 0.5f, setting.centerX, v => setting.centerX = v, step);
            DrawSlider(view, "中心 Y", 0f, 1f, 0.5f, setting.centerY, v => setting.centerY = v, step);

            view.DrawToggle("左右に出す", setting.vertical, 250, 20, value =>
            {
                setting.vertical = value;
                SetDirty();
            });
        }
    }
}
