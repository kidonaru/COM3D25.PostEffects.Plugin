// シャープネス: AMD FidelityFX CAS (Contrast Adaptive Sharpening) の簡易移植。
// 近傍のコントラストに応じてシャープ量を自動調整するため、輪郭のリンギングが出にくい
Shader "Hidden/PostEffects/CasSharpen"
{
    Properties
    {
        _MainTex ("Base", 2D) = "" {}
    }

    SubShader
    {
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            // 0〜1。CAS 定義に沿って developer maximum (-0.2) 〜 minimum (-0.125) を補間する
            float _Sharpness;

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

            fixed4 frag(v2f i) : SV_Target
            {
                // 十字近傍サンプル (原典 CAS の記号に合わせる): a=上 b=左 c=中心 d=右 e=下
                float2 t = _MainTex_TexelSize.xy;
                float3 a = tex2D(_MainTex, i.uv + float2(0, -t.y)).rgb;
                float3 b = tex2D(_MainTex, i.uv + float2(-t.x, 0)).rgb;
                float4 center = tex2D(_MainTex, i.uv);
                float3 c = center.rgb;
                float3 d = tex2D(_MainTex, i.uv + float2(t.x, 0)).rgb;
                float3 e = tex2D(_MainTex, i.uv + float2(0, t.y)).rgb;

                float3 mn = min(min(min(a, b), min(d, e)), c);
                float3 mx = max(max(max(a, b), max(d, e)), c);

                // 近傍の明暗レンジが狭いほど強く締める (CAS の適応項)
                float3 amp = sqrt(saturate(min(mn, 1 - mx) / max(mx, 1e-4)));
                float peak = lerp(-0.125, -0.2, _Sharpness);
                float3 w = amp * peak;

                float3 col = (c + (a + b + d + e) * w) / (1 + 4 * w);
                return fixed4(saturate(col), center.a);
            }
            ENDCG
        }
    }
}
