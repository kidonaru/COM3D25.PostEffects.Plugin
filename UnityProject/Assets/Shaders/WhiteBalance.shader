// ホワイトバランス: 色温度 / ティントを LMS 色空間で補正する (PPSv2 の White Balance 相当)。
// 温度→CIE xy→LMS のホワイトポイント比は C# 側で計算し _Balance として受け取る
Shader "Hidden/PostEffects/WhiteBalance"
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
            float3 _Balance;

            // PPSv2 と同じ CAT02 ベースの RGB⇔LMS 変換行列
            static const float3x3 LIN_2_LMS_MAT = float3x3(
                3.90405e-1, 5.49941e-1, 8.92632e-3,
                7.08416e-2, 9.63172e-1, 1.35775e-3,
                2.31082e-2, 1.28021e-1, 9.36245e-1);

            static const float3x3 LMS_2_LIN_MAT = float3x3(
                 2.85847e+0, -1.62879e+0, -2.48910e-2,
                -2.10182e-1,  1.15820e+0,  3.24281e-4,
                -4.18120e-2, -1.18169e-1,  1.06867e+0);

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
                float4 c = tex2D(_MainTex, i.uv);
                // 画面バッファはガンマ空間 (本プロジェクトは Gamma カラースペース前提) のため、
                // リニア化してから LMS で補正する。Linear 設定に変えた場合はこの pow を外すこと
                float3 lin = pow(max(c.rgb, 0), 2.2);
                float3 lms = mul(LIN_2_LMS_MAT, lin) * _Balance;
                lin = max(mul(LMS_2_LIN_MAT, lms), 0);
                return fixed4(saturate(pow(lin, 1.0 / 2.2)), c.a);
            }
            ENDCG
        }
    }
}
