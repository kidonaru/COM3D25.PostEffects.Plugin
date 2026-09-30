using System;
using System.Collections.Generic;
using COM3D2.MotionTimelineEditor;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    public class MainWindow : DockableWindowBase
    {
        public readonly static int WINDOW_ID = 815377;

        // ウィンドウの最小サイズ。カテゴリ行 (コンボ + ラベル + リセット) が折り返さない幅を下限にする
        public readonly static int MIN_WINDOW_WIDTH = 300;
        public readonly static int MIN_WINDOW_HEIGHT = 200;

        private static PostEffectsPlugin plugin => PostEffectsPlugin.instance;
        private static Config config => ConfigManager.instance.config;
        private static EffectSettings settings => EffectSettings.instance;
        private static PostEffectManager postEffectManager => PostEffectManager.instance;
        private static PresetManager presetManager => PresetManager.instance;

        protected override int windowId => WINDOW_ID;
        protected override string windowTitle => PluginInfo.WindowName;
        protected override int minWidth => MIN_WINDOW_WIDTH;
        protected override int minHeight => MIN_WINDOW_HEIGHT;

        // 一覧の絞り込み用カテゴリ選択
        private GUIComboBox<EffectCategory> _categoryComboBox = new GUIComboBox<EffectCategory>
        {
            items = new List<EffectCategory>((EffectCategory[])Enum.GetValues(typeof(EffectCategory))),
            getName = (category, _) => category.GetName(),
            buttonSize = new Vector2(140, 20),
            labelWidth = 70,
        };

        private static readonly string[] ModeNames = { "エフェクト", "タイムライン", "プリセット", "設定" };

        // ModeNames の添字
        private const int MODE_EFFECT = 0;
        private const int MODE_TIMELINE = 1;
        private const int MODE_PRESET = 2;
        private const int MODE_SETTING = 3;

        private int _modeIndex = MODE_EFFECT;

        // モード切替ボタンの幅。「設定」は文字数が少ないので詰めて、右端の「有効」トグルとの間を空ける
        private const int MODE_BUTTON_WIDTH = 80;
        private const int SETTING_MODE_BUTTON_WIDTH = 50;

        private readonly UIScaleSliderRow _uiScaleRow = new UIScaleSliderRow();

        /// <summary>
        /// タイムラインモードへ切り替える。SceneEditor がタイムラインを読み込んだときに
        /// TimelineBridge 経由で呼ばれる。ウィンドウの開閉は変えず、タブだけを移す
        /// </summary>
        public void ShowTimelineMode()
        {
            _modeIndex = MODE_TIMELINE;
        }

        // タイムライン対応 7 系統。所属判定 (IsTimelineDriven) 専用で、並び順に意味は無い
        private readonly List<EffectControllerBase> _timelineControllers = new List<EffectControllerBase>();

        // タイムラインタブ内のタブ。同系統のエフェクトは 1 タブにまとめる (被写界深度とシネマティック被写界深度)。
        // タブ名は先頭のエフェクト名。タブの並びは SceneEditor のポストエフェクトレイヤーの項目順に合わせる
        private readonly List<List<EffectControllerBase>> _timelineTabs = new List<List<EffectControllerBase>>();

        // タイムラインタブ内で表示中のタブ
        private int _timelineTabIndex = 0;
        private const float TIMELINE_TAB_WIDTH = 100f;

        // エフェクト行右端のボタン (コピー/ペースト/リセット) 1 個分の幅
        private const float ROW_BUTTON_WIDTH = 60f;

        /// <summary>SceneEditor 側のポストエフェクトレイヤーのクラス名。TimelineLayerGateHost / AutoEditModeHost の文字列契約</summary>
        private const string POST_EFFECT_LAYER_NAME = "PostEffectTimelineLayer";

        // 値を書く直前に SceneEditor の編集モードへ入り、変更前の状態を履歴に控える。
        // GUIComboBox は描画時にフックのデリゲートを掴み、ポップアップ確定時 (描画パス終了後) に
        // 呼ぶため、対象をフィールドで差し替える方式だと確定時に対象を見失う。
        // コントローラごとのクロージャをキャッシュして、デリゲート自体が対象を持つようにする
        private readonly Dictionary<EffectControllerBase, Action> _beforeEditHooks =
            new Dictionary<EffectControllerBase, Action>();

        // 履歴の捕捉・復元はプリセット XML で全エフェクトを丸ごと扱う (エフェクト単位の直列化は無い)
        private static readonly Func<string> CaptureHistoryState = () => presetManager.CapturePresetXml();
        private static readonly Action<string> ApplyHistoryState = xml => presetManager.ApplyPresetXml(xml);

        private Action GetBeforeEditHook(EffectControllerBase controller)
        {
            Action hook;
            if (!_beforeEditHooks.TryGetValue(controller, out hook))
            {
                hook = () => BeforeEdit(controller);
                _beforeEditHooks[controller] = hook;
            }
            return hook;
        }

        /// <summary>
        /// 値を書く直前に呼ぶ。編集モードへ入ってレイヤーを切り替え、履歴の変更前を控える。
        /// HistoryClient.BeforeEdit もホスト側で編集モードへ入るが、レイヤー切替は
        /// AutoEditModeClient.Enter だけが行うため両方呼ぶ
        /// </summary>
        private static void BeforeEdit(EffectControllerBase controller)
        {
            AutoEditModeClient.Enter(POST_EFFECT_LAYER_NAME);
            HistoryClient.BeforeEdit(
                "ポストエフェクト: " + controller.effectName, controller.effectId,
                CaptureHistoryState, ApplyHistoryState);
        }

        /// <summary>
        /// タイムラインが駆動する 7 系統か。エフェクトタブで触ったときも
        /// 編集モードへ入らないと再生値に巻き戻されるため、タブに関わらずこれで判定する
        /// </summary>
        private bool IsTimelineDriven(EffectControllerBase controller)
        {
            EnsureTimelineControllers();
            return _timelineControllers.Contains(controller);
        }

        private bool HasTimelineDrivenController(EffectCategory category)
        {
            foreach (var controller in postEffectManager.controllers)
            {
                if (controller.category == category && IsTimelineDriven(controller))
                {
                    return true;
                }
            }
            return false;
        }

        private string _presetName = "";

        private GUIView _rootView = new GUIView();
        private GUIView _contentView = new GUIView();

        public override void Init()
        {
            base.Init();
            InitView();
        }

        protected override void LoadPlacement(out int x, out int y, out int width, out int height)
        {
            // 画面解像度が変わった後でも収まるよう、保存値は読み込み時点で丸める
            width = Mathf.Min(config.mainWindowWidth, Screen.width);
            height = Mathf.Min(config.mainWindowHeight, Screen.height);

            x = config.mainWindowPosX;
            y = config.mainWindowPosY;

            // 初回は画面右寄せ (基底の既定は中央のため、従来の表示位置を保つ)
            if (x < 0 || y < 0)
            {
                x = Screen.width - width - 30;
                y = 100;
            }
        }

        protected override void StorePlacement(int x, int y, int width, int height)
        {
            config.mainWindowPosX = x;
            config.mainWindowPosY = y;
            config.mainWindowWidth = width;
            config.mainWindowHeight = height;
            config.dirty = true;
        }

        private void InitView()
        {
            _rootView.Init(localWindowRect);

            _contentView.parent = _rootView;
            _contentView.Init(contentRect);
        }

        protected override void OnSizeChanged(int width, int height)
        {
            InitView();
        }

        public override void OnLoad()
        {
            // プラグイン有効化時に呼ばれるためウィンドウを表示する
            isShowWnd = true;
            base.OnLoad();
        }

        /// <summary>
        /// 画面が縮んだときはウィンドウも収まるサイズへ詰める。
        /// 基底は位置のクランプしか行わないため、サイズ側はここで面倒を見る
        /// </summary>
        public override void OnScreenSizeChanged()
        {
            var rect = windowRect;
            rect.width = Mathf.Max(Mathf.Min(rect.width, Screen.width), minWidth);
            rect.height = Mathf.Max(Mathf.Min(rect.height, Screen.height), minHeight);
            windowRect = rect;

            base.OnScreenSizeChanged();
        }

        public override void Close()
        {
            // ヘッダーの閉じるボタンはプラグインごと無効化する。
            // 無効化経路 (OnPluginDisable) からも Close が呼ばれるため、
            // 有効なときだけ触って isEnable セッターの再入に頼らない
            base.Close();
            _uiScaleRow.Discard();

            if (plugin.isEnable)
            {
                plugin.isEnable = false;
            }
        }

        // 描かれない間は操作の終わりを判定できないため、保留中の UI 倍率は反映せず捨てる
        protected override void OnTabVisibleChanged(bool visible)
        {
            _uiScaleRow.Discard();
        }

        protected override void DrawContent()
        {
            // モードを切り替えても保留が残らないよう、モードに関係なく毎回判定する
            float newScale;
            if (_uiScaleRow.TryCommit(UIScaleClient.Resolve(config.uiScale), out newScale))
            {
                // SceneEditor に従っている間はそちらの設定へ書き、書けなければ自前の設定へ書く
                if (!UIScaleClient.TrySetHostScale(newScale))
                {
                    config.uiScale = newScale;
                    config.dirty = true;
                }
            }

            _rootView.ResetLayout();

            try
            {
                DrawModeContent();
            }
            finally
            {
                // レイヤーゲートで強制無効にした状態を、早期 return や例外に関わらずここで必ず解く
                TimelineLayerGateDrawer.End(_contentView);
            }

            // ボタン押下で _rootView に登録されたフォーカスをポップアップへ引き渡す
            ComboBoxPopupWindow.instance.ProcessFocus(_rootView, this);
        }

        private void DrawModeContent()
        {
            var view = _contentView;
            view.ResetLayout();

            view.BeginHorizontal();
            {
                for (var i = 0; i < ModeNames.Length; i++)
                {
                    var selected = i == _modeIndex;
                    var buttonWidth = i == MODE_SETTING ? SETTING_MODE_BUTTON_WIDTH : MODE_BUTTON_WIDTH;
                    if (view.DrawButton(ModeNames[i], buttonWidth, 20, true, selected ? GUIView.option.accentColor : (Color?)null))
                    {
                        _modeIndex = i;
                    }
                }

                // 全エフェクトの一時無効化トグル。個々の有効状態は保ったまま適用だけを止める。
                // UI 倍率を上げると窓内の論理幅が縮むため、モードボタンに重ならない位置より左へは寄せない
                view.currentPos.x = Mathf.Max(view.currentPos.x, view.viewRect.width - 80);
                view.DrawToggle("有効", postEffectManager.effectsEnabled, 60, 20,
                    value => postEffectManager.effectsEnabled = value);
            }
            view.EndLayout();

            view.DrawHorizontalLine(Color.gray);

            if (_modeIndex == MODE_EFFECT)
            {
                DrawEffectContent(view);
            }
            else if (_modeIndex == MODE_TIMELINE)
            {
                DrawTimelineContent(view);
            }
            else if (_modeIndex == MODE_PRESET)
            {
                DrawPresetContent(view);
            }
            else
            {
                DrawSettingContent(view);
            }
        }

        /// <summary>設定モード。UI 倍率 (SceneEditor に従う間は変更もそちらへ書く)</summary>
        private void DrawSettingContent(GUIView view)
        {
            // 従っている間は、使われない自前の値ではなく実際の倍率を見せる
            _uiScaleRow.Draw(view, "UI 倍率 %", 80, UIScaleClient.Resolve(config.uiScale), UIScaleClient.isScaleEditable);
            if (UIScaleClient.isFollowingHost)
            {
                view.DrawLabel(UIScaleClient.followingHostMessage, -1, 20, textColor: Color.gray);
            }
        }

        // 現在位置からビューの下端までをスクロール領域に充てる
        private static float GetScrollHeight(GUIView view)
        {
            return view.viewRect.height - view.currentPos.y - 5;
        }

        private void DrawEffectContent(GUIView view)
        {
            view.BeginHorizontal();
            {
                // 矢印での送りは同フレーム内で選択を変えるため、選択値は描画後に読む
                _categoryComboBox.DrawButton("カテゴリ", view);

                view.currentPos.x = view.viewRect.width - 80;
                if (view.DrawButton("リセット", 60, 20))
                {
                    var category = _categoryComboBox.currentItem;
                    // カテゴリ内にタイムライン対応の系統があれば、書き換える前に編集モードへ入り履歴を控える。
                    // このボタンは DrawEffectRow の外にありフックが差さっていないため、ここだけ直接呼ぶ
                    if (HasTimelineDrivenController(category))
                    {
                        AutoEditModeClient.Enter(POST_EFFECT_LAYER_NAME);
                        HistoryClient.BeforeEdit(
                            "ポストエフェクト: " + category.GetName() + " をリセット", "category:" + category,
                            CaptureHistoryState, ApplyHistoryState);
                    }
                    ResetCategory(category);
                }
            }
            view.EndLayout();

            view.DrawHorizontalLine(Color.gray);
            view.AddSpace(5);

            var selectedCategory = _categoryComboBox.currentItem;

            view.BeginScrollView(-1, GetScrollHeight(view), GUIView.AutoScrollViewRect, false, true);
            {
                foreach (var controller in postEffectManager.controllers)
                {
                    if (controller.category != selectedCategory)
                    {
                        continue;
                    }

                    DrawEffectRow(view, controller);
                }
            }
            view.EndScrollView();
        }

        /// <summary>
        /// タイムライン対応 7 系統のビュー。
        /// SceneEditor のタイムラインが駆動する対象をタブで切り替えて編集する。
        /// 描画は既存の DrawEffectRow をそのまま使う (個別タブと同じ操作性)
        /// </summary>
        private void DrawTimelineContent(GUIView view)
        {
            if (!EnsureTimelineControllers())
            {
                return;
            }

            DrawTimelineTabs(view);

            view.DrawHorizontalLine(Color.gray);
            view.AddSpace(5);

            // エフェクトタブの後に置き、タブ切替は無効化しない。
            // レイヤー名は SceneEditor 側 PostEffectTimelineLayer のクラス名 (文字列契約)
            TimelineLayerGateDrawer.Begin(view, POST_EFFECT_LAYER_NAME, 20f);

            var tab = _timelineTabs[Mathf.Clamp(_timelineTabIndex, 0, _timelineTabs.Count - 1)];

            view.BeginScrollView(-1, GetScrollHeight(view), GUIView.AutoScrollViewRect, false, true);
            {
                foreach (var controller in tab)
                {
                    DrawEffectRow(view, controller);
                }
            }
            view.EndScrollView();
        }

        // タブ名の行。幅が足りなければ次の行へ折り返す
        private void DrawTimelineTabs(GUIView view)
        {
            view.BeginHorizontal();
            {
                for (var i = 0; i < _timelineTabs.Count; i++)
                {
                    if (view.currentPos.x + TIMELINE_TAB_WIDTH > view.viewRect.width)
                    {
                        view.EndLayout();
                        view.BeginHorizontal();
                    }

                    var selected = i == _timelineTabIndex;
                    if (view.DrawButton(_timelineTabs[i][0].effectName, TIMELINE_TAB_WIDTH, 20, true,
                        selected ? GUIView.option.accentColor : Color.white))
                    {
                        _timelineTabIndex = i;
                    }
                }
            }
            view.EndLayout();
        }

        // コントローラ登録の完了後に確実に解決させるため、描画時に遅延で組み立てる。
        // 1 件も解決できなければ空のままにして次回描画で再試行する (戻り値は解決できたか)
        private bool EnsureTimelineControllers()
        {
            if (_timelineControllers.Count > 0)
            {
                return true;
            }

            var manager = postEffectManager;
            AddTimelineTab(
                manager.GetController<DepthOfFieldController>(),
                manager.GetController<CinematicDepthOfFieldController>());
            AddTimelineTab(manager.GetController<GTToneMapController>());
            AddTimelineTab(manager.GetController<ParaffinController>());
            AddTimelineTab(manager.GetController<DistanceFogController>());
            AddTimelineTab(manager.GetController<RimlightController>());
            AddTimelineTab(manager.GetController<BloomController>());
            return _timelineControllers.Count > 0;
        }

        // controllers を 1 タブとして _timelineTabs と _timelineControllers の両方へ登録する
        private void AddTimelineTab(params EffectControllerBase[] controllers)
        {
            var tab = new List<EffectControllerBase>(controllers);
            // 登録前に呼ばれた場合に null を掴まないよう除去する
            tab.RemoveAll(c => c == null);
            if (tab.Count == 0)
            {
                return;
            }
            _timelineTabs.Add(tab);
            _timelineControllers.AddRange(tab);
        }

        // カテゴリ内のエフェクトをまとめて無効化し、値も初期状態へ戻す
        private void ResetCategory(EffectCategory category)
        {
            foreach (var controller in postEffectManager.controllers)
            {
                if (controller.category != category)
                {
                    continue;
                }

                // ResetSetting は有効状態を保つため、無効化は後から行う
                controller.ResetSetting();
                controller.effectEnabled = false;
            }
            settings.dirty = true;
        }

        private void DrawEffectRow(GUIView view, EffectControllerBase controller)
        {
            // タイムライン対応の系統は、この行と設定項目の値変更で編集モードへ自動移行する
            var isTimelineDriven = IsTimelineDriven(controller);
            if (isTimelineDriven)
            {
                view.onBeforeValueChanged = GetBeforeEditHook(controller);
            }

            try
            {
                view.BeginHorizontal();
                {
                    // 行のチェックボックスが有効トグルそのもの。ON で下に設定項目を展開する。
                    // 右端のボタン群 (コピー/ペースト/リセット) の分だけ幅を空ける
                    var showClipboard = controller.effectEnabled && controller.supportsDataClipboard;
                    var resetX = view.viewRect.width - 20 - ROW_BUTTON_WIDTH;
                    var buttonsX = showClipboard ? resetX - (ROW_BUTTON_WIDTH + view.margin) * 2 : resetX;
                    view.DrawToggle(controller.effectName, controller.effectEnabled,
                        buttonsX - 10, 20, value =>
                    {
                        controller.effectEnabled = value;
                        settings.dirty = true;
                    });

                    if (showClipboard)
                    {
                        view.currentPos.x = buttonsX;
                        if (view.DrawButton("コピー", ROW_BUTTON_WIDTH, 20, controller.canCopyData))
                        {
                            controller.CopyData();
                        }
                        if (view.DrawButton("ペースト", ROW_BUTTON_WIDTH, 20, controller.canPasteData))
                        {
                            view.NotifyBeforeValueChanged();
                            controller.PasteData();
                        }
                    }

                    if (controller.effectEnabled)
                    {
                        view.currentPos.x = resetX;
                        if (view.DrawButton("リセット", ROW_BUTTON_WIDTH, 20))
                        {
                            // DrawButton はフックを通さないため、書き換える直前に自分で通す
                            view.NotifyBeforeValueChanged();
                            controller.ResetSetting();
                        }
                    }
                }
                view.EndLayout();

                if (controller.effectEnabled)
                {
                    // 設定項目を左右にインデントして、行との親子関係を見せる
                    var savedPadding = view.padding;
                    view.padding = new Vector2(savedPadding.x + 15, savedPadding.y);
                    controller.DrawContent(view);
                    view.padding = savedPadding;
                }

                view.DrawHorizontalLine(Color.gray);
            }
            finally
            {
                // 次の行 (タイムライン非対応の系統) へフックを持ち越さない
                if (isTimelineDriven)
                {
                    view.onBeforeValueChanged = null;
                }
            }
        }

        private void DrawPresetContent(GUIView view)
        {
            // 名前入力と保存
            view.BeginHorizontal();
            {
                view.DrawTextField("名前", 40, _presetName, view.viewRect.width - 120, 20,
                    value => _presetName = value);

                if (view.DrawButton("保存", 60, 20))
                {
                    presetManager.SavePreset(_presetName);
                }
            }
            view.EndLayout();

            view.DrawHorizontalLine(Color.gray);
            view.AddSpace(5);

            view.BeginHorizontal();
            {
                view.DrawLabel("「既定」で選んだプリセットを起動時に読み込みます", view.viewRect.width - 90, 20);

                // 手動でファイルを追加・削除したとき用に一覧を再読み込みする
                if (view.DrawButton("更新", 60, 20))
                {
                    presetManager.UpdatePresetNames();
                }
            }
            view.EndLayout();

            view.BeginScrollView(-1, GetScrollHeight(view), GUIView.AutoScrollViewRect, false, true);
            {
                // 描画中に削除されると列挙が壊れるためスナップショットを取る
                var names = presetManager.presetNames.ToArray();

                foreach (var name in names)
                {
                    // 固定プリセットは上書き・削除ができない
                    var isDefault = PresetManager.IsDefaultPreset(name);
                    var isStartup = name == config.startupPresetName;

                    view.BeginHorizontal();
                    {
                        // 名前フィールドにも反映し、そのまま上書き保存しやすくする
                        if (view.DrawButton(name, view.viewRect.width - 120, 20))
                        {
                            presetManager.LoadPreset(name);
                            _presetName = isDefault ? "" : name;
                        }

                        if (view.DrawButton("既定", 50, 20, true, isStartup ? GUIView.option.accentColor : (Color?)null))
                        {
                            config.startupPresetName = name;
                            config.dirty = true;
                        }

                        if (view.DrawButton("削除", 50, 20, !isDefault))
                        {
                            presetManager.DeletePreset(name);
                        }
                    }
                    view.EndLayout();
                }
            }
            view.EndScrollView();
        }
    }
}
