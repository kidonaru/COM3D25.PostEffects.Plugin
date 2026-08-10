using System;
using System.Collections.Generic;
using COM3D2.MotionTimelineEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace COM3D25.PostEffects.Plugin
{
    public class MainWindow : IGUIWindow
    {
        public readonly static int WINDOW_ID = 815377;

        // ウィンドウの最小サイズ。エフェクト行が折り返さない幅を下限にする
        public readonly static int MIN_WINDOW_WIDTH = 400;
        public readonly static int MIN_WINDOW_HEIGHT = 320;

        public readonly static int HEADER_HEIGHT = 20;

        // リサイズグリップを置く下端の高さ
        public readonly static int FOOTER_HEIGHT = 20;

        private static PostEffectsPlugin plugin => PostEffectsPlugin.instance;
        private static Config config => ConfigManager.instance.config;
        private static EffectSettings settings => EffectSettings.instance;
        private static PostEffectManager postEffectManager => PostEffectManager.instance;
        private static PresetManager presetManager => PresetManager.instance;

        public int windowIndex { get; set; }
        public bool isShowWnd { get; set; }

        private Rect _windowRect;
        public Rect windowRect
        {
            get => _windowRect;
            set => _windowRect = value;
        }

        private int _windowWidth = MIN_WINDOW_WIDTH;
        private int _windowHeight = MIN_WINDOW_HEIGHT;
        private bool _initializedGUI = false;

        private GUIView.DragInfo _windowSizeDragInfo = new GUIView.DragInfo();

        // 一覧の絞り込み用カテゴリ選択
        private GUIComboBox<EffectCategory> _categoryComboBox = new GUIComboBox<EffectCategory>
        {
            items = new List<EffectCategory>((EffectCategory[])Enum.GetValues(typeof(EffectCategory))),
            getName = (category, _) => category.GetName(),
            buttonSize = new Vector2(140, 20),
            labelWidth = 70,
        };

        private static readonly string[] ModeNames = { "エフェクト", "プリセット" };

        private int _modeIndex = 0;  // 0: エフェクト, 1: プリセット
        private string _presetName = "";

        private GUIView _rootView = new GUIView();
        private GUIView _headerView = new GUIView();
        private GUIView _contentView = new GUIView();
        private GUIView _footerView = new GUIView();

        public MainWindow()
        {
            this.windowIndex = 0;
            this.isShowWnd = false;
            this.windowRect = new Rect(
                Screen.width - _windowWidth - 30,
                100,
                _windowWidth,
                _windowHeight
            );
        }

        public void Init()
        {
        }

        public void Update()
        {
        }

        public void Close()
        {
            isShowWnd = false;
        }

        private static void ClampWindowSize()
        {
            config.mainWindowWidth = Mathf.Clamp(
                config.mainWindowWidth, MIN_WINDOW_WIDTH, Screen.width);
            config.mainWindowHeight = Mathf.Clamp(
                config.mainWindowHeight, MIN_WINDOW_HEIGHT, Screen.height);
        }

        public void InitView()
        {
            _rootView.Init(0, 0, _windowWidth, _windowHeight);
            _headerView.Init(0, 0, _windowWidth, HEADER_HEIGHT);
            _contentView.Init(0, HEADER_HEIGHT, _windowWidth,
                _windowHeight - HEADER_HEIGHT - FOOTER_HEIGHT);
            _footerView.Init(0, _windowHeight - FOOTER_HEIGHT, _windowWidth, FOOTER_HEIGHT);

            _headerView.parent = _rootView;
            _contentView.parent = _rootView;
            _footerView.parent = _rootView;
        }

        public void OnLoad()
        {
            // プラグイン有効化時に呼ばれるためウィンドウを表示する
            isShowWnd = true;
            MTEUtils.AdjustWindowPosition(ref _windowRect);
        }

        public void OnChangedSceneLevel(Scene scene, LoadSceneMode sceneMode)
        {
        }

        public void OnScreenSizeChanged()
        {
            ClampWindowSize();
            MTEUtils.AdjustWindowPosition(ref _windowRect);
        }

        public void InitGUI()
        {
            if (_initializedGUI)
            {
                return;
            }
            _initializedGUI = true;

            // 画面解像度が変わった後でも収まるよう、保存値は読み込み時点で丸める
            ClampWindowSize();

            _windowWidth = config.mainWindowWidth;
            _windowHeight = config.mainWindowHeight;
            _windowRect.width = _windowWidth;
            _windowRect.height = _windowHeight;

            InitView();

            if (config.mainWindowPosX != -1 && config.mainWindowPosY != -1)
            {
                _windowRect.x = config.mainWindowPosX;
                _windowRect.y = config.mainWindowPosY;
            }

            MTEUtils.AdjustWindowPosition(ref _windowRect);
        }

        public void OnGUI()
        {
            if (!isShowWnd)
            {
                return;
            }

            InitGUI();

            if (_windowWidth != config.mainWindowWidth ||
                _windowHeight != config.mainWindowHeight)
            {
                _windowWidth = config.mainWindowWidth;
                _windowHeight = config.mainWindowHeight;
                _windowRect.width = _windowWidth;
                _windowRect.height = _windowHeight;
                InitView();

                // 拡大でウィンドウが画面外へはみ出さないよう位置も詰め直す
                MTEUtils.AdjustWindowPosition(ref _windowRect);
            }

            windowRect = GUI.Window(WINDOW_ID, windowRect, DrawWindow, PluginInfo.WindowName, GUIView.gsWin);
            MTEUtils.ResetInputOnScroll(windowRect);

            if (config.mainWindowPosX != (int)windowRect.x ||
                config.mainWindowPosY != (int)windowRect.y)
            {
                config.mainWindowPosX = (int)windowRect.x;
                config.mainWindowPosY = (int)windowRect.y;
                config.dirty = true;
            }
        }

        private void DrawWindow(int id)
        {
            _rootView.ResetLayout();

            DrawHeader();
            DrawContent();
            DrawResizeGrip();

            _rootView.DrawComboBox();

            // リサイズ中にウィンドウ移動が同時に走ると位置とサイズが競合するため抑止する
            if (!_windowSizeDragInfo.isDragging)
            {
                GUI.DragWindow();
            }
        }

        // 右下のリサイズグリップ。実サイズは config 経由で OnGUI が反映する
        private void DrawResizeGrip()
        {
            var view = _footerView;
            view.ResetLayout();

            // フッター内の右端に合わせるため、padding/margin は入れない
            view.padding = Vector2.zero;
            view.margin = 0;

            view.BeginLayout(GUIView.LayoutDirection.Free);

            view.currentPos.x = _windowWidth - FOOTER_HEIGHT;

            view.DrawDraggableButton("□", FOOTER_HEIGHT, FOOTER_HEIGHT,
                _windowSizeDragInfo,
                new Vector2(_windowWidth, _windowHeight),
                null,
                value =>
                {
                    config.mainWindowWidth = (int)value.x;
                    config.mainWindowHeight = (int)value.y;

                    ClampWindowSize();

                    config.dirty = true;
                });
        }

        private void DrawHeader()
        {
            var view = _headerView;
            view.ResetLayout();

            view.padding = Vector2.zero;

            view.currentPos.x = _windowWidth - 20;

            if (view.DrawButton("x", 20, 20))
            {
                plugin.isEnable = false;
            }
        }

        private void DrawContent()
        {
            var view = _contentView;
            view.ResetLayout();
            view.SetEnabled(!view.IsComboBoxFocused());

            view.BeginHorizontal();
            {
                for (var i = 0; i < ModeNames.Length; i++)
                {
                    var selected = i == _modeIndex;
                    if (view.DrawButton(ModeNames[i], 80, 20, true, selected ? Color.green : (Color?)null))
                    {
                        _modeIndex = i;
                    }
                }
            }
            view.EndLayout();

            view.DrawHorizontalLine(Color.gray);

            if (_modeIndex == 0)
            {
                DrawEffectContent(view);
            }
            else
            {
                DrawPresetContent(view);
            }
        }

        // 現在位置からビューの下端までをスクロール領域に充てる
        // (ビューの高さはヘッダ・フッタを除いた値が InitView で設定されている)
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

                view.currentPos.x = _windowWidth - 80;
                if (view.DrawButton("リセット", 60, 20))
                {
                    ResetCategory(_categoryComboBox.currentItem);
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
            view.BeginHorizontal();
            {
                // 行のチェックボックスが有効トグルそのもの。ON で下に設定項目を展開する
                view.DrawToggle(controller.effectName, controller.effectEnabled,
                    view.viewRect.width - 90, 20, value =>
                {
                    controller.effectEnabled = value;
                    settings.dirty = true;
                });

                if (controller.effectEnabled)
                {
                    view.currentPos.x = view.viewRect.width - 80;
                    if (view.DrawButton("リセット", 60, 20))
                    {
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

        private void DrawPresetContent(GUIView view)
        {
            // 名前入力と保存
            view.BeginHorizontal();
            {
                view.DrawTextField("名前", 40, _presetName, _windowWidth - 120, 20,
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
                view.DrawLabel("「既定」で選んだプリセットを起動時に読み込みます", _windowWidth - 90, 20);

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

                        if (view.DrawButton("既定", 50, 20, true, isStartup ? Color.green : (Color?)null))
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
