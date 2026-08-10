// キャラ除外の汎用合成: エフェクト適用結果と元画像をキャラマスクで補間する
// _MainTex = エフェクト適用結果, _SrcTex = 元画像, _MaskTex = キャラマスク (白 = 除外)
// Pass 0 = 単純合成, Pass 1 = マスク境界を _Spread px 膨張して合成 (ブラー系の回り込み対策)
Shader "Hidden/PostEffects/CharMaskComposite"
{
    Properties
    {
        _MainTex ("Effected", 2D) = "" {}
        _SrcTex ("Source", 2D) = "" {}
        _MaskTex ("Mask", 2D) = "" {}
        _Spread ("Spread", Float) = 0
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _SrcTex;
    sampler2D _MaskTex;
    float _Spread;

    struct v2f
    {
        float4 pos : SV_Position;
        float2 uv : TEXCOORD0;
        float2 uvMask : TEXCOORD1;
    };

    v2f vert(appdata_img v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.texcoord;
        o.uvMask = v.texcoord;
        // Blit 側の RT が上下反転しているとき、カメラ描画のマスク RT と向きを合わせる
        #if UNITY_UV_STARTS_AT_TOP
        if (_MainTex_TexelSize.y < 0)
        {
            o.uvMask.y = 1 - o.uvMask.y;
        }
        #endif
        return o;
    }

    fixed4 blend(v2f i, fixed mask)
    {
        fixed4 effected = tex2D(_MainTex, i.uv);
        fixed4 src = tex2D(_SrcTex, i.uv);
        return lerp(effected, src, mask);
    }

    fixed4 fragSimple(v2f i) : SV_Target
    {
        return blend(i, tex2D(_MaskTex, i.uvMask).r);
    }

    fixed4 fragSpread(v2f i) : SV_Target
    {
        // 周囲 8 近傍の最大値でマスクを膨張し、キャラ輪郭のすぐ外側もキャラ側として扱う
        float2 d = abs(_MainTex_TexelSize.xy) * _Spread;
        fixed mask = tex2D(_MaskTex, i.uvMask).r;
        mask = max(mask, tex2D(_MaskTex, i.uvMask + float2( d.x, 0)).r);
        mask = max(mask, tex2D(_MaskTex, i.uvMask + float2(-d.x, 0)).r);
        mask = max(mask, tex2D(_MaskTex, i.uvMask + float2(0,  d.y)).r);
        mask = max(mask, tex2D(_MaskTex, i.uvMask + float2(0, -d.y)).r);
        mask = max(mask, tex2D(_MaskTex, i.uvMask + float2( d.x,  d.y)).r);
        mask = max(mask, tex2D(_MaskTex, i.uvMask + float2( d.x, -d.y)).r);
        mask = max(mask, tex2D(_MaskTex, i.uvMask + float2(-d.x,  d.y)).r);
        mask = max(mask, tex2D(_MaskTex, i.uvMask + float2(-d.x, -d.y)).r);
        return blend(i, mask);
    }
    ENDCG

    SubShader
    {
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragSimple
            ENDCG
        }
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragSpread
            ENDCG
        }
    }
}
