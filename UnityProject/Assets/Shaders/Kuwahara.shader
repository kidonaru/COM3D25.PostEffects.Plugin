// Kuwahara フィルタ (油絵風): 注目画素の周囲 4 象限それぞれで平均と分散を取り、
// 最も分散が小さい (=最も平坦な) 象限の平均色を採用する。エッジを保ちながら面を潰す
Shader "Hidden/PostEffects/Kuwahara"
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
            int _Radius;

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

            // offset 方向の (radius+1)^2 画素の平均と輝度分散を求める
            void SampleRegion(float2 uv, float2 dir, out float3 mean, out float variance)
            {
                float3 sum = 0;
                float lumSum = 0;
                float lumSqSum = 0;
                int n = 0;
                for (int y = 0; y <= _Radius; y++)
                {
                    for (int x = 0; x <= _Radius; x++)
                    {
                        float2 offset = float2(x, y) * dir * _MainTex_TexelSize.xy;
                        float3 c = tex2D(_MainTex, uv + offset).rgb;
                        // UnityCG の Luminance() は実機で係数が壊れているため明示係数を使う
                        float lum = dot(c, float3(0.299, 0.587, 0.114));
                        sum += c;
                        lumSum += lum;
                        lumSqSum += lum * lum;
                        n++;
                    }
                }
                mean = sum / n;
                variance = lumSqSum / n - (lumSum / n) * (lumSum / n);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 mean[4];
                float variance[4];
                SampleRegion(i.uv, float2(1, 1), mean[0], variance[0]);
                SampleRegion(i.uv, float2(-1, 1), mean[1], variance[1]);
                SampleRegion(i.uv, float2(1, -1), mean[2], variance[2]);
                SampleRegion(i.uv, float2(-1, -1), mean[3], variance[3]);

                float3 col = mean[0];
                float minVar = variance[0];
                for (int r = 1; r < 4; r++)
                {
                    if (variance[r] < minVar)
                    {
                        minVar = variance[r];
                        col = mean[r];
                    }
                }
                return fixed4(col, tex2D(_MainTex, i.uv).a);
            }
            ENDCG
        }
    }
}
