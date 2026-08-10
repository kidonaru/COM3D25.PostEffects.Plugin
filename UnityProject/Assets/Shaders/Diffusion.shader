// ディフュージョン (ソフトフォーカス): 明部だけを淡くにじませるポートレート定番エフェクト
// パス 0 = 明部抽出 (縮小バッファへ), パス 1/2 = ガウスぼかし 横/縦, パス 3 = 合成
Shader "Hidden/PostEffects/Diffusion"
{
    Properties
    {
        _MainTex ("Base", 2D) = "" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _BlurTex;
    float _Threshold;
    float _BlurSize;
    float _Intensity;
    // 0 = Screen, 1 = Lighten
    float _UseLighten;

    struct v2f
    {
        float4 pos : SV_Position;
        float2 uv : TEXCOORD0;
    };

    v2f vert(appdata_img v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.texcoord;
        return o;
    }

    // UnityCG の Luminance() は unity_ColorSpaceLuminance 依存で、実機では白が 0.5 になるため使わない
    float luma(float3 c)
    {
        return dot(c, float3(0.299, 0.587, 0.114));
    }

    // 明部抽出: しきい値以下を切り捨て、超過分を 0〜1 に正規化する
    fixed4 frag_prefilter(v2f i) : SV_Target
    {
        fixed4 c = tex2D(_MainTex, i.uv);
        float lum = luma(c.rgb);
        float amount = saturate((lum - _Threshold) / max(1e-4, 1 - _Threshold));
        return fixed4(c.rgb * amount, 1);
    }

    // 9 タップガウス (係数は正規化済み)
    static const float weights[5] = { 0.227027, 0.194594, 0.121621, 0.054054, 0.016216 };

    fixed4 blur(v2f i, float2 dir)
    {
        float2 step = _MainTex_TexelSize.xy * dir * _BlurSize;
        fixed3 sum = tex2D(_MainTex, i.uv).rgb * weights[0];
        for (int t = 1; t < 5; t++)
        {
            sum += tex2D(_MainTex, i.uv + step * t).rgb * weights[t];
            sum += tex2D(_MainTex, i.uv - step * t).rgb * weights[t];
        }
        return fixed4(sum, 1);
    }

    fixed4 frag_blur_h(v2f i) : SV_Target { return blur(i, float2(1, 0)); }
    fixed4 frag_blur_v(v2f i) : SV_Target { return blur(i, float2(0, 1)); }

    fixed4 frag_composite(v2f i) : SV_Target
    {
        fixed4 base = tex2D(_MainTex, i.uv);
        fixed3 glow = tex2D(_BlurTex, i.uv).rgb * _Intensity;

        fixed3 screen = 1 - (1 - base.rgb) * (1 - glow);
        fixed3 lighten = max(base.rgb, glow);
        return fixed4(lerp(screen, lighten, _UseLighten), base.a);
    }
    ENDCG

    SubShader
    {
        ZTest Always Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag_prefilter
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag_blur_h
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag_blur_v
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag_composite
            ENDCG
        }
    }
}
