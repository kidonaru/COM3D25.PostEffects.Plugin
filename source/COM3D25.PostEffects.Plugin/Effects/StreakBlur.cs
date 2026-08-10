using System.Collections.Generic;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// 明部を一方向へ引き伸ばす光条のぼかし処理。Streak と FilmicBloom の光条段で共通なので切り出したもの。
    /// しきい値・伸び・強度・色・ブレンドモードのキーワードは呼び出し側でマテリアルへ設定しておく
    /// </summary>
    internal static class StreakBlur
    {
        // kino/filmic の光条シェーダーで共通のパス番号
        private const int PrefilterPass = 0;
        private const int DownsamplePass = 1;
        private const int UpsamplePass = 2;
        private const int CompositePass = 3;
        private const int CombinePass = 5;

        // ダウンサンプルの打ち切りサイズ (これ以下には縮小しない)
        private const int MinMipSize = 16;

        /// <summary>
        /// 縮小段が 1 段も作れない極小解像度かどうか。true の間は光条を掛けられない
        /// </summary>
        public static bool IsTooSmall(RenderTexture source)
        {
            return source.width <= MinMipSize * 2 || source.height <= MinMipSize * 2;
        }

        private static RenderTexture GetTempRT(int width, int height)
        {
            return RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGBHalf);
        }

        /// <summary>
        /// 縦か横の一方向へ光条を伸ばして <paramref name="highTex"/> へ合成する。
        /// <paramref name="input"/> は光条の元になる画 (解放は呼び出し側の責任)
        /// </summary>
        public static void RenderOneWay(
            Material material, RenderTexture input, RenderTexture highTex, RenderTexture destination,
            Stack<RenderTexture> stack, bool vertical)
        {
            var width = vertical ? highTex.width / 2 : highTex.width;
            var height = vertical ? highTex.height : highTex.height / 2;

            if (vertical)
            {
                material.EnableKeyword("_VERTICAL");
            }

            var prefiltered = GetTempRT(width, height);
            Graphics.Blit(input, prefiltered, material, PrefilterPass);

            var last = Downsample(material, prefiltered, stack, width, height, vertical);
            last = Upsample(material, last, stack);

            material.SetTexture("_HighTex", highTex);
            Graphics.Blit(last, destination, material, CompositePass);

            // 縮小段が 0 段だと last と prefiltered が同一になるので二重解放を避ける
            RenderTexture.ReleaseTemporary(last);
            if (last != prefiltered)
            {
                RenderTexture.ReleaseTemporary(prefiltered);
            }
        }

        /// <summary>縦横それぞれに光条を伸ばして合成する</summary>
        public static void RenderTwoWay(
            Material material, RenderTexture input, RenderTexture highTex, RenderTexture destination,
            Stack<RenderTexture> verticalStack, Stack<RenderTexture> horizontalStack)
        {
            var vWidth = highTex.width / 2;
            var vHeight = highTex.height;
            var hWidth = highTex.width;
            var hHeight = highTex.height / 2;

            var vPrefiltered = GetTempRT(vWidth, vHeight);
            var hPrefiltered = GetTempRT(hWidth, hHeight);

            material.EnableKeyword("_VERTICAL");
            Graphics.Blit(input, vPrefiltered, material, PrefilterPass);
            material.DisableKeyword("_VERTICAL");
            Graphics.Blit(input, hPrefiltered, material, PrefilterPass);

            material.EnableKeyword("_VERTICAL");
            var vLast = Downsample(material, vPrefiltered, verticalStack, vWidth, vHeight, true);
            material.DisableKeyword("_VERTICAL");
            var hLast = Downsample(material, hPrefiltered, horizontalStack, hWidth, hHeight, false);

            vLast = Upsample(material, vLast, verticalStack);
            hLast = Upsample(material, hLast, horizontalStack);

            material.SetTexture("_HighTex", vLast);
            Graphics.Blit(hLast, vPrefiltered, material, CombinePass);

            material.SetTexture("_HighTex", highTex);
            Graphics.Blit(vPrefiltered, destination, material, CompositePass);

            // 縮小段が 0 段だと last と prefiltered が同一になるので二重解放を避ける
            RenderTexture.ReleaseTemporary(vLast);
            RenderTexture.ReleaseTemporary(hLast);
            if (vLast != vPrefiltered)
            {
                RenderTexture.ReleaseTemporary(vPrefiltered);
            }
            if (hLast != hPrefiltered)
            {
                RenderTexture.ReleaseTemporary(hPrefiltered);
            }
        }

        // 伸ばす方向に半分ずつ縮小しながら中間結果を stack に積む。戻り値は最小段
        private static RenderTexture Downsample(
            Material material, RenderTexture prefiltered, Stack<RenderTexture> stack,
            int width, int height, bool vertical)
        {
            var last = prefiltered;
            if (vertical)
            {
                while (height > MinMipSize)
                {
                    height /= 2;
                    var rt = GetTempRT(width, height);
                    Graphics.Blit(last, rt, material, DownsamplePass);
                    stack.Push(last = rt);
                }
            }
            else
            {
                while (width > MinMipSize)
                {
                    width /= 2;
                    var rt = GetTempRT(width, height);
                    Graphics.Blit(last, rt, material, DownsamplePass);
                    stack.Push(last = rt);
                }
            }

            // 最小段は last として持ち回るので stack からは降ろす
            // (IsTooSmall で弾いているので通常 1 段以上積まれているが、念のため空を許容する)
            if (stack.Count > 0)
            {
                stack.Pop();
            }
            return last;
        }

        // 積んだ中間結果と混ぜながら元の大きさへ戻す
        private static RenderTexture Upsample(Material material, RenderTexture last, Stack<RenderTexture> stack)
        {
            while (stack.Count > 0)
            {
                var high = stack.Pop();
                var rt = GetTempRT(high.width, high.height);
                material.SetTexture("_HighTex", high);
                Graphics.Blit(last, rt, material, UpsamplePass);
                RenderTexture.ReleaseTemporary(last);
                RenderTexture.ReleaseTemporary(high);
                last = rt;
            }
            return last;
        }
    }
}
