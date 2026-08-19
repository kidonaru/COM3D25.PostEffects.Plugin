using System;
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

            var bundle = GetBundle(bundleName, assetName);
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

        private static AssetBundle GetBundle(string name, string assetName)
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
                // 他プラグイン (ShaderChange 等) が同一内容のバンドルを先に読み込んでいると
                // Unity が二重ロードを拒否するため、ロード済みのものを探して共用する
                bundle = FindLoadedBundle(name, assetName);
            }
            if (bundle == null)
            {
                MTEUtils.LogError("シェーダーバンドルの読込に失敗しました: {0}", path);
                _failedBundles.Add(name);
                return null;
            }

            _bundles[name] = bundle;
            return bundle;
        }

        /// <summary>
        /// ロード済みの AssetBundle から目的のものを探す。
        /// Unity 5.6 (COM3D2) には AssetBundle.GetAllLoadedAssetBundles が無いため
        /// 両バージョンで動く Resources.FindObjectsOfTypeAll で代用している。
        /// 見つけたバンドルは他プラグインの所有物なので Unload してはいけない
        /// </summary>
        private static AssetBundle FindLoadedBundle(string name, string assetName)
        {
            var loaded = Resources.FindObjectsOfTypeAll<AssetBundle>();
            AssetBundle fallback = null;

            foreach (var candidate in loaded)
            {
                if (candidate == null)
                {
                    continue;
                }
                if (string.Equals(candidate.name, name, StringComparison.OrdinalIgnoreCase))
                {
                    MTEUtils.LogWarning("シェーダーバンドルは他プラグインが読込済みのため共用します: {0}", name);
                    return candidate;
                }
                // バンドル名が一致しない場合に備え、目的のシェーダーを持つものを次善の候補にする
                if (fallback == null && candidate.Contains(assetName))
                {
                    fallback = candidate;
                }
            }

            if (fallback != null)
            {
                MTEUtils.LogWarning(
                    "シェーダー {0} を他プラグインの読込済みバンドル {1} から取得します ({2} は読込できませんでした)",
                    assetName, fallback.name, name);
            }
            return fallback;
        }
    }
}
