using UnityEditor;
using UnityEngine;
using System.IO;

// posteffects シェーダーバンドルのビルド。メニューまたは batchmode (-executeMethod) から実行する
public static class BuildShaderBundles
{
    private const string OutputDir = "AssetBundles";

    [MenuItem("PostEffects/Build Shader Bundles")]
    public static void Build()
    {
        Directory.CreateDirectory(OutputDir);

        var build = new AssetBundleBuild
        {
            assetBundleName = "posteffects",
            assetNames = new[]
            {
                "Assets/Shaders/CharMaskWhite.shader",
                "Assets/Shaders/CharMaskChannel.shader",
                "Assets/Shaders/ObscuranceMask.shader",
                "Assets/Shaders/CharMaskComposite.shader",
                "Assets/Shaders/Diffusion.shader",
                "Assets/Shaders/CasSharpen.shader",
                "Assets/Shaders/Halftone.shader",
                "Assets/Shaders/WhiteBalance.shader",
                "Assets/Shaders/RadialBlur.shader",
                "Assets/Shaders/Kuwahara.shader",
                "Assets/Shaders/PostEffect.shader",
                "Assets/Shaders/GTToneMap.shader",
            },
        };

        var manifest = BuildPipeline.BuildAssetBundles(
            OutputDir,
            new[] { build },
            BuildAssetBundleOptions.None,
            BuildTarget.StandaloneWindows64);

        if (manifest == null)
        {
            Debug.LogError("AssetBundle のビルドに失敗しました");
            EditorApplication.Exit(1);
            return;
        }
        Debug.Log("AssetBundle をビルドしました: " + Path.GetFullPath(OutputDir));
    }
}
