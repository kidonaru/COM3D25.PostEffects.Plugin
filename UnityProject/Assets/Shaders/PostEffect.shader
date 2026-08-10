Shader "PostEffects/PostEffect"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend Off
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile __ DEBUG_VIEW
            #pragma multi_compile __ EXTRA_BLEND
            #pragma multi_compile __ RIMLIGHT
            #pragma multi_compile __ DISTANCE_FOG
            #pragma multi_compile __ PARAFFIN
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            sampler2D _CameraDepthTexture;

            #if PARAFFIN || RIMLIGHT
            sampler2D _CharMaskTex; // キャラマスク (白=キャラ)。CharacterMask.Render 由来
            #endif

            #if RIMLIGHT
            sampler2D _CameraDepthNormalsTexture;
            #endif

            #if RIMLIGHT
            struct RimlightBuffer
            {
                float4 color1;
                float4 color2;
                float3 direction;
                float lightArea;
                float fadeRange;
                float fadeExp;
                float useNormal;
                float useAdd;
                float useMultiply;
                float useOverlay;
                float useSubstruct;
                float excludeFace;
                float applyHair;
                float maskMode; // 0=なし / 1=キャラ除外 / 2=キャラのみ
            };

            StructuredBuffer<RimlightBuffer> _RimlightBuffer;
            int _RimlightCount;
            sampler2D _HeadMaskTex; // 頭部マスク (R=顔, G=髪)。Hub の CommandBuffer DrawRenderer 由来
            #endif

            #if DISTANCE_FOG
            struct DistanceFogBuffer
            {
                float4 color1;
                float4 color2;
                float fogStart;
                float fogEnd;
                float fogExp;
                float useNormal;
                float useAdd;
                float useMultiply;
                float useOverlay;
                float useSubstruct;
            };

            StructuredBuffer<DistanceFogBuffer> _DistanceFogBuffer;
            int _DistanceFogCount;
            #endif

            #if PARAFFIN
            struct ParaffinBuffer
            {
                float4 color1;
                float4 color2;
                float2 centerPosition;
                float radiusFar;
                float radiusNear;
                float2 radiusScale;
                float useNormal;
                float useAdd;
                float useMultiply;
                float useOverlay;
                float useSubstruct;
                float maskMode; // 0=なし / 1=キャラ除外 / 2=キャラのみ
            };

            StructuredBuffer<ParaffinBuffer> _ParaffinBuffer;
            int _ParaffinCount;
            #endif

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 BlendOverlay(float4 base, float4 blend)
            {
                float4 result;
                result.rgb = base.rgb < 0.5 ? 
                    2.0 * base.rgb * blend.rgb :
                    1.0 - 2.0 * (1.0 - base.rgb) * (1.0 - blend.rgb);
                result.a = base.a;
                return result;
            }

            float4 CalculateBlendAdd(
                float4 src,
                float4 dst,
                float4 useNormal,
                float4 useAdd,
                float4 useMultiply,
                float4 useOverlay,
                float4 useSubstruct)
            {
                float4 blend = float4(0, 0, 0, 0);
                #if EXTRA_BLEND
                blend += (dst - src) * useNormal;
                blend += (dst) * useAdd;
                blend += (src * dst - src) * useMultiply;
                blend += (BlendOverlay(src, dst) - src) * useOverlay;
                blend += (-dst) * useSubstruct;
                #else
                blend += (dst) * useAdd;
                #endif
                return blend * dst.a;
            }

            float4 CalculateBlendNormal(
                float4 src,
                float4 dst,
                float4 useNormal,
                float4 useAdd,
                float4 useMultiply,
                float4 useOverlay,
                float4 useSubstruct)
            {
                float4 blend = float4(0, 0, 0, 0);
                #if EXTRA_BLEND
                blend += (dst - src) * useNormal;
                blend += (dst) * useAdd;
                blend += (src * dst - src) * useMultiply;
                blend += (BlendOverlay(src, dst) - src) * useOverlay;
                blend += (-dst) * useSubstruct;
                #else
                blend += (dst - src) * useNormal;
                #endif
                return blend * dst.a;
            }

            float4 CalculateBlendDebug(
                float4 src,
                float4 dst,
                float4 useNormal,
                float4 useAdd,
                float4 useMultiply,
                float4 useOverlay,
                float4 useSubstruct)
            {
                float4 blend = dst;

                float useFactor = useNormal + useAdd + useMultiply + useOverlay + useSubstruct;

                blend.rgb *= blend.a * useFactor;
                return blend;
            }

            float SampleDepth(float2 uv)
            {
                return LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv));
            }

            #if RIMLIGHT

            float4 Rimlight_CalculateBlend(float4 src, RimlightBuffer data, float2 uv, float3 normal)
            {
                float rimFactor = 1.0 - dot(normal, data.direction);
                float basicRim = smoothstep(data.lightArea - data.fadeRange, data.lightArea + data.fadeRange, rimFactor);
                float rimIntensity = pow(basicRim, data.fadeExp);
                float4 rimColor = lerp(data.color2, data.color1, rimIntensity);

                #if DEBUG_VIEW
                return CalculateBlendDebug(src, rimColor, data.useNormal, data.useAdd, data.useMultiply, data.useOverlay, data.useSubstruct);
                #else
                return CalculateBlendAdd(src, rimColor, data.useNormal, data.useAdd, data.useMultiply, data.useOverlay, data.useSubstruct);
                #endif
            }

            float4 Rimlight_frag(v2f i, float4 src)
            {
                float4 dst = src;
                float3 normal = DecodeViewNormalStereo(tex2D(_CameraDepthNormalsTexture, i.uv));

                // 同一 CommandBuffer 内の DrawRenderer 由来なので Blit と同じ向きのはず (実機で要確認)
                float2 headMask = tex2D(_HeadMaskTex, i.uv).rg; // R=顔, G=髪

                float2 charMaskUV = i.uv;
                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0) { charMaskUV.y = 1 - charMaskUV.y; }
                #endif
                float charMask = tex2D(_CharMaskTex, charMaskUV).r;

                [loop]
                for(int idx = 0; idx < _RimlightCount; idx++)
                {
                    float4 blend = Rimlight_CalculateBlend(dst, _RimlightBuffer[idx], i.uv, normal);

                    // キャラマスク: 0=なし / 1=キャラ除外 / 2=キャラのみ
                    float mode = _RimlightBuffer[idx].maskMode;
                    blend *= mode == 1 ? 1 - charMask : (mode == 2 ? charMask : 1);

                    // 顔除外: 頭部マスク領域のリム寄与を消し込む (applyHair 時は顔のみ除外し髪は残す)
                    float excludeMask = lerp(max(headMask.r, headMask.g), headMask.r, _RimlightBuffer[idx].applyHair);
                    blend *= lerp(1, 1 - excludeMask, _RimlightBuffer[idx].excludeFace);

                    dst += blend;
                }

                dst.a = src.a;

                return dst;
            }
            #endif

            #if DISTANCE_FOG
            float4 DistanceFog_CalculateFogBlend(float4 src, DistanceFogBuffer data, float depth)
            {
                // fogEnd == fogStart のゼロ除算 (NaN) を防ぐ
                float range = max(data.fogEnd - data.fogStart, 1e-5);
                float fogFactor = pow(saturate((depth - data.fogStart) / range), data.fogExp);
                fogFactor = smoothstep(0, 1, fogFactor);

                float4 fogColor = lerp(data.color2, data.color1, fogFactor);

                #if DEBUG_VIEW
                return CalculateBlendDebug(src, fogColor, data.useNormal, data.useAdd, data.useMultiply, data.useOverlay, data.useSubstruct);
                #else
                return CalculateBlendNormal(src, fogColor, data.useNormal, data.useAdd, data.useMultiply, data.useOverlay, data.useSubstruct);
                #endif
            }

            float4 DistanceFog_frag(v2f i, float4 src, float depth)
            {
                float4 dst = src;

                [loop]
                for(int idx = 0; idx < _DistanceFogCount; idx++)
                {
                    dst += DistanceFog_CalculateFogBlend(dst, _DistanceFogBuffer[idx], depth);
                }

                dst.a = src.a;

                return dst;
            }
            #endif

            #if PARAFFIN
            float2 Paraffin_AdjustUV(float2 uv, float2 radiusScale)
            {
                float2 centeredUV = uv - 0.5;
                centeredUV.x *= radiusScale.x;
                centeredUV.y *= radiusScale.y;
                return centeredUV + 0.5;
            }

            float4 Paraffin_CalculateGradientColor(ParaffinBuffer data, float2 uv)
            {
                float2 adjustedUV = Paraffin_AdjustUV(uv, data.radiusScale);
                float dist = distance(adjustedUV, data.centerPosition);
                float t = smoothstep(data.radiusNear, data.radiusFar, dist);
                return lerp(data.color1, data.color2, t);
            }

            float4 Paraffin_CalculateBlend(float4 col, ParaffinBuffer data, float2 uv)
            {
                float4 gradientColor = Paraffin_CalculateGradientColor(data, uv);

                #if DEBUG_VIEW
                return CalculateBlendDebug(col, gradientColor, data.useNormal, data.useAdd, data.useMultiply, data.useOverlay, data.useSubstruct);
                #else
                return CalculateBlendAdd(col, gradientColor, data.useNormal, data.useAdd, data.useMultiply, data.useOverlay, data.useSubstruct);
                #endif
            }

            float4 Paraffin_frag(v2f i, float4 src, float depth)
            {
                float4 dst = src;

                // マスク UV は Camera.Render 由来のため、Blit 側と上下が逆の環境では反転する
                float2 maskUV = i.uv;
                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0) { maskUV.y = 1 - maskUV.y; }
                #endif
                float charMask = tex2D(_CharMaskTex, maskUV).r;

                [loop]
                for(int idx = 0; idx < _ParaffinCount; idx++)
                {
                    float4 blend = Paraffin_CalculateBlend(src, _ParaffinBuffer[idx], i.uv);

                    // 0=なし / 1=キャラ除外 / 2=キャラのみ
                    float mode = _ParaffinBuffer[idx].maskMode;
                    float maskFactor = mode == 1 ? 1 - charMask : (mode == 2 ? charMask : 1);
                    blend *= maskFactor;

                    dst += blend;
                }

                dst.a = src.a;
                return dst;
            }
            #endif

            fixed4 frag (v2f i) : SV_Target
            {
                float4 col = tex2D(_MainTex, i.uv);

                // 深度は全エフェクト共通なので一度だけサンプルする (未使用構成ではコンパイラが除去する)
                float depth = SampleDepth(i.uv);

                #if DEBUG_VIEW
                col = float4(0, 0, 0, 1);
                #endif

                #if RIMLIGHT
                col = Rimlight_frag(i, col);
                #endif

                #if DISTANCE_FOG
                col = DistanceFog_frag(i, col, depth);
                #endif

                #if PARAFFIN
                col = Paraffin_frag(i, col, depth);
                #endif

                return col;
            }
            ENDCG
        }
    }
}