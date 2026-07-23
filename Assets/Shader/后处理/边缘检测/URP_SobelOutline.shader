Shader "Unlit/URP_SobelOutline_Legacy"
{
    Properties
    {
        _MainTex ("Base", 2D) = "white" {}
        _EdgeOnly("边缘检测强度", Float) = 0
        _EdgeColor("边缘检测颜色", Color) = (0,0,0,1)
        _BackgroundColor("背景颜色", Color) = (1,1,1,1)
    }
    SubShader
    {
        // 添加 URP 渲染管线标签
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            ZWrite Off
            Cull Off
            
            // 使用 HLSLPROGRAM 替代 CGPROGRAM
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // 引入 URP 核心库（提供 TransformObjectToHClip 等基础函数）
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // 使用传统的 sampler2D 声明，保证完美兼容各种 URP 版本
            sampler2D _MainTex;
            float4 _MainTex_TexelSize; // 这个值由 C# 脚本在 Blit 时自动传入
            float _EdgeOnly;
            float4 _EdgeColor;
            float4 _BackgroundColor;

            // 自定义顶点输入结构体（替代老旧的 appdata_img）
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv[9] : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };
            
            v2f vert (appdata v)
            {
                v2f o;
                // 使用 URP 的顶点变换函数
                o.vertex = TransformObjectToHClip(v.vertex.xyz);

                // 【关键修改】处理 URP 中后处理可能出现的 UV 翻转问题
                float2 uv = v.uv;
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0 - uv.y;
                #endif
                
                o.uv[0] = uv + _MainTex_TexelSize.xy * float2(-1,-1); 
                o.uv[1] = uv + _MainTex_TexelSize.xy * float2(0, -1); 
                o.uv[2] = uv + _MainTex_TexelSize.xy * float2(1, -1); 
                o.uv[3] = uv + _MainTex_TexelSize.xy * float2(-1, 0); 
                o.uv[4] = uv + _MainTex_TexelSize.xy * float2(0, 0); 
                o.uv[5] = uv + _MainTex_TexelSize.xy * float2(1, 0); 
                o.uv[6] = uv + _MainTex_TexelSize.xy * float2(-1, 1); 
                o.uv[7] = uv + _MainTex_TexelSize.xy * float2(0, 1); 
                o.uv[8] = uv + _MainTex_TexelSize.xy * float2(1, 1); 
                
                return o;
            }

            // 计算亮度
            float Luminance(float4 color)
            {
                return 0.2125 * color.r + 0.7154 * color.g + 0.0721 * color.b;
            }

            // Sobel 算子计算边缘
            float Sobel(v2f i)
            {
                // 把 half 改成 float，避免某些 HLSL 编译器的精度警告
                const float Gx[9] = {-1, 0, 1, -2, 0, 2, -1, 0, 1};
                const float Gy[9] = {-1, -2, -1, 0, 0, 0, 1, 2, 1};	
                
                float texColor;
                float edgeX = 0;
                float edgeY = 0;
                
                for (int it = 0; it < 9; it++)
                {
                    texColor = Luminance(tex2D(_MainTex, i.uv[it]));
                    edgeX += texColor * Gx[it];
                    edgeY += texColor * Gy[it];
                }
                
                float edge = 1.0 - abs(edgeX) - abs(edgeY);
                return edge;
            }

            float4 frag (v2f i) : SV_Target
            {
                half edge = Sobel(i);
				
				// 混合边缘颜色和背景颜色
				float4 withEdgeColor = lerp(_EdgeColor, tex2D(_MainTex, i.uv[4]), edge);
				float4 onlyEdgeColor = lerp(_EdgeColor, _BackgroundColor, edge);
				
				return lerp(withEdgeColor, onlyEdgeColor, _EdgeOnly);
            }

            ENDHLSL
        }
    }
}
