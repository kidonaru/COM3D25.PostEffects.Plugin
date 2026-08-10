using System.Collections.Generic;
using System.IO;
using COM3D2.MotionTimelineEditor;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// SceneCapture 由来のシェーダー AssetBundle (Config\PostEffects\Shaders\*) を遅延読込して
    /// シェーダーを提供する。バンドルは Unity 5.6 世代ビルドだが 2.5 (Unity 2022.3) でも
    /// 全シェーダーが動作することを実機確認済み (docs/scenecapture-ui-diff.md §5.1)
    /// </summary>
    public static class EffectShaders
    {
        public const string ImageEffects = "imageeffects";
        public const string Kino = "kino";
        public const string Cinematic = "cinematic";
        public const string LightShafts = "lightshafts";
        public const string Filmic = "filmic";
        // 本プラグイン自前のシェーダー (UnityProject でビルド)
        public const string PostEffects = "posteffects";

        private static readonly string ShaderDir =
            Path.Combine(PluginUtils.UserDataPath, Path.Combine("PostEffects", "Shaders"));

        private static readonly Dictionary<string, AssetBundle> _bundles = new Dictionary<string, AssetBundle>();
        // 読込失敗したバンドルを毎フレーム再試行しないための記録
        private static readonly HashSet<string> _failedBundles = new HashSet<string>();
        // 呼び出し側 (ApplySetting) はシェーダーが取れない限り毎フレーム再要求してくるため、
        // 失敗 (null) も含めて結果をキャッシュし、再試行とログスパムを防ぐ
        private static readonly Dictionary<string, Shader> _shaderCache = new Dictionary<string, Shader>();

        public static Shader GetShader(string bundleName, string assetName)
        {
            var cacheKey = bundleName + "/" + assetName;
            Shader shader;
            if (_shaderCache.TryGetValue(cacheKey, out shader))
            {
                return shader;
            }

            var bundle = GetBundle(bundleName);
            if (bundle == null)
            {
                return null;
            }

            shader = bundle.LoadAsset<Shader>(assetName);
            if (shader == null)
            {
                MTEUtils.LogError("シェーダーが見つかりません: {0}/{1}", bundleName, assetName);
            }
            _shaderCache[cacheKey] = shader;
            return shader;
        }

        private static AssetBundle GetBundle(string name)
        {
            AssetBundle bundle;
            if (_bundles.TryGetValue(name, out bundle))
            {
                return bundle;
            }
            if (_failedBundles.Contains(name))
            {
                return null;
            }

            var path = Path.Combine(ShaderDir, name);
            bundle = AssetBundle.LoadFromFile(path);
            if (bundle == null)
            {
                MTEUtils.LogError("シェーダーバンドルの読込に失敗しました: {0}", path);
                _failedBundles.Add(name);
                return null;
            }

            _bundles[name] = bundle;
            return bundle;
        }
    }
}
