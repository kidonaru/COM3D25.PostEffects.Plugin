// 頭部マスク描画用: 対象を白で塗るが、書き込み先チャンネルを _ColorMask で選択できる (R=顔 / G=髪 の塗り分け)。
// CharMaskWhite とは分離している。あちらは CharacterMask の RenderWithShader (置換描画) で使われ、
// 置換描画ではプロパティが各オブジェクトのマテリアルから解決されるため ColorMask プロパティを持たせられない。
// このシェーダーは CommandBuffer.DrawRenderer + 自前マテリアル専用
Shader "Hidden/PostEffects/CharMaskChannel"
{
    Properties
    {
        // 書き込み先チャンネル (R=8, G=4, B=2, A=1 のビット和)
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Pass
        {
            ZWrite On
            ZTest LEqual
            Cull Off
            ColorMask [_ColorMask]
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 vert(float4 vertex : POSITION) : SV_Position
            {
                return UnityObjectToClipPos(vertex);
            }

            fixed4 frag() : SV_Target
            {
                return fixed4(1, 1, 1, 1);
            }
            ENDCG
        }
    }
}
