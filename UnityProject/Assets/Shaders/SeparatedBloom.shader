Shader "Hidden/PostEffects/SeparatedBloom"
{
    Properties
    {
        _MainTex ("Source", 2D) = "black" {}
        _MaskTex ("Mask", 2D) = "black" {}
        _CharacterTex ("Characters", 2D) = "black" {}
        _BackgroundTex ("Background", 2D) = "black" {}
    }
    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex, _MaskTex, _CharacterTex, _BackgroundTex;
    float4 _MainTex_TexelSize;
    float _Characters;

    float4 extract(v2f_img i) : SV_Target
    {
        float2 uvMask = i.uv;
        #if UNITY_UV_STARTS_AT_TOP
        if (_MainTex_TexelSize.y < 0) uvMask.y = 1 - uvMask.y;
        #endif
        float mask = saturate(tex2D(_MaskTex, uvMask).r);
        float weight = lerp(1 - mask, mask, _Characters);
        return tex2D(_MainTex, i.uv) * weight;
    }

    float4 composite(v2f_img i) : SV_Target
    {
        // HDR の輝度を保持し、アルファは元画像から引き継ぐ。
        float3 color = tex2D(_CharacterTex, i.uv).rgb + tex2D(_BackgroundTex, i.uv).rgb;
        return float4(color, tex2D(_MainTex, i.uv).a);
    }
    ENDCG
    SubShader
    {
        ZTest Always Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment extract
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment composite
            ENDCG
        }
    }
}
