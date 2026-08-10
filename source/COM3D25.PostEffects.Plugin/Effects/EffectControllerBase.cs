using System;
using COM3D2.MotionTimelineEditor;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    public abstract class EffectControllerBase
    {
        protected static EffectSettings settings => EffectSettings.instance;

        // 一覧の絞り込み用カテゴリ。PostEffectManager の登録時に設定する
        public EffectCategory category { get; internal set; } = EffectCategory.Other;

        // 外部連携・セーブデータ用の安定 ID (EffectSettings のフィールド名と揃える)。
        // effectName は表示用の日本語名で変更されうるため、キーにはこちらを使う
        public string effectId { get; internal set; }

        public abstract string effectName { get; }
        public abstract bool effectEnabled { get; set; }

        // ゲーム側が標準で有効化しているエフェクト (ブルーム等) を、
        // プラグインのエフェクトを使わないときでも強制無効化できるか
        public virtual bool canDisableGameEffect => false;

        // ゲーム標準エフェクトの強制無効化フラグ (対応コントローラが設定に紐付ける)
        public virtual bool gameEffectDisabled { get => false; set { } }

        // ゲーム側 (CameraMain.Update 等) が毎フレーム有効化し直すため、
        // gameEffectDisabled が立っている間は毎フレーム LateUpdate から呼んで無効化で対抗する
        public virtual void SuppressGameEffect() { }

        // 有効中は毎フレーム LateUpdate から呼ばれ、設定値をカメラのコンポーネントへ書き込む。
        // ゲーム本体 (CameraMain.Update 等) が毎フレーム値を上書きするエフェクトがあるため、
        // 一度だけの適用ではなく毎フレーム書き込みで対抗する
        public abstract void Apply();

        // 無効化時に取得時の状態へ戻す
        public abstract void Restore();

        // 設定値を初期値に戻す
        public abstract void ResetSetting();

        // メインウィンドウのタブ内容を描画する
        public abstract void DrawContent(GUIView view);

        protected static GameObject cameraObject
        {
            get
            {
                var mainCamera = GameMain.Instance.MainCamera;
                return mainCamera != null ? mainCamera.gameObject : null;
            }
        }

        protected static void SetDirty()
        {
            settings.dirty = true;
        }

        // エフェクトの設定項目はほぼ同じ体裁のスライダーなので、その定型をまとめたもの
        protected void DrawSlider(GUIView view, string label, float min, float max, float defaultValue,
            float value, Action<float> onChanged, float step = 0.01f)
        {
            view.DrawSliderValue(new GUIView.SliderOption
            {
                label = label,
                labelWidth = 100,
                width = -1,
                min = min,
                max = max,
                step = step,
                defaultValue = defaultValue,
                value = value,
                onChanged = v => { onChanged(v); SetDirty(); },
            });
        }
    }

    public abstract class EffectControllerBase<TComponent, TSetting> : EffectControllerBase
        where TComponent : Behaviour
        where TSetting : class, new()
    {
        protected TComponent _component;
        // 自分が AddComponent したコンポーネントか (復元時は無効化するだけでよい)
        protected bool _wasAdded;
        protected bool _captured;

        // ゲーム側が元々使っていたコンポーネントの、取得時の値
        protected readonly TSetting _capturedSetting = new TSetting();
        protected bool _capturedEnabled;

        // EffectSettings 上の設定値。リセットで丸ごと差し替えるため setter も要る
        protected abstract TSetting setting { get; set; }

        protected TComponent GetOrAddComponent()
        {
            // シーン遷移等で破棄されたら取得し直す (復元値は最初の取得時のものを保持し続ける)
            if (TryUseExistingComponent() != null)
            {
                return _component;
            }

            var go = cameraObject;
            if (go == null)
            {
                return null;
            }

            _component = go.AddComponent<TComponent>();
            _wasAdded = true;
            _captured = true;
            return _component;
        }

        public override void SuppressGameEffect()
        {
            // 自分で追加したコンポーネントは対象外 (ゲーム標準の既存コンポーネントのみ無効化する)
            var component = TryUseExistingComponent();
            if (component != null && !_wasAdded)
            {
                component.enabled = false;
            }
        }

        // カメラ上の既存コンポーネントを掴み、初回なら取得時の値を捕捉する
        private TComponent TryUseExistingComponent()
        {
            if (_component != null)
            {
                return _component;
            }

            var go = cameraObject;
            if (go == null)
            {
                return null;
            }

            var component = go.GetComponent<TComponent>();
            if (component == null)
            {
                return null;
            }

            _component = component;
            if (!_captured)
            {
                Capture(component);
                _captured = true;
            }
            return _component;
        }

        public override void ResetSetting()
        {
            var enabled = effectEnabled;
            setting = new TSetting();
            effectEnabled = enabled;
            SetDirty();
        }

        public override void Apply()
        {
            var component = GetOrAddComponent();
            if (component == null)
            {
                return;
            }

            component.enabled = true;
            ApplySetting(component);
        }

        public override void Restore()
        {
            if (_component == null)
            {
                return;
            }

            if (_wasAdded)
            {
                _component.enabled = false;
            }
            else
            {
                RestoreSetting(_component);
            }
        }

        // 設定値をコンポーネントへ書き込む
        protected abstract void ApplySetting(TComponent component);

        // ゲーム側が元々使っていたコンポーネントの取得時の値を保存する
        protected abstract void Capture(TComponent component);

        // 保存した値をコンポーネントへ書き戻す
        protected abstract void RestoreSetting(TComponent component);
    }
}
