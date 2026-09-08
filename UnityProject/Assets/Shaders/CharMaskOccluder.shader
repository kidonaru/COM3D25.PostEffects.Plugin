// 背景の不透明面を深度に書き、壁の裏のキャラをマスクに出さない。
Shader "Hidden/PostEffects/CharMaskOccluder"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Cutoff ("Cutoff", Float) = 0.5
    }
    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex;
    float4 _MainTex_ST;
    float _Cutoff;
    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
    };
    v2f vert(appdata_base v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
        return o;
    }
    fixed4 opaque(v2f i) : SV_Target { return 0; }
    fixed4 cutout(v2f i) : SV_Target
    {
        clip(tex2D(_MainTex, i.uv).a - _Cutoff);
        return 0;
    }
    ENDCG
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Pass
        {
            ZWrite On ZTest LEqual Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment opaque
            ENDCG
        }
    }
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" }
        Pass
        {
            ZWrite On ZTest LEqual Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment cutout
            ENDCG
        }
    }
}
