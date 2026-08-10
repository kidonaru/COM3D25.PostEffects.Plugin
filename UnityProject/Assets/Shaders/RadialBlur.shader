// ラジアルブラー (ズームブラー): 中心から放射状にサンプルをずらして合成する。
// 集中線的な演出用。中心付近はぼけず、外周ほど強く流れる
// Pass 0 = 通常, Pass 1 = キャラマスク画素をサンプルから除外 (キャラの残像が背景へ流れるのを防ぐ)
Shader "Hidden/PostEffects/RadialBlur"
{
    Properties
    {
        _MainTex ("Base", 2D) = "" {}
        _MaskTex ("Mask", 2D) = "" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _MaskTex;
    float2 _Center;
    float _Strength;
    int _SampleCount;

    struct v2f
    {
        float4 pos : SV_Position;
        float2 uv : TEXCOORD0;
        // マスク RT が上下反転しているとき 1。サンプル位置ごとに UV を変換するためフラグで持つ
        float maskFlip : TEXCOORD1;
    };

    v2f vert(appdata_img v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.texcoord;
        o.maskFlip = 0;
        // Blit 側の RT が上下反転しているとき、カメラ描画のマスク RT と向きを合わせる
        #if UNITY_UV_STARTS_AT_TOP
        if (_MainTex_TexelSize.y < 0)
        {
            o.maskFlip = 1;
        }
        #endif
        return o;
    }

    float2 maskUV(float2 uv, float flip)
    {
        return float2(uv.x, lerp(uv.y, 1 - uv.y, flip));
    }

    // 1 サンプルあたりの後退量。強度 1 で中心方向へ最大 10% 縮む
    float2 sampleStep(float2 uv)
    {
        return (uv - _Center) * (_Strength * 0.1 / _SampleCount);
    }

    fixed4 frag(v2f i) : SV_Target
    {
        float2 step = sampleStep(i.uv);
        float4 acc = 0;
        float2 uv = i.uv;
        for (int s = 0; s < _SampleCount; s++)
        {
            acc += tex2D(_MainTex, uv);
            uv -= step;
        }
        return acc / _SampleCount;
    }

    fixed4 fragMasked(v2f i) : SV_Target
    {
        float2 step = sampleStep(i.uv);
        float4 acc = 0;
        float weight = 0;
        float2 uv = i.uv;
        for (int s = 0; s < _SampleCount; s++)
        {
            // キャラ画素は重み 0 で捨てて正規化し、キャラの色が背景へ流れないようにする
            float w = 1 - tex2D(_MaskTex, maskUV(uv, i.maskFlip)).r;
            acc += tex2D(_MainTex, uv) * w;
            weight += w;
            uv -= step;
        }
        // 全サンプルがキャラ画素だった場合は元画素をそのまま返す (キャラ内部は合成で元絵に戻る)
        if (weight <= 0)
        {
            return tex2D(_MainTex, i.uv);
        }
        return acc / weight;
    }
    ENDCG

    SubShader
    {
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDCG
        }
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragMasked
            ENDCG
        }
    }
}
