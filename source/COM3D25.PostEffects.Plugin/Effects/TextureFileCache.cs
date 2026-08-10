using System;
using System.IO;
using COM3D2.MotionTimelineEditor;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    // パス指定のテクスチャ読み込みと、パス入力 UI 一式をまとめたもの。
    // ScreenOverlay のオーバーレイ画像と DoF の DX11 ボケテクスチャで共用する
    public class TextureFileCache
    {
        /// <summary>同梱・ユーザー追加の画像を置くフォルダ</summary>
        public static readonly string ImageDir =
            Path.Combine(PluginUtils.UserDataPath, Path.Combine("PostEffects", "Images"));

        // 用途別のサブフォルダ名。ImageDir 配下に同名のフォルダを同梱している
        public const string SUB_DIR_BOKEH = "Bokeh";
        public const string SUB_DIR_LUT = "LUTs";
        public const string SUB_DIR_LENS_DIRT = "LensDirt";
        public const string SUB_DIR_OVERLAY = "Overlay";
        public const string SUB_DIR_RAMP = "Ramp";

        private Texture2D _texture;
        private string _loadedPath;
        private string _loadError;

        // 色変換テーブル等、ガンマ補正をかけずに画素値をそのまま扱いたい用途で true にする
        private readonly bool _linear;

        // 画像選択ウィンドウと前後送りが走査する ImageDir 配下のサブフォルダ
        private readonly string _searchDir;

        private const int BUTTON_SIZE = 20;

        /// <param name="subDir">用途別のサブフォルダ名（Bokeh / LUTs / LensDirt など）</param>
        public TextureFileCache(string subDir, bool linear = false)
        {
            _searchDir = Path.Combine(ImageDir, subDir);
            _linear = linear;
        }

        // path は絶対パス、または Config フォルダからの相対パス
        public Texture2D GetOrLoad(string path)
        {
            // 読み込み済みテクスチャがシーン遷移等で破棄されていたら、黙って消えないよう読み直す
            var alive = _texture != null || string.IsNullOrEmpty(_loadedPath) || _loadError != null;
            if (path == _loadedPath && alive)
            {
                return _texture;
            }

            if (_texture != null)
            {
                UnityEngine.Object.Destroy(_texture);
                _texture = null;
            }
            _loadedPath = path;
            _loadError = null;

            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            var fullPath = Path.IsPathRooted(path) ? path : Path.Combine(PluginUtils.UserDataPath, path);
            if (!File.Exists(fullPath))
            {
                _loadError = "ファイルが見つかりません: " + fullPath;
                MTEUtils.LogError(_loadError);
                return null;
            }

            var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false, _linear);
            if (!texture.LoadImage(File.ReadAllBytes(fullPath)))
            {
                UnityEngine.Object.Destroy(texture);
                _loadError = "画像の読み込みに失敗しました: " + fullPath;
                MTEUtils.LogError(_loadError);
                return null;
            }

            // Resources.UnloadUnusedAssets で回収されないようにする
            texture.hideFlags = HideFlags.HideAndDontSave;
            _texture = texture;
            return _texture;
        }

        /// <summary>
        /// 対象フォルダ内の画像を並び順で前後に送ったパスを返す。
        /// 現在のパスが一覧に無い（フォルダ外を直接指定している等）場合は端から始める
        /// </summary>
        private string GetSiblingPath(string path, int offset)
        {
            var files = TexturePickerWindow.ListImageFiles(_searchDir, PluginUtils.UserDataPath);
            if (files.Count == 0)
            {
                return path;
            }

            // path は絶対パスのこともあるため、一覧側と同じ相対パスに揃えて探す
            var currentPath = TexturePickerWindow.MakeRelativePath(PluginUtils.UserDataPath, path ?? "");
            var index = files.FindIndex(f => string.Equals(f, currentPath, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                return offset > 0 ? files[0] : files[files.Count - 1];
            }

            // 端では反対側へ回り込む
            return files[((index + offset) % files.Count + files.Count) % files.Count];
        }

        // パス入力欄・前後送り/選択/再読込ボタン・読込状態表示をまとめて描画する
        public void DrawPathField(GUIView view, string label, string path, Action<string> onChanged)
        {
            var picker = TexturePickerWindow.instance;
            var isPicking = picker.IsEditing(this);

            view.DrawLabel(label + " (絶対 or Config 相対)", 250, 20);

            view.BeginHorizontal();
            {
                view.DrawTextField(new GUIView.TextFieldOption
                {
                    value = path,
                    // ボタン 4 つ分を空けて入力欄を敷く
                    width = view.viewRect.width - view.padding.x * 2 - BUTTON_SIZE * 4,
                    onChanged = onChanged,
                    // クリップボードの C/P より前後送りの方が使うため差し替える
                    hiddenButton = true,
                });

                if (view.DrawButton("<", BUTTON_SIZE, BUTTON_SIZE))
                {
                    onChanged(GetSiblingPath(path, -1));
                }

                if (view.DrawButton(">", BUTTON_SIZE, BUTTON_SIZE))
                {
                    onChanged(GetSiblingPath(path, 1));
                }

                // 選択ウィンドウをボタンに被らない位置へ出すため、描画前に矩形を控えておく
                var buttonRect = view.GetDrawRect(BUTTON_SIZE, BUTTON_SIZE);

                if (view.DrawTextureButton(PluginResources.openIcon, BUTTON_SIZE, BUTTON_SIZE, 4))
                {
                    if (isPicking)
                    {
                        picker.Close();
                    }
                    else
                    {
                        var screenPos = GUIUtility.GUIToScreenPoint(buttonRect.position);
                        var anchorRect = new Rect(screenPos.x, screenPos.y, buttonRect.width, buttonRect.height);

                        picker.Open(this, label, path, _searchDir, PluginUtils.UserDataPath, onChanged, anchorRect);
                    }
                }

                if (view.DrawTextureButton(PluginResources.updateIcon, BUTTON_SIZE, BUTTON_SIZE, 4))
                {
                    // 同一パスでもファイル内容の変更を反映できるよう、キャッシュを破棄する
                    _loadedPath = null;
                }
            }
            view.EndLayout();

            // 選択ウィンドウへ最新の状態を渡す。渡されなくなったら向こう側で自動的に閉じる
            picker.Sync(this, path, onChanged);

            if (_loadError != null)
            {
                view.DrawLabel(_loadError, -1, 20, Color.red);
            }
            else if (_texture != null)
            {
                view.DrawLabel(
                    string.Format("読込済: {0}x{1}", _texture.width, _texture.height),
                    -1, 20);
            }
        }
    }
}
