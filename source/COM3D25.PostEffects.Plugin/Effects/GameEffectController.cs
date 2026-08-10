using System.Collections.Generic;
using COM3D2.MotionTimelineEditor;

namespace COM3D25.PostEffects.Plugin
{
    public class GameEffectSetting
    {
        public bool enabled = false;
    }

    // ゲームが標準で有効化しているエフェクト (ブルーム/ビネット等) をまとめて
    // 強制無効化する疑似エフェクト。自前のコンポーネントは持たず、
    // canDisableGameEffect な各コントローラの gameEffectDisabled を束ねて適用する
    public class GameEffectController : EffectControllerBase
    {
        public override string effectName => "ゲーム標準エフェクト無効化";

        private GameEffectSetting setting => settings.gameEffect;

        public override bool effectEnabled
        {
            get => setting.enabled;
            set => setting.enabled = value;
        }

        // 抑制中の対象コントローラ。無効化解除・Restore 時に復元する
        private HashSet<EffectControllerBase> _suppressed = new HashSet<EffectControllerBase>();

        private List<EffectControllerBase> _targets;
        private List<EffectControllerBase> targets
        {
            get
            {
                if (_targets == null)
                {
                    _targets = new List<EffectControllerBase>();
                    foreach (var controller in PostEffectManager.instance.controllers)
                    {
                        if (controller.canDisableGameEffect)
                        {
                            _targets.Add(controller);
                        }
                    }
                }
                return _targets;
            }
        }

        public override void Apply()
        {
            foreach (var target in targets)
            {
                // プラグイン側エフェクトが有効な間は、そのコントローラ自身が
                // コンポーネントを制御するため抑制しない
                var shouldSuppress = target.gameEffectDisabled && !target.effectEnabled;
                if (shouldSuppress)
                {
                    target.SuppressGameEffect();
                    _suppressed.Add(target);
                }
                else if (_suppressed.Contains(target))
                {
                    // 対象のプラグイン側エフェクトが有効な場合、本コントローラより
                    // 登録順が前の対象自身の Apply が同フレームで書き戻し済みのため
                    // Restore してはいけない (適用値を捕捉値で潰してしまう)
                    if (!target.effectEnabled)
                    {
                        target.Restore();
                    }
                    _suppressed.Remove(target);
                }
            }
        }

        public override void Restore()
        {
            foreach (var target in _suppressed)
            {
                if (!target.effectEnabled)
                {
                    target.Restore();
                }
            }
            _suppressed.Clear();
        }

        public override void ResetSetting()
        {
            // 復元は次フレームの Apply (抑制解除検知) が行う
            foreach (var target in targets)
            {
                target.gameEffectDisabled = false;
            }
            SetDirty();
        }

        public override void DrawContent(GUIView view)
        {
            foreach (var target in targets)
            {
                view.DrawToggle(target.effectName + "を無効化", target.gameEffectDisabled, 200, 20, value =>
                {
                    target.gameEffectDisabled = value;
                    SetDirty();
                });
            }
        }
    }
}
