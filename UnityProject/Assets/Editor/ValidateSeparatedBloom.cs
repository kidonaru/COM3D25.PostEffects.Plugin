using System;
using UnityEditor;
using UnityEngine;

// batchmode でも GPU 上で検証する。-nographics は指定しないこと。
public static class ValidateSeparatedBloom
{
    [MenuItem("PostEffects/Validate Separated Bloom")]
    public static void Run()
    {
        try
        {
            ValidateSplit();
            ValidateOcclusion();
            Debug.Log("ブルーム分離検証: HDR抽出・相補性・合成・アルファ・背景遮蔽がすべて成功しました");
        }
        catch (Exception e)
        {
            Debug.LogError("ブルーム分離検証に失敗しました: " + e);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            else throw;
        }
    }

    private static void ValidateSplit()
    {
        var shader = Shader.Find("Hidden/PostEffects/SeparatedBloom");
        Require(shader != null && shader.isSupported, "分離シェーダーを利用できません");
        var material = new Material(shader);
        var source = new RenderTexture(8, 8, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        var character = new RenderTexture(8, 8, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        var background = new RenderTexture(8, 8, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        var output = new RenderTexture(8, 8, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        var mask = new Texture2D(8, 8, TextureFormat.RGBAFloat, false, true);
        var originalActive = RenderTexture.active;
        try
        {
            RenderTexture.active = source;
            GL.Clear(false, true, new Color(4f, 2f, 0.5f, 0.3f));
            for (var y = 0; y < 8; y++)
                for (var x = 0; x < 8; x++)
                    mask.SetPixel(x, y, new Color(x < 3 ? 1f : x < 5 ? 0.25f : 0f, 0f, 0f, 1f));
            mask.Apply();
            mask.filterMode = FilterMode.Point;
            material.SetTexture("_MaskTex", mask);
            material.SetFloat("_Characters", 1f);
            Graphics.Blit(source, character, material, 0);
            material.SetFloat("_Characters", 0f);
            Graphics.Blit(source, background, material, 0);
            Near(Read(character, 1, 4).r, 4f, "キャラのHDR輝度");
            Near(Read(character, 6, 4).r, 0f, "キャラ入力への背景混入");
            Near(Read(background, 1, 4).r, 0f, "背景入力へのキャラ混入");
            Near(Read(background, 6, 4).r, 4f, "背景のHDR輝度");
            Near(Read(character, 4, 4).r, 1f, "境界マスクの重み");
            Near(Read(background, 4, 4).r, 3f, "境界マスクの相補性");

            material.SetTexture("_CharacterTex", character);
            material.SetTexture("_BackgroundTex", background);
            Graphics.Blit(source, output, material, 1);
            foreach (var x in new[] { 1, 4, 6 })
            {
                var pixel = Read(output, x, 4);
                Near(pixel.r, 4f, "無効時の元画像復元");
                Near(pixel.g, 2f, "合成の色保持");
                Near(pixel.a, 0.3f, "元画像のアルファ保持");
            }
            // 輪郭外に広がったキャラ光を想定した結果を合成しても、再マスクで消さない。
            RenderTexture.active = character;
            GL.Clear(false, true, new Color(0.5f, 0f, 0f, 0f));
            Graphics.Blit(source, output, material, 1);
            Near(Read(output, 6, 4).r, 4.5f, "輪郭外の光の保持");
        }
        finally
        {
            RenderTexture.active = originalActive;
            UnityEngine.Object.DestroyImmediate(material);
            UnityEngine.Object.DestroyImmediate(mask);
            foreach (var rt in new[] { source, character, background, output })
            {
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }
    }

    private static void ValidateOcclusion()
    {
        var go = new GameObject("ブルームマスク検証カメラ");
        var camera = go.AddComponent<Camera>();
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var character = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var rt = new RenderTexture(32, 32, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        try
        {
            camera.enabled = false;
            camera.transform.position = new Vector3(0f, 0f, -5f);
            camera.orthographic = true;
            camera.orthographicSize = 2f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 20f;
            camera.targetTexture = rt;
            camera.allowMSAA = false;
            camera.allowHDR = false;
            wall.layer = 30;
            character.layer = 31;
            wall.transform.position = Vector3.zero;
            character.transform.position = new Vector3(0f, 0f, 2f);
            var occluder = Shader.Find("Hidden/PostEffects/CharMaskOccluder");
            var white = Shader.Find("Hidden/PostEffects/CharMaskWhite");
            Require(occluder != null && occluder.isSupported && white != null && white.isSupported,
                "マスクシェーダーを利用できません");
            foreach (var visible in new[] { false, true })
            {
                wall.SetActive(!visible);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.cullingMask = 1 << wall.layer;
                camera.RenderWithShader(occluder, "RenderType");
                camera.clearFlags = CameraClearFlags.Nothing;
                camera.cullingMask = 1 << character.layer;
                camera.RenderWithShader(white, "");
                Near(Read(rt, 16, 16).r, visible ? 1f : 0f, "背景によるキャラの遮蔽");
            }
        }
        finally
        {
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(wall);
            UnityEngine.Object.DestroyImmediate(character);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
        }
    }

    private static Color Read(RenderTexture rt, int x, int y)
    {
        var previous = RenderTexture.active;
        var readback = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
        try
        {
            RenderTexture.active = rt;
            readback.ReadPixels(new Rect(x, y, 1, 1), 0, 0);
            readback.Apply();
            return readback.GetPixel(0, 0);
        }
        finally
        {
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(readback);
        }
    }

    private static void Near(float actual, float expected, string label)
    {
        Require(Mathf.Abs(actual - expected) < 0.002f,
            label + ": 期待値=" + expected + ", 実測=" + actual);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
