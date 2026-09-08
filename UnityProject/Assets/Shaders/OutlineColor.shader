// 元の輪郭の被覆率を使い、近傍の色を線の部分だけへ混ぜる。
Shader "Hidden/PostEffects/OutlineColor"
{
    Properties
    {
        _MainTex ("元画像", 2D) = "white" {}
        _MaskTex ("輪郭マスク", 2D) = "black" {}
    }
    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex;
    sampler2D _MaskTex;
    float4 _MainTex_TexelSize;
    float _Intensity, _SampleRadius, _Saturation, _TintStrength;
    float4 _Tint;

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
        float2 maskUV : TEXCOORD1;
    };

    v2f vert(appdata_img v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.texcoord;
        o.maskUV = v.texcoord;
        #if UNITY_UV_STARTS_AT_TOP
        if (_MainTex_TexelSize.y < 0) o.maskUV.y = 1 - o.maskUV.y;
        #endif
        return o;
    }

    float Luma(float3 c) { return dot(c, float3(0.2126, 0.7152, 0.0722)); }

    void Accumulate(v2f i, float2 offset, inout float3 sum, inout float weight)
    {
        float2 maskOffset = offset;
        #if UNITY_UV_STARTS_AT_TOP
        if (_MainTex_TexelSize.y < 0) maskOffset.y = -maskOffset.y;
        #endif
        float3 c = max(0, tex2D(_MainTex, i.uv + offset).rgb);
        // 黒い線同士の平均へ寄りすぎないよう、線の外側と明るい近傍を優先する。
        float w = (1 - saturate(tex2D(_MaskTex, i.maskUV + maskOffset).a)) * (Luma(c) + 0.001);
        sum += c * w;
        weight += w;
    }

    float4 frag(v2f i) : SV_Target
    {
        float4 src = tex2D(_MainTex, i.uv);
        float mask = saturate(tex2D(_MaskTex, i.maskUV).a);
        // 大半を占める線以外の画素では近傍参照を省き、元画像をそのまま返す。
        [branch] if (mask <= 0 || _Intensity <= 0) return src;
        float2 d = abs(_MainTex_TexelSize.xy) * _SampleRadius;
        float3 sum = 0;
        float weight = 0;
        Accumulate(i, float2(d.x, 0), sum, weight);
        Accumulate(i, float2(-d.x, 0), sum, weight);
        Accumulate(i, float2(0, d.y), sum, weight);
        Accumulate(i, float2(0, -d.y), sum, weight);
        Accumulate(i, d * 0.70710678, sum, weight);
        Accumulate(i, -d * 0.70710678, sum, weight);
        Accumulate(i, float2(d.x, -d.y) * 0.70710678, sum, weight);
        Accumulate(i, float2(-d.x, d.y) * 0.70710678, sum, weight);
        float3 nearby = weight > 0.00001 ? sum / weight : src.rgb;
        nearby = max(0, lerp(Luma(nearby).xxx, nearby, _Saturation));
        nearby = lerp(nearby, _Tint.rgb * Luma(nearby), _TintStrength);
        return float4(lerp(src.rgb, nearby, mask * _Intensity), src.a);
    }

    float4 fragMask(v2f i) : SV_Target
    {
        float m = saturate(tex2D(_MaskTex, i.maskUV).a);
        return float4(m, m, m, 1);
    }
    ENDCG
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment fragMask
            ENDCG
        }
    }
    Fallback Off
}
