// ハーフトーン (網点): モノクロ網点 / CMYK カラー網点で印刷物風にする
// パス 0 = モノクロ, パス 1 = CMYK カラー
Shader "Hidden/PostEffects/Halftone"
{
    Properties
    {
        _MainTex ("Base", 2D) = "" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    // 網点セルの 1 辺 (ピクセル)
    float _DotSize;
    // 網角度 (ラジアン)
    float _Angle;
    // ドット輪郭のなめらかさ (0 = くっきり)
    float _Smoothness;

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

    // 指定角度で回転した網点グリッドの被覆率 (0 = ドット外, 1 = ドット内) を返す。
    // amount はインク量 (0〜1)。ドット半径をインク量に応じて変える
    float dotCoverage(float2 screenPx, float angle, float amount)
    {
        float s = sin(angle);
        float c = cos(angle);
        float2 p = float2(screenPx.x * c - screenPx.y * s, screenPx.x * s + screenPx.y * c) / _DotSize;
        float2 grid = frac(p) - 0.5;
        float dist = length(grid);
        // 被覆率 = インク量になるよう半径を決める (πr^2 = amount)。
        // 円がセル境界に達する amount = π/4 以降は面積式が成り立たないため、対角 (全埋まり) へ線形に伸ばす
        float radius = amount <= 0.7854
            ? sqrt(amount / 3.14159)
            : lerp(0.5, 0.7071, (amount - 0.7854) / 0.2146);
        float aa = max(_Smoothness * 0.25, fwidth(dist) * 0.5);
        return smoothstep(radius + aa, radius - aa, dist);
    }

    fixed4 frag_mono(v2f i) : SV_Target
    {
        float2 screenPx = i.uv * _MainTex_TexelSize.zw;
        // UnityCG の Luminance() は unity_ColorSpaceLuminance 依存で、実機では白が 0.5 になるため使わない
        float lum = dot(tex2D(_MainTex, i.uv).rgb, float3(0.299, 0.587, 0.114));
        // 暗いほどインクが乗る
        float ink = dotCoverage(screenPx, _Angle, 1 - lum);
        return fixed4((1 - ink).xxx, 1);
    }

    fixed4 frag_cmyk(v2f i) : SV_Target
    {
        float2 screenPx = i.uv * _MainTex_TexelSize.zw;
        fixed3 col = tex2D(_MainTex, i.uv).rgb;

        // RGB → CMYK (印刷の慣例に合わせ K で下色除去)
        float k = 1 - max(col.r, max(col.g, col.b));
        float invK = max(1e-4, 1 - k);
        float3 cmy = (1 - col - k) / invK;

        // 版ごとに実際の印刷で使う網角度をずらしてモアレを散らす (C=15° M=75° Y=0° K=45°)
        float covC = dotCoverage(screenPx, _Angle + radians(15.0), cmy.x);
        float covM = dotCoverage(screenPx, _Angle + radians(75.0), cmy.y);
        float covY = dotCoverage(screenPx, _Angle, cmy.z);
        float covK = dotCoverage(screenPx, _Angle + radians(45.0), k);

        // 白地に各インクを乗算合成する
        fixed3 outCol = 1;
        outCol *= 1 - covC * fixed3(1, 0, 0);
        outCol *= 1 - covM * fixed3(0, 1, 0);
        outCol *= 1 - covY * fixed3(0, 0, 1);
        outCol *= 1 - covK;
        return fixed4(outCol, 1);
    }
    ENDCG

    SubShader
    {
        ZTest Always Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag_mono
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag_cmyk
            ENDCG
        }
    }
}
