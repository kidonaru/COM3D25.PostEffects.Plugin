using System.Reflection;
using COM3D2.MotionTimelineEditor;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// アセンブリに埋め込んだ画像リソースを読み込む。
    /// MotionTimelineEditor はアセットバンドルを使っているが、
    /// アイコン数枚のためにバンドルを持つ必要はないので PNG を直接埋め込んでいる
    /// </summary>
    public static class PluginResources
    {
        private static Texture2D _changeIcon = null;
        public static Texture2D changeIcon
        {
            get
            {
                if (_changeIcon == null) _changeIcon = LoadTexture("change_icon.png");
                return _changeIcon;
            }
        }

        private static Texture2D _openIcon = null;
        public static Texture2D openIcon
        {
            get
            {
                if (_openIcon == null) _openIcon = LoadTexture("open_icon.png");
                return _openIcon;
            }
        }

        private static Texture2D _updateIcon = null;
        public static Texture2D updateIcon
        {
            get
            {
                if (_updateIcon == null) _updateIcon = LoadTexture("update_icon.png");
                return _updateIcon;
            }
        }

        public static Texture2D LoadTexture(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    MTEUtils.LogError("リソースが見つかりません: {0}", resourceName);
                    return null;
                }

                var bytes = new byte[stream.Length];
                stream.Read(bytes, 0, bytes.Length);

                // シーン遷移時の Resources.UnloadUnusedAssets() で破棄されないように保護する
                var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };

                if (!texture.LoadImage(bytes))
                {
                    MTEUtils.LogError("テクスチャの読み込みに失敗しました: {0}", resourceName);
                    return null;
                }

                return texture;
            }
        }
    }
}
