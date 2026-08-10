using UnityEngine;
using COM3D2.MotionTimelineEditor;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// リムライト (法線ベースの輪郭光)。MTE 由来の CommandBuffer 実装 (PostEffectHub) を使う
    /// </summary>
    public class RimlightController : EffectControllerBase
    {
        public override string effectName => "リムライト";

        private RimlightEffectSettings setting
        {
            get => settings.rimlight;
            set => settings.rimlight = value;
        }

        public override bool effectEnabled
        {
            get => setting.enabled;
            set
            {
                setting.enabled = value;
                // データ 0 件のまま有効化しても何も起きず戸惑うため、初回は既定データを 1 件足す
                if (value && setting.GetDataCount() == 0)
                {
                    setting.AddData(new RimlightData { enabled = true });
                    _dataIndex = 0;
                }
            }
        }

        // GUI で編集対象にしているデータ番号
        private int _dataIndex = 0;

        private static readonly string[] MaskModeNames = { "なし", "キャラ除外", "キャラのみ" };

        private readonly GUIComboBox<int> _maskComboBox = new GUIComboBox<int>
        {
            items = new System.Collections.Generic.List<int> { 0, 1, 2 },
            getName = (mode, _) => MaskModeNames[mode],
            buttonSize = new Vector2(100, 20),
        };

        public override void Apply()
        {
            var hub = PostEffectHub.GetOrAdd(cameraObject);
            if (hub != null)
            {
                hub.enabled = true;
            }
        }

        public override void Restore()
        {
            // Hub は 3 エフェクト共有のためここでは無効化しない (意図的な非対称)。
            // setting.enabled=false でモデルが非アクティブになり、全モデル非アクティブ時は
            // Hub 側が no-op + depthTextureMode 書き戻しを行うため描画コストは残らない
        }

        public override void ResetSetting()
        {
            var enabled = effectEnabled;
            setting = new RimlightEffectSettings();
            effectEnabled = enabled;
            _dataIndex = 0;
            SetDirty();
        }

        public override void DrawContent(GUIView view)
        {
            var s = setting;

            view.BeginHorizontal();
            {
                view.DrawLabel(string.Format("データ {0}/{1}", s.GetDataCount() == 0 ? 0 : _dataIndex + 1, s.GetDataCount()), 100, 20);
                if (view.DrawButton("追加", 60, 20, s.GetDataCount() < RimlightEffectModel.MAX_RIMLIGHT_COUNT))
                {
                    s.AddData(new RimlightData { enabled = true });
                    _dataIndex = s.GetDataCount() - 1;
                    SetDirty();
                }
                if (view.DrawButton("削除", 60, 20, s.GetDataCount() > 0))
                {
                    s.RemoveData(_dataIndex);
                    SetDirty();
                }
                if (view.DrawButton("<", 25, 20, _dataIndex > 0))
                {
                    --_dataIndex;
                }
                if (view.DrawButton(">", 25, 20, _dataIndex < s.GetDataCount() - 1))
                {
                    ++_dataIndex;
                }
            }
            view.EndLayout();

            if (s.GetDataCount() == 0)
            {
                view.DrawLabel("「追加」でリムライトを追加してください", 300, 20);
                return;
            }

            _dataIndex = Mathf.Clamp(_dataIndex, 0, s.GetDataCount() - 1);
            var data = s.GetData(_dataIndex);

            view.DrawToggle("有効", data.enabled, 120, 20, value => { data.enabled = value; SetDirty(); });

            view.DrawColor(
                view.GetColorFieldCache("色1", true),
                data.color1,
                new Color(0.77f, 0.70f, 1f, 1f),
                color => { data.color1 = color; SetDirty(); });
            view.DrawColor(
                view.GetColorFieldCache("色2", true),
                data.color2,
                new Color(0.77f, 0.70f, 1f, 0f),
                color => { data.color2 = color; SetDirty(); });

            DrawSlider(view, "角度X", -180f, 180f, 10f, data.rotation.x, v => data.rotation.x = v, 1f);
            DrawSlider(view, "角度Y", -180f, 180f, -40f, data.rotation.y, v => data.rotation.y = v, 1f);
            DrawSlider(view, "角度Z", -180f, 180f, 0f, data.rotation.z, v => data.rotation.z = v, 1f);
            view.DrawToggle("ワールド空間", data.isWorldSpace, 250, 20, value => { data.isWorldSpace = value; SetDirty(); });
            view.BeginHorizontal();
            {
                view.DrawToggle("顔にかけない", data.excludeFace, 120, 20, value => { data.excludeFace = value; SetDirty(); });
                if (data.excludeFace)
                {
                    view.DrawToggle("髪には適用", data.applyHair, 120, 20, value => { data.applyHair = value; SetDirty(); });
                }
            }
            view.EndLayout();

            view.BeginHorizontal();
            {
                view.DrawLabel("マスク", 60, 20);
                _maskComboBox.currentIndex = data.maskMode;
                _maskComboBox.onSelected = (mode, _) => { data.maskMode = mode; SetDirty(); };
                _maskComboBox.DrawButton(view);
            }
            view.EndLayout();

            DrawSlider(view, "影響", 0f, 2f, 1f, data.lightArea, v => data.lightArea = v);
            DrawSlider(view, "幅", 0f, 2f, 0.2f, data.fadeRange, v => data.fadeRange = v);
            DrawSlider(view, "指数", 0f, 5f, 1f, data.fadeExp, v => data.fadeExp = v);

            view.DrawHorizontalLine(Color.gray);
            view.DrawLabel("ブレンド", 120, 20);
            DrawSlider(view, "通常", 0f, 2f, 0f, data.useNormal, v => data.useNormal = v);
            DrawSlider(view, "加算", 0f, 2f, 0.8f, data.useAdd, v => data.useAdd = v);
            DrawSlider(view, "乗算", 0f, 2f, 0f, data.useMultiply, v => data.useMultiply = v);
            DrawSlider(view, "オーバーレイ", 0f, 2f, 0f, data.useOverlay, v => data.useOverlay = v);
            DrawSlider(view, "減算", 0f, 2f, 0f, data.useSubstruct, v => data.useSubstruct = v);

            view.DrawToggle("デバッグ表示", s.isDebugView, 250, 20, value => { s.isDebugView = value; SetDirty(); });
        }
    }
}
