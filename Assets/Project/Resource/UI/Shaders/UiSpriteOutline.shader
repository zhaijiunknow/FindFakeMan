Shader "Project/UI Sprite Outline"
{
    // 用途：给 UI 图做**贴着不透明轮廓**的描边，而且**一个像素都不碰原图** ✓。
    //
    // 挂在哪：垫在原图**下面**的那一份副本上（同一个 sprite、同一个 rect ✓）。
    // 算法：轮廓 = 周围一圈采样里最大的 alpha（= 膨胀 ✓）；然后**减去自己** ✓ ——
    //       于是只剩"原图外面临界的那一圈" ✓，原图实心处、原图半透明的软边处都不叠色 ✓。
    //
    // 为什么不用"同一张图偏移叠 8 份"的老做法 ✗（SpriteOutline 以前就是这么干的）：
    // 副本垫在原图下面 ✓，原图实心部分盖得住它们 ✓，但原图**软边**（alpha 0.5~1）盖不住 ✗
    // → 描边色从软边透上来 ✓ = "挂上描边之后原图轻微变色" ✗。这里"减去自己"从根上消掉它 ✓。
    //
    // 还能顺手做的效果 ✓（都在这个 pass 里，不用多画层 ✓）：
    //   _OutlineWidth 调粗细 · _Softness 调边缘软硬 · _Alpha 淡入淡出（悬停渐显 ✓）
    //   _Color 由 Image.color 驱动 ✓ 所以换色不用动材质 ✓。
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Outline Color", Color) = (0.42, 0.66, 1, 0.85)
        _OutlineWidth ("Outline Width (texel)", Range(0, 48)) = 6
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _Softness ("Edge Softness", Range(0.001, 0.5)) = 0.25
        _Alpha ("Outline Alpha", Range(0, 1)) = 1
        _Glow ("Glow (把轮廓也当柔光叠一圈)", Range(0, 1)) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "SpriteOutline"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            float _OutlineWidth;
            float _Cutoff;
            float _Softness;
            float _Alpha;
            float _Glow;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            // 8 个方向（上/下/左/右 + 四个斜角 ✓）；每方向采两环 → 一共 16 次采样 ✓。
            static const float2 kDirs[8] =
            {
                float2(0, 1), float2(0, -1), float2(-1, 0), float2(1, 0),
                float2(0.7071068, 0.7071068), float2(-0.7071068, 0.7071068),
                float2(0.7071068, -0.7071068), float2(-0.7071068, -0.7071068)
            };

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 step = _MainTex_TexelSize.xy * max(_OutlineWidth, 0.0001);

                // 自己：判断"这个像素是不是原图的实心部分" ✓（用**原图** alpha ✓，不乘 tint ✗）。
                float self = tex2D(_MainTex, IN.texcoord).a + _TextureSampleAdd.a;

                // 周围一圈的最大 alpha = 膨胀后的轮廓 ✓。
                float ring = 0;
                [unroll]
                for (int i = 0; i < 8; i++)
                {
                    ring = max(ring, tex2D(_MainTex, IN.texcoord + kDirs[i] * step).a + _TextureSampleAdd.a);
                    ring = max(ring, tex2D(_MainTex, IN.texcoord + kDirs[i] * step * 0.5).a + _TextureSampleAdd.a);
                }

                // **减去自己** ✓：只留"外面那一圈"，原图身上一个像素都不叠 ✓。
                float outer = saturate((ring - _Cutoff) / _Softness);
                float inner = saturate((self - _Cutoff) / _Softness);
                float outline = saturate(outer - inner);

                // 柔光模式：把自己身上也算进去一点，得到"轮廓 + 外发光"的效果 ✓（默认关 ✓）。
                outline = saturate(outline + _Glow * outer * 0.5);

                clip(outline - 0.001);

                fixed4 color = IN.color;
                color.a *= outline * _Alpha;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }

    Fallback "UI/Default"
}
