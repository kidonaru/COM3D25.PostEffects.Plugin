// キャラマスク描画用: 対象を単色白で塗りつぶす (RenderWithShader / 置き換え描画用)
// アルファテスト系 (髪・まつ毛) も輪郭ごとマスクしたいので不透明として全塗りする
Shader "Hidden/PostEffects/CharMaskWhite"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Pass
        {
            ZWrite On
            ZTest LEqual
            Cull Off
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
