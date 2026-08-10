using UnityEngine;
using COM3D2.MotionTimelineEditor;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// 距離フォグ (深度ベースの色フェード)。MTE 由来の CommandBuffer 実装 (PostEffectHub) を使う
    /// </summary>
    public class DistanceFogController : EffectControllerBase
    {
        public override string effectName => "距離フォグ";

        private DistanceFogEffectSettings setting
        {
            get => settings.distanceFog;
            set => settings.distanceFog = value;
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
                    setting.AddData(new DistanceFogData { enabled = true });
                    _dataIndex = 0;
                }
            }
        }

        // GUI で編集対象にしているデータ番号
        private int _dataIndex = 0;

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
            setting = new DistanceFogEffectSettings();
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
                if (view.DrawButton("追加", 60, 20, s.GetDataCount() < DistanceFogEffectModel.MAX_FOG_COUNT))
                {
                    s.AddData(new DistanceFogData { enabled = true });
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
                view.DrawLabel("「追加」で距離フォグを追加してください", 300, 20);
                return;
            }

            _dataIndex = Mathf.Clamp(_dataIndex, 0, s.GetDataCount() - 1);
            var data = s.GetData(_dataIndex);

            view.DrawToggle("有効", data.enabled, 120, 20, value => { data.enabled = value; SetDirty(); });

            view.DrawColor(
                view.GetColorFieldCache("色1", true),
                data.color1,
                new Color(1f, 1f, 1f, 1f),
                color => { data.color1 = color; SetDirty(); });
            view.DrawColor(
                view.GetColorFieldCache("色2", true),
                data.color2,
                new Color(1f, 1f, 1f, 0f),
                color => { data.color2 = color; SetDirty(); });

            DrawSlider(view, "開始深度", 0f, 100f, 0f, data.fogStart, v => data.fogStart = v, 0.1f);
            DrawSlider(view, "終了深度", 0f, 100f, 40f, data.fogEnd, v => data.fogEnd = v, 0.1f);
            DrawSlider(view, "指数", 0f, 5f, 1f, data.fogExp, v => data.fogExp = v);

            view.DrawHorizontalLine(Color.gray);
            view.DrawLabel("ブレンド", 120, 20);
            DrawSlider(view, "通常", 0f, 2f, 1f, data.useNormal, v => data.useNormal = v);
            DrawSlider(view, "加算", 0f, 2f, 0f, data.useAdd, v => data.useAdd = v);
            DrawSlider(view, "乗算", 0f, 2f, 0f, data.useMultiply, v => data.useMultiply = v);
            DrawSlider(view, "オーバーレイ", 0f, 2f, 0f, data.useOverlay, v => data.useOverlay = v);
            DrawSlider(view, "減算", 0f, 2f, 0f, data.useSubstruct, v => data.useSubstruct = v);

            view.DrawToggle("デバッグ表示", s.isDebugView, 250, 20, value => { s.isDebugView = value; SetDirty(); });
        }
    }
}
