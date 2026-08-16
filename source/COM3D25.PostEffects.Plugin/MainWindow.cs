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

        private static readonly string[] ModeNames = { "エフェクト", "プリセット" };

        private int _modeIndex = 0;  // 0: エフェクト, 1: プリセット
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
            _rootView.Init(new Rect(0, 0, windowRect.width, windowRect.height));

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

            if (plugin.isEnable)
            {
                plugin.isEnable = false;
            }
        }

        protected override void DrawContent()
        {
            _rootView.ResetLayout();

            DrawModeContent();

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
                    if (view.DrawButton(ModeNames[i], 80, 20, true, selected ? GUIView.option.accentColor : (Color?)null))
                    {
                        _modeIndex = i;
                    }
                }

                // 全エフェクトの一時無効化トグル。個々の有効状態は保ったまま適用だけを止める
                view.currentPos.x = view.viewRect.width - 80;
                view.DrawToggle("有効", postEffectManager.effectsEnabled, 60, 20,
                    value => postEffectManager.effectsEnabled = value);
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
