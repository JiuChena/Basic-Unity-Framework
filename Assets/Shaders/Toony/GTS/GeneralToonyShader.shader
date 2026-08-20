Shader "GTS/General Toony Shader"
{
    Properties
    {
        _Albedo("主纹理图", 2D) = "white" {}
        _Color("主色", Color) = (1,1,1,1)
        _OcclusionMap("遮蔽贴图", 2D) = "black" {}
        _OcclusionMapScale("遮蔽强度", Range(0, 1)) = 1
        _OcclusionThreshold("遮蔽过滤阈值(0=关闭过滤)", Range(0, 1)) = 0

        _NormalMap("法线贴图", 2D) = "bump" {}
        _NormalMapScale("法线强度", Range(0, 1)) = 1

        _MainLightDiffuseScale("主光漫反射强度", Range(0, 5)) = 1
        _DiffuseWrap("漫反射包裹", Range(0, 1)) = 0
        _DiffuseSteps("漫反射色阶化处理", Range(2, 50)) = 3
        _DiffuseSmooth("漫反射柔化", Range(0, 1)) = 0.2
        _HColor("亮面色", Color) = (1,1,1,1)
        _ShadowColor("阴影色", Color) = (0,0,0,1)
        [Toggle(_USESHADOWBASEMIX_ON)] _UseShadowBaseMix("阴影色混合贴图颜色", Float) = 0
        _ShadowBaseMix("阴影色混合贴图颜色强度", Range(0, 1)) = 0.5
        _IndirectlightScale("间接光强度", Range(0, 1)) = 0.4
        _AmbientScale("Ambient全局光照强度", Range(0, 2)) = 1

        [Toggle(_USEADDITIONALLIGHTDIFFUSE_ON)] _UseAdditionalLightsDiffuse("附加光漫反射", Float) = 0
        _AdditionalLightsScale("附加光强度", Range(0, 1)) = 1

        _SpecularMap("高光贴图", 2D) = "white" {}
        [Toggle(_USEHAIRDIRECTIONHIGHLIGHT_ON)] _UseHairDirectionHighlight("头发各向异性高光(切线场试验)", Float) = 0
        _HairDirectionHighlightThreshold("头发方向匹配阈值", Range(-1, 1)) = 0.82
        _HairDirectionHighlightSoftness("头发方向高光柔化", Range(0.001, 0.25)) = 0.08
        _HairDirectionHighlightIntensity("头发方向高光强度", Range(0, 5)) = 1
        _HairDirectionHighlightAnisotropy("头发高光各向异性指数", Range(1, 64)) = 16
        _HairDirectionHighlightTangentBlend("贴图切线混合", Range(0, 1)) = 1
        _HairDirectionHighlightLobeOffset("高光带方向偏移", Range(-0.5, 0.5)) = 0
        _HairDirectionHighlightAlphaWeight("Alpha响应权重", Range(0, 1)) = 0.5
        _HairDirectionHighlightAlphaPower("Alpha响应曲线", Range(0.25, 4)) = 1
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
        [Toggle(_USEMETAL_ON)] _UseMetal("金属材质", Float) = 0
        [Toggle(_USEEMISSION_ON)] _UseEmission("自发光", Float) = 0
        [HDR] _EmissionColor("自发光颜色", Color) = (0,0,0,1)
        _EmissionMap("自发光贴图", 2D) = "white" {}
        _EmissionIntensity("自发光强度", Range(0, 5)) = 1

        _Contrast("对比度", Range(0, 2)) = 1

        [Enum(Off, Write, Read)] _StencilMode("模板测试模式", Float) = 0
        _StencilRef("模板值", Range(0, 255)) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilCompare("模板比较", Float) = 6
        [HideInInspector] _StencilForwardComp("Stencil Forward Comp", Float) = 8
        [HideInInspector] _StencilForwardOp("Stencil Forward Op", Float) = 2

        _RimColor("边缘光色", Color) = (1,1,1,1)
        _RimMin("边缘光起始", Range(0, 1)) = 0.8
        _RimMax("边缘光结束", Range(0, 1)) = 1
        _RimFresnelSoftness("边缘光菲涅尔软化", Range(0.1, 4)) = 1
        _RimTextureWeight("边缘光贴图色权重", Range(0, 1)) = 0
        [Toggle(_USERIMLIGHT_ON)] _UseRimLight("边缘光", Float) = 0

        [Toggle(_USEOUTLINE_ON)] _UseOutline("描边", Float) = 1
        _OutlineColor("描边颜色", Color) = (0,0,0,1)
        _OutlineWidth("描边宽度", Range(0, 1)) = 1
        [Space(8)]
        //世界空间描边参数（纯顶点外拓 + 距离自适应）
        _AdaptiveWidth("自适应描边宽度", Range(0, 1)) = 0.3
        _OutlineMaxScale("描边自适应最大宽度", Range(1, 100)) = 20

        [Space(8)]
        //半透明（头发等）：混合源/混合目标下拉框 + 最终前向Alpha
        [Enum(UnityEngine.Rendering.BlendMode)] _BlendSrc("混合源(BlendSrc)", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _BlendDst("混合目标(BlendDst)", Float) = 10
        _Transparency("透明度", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 0
        
        //模板测试（前向门控）：写入模式=标记缓冲区；读取模式=比较通过才渲染（不过不画）；关闭=中立
        Stencil { Ref [_StencilRef] Comp [_StencilForwardComp] Pass [_StencilForwardOp] }

        //描边通道
        Pass
        {
            Name "Outline"
            
            Cull Front
            Blend [_BlendSrc] [_BlendDst]

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
            float _Transparency;
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
                float3 normalWS = TransformObjectToWorldNormal(v.normal);
#ifdef _USEOUTLINE_ON
                // 世界空间模式：纯顶点外拓 + 距离等比自适应（上下限钳制）
                // 顶点与法线统一在世界空间外扩，避免模型旋转/缩放时方向错乱
                float3 worldPos = TransformObjectToWorld(v.vertex.xyz);
                float lerpResult = clamp(lerp(1.0, distance(_WorldSpaceCameraPos, worldPos), _AdaptiveWidth), 1.0, _OutlineMaxScale);
                // 尖端收边：color.a 由工具烘焙（1=平滑区→不变，尖端趋近0→收窄）
                worldPos += normalWS * (0.01 * _OutlineWidth * lerpResult);
                o.pos = TransformWorldToHClip(worldPos);
#else
                // 描边关闭：所有顶点塌缩到剪裁空间原点，零面积三角形不产生任何片元
                o.pos = float4(0, 0, 0, 1);
#endif

                return o;
            }
            
            half4 frag(VertexOutput o) : SV_Target
            {
                // 头发半透明：描边随 _Transparency 一起淡出
                return _OutlineColor;
            }
            
            ENDHLSL
        }

        //前向渲染通道
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend [_BlendSrc] [_BlendDst]

            HLSLPROGRAM
            
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma shader_feature_local _USEADDITIONALLIGHTDIFFUSE_ON
            #pragma shader_feature_local _USESPECULAR_ON
            #pragma shader_feature_local _USEHAIRDIRECTIONHIGHLIGHT_ON
            #pragma shader_feature_local _USEADDITIONALLIGHTSPECULAR_ON
            #pragma shader_feature_local _USEENVIRONMENTREFLETION_ON
            #pragma shader_feature_local _USERIMLIGHT_ON
            #pragma shader_feature_local _USEMETAL_ON
            #pragma shader_feature_local _USEEMISSION_ON
            #pragma shader_feature_local _USESHADOWBASEMIX_ON
            
            CBUFFER_START(UnityPerMaterial)
            //主纹理
            half4 _Albedo_ST;
            half4 _OcclusionMap_ST;
            float4 _Color;
            float _OcclusionMapScale;
            float _OcclusionThreshold;
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
            float _ShadowBaseMix;
            float _IndirectlightScale;
            float _AmbientScale;
            //附加光源
            float _AdditionalLightsScale;
            //高光
            half4 _SpecularMap_ST;
            half4 _SpecularColor;
            float _HairDirectionHighlightThreshold;
            float _HairDirectionHighlightSoftness;
            float _HairDirectionHighlightIntensity;
            float _HairDirectionHighlightAnisotropy;
            float _HairDirectionHighlightTangentBlend;
            float _HairDirectionHighlightLobeOffset;
            float _HairDirectionHighlightAlphaWeight;
            float _HairDirectionHighlightAlphaPower;
            float _SpecularScale;
            float _SpecularSize;
            float _SpecularPosterizeSteps;
            float _SpecularFaloff;
            float _AdditionalSpecularFaloff;
            float _EnvReflectionStrength;
            //自发光
            half4 _EmissionColor;
            float _EmissionIntensity;
            half4 _EmissionMap_ST;
            //对比度
            float _Contrast;
            //边缘光
            half4 _RimColor;
            float _RimMin;
            float _RimMax;
            float _RimFresnelSoftness;
            float _RimTextureWeight;
            float _Transparency;
            CBUFFER_END
            
            sampler2D _Albedo;
            sampler2D _OcclusionMap;
            sampler2D _NormalMap;
            sampler2D _SpecularMap;
            sampler2D _EmissionMap;

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
                else return floor(faloff * Steps) / Steps;
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
                half3x3 TBN = half3x3(o.worldTangent, o.worldBitangent, o.worldNormal);
                half3 worldNormal = SafeNormalize(TransformTangentToWorld(tangentNormal, TBN));
                
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
                    TransformWorldToShadowCoord(o.worldPosition);
                    #endif
                }
                
                //Lambert -> HalfLambert漫反射插值
                half wrapNL = lerp(max(0, NL), (NL + 1) * 0.5, _DiffuseWrap);
                
                //计算阴影部分大段、小段的对应值离散化出来
                half steps = max(round(_DiffuseSteps), 2);
                half bandPos = wrapNL * (steps - 1);
                half bandIdx = floor(bandPos);
                half bandFrac = frac(bandPos);
                half bandBlend = smoothstep(max(1.0 - _DiffuseSmooth, 0.0001), 1.0, bandFrac);
                half rampStep = saturate((bandIdx + bandBlend) / (steps - 1));
                rampStep *= lightShadowAttenuation;
                
                //主纹理与遮蔽提前采样（阴影色混合需要两者）
                half4 mainTextureSample = tex2D(_Albedo, uv);
                half occValue = tex2D(_OcclusionMap, uv).g;
                // 采样值减去过滤阈值得到遮蔽阴影强度（sat 截断）；阈值=0 时退化为原连续渐变
                half occShadow = saturate(occValue - _OcclusionThreshold);

                //计算暗部阴影色、根据当前亮度得出该像素应该是算出的暗部阴影色还是亮部色进行插值
                half shadowIntensity = _ShadowColor.a;
                half3 shadowColorMixed = lerp(_HColor.rgb, _ShadowColor.rgb, shadowIntensity);
                // 阴影色混合贴图颜色：阴影着色tint与贴图颜色插值（阴影色权重 (1-w)，贴图色权重 w）
                #ifdef _USESHADOWBASEMIX_ON
                shadowColorMixed = lerp(shadowColorMixed, mainTextureSample.rgb, _ShadowBaseMix);
                #endif
                half3 mainDiffuse = lerp(shadowColorMixed, _HColor.rgb, rampStep) * _MainLightColor.rgb * _MainLightDiffuseScale;
                //金属漫反射减弱，能量转移到高光/环境反射
                #ifdef _USEMETAL_ON
                mainDiffuse *= 0.6;
                #endif
                
                //遮蔽遮罩是标量阴影遮罩：按遮蔽强度把贴图色从亮色插向阴影着色版（阴影色已含贴图颜色混合）
                half occFactor = saturate(occShadow * _OcclusionMapScale);
                half4 mainTexture = _Color * half4(lerp(mainTextureSample.rgb, shadowColorMixed, occFactor), 1);
                
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
                
                //视角向量计算，高光贴图采样，贴图采样过滤、缩放
                float3 worldViewDir = SafeNormalize(_WorldSpaceCameraPos.xyz - o.worldPosition);
                half4 specularMapSample = tex2D(_SpecularMap, uv);
                half smoothness = (specularMapSample.a - 0.2) * _SpecularScale;
                
                //高光主光计算、高光主光色阶化处理
                #ifdef _USESPECULAR_ON
                half3 mainLightDir = SafeNormalize(GetMainLight().direction);
                half3 halfDir = SafeNormalize(mainLightDir + worldViewDir);
                half NH0 = saturate(dot(worldNormal, halfDir));

                half specularSize = clamp(1 - _SpecularSize * smoothness, 0.001, 0.999);

                NH0 = saturate((NH0 - specularSize) / (1 - specularSize));

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
                    half3 lightDir = SafeNormalize(light.direction);
                    half3 halfDir = SafeNormalize(lightDir + worldViewDir);
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
                
                // 头发各向异性高光：把贴图方向投影到表面切平面，得到每个发簇的高光切线。
                #ifdef _USEHAIRDIRECTIONHIGHLIGHT_ON
                half3 hairDirectionOS = SafeNormalize(specularMapSample.rgb * 2.0 - 1.0);
                half3 hairDirectionWS = SafeNormalize(TransformObjectToWorldDir(hairDirectionOS));
                half3 projectedTangent = hairDirectionWS - worldNormal * dot(hairDirectionWS, worldNormal);
                half projectedLength = length(projectedTangent);
                half projectionValid = step(0.0001, projectedLength);
                projectedTangent = SafeNormalize(projectedTangent);
                half3 fallbackTangent = SafeNormalize(o.worldTangent);
                half tangentBlend = saturate(_HairDirectionHighlightTangentBlend) * projectionValid;
                half3 hairTangentWS = SafeNormalize(lerp(fallbackTangent, projectedTangent, tangentBlend));
                half3 mainHalfLV = SafeNormalize(mainLight.direction + worldViewDir);
                half tangentDistance = abs(dot(hairTangentWS, mainHalfLV) - _HairDirectionHighlightLobeOffset);
                half tangentMatch = saturate(1.0 - tangentDistance);
                half anisotropicLobe = pow(tangentMatch, _HairDirectionHighlightAnisotropy);
                half directionHighlight = smoothstep(_HairDirectionHighlightThreshold, _HairDirectionHighlightThreshold + _HairDirectionHighlightSoftness, anisotropicLobe);
                half alphaMask = pow(saturate(specularMapSample.a * 2.0), _HairDirectionHighlightAlphaPower);
                half alphaResponse = lerp(1.0, alphaMask, _HairDirectionHighlightAlphaWeight);
                half3 hairDirectionHighlight = directionHighlight * alphaResponse * _HairDirectionHighlightIntensity * mainLight.color * lightShadowAttenuation; // 高光颜色在组装末尾统一乘一次
                #else
                half3 hairDirectionHighlight = 0;
                #endif
                
                //高光组装：直接相加，没开的开关贡献为0（组装不需要开关），组装完成后统一乘一次高光颜色
                half3 specTint = _SpecularColor.rgb;
                #ifdef _USEMETAL_ON
                specTint = mainTexture.rgb; // 金属高光着色=物体色
                #endif
                half3 specularColor = (specularPosterized * _MainLightColor.rgb + additionalSpecular + hairDirectionHighlight) * specTint + envReflection;
                //金属：额外环境反射加成
                #ifdef _USEMETAL_ON
                specularColor += envReflection * 0.5;
                #endif

                //边缘光
                #ifdef _USERIMLIGHT_ON
                half ndv = 1 - max(0, dot( SafeNormalize( worldNormal ), worldViewDir ));
                ndv = pow(ndv, _RimFresnelSoftness); // 菲涅尔值幂次软化（Schlick式）：<1铺开更柔 >1贴轮廓 1=不变
                half rimLight = smoothstep(_RimMin, _RimMax, ndv);
                // 贴图色权重：0=纯边缘光色，1=纯贴图采样颜色
                half3 rimFinal = rimLight * lerp(_RimColor.rgb, mainTextureSample.rgb, _RimTextureWeight);
                #else
                half3 rimFinal = 0;
                #endif

                //最终输出
                half4 litColorFinal = half4(finalDiffuse + specularColor + rimFinal, _Transparency);
                //自发光：贴图 × 颜色(HDR) × 强度
                #ifdef _USEEMISSION_ON
                litColorFinal.rgb += tex2D(_EmissionMap, uv).rgb * _EmissionColor.rgb * _EmissionIntensity;
                #endif

                //总对比度：作用于最终片元结果，以0.5灰为轴拉伸（1=不变，>1增强，<1减弱）
                litColorFinal.rgb = lerp(half3(0.5, 0.5, 0.5), litColorFinal.rgb, _Contrast);

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
