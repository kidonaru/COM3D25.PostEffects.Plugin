using UnityEditor;
using UnityEngine;
using System.IO;

// posteffects シェーダーバンドルのビルド。メニューまたは batchmode (-executeMethod) から実行する
// Unity 5.6.4f1 (COM3D2 と同世代) でビルドしたバンドルは COM3D2.5 (Unity 2022.3) でも読めるため、
// 両バージョン共通の 1 バンドルとして UnityInjector\Config\PostEffects\Shaders\ に配置する
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
                "Assets/Shaders/CharMaskOccluder.shader",
                "Assets/Shaders/CharMaskChannel.shader",
                "Assets/Shaders/ObscuranceMask.shader",
                "Assets/Shaders/CharMaskComposite.shader",
                "Assets/Shaders/SeparatedBloom.shader",
                "Assets/Shaders/OutlineColor.shader",
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

        // MotionTimelineEditor と同じ設定。Windows 向けバンドルは x86/x64 で共通フォーマットなので
        // StandaloneWindows で両ゲーム (64bit) に使える。ForceRebuild は増分キャッシュ由来の古い
        // シェーダー混入を防ぐため (15 本程度なので毎回フルビルドしても十分速い)
        AssetBundleManifest manifest;
        try
        {
            manifest = BuildPipeline.BuildAssetBundles(
                OutputDir,
                new[] { build },
                BuildAssetBundleOptions.ForceRebuildAssetBundle,
                BuildTarget.StandaloneWindows);
        }
        catch (System.Exception e)
        {
            // batchmode の Unity は例外でも終了コード 0 で抜けることがあるため明示的に失敗させる
            Debug.LogError("AssetBundle のビルドで例外が発生しました: " + e);
            EditorApplication.Exit(1);
            return;
        }

        if (manifest == null)
        {
            Debug.LogError("AssetBundle のビルドに失敗しました");
            EditorApplication.Exit(1);
            return;
        }
        Debug.Log("AssetBundle をビルドしました: " + Path.GetFullPath(OutputDir));
    }
}
