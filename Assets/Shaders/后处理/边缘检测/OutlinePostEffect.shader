Shader "Unlit/URP_SobelOutline"
{
    Properties
    {
        // _MainTex 由 SobelEdgeFeature 运行时注入，不在此声明默认值
        // (若在 Properties 中声明 "white"{} 默认值，Material 缓存会覆盖 cmd.SetGlobalTexture 导致白屏)
        _EdgeOnly("仅显示边缘", Range(0,1)) = 0
        _EdgeColor("边缘颜色", Color) = (0,0,0,1)
        _BackgroundColor("背景颜色", Color) = (1,1,1,1)
        _EdgeWidth("描边粗细", Range(0.5, 5.0)) = 1.0
        _ColorSensitivity("颜色灵敏度", Range(0, 5)) = 1.0
        _DepthSensitivity("深度灵敏度", Range(0, 20)) = 3.0
        _EdgeThreshold("边缘阈值（越小越多边缘）", Range(0, 0.5)) = 0.05
        _EdgeSharpness("边缘清晰度", Range(1, 30)) = 8.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        // 跳过 Player/Enemy 写入了 Ref=1 的区域
        Stencil
        {
            Ref 1
            Comp NotEqual
        }

        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // 提供 SampleSceneDepth(uv) 与 _CameraDepthTexture
            // 需要在 URP 管线资产中勾选 "Depth Texture"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            sampler2D _MainTex;
            float4    _MainTex_TexelSize;
            float     _EdgeOnly;
            float4    _EdgeColor;
            float4    _BackgroundColor;
            float     _EdgeWidth;
            float     _ColorSensitivity;
            float     _DepthSensitivity;
            float     _EdgeThreshold;
            float     _EdgeSharpness;

            struct Attributes
            {
                float4 positionCS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 vertex    : SV_POSITION;
                float2 uv[9]     : TEXCOORD0;   // 3x3 邻域 UV（已含 EdgeWidth 缩放）
                float2 centerUV  : TEXCOORD9;   // 未缩放的中心 UV，供深度 1px 采样用
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.vertex = v.positionCS;

                float2 uv = v.uv;
                // D3D/Metal 渲染到 RT 时 Y 翻转修正
                #if UNITY_UV_STARTS_AT_TOP
                uv.y = 1.0 - uv.y;
                #endif

                // 颜色 Sobel 采样偏移（受 EdgeWidth 影响）
                float2 ts = _MainTex_TexelSize.xy * _EdgeWidth;
                o.uv[0] = uv + ts * float2(-1, -1);
                o.uv[1] = uv + ts * float2( 0, -1);
                o.uv[2] = uv + ts * float2( 1, -1);
                o.uv[3] = uv + ts * float2(-1,  0);
                o.uv[4] = uv;
                o.uv[5] = uv + ts * float2( 1,  0);
                o.uv[6] = uv + ts * float2(-1,  1);
                o.uv[7] = uv + ts * float2( 0,  1);
                o.uv[8] = uv + ts * float2( 1,  1);

                // 深度 Sobel 使用固定 1px 偏移（不随 EdgeWidth 变化，保证深度边缘锐利）
                o.centerUV = uv;

                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                const float Gx[9] = {-1, 0, 1, -2, 0, 2, -1, 0, 1};
                const float Gy[9] = {-1, -2, -1,  0, 0, 0,  1, 2, 1};

                // ── 颜色通道 Sobel ──────────────────────────────────────────────
                // 对 R/G/B 三通道分别计算梯度，取最大值
                // sqrt() 做类 gamma 变换：把暗部差值放大（如 0.01→0.02 变为 0.10→0.14），
                // 让画面较暗时的色差同样可以被检测到
                float3 cgx = 0, cgy = 0;
                [unroll] for (int ci = 0; ci < 9; ci++)
                {
                    float3 c = sqrt(saturate(tex2D(_MainTex, i.uv[ci]).rgb));
                    cgx += c * Gx[ci];
                    cgy += c * Gy[ci];
                }
                float3 cGrad    = sqrt(cgx * cgx + cgy * cgy); // 真梯度幅值
                float colorEdge = max(cGrad.r, max(cGrad.g, cGrad.b)) * _ColorSensitivity;

                // ── 深度通道 Sobel ──────────────────────────────────────────────
                // 基于 _CameraDepthTexture（需开启 URP Depth Texture）
                // 深度不受光照影响，在暗部/纯色区域也能可靠检测物体轮廓
                float2 dts = _MainTex_TexelSize.xy; // 固定 1 像素间距
                float dgx = 0, dgy = 0;
                [unroll] for (int di = 0; di < 9; di++)
                {
                    float2 off = float2(float(di % 3) - 1.0, float(di / 3) - 1.0);
                    float rawD = SampleSceneDepth(i.centerUV + dts * off);
                    // 线性化到 [0,1]，消除透视压缩，让远近物体的边缘强度一致
                    float linD = Linear01Depth(rawD, _ZBufferParams);
                    dgx += linD * Gx[di];
                    dgy += linD * Gy[di];
                }
                float depthEdge = sqrt(dgx * dgx + dgy * dgy) * _DepthSensitivity;

                // ── 合并两个通道，映射到 [0,1] ─────────────────────────────────
                // 取颜色和深度边缘中的较大值
                float totalEdge = max(colorEdge, depthEdge);

                // smoothstep 边缘映射：
                //   totalEdge < threshold        → edge = 1（无边缘，显示原色）
                //   totalEdge > threshold+margin → edge = 0（完全边缘，显示描边色）
                float margin = 1.0 / max(_EdgeSharpness, 0.01);
                float edge = 1.0 - smoothstep(_EdgeThreshold, _EdgeThreshold + margin, totalEdge);

                float4 sceneColor = tex2D(_MainTex, i.uv[4]);
                float4 withEdge   = lerp(_EdgeColor, sceneColor, edge);
                float4 onlyEdge   = lerp(_EdgeColor, _BackgroundColor, edge);
                return lerp(withEdge, onlyEdge, _EdgeOnly);
            }
            ENDHLSL
        }
    }
}
