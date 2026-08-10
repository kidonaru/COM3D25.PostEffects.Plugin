// 遮蔽 (AO) テクスチャの消し込み用: マスクが立っている画素の遮蔽量を 0 にする
// _MainTex = kino/obscurance のぼかし後遮蔽 RT (R8, 値が大きいほど遮蔽が濃い)
// _MaskTex = キャラマスク RT (白 = AO を受けない)
Shader "Hidden/PostEffects/ObscuranceMask"
{
    Properties
    {
        _MainTex ("Occlusion", 2D) = "" {}
        _MaskTex ("Mask", 2D) = "" {}
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
            sampler2D _MaskTex;

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

            fixed4 frag(v2f i) : SV_Target
            {
                fixed occ = tex2D(_MainTex, i.uv).r;
                fixed mask = tex2D(_MaskTex, i.uvMask).r;
                return occ * (1 - mask);
            }
            ENDCG
        }
    }
}
