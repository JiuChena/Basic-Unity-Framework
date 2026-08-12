Shader "Toony/General Toony Shader"
{
    Properties
    {
        _Albedo("主纹理图", 2D) = "white" {}
        _Color("主色", Color) = (1,1,1,1)
        _OcclusionMap("遮蔽贴图", 2D) = "black" {}
        _OcclusionMapScale("遮蔽强度", Range(0, 1)) = 1

        _NormalMap("法线贴图", 2D) = "bump" {}
        _NormalMapScale("法线强度", Range(0, 1)) = 1

        _MainLightDiffuseScale("主光漫反射强度", Range(0, 5)) = 1
        _DiffuseWrap("漫反射包裹", Range(0, 1)) = 0
        _DiffuseSteps("漫反射色阶化处理", Range(2, 50)) = 3
        _DiffuseSmooth("漫反射柔化", Range(0, 1)) = 0.2
        _HColor("亮面色", Color) = (1,1,1,1)
        _ShadowColor("阴影色", Color) = (0,0,0,1)
        _IndirectlightScale("间接光强度", Range(0, 1)) = 0.4
        _AmbientScale("Ambient全局光照强度", Range(0, 2)) = 1

        [Toggle(_USEADDITIONALLIGHTDIFFUSE_ON)] _UseAdditionalLightsDiffuse("附加光漫反射", Float) = 0
        _AdditionalLightsScale("附加光强度", Range(0, 1)) = 1

        _SpecularMap("高光贴图", 2D) = "white" {}
        _SpecularColor("高光颜色", Color) = (1,1,1,1)
        _SpecularScale("高光强度", Range(0, 1)) = 0.5
        _SpecularSize("高光大小", Range(0, 1)) = 0.5
        _SpecularPosterizeSteps("高光色阶数", Range(1, 15)) = 5
        _SpecularFaloff("高光衰减", Range(0, 1)) = 0
        _AdditionalSpecularFaloff("附加光高光过渡", Range(0, 1)) = 1
        [Toggle(_USESPECULAR_ON)] _UseSpecular("高光", Float) = 1
        [Toggle(_USEADDITIONALLIGHTSPECULAR_ON)] _UseAdditionalLightsSpecular("附加光高光", Float) = 1
        [Toggle(_USEENVIRONMENTREFLETION_ON)] _UseEnvironmentReflection("环境反射", Float) = 0
        _EnvReflectionStrength("环境反射强度", Range(0, 1)) = 0.5

        _RimColor("边缘光色", Color) = (1,1,1,1)
        _RimColorMask("边缘光遮罩", Color) = (1,1,1,1)
        _RimMin("边缘光起始", Range(0, 1)) = 0.8
        _RimMax("边缘光结束", Range(0, 1)) = 1
        [Toggle(_USERIMLIGHT_ON)] _UseRimLight("边缘光", Float) = 0

        [Toggle(_USEOUTLINE_ON)] _UseOutline("描边", Float) = 1
        _OutlineColor("描边颜色", Color) = (0,0,0,1)
        _OutlineWidth("描边宽度", Range(0, 0.01)) = 0.005
        _AdaptiveWidth("自适应描边宽度", Range(0, 1)) = 0.3
        _OutlineMaxScale("描边自适应最大宽度", Range(1, 100)) = 20
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 0
        
        //XRay透视使用
        Stencil { Ref 1 Comp Always Pass Replace }

        //描边通道
        Pass
        {
            Name "Outline"
            
            Cull Front
            
            HLSLPROGRAM
            
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _USEOUTLINE_ON
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            CBUFFER_START(UnityPerMaterial)
            half4 _OutlineColor;
            float _OutlineWidth;
            float _AdaptiveWidth;
            float _OutlineMaxScale;
            CBUFFER_END
            
            struct VertexInput
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct VertexOutput
            {
                float4 pos : SV_POSITION;
            };
            
            VertexOutput vert(VertexInput v)
            {
                VertexOutput o;
#ifdef _USEOUTLINE_ON
                float3 worldPos = TransformObjectToWorld(v.vertex).xyz;
                //距离等比自适应 + 上下限钳制：防止描边随距离无限变大（下限 1.0 = 基础宽度，上限 _OutlineMaxScale）
                float lerpResult = clamp(lerp(1.0, distance(_WorldSpaceCameraPos, worldPos), _AdaptiveWidth), 1.0, _OutlineMaxScale);
                half3 finalOffset = lerpResult * (v.normal * _OutlineWidth);
                v.vertex.xyz += finalOffset;
#endif
                o.pos = TransformObjectToHClip(v.vertex.xyz);
                
                return o;
            }
            
            half4 frag(VertexOutput o) : SV_Target
            {
                return _OutlineColor;
            }
            
            ENDHLSL
        }

        //前向渲染通道
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            
            HLSLPROGRAM
            
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma shader_feature_local _USEADDITIONALLIGHTDIFFUSE_ON
            #pragma shader_feature_local _USESPECULAR_ON
            #pragma shader_feature_local _USEADDITIONALLIGHTSPECULAR_ON
            #pragma shader_feature_local _USEENVIRONMENTREFLETION_ON
            #pragma shader_feature_local _USERIMLIGHT_ON
            
            CBUFFER_START(UnityPerMaterial)
            //主纹理
            half4 _Albedo_ST;
            half4 _OcclusionMap_ST;
            float4 _Color;
            float _OcclusionMapScale;
            //法线
            half4 _NormalMap_ST;
            float _NormalMapScale;
            //卡通漫反射
            half _DiffuseSteps;
            half _DiffuseSmooth;
            float _MainLightDiffuseScale;
            half _DiffuseWrap;
            float4 _HColor;
            float4 _ShadowColor;
            float _IndirectlightScale;
            float _AmbientScale;
            //附加光源
            float _AdditionalLightsScale;
            //高光
            half4 _SpecularMap_ST;
            half4 _SpecularColor;
            float _SpecularScale;
            float _SpecularSize;
            float _SpecularPosterizeSteps;
            float _SpecularFaloff;
            float _AdditionalSpecularFaloff;
            float _EnvReflectionStrength;
            //边缘光
            half4 _RimColor;
            half4 _RimColorMask;
            float _RimMin;
            float _RimMax;
            CBUFFER_END
            
            sampler2D _Albedo;
            sampler2D _OcclusionMap;
            sampler2D _NormalMap;
            sampler2D _SpecularMap;

            struct VertexInput
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float4 uv : TEXCOORD0;
            };

            struct VertexOutput
            {
                float4 clipPosition : SV_POSITION;
                float3 worldPosition : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldTangent : TEXCOORD2;
                float3 worldBitangent : TEXCOORD3;
                float4 lightmapUVOrSH : TEXCOORD4;
                float4 uv : TEXCOORD5;
            };
            
            half PosterizeFaloff( half IN, half Steps, half Faloff )
            {
                float minOut = 0.5 * Faloff - 0.005;
                float faloff = lerp(IN, smoothstep(minOut, 0.5, IN), Faloff);
                if(Steps < 1) return faloff;
                else return floor(faloff / (1 / Steps)) * (1 / Steps);
            }
            
            VertexOutput vert(VertexInput v)
            {
                //output初始化
                VertexOutput o = (VertexOutput)0;
                //世界空间TBN转换、赋值
                float3 worldTangent = TransformObjectToWorldDir(v.tangent.xyz);
                float3 worldNormal = TransformObjectToWorldNormal(v.normal);
                float tangentSign = v.tangent.w * unity_WorldTransformParams.w;
                float3 worldBitangent = cross(worldNormal, worldTangent) * tangentSign;
                
                o.worldNormal = worldNormal;
                o.worldTangent = worldTangent;
                o.worldBitangent = worldBitangent;
                
                //烘焙光采样、球谐光照附加光计算
                OUTPUT_LIGHTMAP_UV(v.uv, unity_LightmapST, o.lightmapUVOrSH.xy);
                OUTPUT_SH(worldNormal, o.lightmapUVOrSH.xyz);
                
                o.uv.xy = v.uv.xy;
                o.uv.zw = 0;
                
                o.worldPosition = TransformObjectToWorld(v.vertex.xyz);
                o.clipPosition = TransformWorldToHClip(o.worldPosition);
                
                return o;
            }
            
            half4 frag(VertexOutput o) : SV_Target
            {
                //uv处理、切线空间TBN矩阵计算、世界法线转换
                half2 uv = o.uv.xy * _OcclusionMap_ST.xy + _OcclusionMap_ST.zw;
                
                half3 tangentNormal = lerp(half3(0,0,1), UnpackNormalScale(tex2D(_NormalMap, uv), 1.0), _NormalMapScale);
                half3 tangentWorld0 = float3(o.worldTangent.x, o.worldBitangent.x, o.worldNormal.x);
                half3 tangentWorld1 = float3(o.worldTangent.y, o.worldBitangent.y, o.worldNormal.y);
                half3 tangentWorld2 = float3(o.worldTangent.z, o.worldBitangent.z, o.worldNormal.z);
                float3 worldNormal = normalize(float3(dot(tangentWorld0, tangentNormal), dot(tangentWorld1, tangentNormal), dot(tangentWorld2, tangentNormal)));
                
                //漫反射主光、色阶化处理
                half NL = dot(worldNormal, _MainLightPosition.xyz);
                
                //计算阴影像素所在位置、计算该像素所受光照（Light）、计算该像素的光照衰减
                half4 shadowCoords = 0;
                Light mainLight = GetMainLight(shadowCoords);
                half lightShadowAttenuation = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                {
                    #if SHADOWS_SCREEN
                    half4 clipPosition = TransformWorldToHClip(o.worldPosition);
                    shadowCoords = ComputeScreenPos(clipPosition);
                    #else
                    shadowCoords = TransformWorldToShadowCoord(o.worldPosition);
                    #endif
                }
                
                //Lambert漫反射 -> 半Lambert漫反射 插值
                half wrapNL = lerp(max(0, NL), (NL + 1) * 0.5, _DiffuseWrap);
                
                //计算阴影部分亮度（色阶量化 + 边缘柔滑：N=2 即 Step 硬分界，N=50 逼近连续），计算出该像素色阶阶段（整数部分）+ 像素的该色阶阶段的渐变化程度（柔化小数部分），最后除以steps - 1归一化处理得到应有的亮度
                half steps = max(round(_DiffuseSteps), 2);
                half bandPos = wrapNL * (steps - 1);
                half bandIdx = floor(bandPos);
                half bandFrac = frac(bandPos);
                half bandBlend = smoothstep(max(1.0 - _DiffuseSmooth, 0.0001), 1.0, bandFrac);
                half rampStep = saturate((bandIdx + bandBlend) / (steps - 1));
                rampStep *= lightShadowAttenuation;
                
                //计算暗部阴影色、根据当前亮度得出该像素应该是算出的暗部阴影色还是亮部色进行插值
                half shadowIntensity = _ShadowColor.a;
                half3 shadowColorMixed = lerp(_HColor.rgb, _ShadowColor.rgb, shadowIntensity);
                half3 mainDiffuse = lerp(shadowColorMixed, _HColor.rgb, rampStep) * _MainLightColor.rgb * _MainLightDiffuseScale;
                
                //主纹理取色、主纹理Mask遮罩取遮蔽（lerp(1, 1 - mask.g, scale)）、混合主纹理色
                half4 mainTextureSample = tex2D(_Albedo, uv);
                half occlusion = lerp(1, 1 - tex2D(_OcclusionMap, uv).g, _OcclusionMapScale);
                half4 mainTexture = (_Color * mainTextureSample * half4(occlusion, occlusion, occlusion, 1));
                
                //AO全局光照（环境光、光照探针等）
                half3 bakedGI = SampleSH(worldNormal);
                MixRealtimeAndBakedGI(mainLight, worldNormal, bakedGI);
                half3 ambientColorFactor = lerp(float3(0,0,0), bakedGI, _IndirectlightScale);
                half4 finalAmbientColor = mainTexture * half4(ambientColorFactor * _AmbientScale, 0);
                
                //漫反射附加光光照计算
                #ifdef _USEADDITIONALLIGHTDIFFUSE_ON
                half3 lightWrapVector = _DiffuseWrap.xxx;
                //附加光过渡复用主光柔化 _DiffuseSmooth
                half smoothMax = 0.5 + 0.5 * _DiffuseSmooth;
                half smoothMin = 0.5 - 0.5 * _DiffuseSmooth;
                smoothMax = max(smoothMin + 0.0001, smoothMax);
                
                half3 additionalDiffuse = 0;
                for (int i = 0; i < GetAdditionalLightsCount(); i++)
                {
                    Light light = GetAdditionalLight(i, o.worldPosition);
                    
                    float3 dotVector = dot(light.direction, worldNormal);
                    float3 lambert = max(float3(0,0,0), dotVector);
                    float3 halfLambert = saturate((dotVector + 1) * 0.5);
                    
                    half3 additionalLightColor = light.shadowAttenuation * light.distanceAttenuation;
                    float3 colorOut = lerp(lambert, halfLambert, saturate(lightWrapVector)) * additionalLightColor * light.color;
                    float maxColor = max(colorOut.r, max(colorOut.g, colorOut.b));
                    float3 outColor = smoothstep(smoothMin, smoothMax, maxColor) * light.color;
                    
                    additionalDiffuse += outColor;
                }
                additionalDiffuse *= _AdditionalLightsScale;
                #else
                half3 additionalDiffuse = 0;
                #endif
                
                //漫反射最终组装（Step / Floor 双模式统一）
                half3 finalDiffuse = (mainDiffuse + additionalDiffuse) * mainTexture.rgb + finalAmbientColor.rgb;
                
                //高光主光计算、高光主光色阶化处理
                float3 worldViewDir = normalize(_WorldSpaceCameraPos.xyz - o.worldPosition);
                half smoothness = tex2D(_SpecularMap, uv).a * _SpecularScale;
                
                #ifdef _USESPECULAR_ON
                half3 mainLightDir = normalize(GetMainLight().direction);
                half3 halfDir = normalize(mainLightDir + worldViewDir);
                half NH0 = saturate(dot(worldNormal, halfDir));
                
                half specularSize = clamp(1 - _SpecularSize * smoothness, 0.001, 0.999);
                
                NH0 = saturate(NH0 * (1.0 / (1 - specularSize)) - (specularSize / (1 - specularSize)));
                
                half specularPosterized = PosterizeFaloff(NH0, _SpecularPosterizeSteps, _SpecularFaloff);
                #else
                half specularPosterized = 0;
                #endif
                
                //高光附加光
                #ifdef _USEADDITIONALLIGHTSPECULAR_ON
                half3 additionalSpecular = 0;
                for (int j = 0; j < GetAdditionalLightsCount(); j++)
                {
                    Light light = GetAdditionalLight(j, o.worldPosition);
                    half3 lightDir = normalize(light.direction);
                    half3 halfDir = normalize(lightDir + worldViewDir);
                    half NH1 = saturate(dot(worldNormal, halfDir));
                    
                    half specularSize1 = clamp(1 - _SpecularSize * smoothness, 0.001, 0.999);
                    NH1 = saturate(NH1 * (1 / (1 - specularSize1)) - (specularSize1 / (1 - specularSize1)));
                    half specularPosterized1 = PosterizeFaloff(NH1, _SpecularPosterizeSteps, _AdditionalSpecularFaloff);
                    
                    additionalSpecular += specularPosterized1 * light.color * (light.shadowAttenuation * light.distanceAttenuation);
                }
                #else
                half3 additionalSpecular = 0;
                #endif
                
                //环境反射
                #ifdef _USEENVIRONMENTREFLETION_ON
                float3 reflectVector = reflect(-worldViewDir, worldNormal);
                float3 indirectSpecular = GlossyEnvironmentReflection(reflectVector, 1.0 - smoothness, 0.75);
                half3 envReflection = indirectSpecular * _EnvReflectionStrength * smoothness;
                #else
                half3 envReflection = 0;
                #endif
                
                //高光组装
                #ifdef _USESPECULAR_ON
                half3 specularColor = (specularPosterized * _MainLightColor.rgb + additionalSpecular) * _SpecularColor.rgb + envReflection;
                #else
                half3 specularColor = envReflection;
                #endif
                
                //边缘光
                #ifdef _USERIMLIGHT_ON
                half ndv = 1 - max(0, dot( normalize( worldNormal ), worldViewDir ));
                half rimLight = smoothstep(_RimMin, _RimMax, ndv);
                half3 rimFinal = rimLight * _RimColor.rgb * _RimColorMask.rgb;
                #else
                half3 rimFinal = 0;
                #endif
                
                //最终输出
                half4 litColorFinal = half4(finalDiffuse + specularColor + rimFinal, 1);
                
                return litColorFinal;
            }
            
            ENDHLSL
        }

        //阴影投射与深度写入
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
    CustomEditor "GeneralToonyShadeEditor"
    Fallback "Hidden/InternalErrorShader"
}
