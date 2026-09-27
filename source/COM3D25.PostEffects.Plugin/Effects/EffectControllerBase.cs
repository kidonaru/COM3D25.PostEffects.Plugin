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

        // プラグイン有効化時 (とカメラ差し替え時) に PostEffectManager から固定順で呼ばれ、
        // コンポーネントを無効状態でカメラへ追加する。OnRenderImage の実行順はカメラ上の
        // コンポーネント順で決まるため、有効化した順ではなくここで並びを確定させる
        public virtual void Prepare() { }

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

        // 編集中データのコピー/ペーストに対応するか (true ならエフェクト行のリセット左にボタンを出す)
        public virtual bool supportsDataClipboard => false;
        public virtual bool canCopyData => false;
        public virtual bool canPasteData => false;

        // 編集中データをプラグイン内のクリップボードへ控える
        public virtual void CopyData() { }

        // クリップボードの内容を編集中データへ上書きする
        public virtual void PasteData() { }

        // PostEffectManager.PrepareAll からも参照するため internal。外部連携には公開しない
        internal static GameObject cameraObject
        {
            get
            {
                // プラグインの Start 時点では GameMain 自体が未生成のことがある
                var gameMain = GameMain.Instance;
                var mainCamera = gameMain != null ? gameMain.MainCamera : null;
                return mainCamera != null ? mainCamera.gameObject : null;
            }
        }

        protected static void SetDirty()
        {
            settings.dirty = true;
        }

        // データ番号タブ 1 個分の幅。最大 4 件 + 追加/削除ボタンが 1 行に収まる幅にしている
        private const float DATA_TAB_WIDTH = 30f;

        // 複数データを持つエフェクト共通の、データ番号のタブ行 (番号タブ + 追加/削除)。
        // 編集対象のデータ番号は呼び出し側が持つため ref で受けて書き換える
        protected static void DrawDataTabs<T>(GUIView view, PostEffectSettingsBase<T> s,
            ref int dataIndex, int maxCount, Func<T> createData)
            where T : class, IPostEffectData
        {
            var count = s.GetDataCount();

            view.BeginHorizontal();
            {
                view.DrawLabel("データ", 50, 20);

                for (var i = 0; i < count; i++)
                {
                    var selected = i == dataIndex;
                    if (view.DrawButton((i + 1).ToString(), DATA_TAB_WIDTH, 20, true,
                        selected ? GUIView.option.accentColor : (Color?)null))
                    {
                        dataIndex = i;
                    }
                }

                // DrawButton は onBeforeValueChanged を通さないため、
                // データを書き換える直前に自分で通す (編集モードへの自動移行用)
                if (view.DrawButton("追加", 60, 20, count < maxCount))
                {
                    view.NotifyBeforeValueChanged();
                    s.AddData(createData());
                    dataIndex = s.GetDataCount() - 1;
                    SetDirty();
                }
                if (view.DrawButton("削除", 60, 20, count > 0))
                {
                    view.NotifyBeforeValueChanged();
                    s.RemoveData(dataIndex);
                    SetDirty();
                }
            }
            view.EndLayout();
        }

        // 色1 → 色2 のグラデーション色 (パラフィン/距離フォグ/リムライト) を描画する。
        // 簡易表示では色2 の RGB を色1 から流用し、色2 は不透明度だけを編集させる。
        // 簡易表示の切替は表示だけを変え、データは書き換えない。色2 の RGB が色1 と
        // 異なるデータ (プリセット・ペースト・タイムライン由来) は食い違いを隠さないよう
        // 通常表示で描き、「揃える」ボタンで明示的に揃えさせる
        protected static void DrawGradientColors(GUIView view,
            Color color1, Color color2, Color resetColor1, Color resetColor2,
            Action<Color> onColor1Changed, Action<Color> onColor2Changed)
        {
            var config = ConfigManager.instance.config;
            var simple = config.simpleGradientColor && IsSameRgb(color1, color2);

            if (simple)
            {
                var alpha2 = color2.a;
                view.DrawColor(view.GetColorFieldCache("色1", true), color1, resetColor1, c =>
                {
                    onColor1Changed(c);
                    onColor2Changed(WithAlpha(c, alpha2));
                });
            }
            else
            {
                view.DrawColor(view.GetColorFieldCache("色1", true), color1, resetColor1, onColor1Changed);
            }

            // 切替ボタン類は色2 の行の右端に置く。DrawColor は自前で横並びを閉じて
            // 入れ子にできないため、先にボタン類を描いてから行頭へ戻して色2 の行を重ねる
            // (layoutMaxPos.y は最大値を保つので、戻しても次の行の位置は崩れない)。
            // 狭いウィンドウでは色2 の行の中身と重なるため、行の下へ別行で置く
            var rowWidth = view.viewRect.width - view.padding.x * 2;
            var iconX = rowWidth - GRADIENT_ICON_SIZE;
            var buttonsWidth = GRADIENT_ICON_SIZE + view.margin + GRADIENT_ALIGN_BUTTON_WIDTH;
            var colorRowWidth = COLOR_ROW_CONTENT_WIDTH + view.margin * 5;
            var fitsInRow = colorRowWidth + buttonsWidth <= rowWidth;

            if (fitsInRow)
            {
                var rowY = view.currentPos.y;
                DrawGradientButtons(view, iconX, color1, ref color2, onColor2Changed);
                view.currentPos.y = rowY;
            }

            // 切替・揃えるの結果は、同じフレームの色2 の行から反映する
            simple = config.simpleGradientColor && IsSameRgb(color1, color2);
            if (simple)
            {
                view.DrawSliderValue(new GUIView.SliderOption
                {
                    label = "色2 不透明度",
                    labelWidth = 100,
                    width = fitsInRow ? iconX - view.margin : -1,
                    min = 0f,
                    max = 1f,
                    step = 0.01f,
                    defaultValue = resetColor2.a,
                    value = color2.a,
                    onChanged = a => onColor2Changed(WithAlpha(color1, a)),
                });
            }
            else
            {
                view.DrawColor(view.GetColorFieldCache("色2", true), color2, resetColor2, onColor2Changed);
            }

            if (!fitsInRow)
            {
                DrawGradientButtons(view, iconX, color1, ref color2, onColor2Changed);
            }
        }

        // 簡易表示の切替アイコンと、簡易表示中に RGB が食い違うときの「揃える」ボタンを
        // 右寄せの 1 行で描く
        private static void DrawGradientButtons(GUIView view, float iconX,
            Color color1, ref Color color2, Action<Color> onColor2Changed)
        {
            var config = ConfigManager.instance.config;

            view.BeginHorizontal();
            {
                if (config.simpleGradientColor && !IsSameRgb(color1, color2))
                {
                    view.currentPos.x = iconX - view.margin - GRADIENT_ALIGN_BUTTON_WIDTH;
                    if (view.DrawButton("揃える", GRADIENT_ALIGN_BUTTON_WIDTH, 20))
                    {
                        // DrawButton は onBeforeValueChanged を通さないため自分で通す
                        view.NotifyBeforeValueChanged();
                        color2 = WithAlpha(color1, color2.a);
                        onColor2Changed(color2);
                    }
                }

                view.currentPos.x = iconX;
                view.BeginColor(config.simpleGradientColor ? GUIView.option.accentColor : Color.white);
                if (view.DrawTextureButton(PluginResources.changeIcon, GRADIENT_ICON_SIZE, GRADIENT_ICON_SIZE, 0,
                    tooltip: "簡易表示切替 (色2 は色1 の不透明度違いにする)"))
                {
                    config.simpleGradientColor = !config.simpleGradientColor;
                    config.dirty = true;
                }
                view.EndColor();
            }
            view.EndLayout();
        }

        // HEX 表示と同じ 8bit 精度で比べる。ピッカーやタイムライン補間で生じる
        // 1/255 未満の誤差まで食い違いとみなすと、見た目が同じなのに「揃える」が出てしまう
        private static bool IsSameRgb(Color a, Color b)
        {
            return a.IntR() == b.IntR() && a.IntG() == b.IntG() && a.IntB() == b.IntB();
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }

        private const float GRADIENT_ICON_SIZE = 20f;
        private const float GRADIENT_ALIGN_BUTTON_WIDTH = 60f;
        // GUIView.DrawColor の 1 行の要素幅の合計 (ラベル 90 + 色見本 20 + HEX 100 + R 20 + 編集 45)。
        // 要素間の margin は含まない
        private const float COLOR_ROW_CONTENT_WIDTH = 275f;

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

    // 複数データを持つエフェクト (パラフィン/距離フォグ/リムライト) の共通部。
    // GUI で編集対象にしているデータ番号と、データ単位のコピー/ペーストを受け持つ
    public abstract class MultiDataEffectControllerBase<TData> : EffectControllerBase
        where TData : class, IPostEffectData, new()
    {
        // GUI で編集対象にしているデータ番号
        protected int _dataIndex = 0;

        // コピー元データの複製。コピー後に元データを編集・削除しても影響を受けないよう実体を分ける
        private TData _clipboard = null;

        protected abstract PostEffectSettingsBase<TData> dataSettings { get; }

        // エフェクト行は DrawContent の範囲補正より先に描かれ、タイムライン側でデータ数が
        // 減ったフレームは _dataIndex が範囲外に残るため、ここでも補正して参照する
        private int clampedDataIndex =>
            Mathf.Clamp(_dataIndex, 0, Mathf.Max(dataSettings.GetDataCount() - 1, 0));

        // 編集対象のデータ番号を範囲内へ補正して、そのデータを返す (0 件なら null)
        protected TData GetEditingData()
        {
            _dataIndex = clampedDataIndex;
            return dataSettings.GetData(_dataIndex);
        }

        public override bool supportsDataClipboard => true;
        public override bool canCopyData => dataSettings.GetData(clampedDataIndex) != null;
        public override bool canPasteData => _clipboard != null && dataSettings.GetData(clampedDataIndex) != null;

        public override void CopyData()
        {
            var data = dataSettings.GetData(clampedDataIndex);
            if (data == null)
            {
                return;
            }
            _clipboard = new TData();
            _clipboard.CopyFrom(data);
        }

        public override void PasteData()
        {
            if (_clipboard == null)
            {
                return;
            }
            dataSettings.SetData(clampedDataIndex, _clipboard);
            SetDirty();
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

        public override void Prepare()
        {
            var component = GetOrAddComponent();
            // ゲーム標準の既存コンポーネントは取得時の状態を保つ (無効化は Apply/Suppress の責務)
            if (component != null && _wasAdded)
            {
                component.enabled = false;
            }
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
