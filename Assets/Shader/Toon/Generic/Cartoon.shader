// URP Cartoon Toon Shader（手写维护，不依赖任何 Shader 生成器）
Shader "Cartoon"
{
	// 暴露给材质面板的参数。部分参数会转换为 Shader 关键字，
	// 让当前材质不需要的光照分支可以在编译时被裁剪掉。
	Properties
	{
		[Toggle(_USERIMLIGHT_ON)] _UseRimLight("UseRim Light", Float) = 0
		[Toggle(_USEOUTLINE_ON)] _UseOutline("UseOutline", Float) = 0
		[Toggle(_USESPECULAR_ON)] _UseSpecular("UseSpecular Highlights", Float) = 1
		_SpecColor("Specular Value", Color) = (1,1,1,0)
		_Thicnkess("Thicnkess", Range( 0 , 0.1)) = 0
		_AdaptiveThicnkess("Adaptive Thicnkess", Range( 0 , 1)) = 0
		[KeywordEnum(Normal,Position,UVBaked)] _OutlineType("Outline Type", Float) = 0
		[HDR]_OutlineColor("Outline Color", Color) = (0,0,0,0)
		_Color("Color", Color) = (0.6792453,0.6792453,0.6792453,1)
		_SpecularFaloff("Specular Faloff", Range( 0 , 1)) = 0
		_LightRampOffset("Light Ramp Offset", Range( -1 , 1)) = 0
		_MainTex("Albedo Texture", 2D) = "white" {}
		[NoScaleOffset]_LightRampTexture("Light Ramp Texture", 2D) = "white" {}
		_StepOffset("Step Offset", Range( -0.5 , 0.5)) = 0
		_RampSmooth("Ramp Smoothness", Range(0, 0.5)) = 0.01
		[KeywordEnum(Step,DiffuseRamp,Posterize)] _UseLightRamp("Shading Mode", Float) = 0
		[HideInInspector]_RampDiffuseTextureLoaded("RampDiffuseTextureLoaded", Float) = 1
		[HDR]_RimColor("Rim Color", Color) = (1,1,1,0)
		_RimThickness("Rim Thickness", Range( 0 , 3)) = 1
		_RimPower("Rim Power", Range( 1 , 12)) = 12
		_RimSmoothness("Rim Smoothness", Range( 0 , 0.5)) = 0
		[Normal]_BumpMap("Normal Map", 2D) = "bump" {}
		_NormalMapStrength("Normal Map Strength", Float) = 1
		_SpecularPosterizeSteps("Specular Posterize Steps", Range( 0 , 15)) = 15
		[Toggle(_USEENVIRONMENTREFLETION_ON)] _UseEnvironmentRefletion("UseEnvironment Reflections", Float) = 0
		_Strength("Strength", Range( 0 , 1)) = 0
		[NoScaleOffset]_SpecGlossMap("Specular Map", 2D) = "white" {}
		_Glossiness("Smoothness", Range( 0 , 1)) = 0.5
		_Cutoff("Alpha Clip Threshold", Range( 0 , 1)) = 0
		[HDR]_EmissionColor("Emission Color", Color) = (0,0,0,0)
		[HDR][NoScaleOffset]_EmissionMap("Emission Map", 2D) = "white" {}
		_UseEmission("UseEmission", Float) = 0
		_IndirectLightStrength("Indirect Light Strength", Range( 0 , 1)) = 1
		_SpecularShadowMask("Specular Shadow Mask", Range( 0 , 1)) = 0
		_AdditionalLightsSmoothnessMultiplier("Additional Lights Specular Size", Range( 0 , 2)) = 1
		_SmoothnessMultiplier("Main Specular Size", Range( 0 , 2)) = 1
		_AdditionalLightsIntesity("Additional Lights Intesity", Range( 0 , 6)) = 1
		[Toggle(_USEADDITIONALLIGHTSDIFFUSE_ON)] _UseAdditionalLightsDiffuse("UseAdditional Lights", Float) = 1
		_AdditionalLightsAmount("Additional Lights Size", Range( 0 , 1)) = 1
		_AdditionalLightsFaloff("Additional Lights Faloff", Range( 0 , 1)) = 1
		_DiffusePosterizeSteps("Posterize Steps", Range( 1 , 10)) = 3
		_DiffusePosterizePower("Posterize Power", Range( 0.5 , 3)) = 1
		_DiffusePosterizeOffset("Posterize Offset", Range( -0.5 , 0.5)) = 0
		_DiffuseWrap("Diffuse Wrap (Lambert-Half)", Range( 0 , 1)) = 0
		_MainLightIntesity("Main Light Intesity", Range( 0 , 6)) = 1
		_OutlineTextureStrength("Texture Strength ", Range( 0 , 1)) = 0
		_ShadowColor("Shadow Color", Color) = (0,0,0,0)
		[HDR]_HColor("Highlight Color", Color) = (1,1,1,1)
		_RimShadowColor("Rim Shadow Color", Color) = (0,0.05551431,0.9622642,0)
		[KeywordEnum(NoSplit,MultiplyWithDiffuse,UseSecondColor)] _RimSplitColor("Rim Split Color", Float) = 0
		_OcclusionMap("Occlusion Map", 2D) = "white" {}
		_OcclusionStrength("Occlusion Strength ", Range( 0 , 1)) = 1
	}

	SubShader
	{
		// Shader 面向 URP 不透明几何体，并为 URP 可能请求的阶段分别提供 Pass：
		// 描边、前向光照、投射阴影和深度写入。
		LOD 0

		Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+200" }

		// 将已渲染像素标记到模板缓冲区，供后续 Pass 识别使用此材质的对象。
		Stencil { Ref 1 Comp Always Pass Replace }

		Cull Back
		//ASMM抗锯齿的一个子技术，默认关闭，旨在解决植物等shader即使经过ASMM抗锯齿处理后依然会出现锯齿的效果
		AlphaToMask Off

		// 这里放置所有 Pass 共用的编译设置和可选曲面细分辅助函数。
		// 这些函数只声明一次，由启用曲面细分的 Pass 复用。
		HLSLINCLUDE
		#pragma target 3.0

		#pragma prefer_hlslcc gles
		#pragma exclude_renderers d3d11_9x

		ENDHLSL

		Pass
		{
			// 描边 Pass：在主体网格之前渲染一个外扩的背面壳体。
			// 剔除正面后，壳体露出的部分就形成主体边缘的描边。
			Name "Outline"

			Tags { "RenderType"="Opaque" "Queue"="Geometry" }

			// 描边写入不透明颜色并参与深度测试，避免穿透其他无关几何体显示。
			Blend One Zero
			Cull Front
			ZWrite On
			ZTest LEqual
			Offset 0 , 0
			ColorMask RGBA

			HLSLPROGRAM

			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#pragma multi_compile_instancing
			#pragma multi_compile _ DOTS_INSTANCING_ON
			#pragma multi_compile_fog
			#define _ALPHATEST_ON 1


			// 描边使用轻量的顶点和片元阶段；由于描边使用独立颜色，
			// 这里不参与主体光照计算。
			#pragma vertex vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"

			#pragma shader_feature_local _USEOUTLINE_ON
			#pragma shader_feature_local _OUTLINETYPE_NORMAL _OUTLINETYPE_POSITION _OUTLINETYPE_UVBAKED


			// VertexInput 接收网格属性；VertexOutput 只传递描边片元阶段需要的值，
			// 以及实例化和雾效数据。
			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 normalOS : NORMAL;
				float4 uvBakedOutline : TEXCOORD3;
				float4 uv0 : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				float fogFactor : TEXCOORD0;
				float4 uv : TEXCOORD1;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			// UnityPerMaterial 必须与 Unity 期望的材质常量缓冲区布局一致。
			// 每个读取材质参数的 Pass 都会重复这一布局。
			CBUFFER_START(UnityPerMaterial)
			half4 _EmissionColor;
			half4 _SpecColor;
			half4 _RimColor;
			half4 _RimShadowColor;
			half4 _Color;
			half4 _ShadowColor;
			half4 _HColor;
			half4 _OcclusionMap_ST;
			half4 _OutlineColor;
			half _RimThickness;
			half _SpecularPosterizeSteps;
			half _Strength;
			half _SpecularShadowMask;
			half _RimPower;
			half _SmoothnessMultiplier;
			half _MainLightIntesity;
			half _AdditionalLightsIntesity;
			half _SpecularFaloff;
			half _RimSmoothness;
			half _AdditionalLightsSmoothnessMultiplier;
			half _AdditionalLightsFaloff;
			half _IndirectLightStrength;
			half _UseEmission;
			half _AdditionalLightsAmount;
			half _DiffusePosterizeSteps;
			half _DiffusePosterizePower;
			half _DiffusePosterizeOffset;
			half _DiffuseWrap;
			half _LightRampOffset;
			half _NormalMapStrength;
			half _StepOffset;
			half _RampSmooth;
			half _Cutoff;
			half _OcclusionStrength;
			half _OutlineTextureStrength;
			half _Thicnkess;
			half _AdaptiveThicnkess;
			half _Glossiness;
			half _RampDiffuseTextureLoaded;
			CBUFFER_END
			sampler2D _MainTex;
			sampler2D _OcclusionMap;

			// 沿选定的描边方向移动顶点。自适应厚度会根据摄像机距离缩放外扩量，
			// 使远近物体的描边宽度更稳定、更容易辨认。
			VertexOutput VertexFunction( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				float3 worldPos = mul(GetObjectToWorldMatrix(), v.vertex).xyz;
				half adaptiveScale = lerp( 1.0 , distance( _WorldSpaceCameraPos , worldPos ) , _AdaptiveThicnkess);

				// 根据描边类型选择外扩方向：法线、对象空间位置或 UV 烘焙方向。
				#if defined(_OUTLINETYPE_NORMAL)
				half3 outlineDir = v.normalOS;
				#elif defined(_OUTLINETYPE_POSITION)
				half3 outlineDir = v.vertex.xyz;
				#elif defined(_OUTLINETYPE_UVBAKED)
				half3 outlineDir = half3( v.uvBakedOutline.xy ,  0.0 );
				#else
				half3 outlineDir = v.normalOS;
				#endif

				#ifdef _USEOUTLINE_ON
				v.vertex.xyz += ( adaptiveScale * ( outlineDir * _Thicnkess ) );
				#endif

				o.uv.xy = v.uv0.xy;
				o.uv.zw = 0;

				float3 positionWS = TransformObjectToWorld( v.vertex.xyz );
				float4 positionCS = TransformWorldToHClip( positionWS );

				o.fogFactor = ComputeFogFactor( positionCS.z );
				o.clipPos = positionCS;
				return o;
			}

			VertexOutput vert ( VertexInput v )
			{
				return VertexFunction( v );
			}

			// 采样主纹理和遮蔽纹理来塑造描边，再依次应用 Alpha 裁剪、
			// LOD 抖动、雾效和最终描边颜色。
			half4 frag ( VertexOutput IN  ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID( IN );
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );

				// 描边纹理贡献会在白色与主纹理之间插值；遮蔽纹理控制描边的遮挡强度。
				// 主纹理与遮蔽共用同一套 UV（统一应用 _OcclusionMap 的 Tiling/Offset）。
				half2 uv = IN.uv.xy * _OcclusionMap_ST.xy + _OcclusionMap_ST.zw;
				half4 mainTexSample = tex2D( _MainTex, uv );
				half4 outlineTexBlend = lerp( float4( 1,1,1,0 ) , mainTexSample , _OutlineTextureStrength);

				half occlusion = lerp( 1.0 , tex2D( _OcclusionMap, uv ).r , _OcclusionStrength);
				half4 mainTexture = ( _Color * mainTexSample * half4( occlusion, occlusion, occlusion, 1.0 ) );
				half mainTexAlpha = ( mainTexture.a * 1.0 );

				// 关闭描边时把 Alpha 压到 -2.0，使 Alpha Test 必然裁剪掉所有描边像素。
				#ifdef _USEOUTLINE_ON
				half outlineAlpha = mainTexAlpha;
				#else
				half outlineAlpha = -2.0;
				#endif

				float3 Color = ( _OutlineColor * outlineTexBlend ).rgb;
				float Alpha = outlineAlpha;
				float AlphaClipThreshold = _Cutoff;

				#ifdef _ALPHATEST_ON
					clip( Alpha - AlphaClipThreshold );
				#endif

				#ifdef LOD_FADE_CROSSFADE
					LODDitheringTransition( IN.clipPos.xyz, unity_LODFade.x );
				#endif

				Color = MixFog( Color, IN.fogFactor );

				return half4( Color, Alpha );
			}

			ENDHLSL
		}

		Pass
		{
			// Forward Pass：根据主光源、附加光源、烘焙/环境光、镜面光、边缘光和自发光，
			// 计算最终可见的卡通表面颜色。

			Name "Forward"
			Tags { "LightMode"="UniversalForward" "Queue"="Geometry" }

			Blend One Zero, One Zero
			ZWrite On
			ZTest LEqual
			Offset 0 , 0
			ColorMask RGBA

			HLSLPROGRAM

			// 这些变体向材质暴露可选光照功能，同时保留 URP 实例化、雾效、
			// 阴影、光照贴图和 Decal 兼容性。
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#pragma multi_compile_instancing
			#pragma multi_compile _ DOTS_INSTANCING_ON
			#pragma multi_compile _ _DECALS_OFF _DECALS_3RT _DECALS_4RT
			#pragma multi_compile_fog
			#define _ALPHATEST_ON 1

			#pragma vertex vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"

			#pragma shader_feature_local _USELIGHTRAMP_STEP _USELIGHTRAMP_DIFFUSERAMP _USELIGHTRAMP_POSTERIZE
			#pragma shader_feature_local _USEADDITIONALLIGHTSDIFFUSE_ON
			#pragma multi_compile _ DIRLIGHTMAP_COMBINED
			#pragma multi_compile _ LIGHTMAP_ON
			#pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
			#pragma multi_compile _ _SHADOWS_SOFT
			#pragma shader_feature_local _USESPECULAR_ON
			#pragma shader_feature_local _USEENVIRONMENTREFLETION_ON
			#pragma shader_feature_local _RIMSPLITCOLOR_NOSPLIT _RIMSPLITCOLOR_MULTIPLYWITHDIFFUSE _RIMSPLITCOLOR_USESECONDCOLOR
			#pragma shader_feature_local _USERIMLIGHT_ON
			#define SHADOWS_SHADOWMASK
			#define LIGHTMAP_SHADOW_MIXING
			#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
			#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS


			// Forward 顶点阶段向片元阶段传递世界坐标、切线空间基、阴影坐标、
			// 雾效数据以及光照贴图/球谐光照数据。
			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 normalOS : NORMAL;
				float4 uv0 : TEXCOORD0;
				half4 tangentOS : TANGENT;
				float4 uv1 : TEXCOORD1;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				float3 worldPos : TEXCOORD0;
				float fogFactor : TEXCOORD1;
				float4 uv : TEXCOORD2;
				float3 worldTangent : TEXCOORD3;
				float3 worldNormal : TEXCOORD4;
				float3 worldBitangent : TEXCOORD5;
				float4 lightmapUVOrSH : TEXCOORD6;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			half4 _EmissionColor;
			half4 _SpecColor;
			half4 _RimColor;
			half4 _RimShadowColor;
			half4 _Color;
			half4 _ShadowColor;
			half4 _HColor;
			half4 _OcclusionMap_ST;
			half4 _OutlineColor;
			half _RimThickness;
			half _SpecularPosterizeSteps;
			half _Strength;
			half _SpecularShadowMask;
			half _RimPower;
			half _SmoothnessMultiplier;
			half _MainLightIntesity;
			half _AdditionalLightsIntesity;
			half _SpecularFaloff;
			half _RimSmoothness;
			half _AdditionalLightsSmoothnessMultiplier;
			half _AdditionalLightsFaloff;
			half _IndirectLightStrength;
			half _UseEmission;
			half _AdditionalLightsAmount;
			half _DiffusePosterizeSteps;
			half _DiffusePosterizePower;
			half _DiffusePosterizeOffset;
			half _DiffuseWrap;
			half _LightRampOffset;
			half _NormalMapStrength;
			half _StepOffset;
			half _RampSmooth;
			half _Cutoff;
			half _OcclusionStrength;
			half _OutlineTextureStrength;
			half _Thicnkess;
			half _AdaptiveThicnkess;
			half _Glossiness;
			half _RampDiffuseTextureLoaded;
			CBUFFER_END
			sampler2D _BumpMap;
			sampler2D _OcclusionMap;
			sampler2D _LightRampTexture;
			sampler2D _MainTex;
			sampler2D _SpecGlossMap;
			sampler2D _EmissionMap;


			// 将连续的光照值量化为固定数量的卡通明暗色阶。
			half Posterize( half In, half Steps )
			{
				return  floor(In / (1 / Steps)) * (1 / Steps);
			}

			// 累加 URP 附加光源的漫反射贡献。包裹 Lambert 项可以柔化
			// 卡通受光色阶与阴影色阶之间的过渡。
			half3 AdditionalLight( float3 WorldPosition, float3 WorldNormal, half3 LightWrapVector, half SMin, half SMax, half Faloff, half4 shadowmask )
			{
				float3 Color = 0;
				int numLights = GetAdditionalLightsCount();
				for(int i = 0; i<numLights;i++)
				{
					Light light = GetAdditionalLight(i, WorldPosition, shadowmask);

					float3 DotVector = dot(light.direction,WorldNormal);
					float3 lambert = max(float3(0.f,0.f,0.f), DotVector);
					float3 halfLambert = saturate(DotVector * 0.5 + 0.5);

					half3 AttLightColor = (light.shadowAttenuation * light.distanceAttenuation);
					float3 colout = lerp(lambert, halfLambert, saturate(LightWrapVector))*AttLightColor*light.color;
					float maxColor = max(colout.r,max(colout.g,colout.b));
					float3 outColor = smoothstep(SMin,SMax,maxColor)*light.color;
					Color += outColor;
				}
				return Color;
			}

			// 有烘焙光照贴图时使用光照贴图数据，否则采样 URP 提供的环境球谐光照。
			float3 SampleIndirectDiffuse( float2 uvStaticLightmap, float3 normalWS )
			{
			#ifdef LIGHTMAP_ON
				return SampleLightmap( uvStaticLightmap, normalWS );
			#else
				return SampleSH(normalWS);
			#endif
			}

			// 计算附加光源的镜面高光，并根据设置选择是否进行卡通色阶化。
			half3 AdditionalLightsSpecular( float3 WorldPosition, float3 WorldNormal, float3 WorldView, float3 SpecColor, float Smoothness, half Steps, half SpecFaloff )
			{
				float3 Color = 0;
				Smoothness = exp2(10 * Smoothness + 1);
				int numLights = GetAdditionalLightsCount();
				for(int i = 0; i<numLights;i++)
				{
					Light light = GetAdditionalLight(i, WorldPosition, half4(1,1,1,1));

					half3 AttLightColor = light.color *(light.distanceAttenuation * light.shadowAttenuation);
					Color += LightingSpecular(AttLightColor, light.direction, WorldNormal, WorldView, half4(SpecColor, 0), Smoothness);
				}
				float IN = max(Color.b,max(Color.r,Color.g));
				float minOut = 0.5 * SpecFaloff - 0.005;
				float faloff = lerp(IN, smoothstep(minOut, 0.5, IN), SpecFaloff);
				if(Steps < 1)
				{
				    return Color * faloff;
				}
				else
				{
				    return  Color * floor(faloff / (1 / Steps)) * (1 / Steps);
				}
			}

			// 在主光源高光色阶化前，先应用可调节的平滑衰减。
			half PosterizeFaloff( half IN, half SpecFaloff, half Steps )
			{
				float minOut = 0.5 * SpecFaloff - 0.005;
				float faloff = lerp(IN, smoothstep(minOut, 0.5, IN), SpecFaloff);
				if(Steps < 1)
				{
				    return faloff;
				}
				else
				{
				    return  floor(faloff / (1 / Steps)) * (1 / Steps);
				}
			}


			// 构建切线空间到世界空间的转换基、光照贴图/球谐坐标，
			// 以及 Forward 片元阶段需要的插值数据。
			VertexOutput VertexFunction ( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				float3 worldTangent = TransformObjectToWorldDir(v.tangentOS.xyz);
				float3 worldNormal = TransformObjectToWorldNormal(v.normalOS);
				float tangentSign = v.tangentOS.w * unity_WorldTransformParams.w;
				float3 worldBitangent = cross( worldNormal, worldTangent ) * tangentSign;

				o.worldTangent = worldTangent;
				o.worldNormal = worldNormal;
				o.worldBitangent = worldBitangent;

				OUTPUT_LIGHTMAP_UV( v.uv1, unity_LightmapST, o.lightmapUVOrSH.xy );
				OUTPUT_SH( worldNormal, o.lightmapUVOrSH.xyz );

				o.uv.xy = v.uv0.xy;
				o.uv.zw = 0;

				float3 positionWS = TransformObjectToWorld( v.vertex.xyz );
				float4 positionCS = TransformWorldToHClip( positionWS );

				o.worldPos = positionWS;
				o.fogFactor = ComputeFogFactor( positionCS.z );
				o.clipPos = positionCS;
				return o;
			}

			VertexOutput vert ( VertexInput v )
			{
				return VertexFunction( v );
			}

			// Forward 片元流程：重建法线，计算主光源/附加光源，叠加间接光、
			// 镜面光、边缘光和自发光，最后应用 Alpha 和雾效。
			half4 frag ( VertexOutput IN  ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID( IN );
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );

				float3 WorldPosition = IN.worldPos;
				float4 ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
				// 主纹理/遮蔽/法线/高光/自发光统一使用 _OcclusionMap 的 Tiling/Offset。
				half2 uv = IN.uv.xy * _OcclusionMap_ST.xy + _OcclusionMap_ST.zw;

				// 法线贴图先在切线空间解码，再利用顶点阶段准备的转换基
				// 转换到世界空间。
				half3 tangentNormal = lerp( half3(0,0,1) , UnpackNormalScale( tex2D( _BumpMap, uv ), 1.0f ) , _NormalMapStrength);
				half3 tanToWorld0 = float3( IN.worldTangent.x, IN.worldBitangent.x, IN.worldNormal.x );
				half3 tanToWorld1 = float3( IN.worldTangent.y, IN.worldBitangent.y, IN.worldNormal.y );
				half3 tanToWorld2 = float3( IN.worldTangent.z, IN.worldBitangent.z, IN.worldNormal.z );
				float3 worldNormal = normalize( float3( dot(tanToWorld0,tangentNormal), dot(tanToWorld1,tangentNormal), dot(tanToWorld2,tangentNormal) ) );

				// ── TCP2 风格漫反射管线 ──
				// 核心思路：不让暗面走向黑色，阴影颜色完全由美术通过 _HColor / _ShadowColor 控制。
				// 1. N·L 计算
				half NDotL = dot( worldNormal , _MainLightPosition.xyz );

				// 2. 主光源距离/阴影衰减
				half lightShadowAttenuation = 0;
				{
					#if SHADOWS_SCREEN
						half4 clipPos = TransformWorldToHClip(WorldPosition);
						half4 shadowCoord = ComputeScreenPos(clipPos);
					#else
						half4 shadowCoord = TransformWorldToShadowCoord(WorldPosition);
					#endif
					Light mainLight = GetMainLight(shadowCoord);
					lightShadowAttenuation = mainLight.shadowAttenuation;
				}
				// 3. Wrapped Diffuse：把 N·L 从 [-1,1] 映射到 [0,1]，_DiffuseWrap 控制包裹强度
				half wrappedNL = lerp( max( NDotL , 0.0 ) , saturate( NDotL * 0.5 + 0.5 ) , _DiffuseWrap );
				// 将 wrappedNL 直接作为后续 smoothstep / Ramp / Posterize 的输入（值域 [0,1]），
				// 不再提前乘入 _MainLightColor，确保阈值不受 HDR 光强影响。
				half shadingInput = wrappedNL;
				// 4. 明暗分界：_StepOffset 控制阈值，_RampSmooth 控制过渡柔和度
				half rampThreshold = _StepOffset + 0.5;
				half rampStep = smoothstep( rampThreshold - _RampSmooth , rampThreshold + _RampSmooth , shadingInput);
				// 5. 双色插值：_ShadowColor.a 控制阴影强度（TCP2 _SColor.a 机制）
				//    alpha=0 → 阴影与亮面同色；alpha=1 → 完全采用 _ShadowColor.rgb
				half shadowIntensity = _ShadowColor.a;
				half3 shadowColorMixed = lerp( _HColor.rgb , _ShadowColor.rgb , shadowIntensity );
				// 6. 亮暗插值 → 乘以光源颜色和阴影衰减
				half4 litColor = half4( lerp( shadowColorMixed , _HColor.rgb , saturate( rampStep )) , 1.0 );
				half4 stepShading = half4( lerp( _ShadowColor.rgb , litColor.rgb , lightShadowAttenuation ) , 1.0 );
				// Ramp 纹理模式：采样渐变贴图并用 _HColor 着色
				half2 rampUV = clamp( half2( ( _LightRampOffset + shadingInput ) , 0.0 ) , half2( 0.02, 0.02 ) , half2( 0.98, 0.98 ) );
				half4 rampShading = half4( lerp( tex2D( _LightRampTexture, half2( 0.02,0 ) ).rgb , tex2D( _LightRampTexture, rampUV ).rgb , lightShadowAttenuation ) * _HColor.rgb , 1.0 );
				// Posterize 模式：色阶化后用 _HColor/_ShadowColor 着色
				half posterizeInput = pow( saturate( ( shadingInput - _DiffusePosterizeOffset ) ) , _DiffusePosterizePower );
				half posterizeSteps = round( _DiffusePosterizeSteps );
				half posterizeValue = Posterize( posterizeInput , posterizeSteps );
				half3 posterizeColor = lerp( shadowColorMixed , _HColor.rgb , posterizeValue );
				half4 posterizeLit = half4( posterizeColor , 1.0 );
				half4 posterizeShading = half4( lerp( _ShadowColor.rgb , posterizeLit.rgb , lightShadowAttenuation ) , 1.0 );
				#if defined(_USELIGHTRAMP_STEP)
				half4 diffuseShading = stepShading;
				#elif defined(_USELIGHTRAMP_DIFFUSERAMP)
				half4 diffuseShading = rampShading;
				#elif defined(_USELIGHTRAMP_POSTERIZE)
				half4 diffuseShading = posterizeShading;
				#else
				half4 diffuseShading = stepShading;
				#endif
				// 将可选的逐像素附加光源漫反射叠加到主光源结果上。
				// 对应 Shader 关键字关闭时，整个分支可以在编译时移除。
				half3 lightWrapVector = (_DiffuseWrap).xxx;
				half smoothMax = ( 1.0 - ( _AdditionalLightsAmount * 0.9 ) );
				half smoothMin = ( ( smoothMax * _AdditionalLightsFaloff ) - 0.005 );
				half3 additionalDiffuse = AdditionalLight( WorldPosition , worldNormal , lightWrapVector , smoothMin , smoothMax , 0.0 , float4( 1,1,1,1 ) );
				#ifdef _USEADDITIONALLIGHTSDIFFUSE_ON
				half4 diffuseShadingPlus = ( diffuseShading + half4( additionalDiffuse , 0.0 ) );
				#else
				half4 diffuseShadingPlus = diffuseShading;
				#endif
				// 主纹理与遮蔽：材质颜色 × 主纹理 × 灰度遮蔽遮罩
				half4 mainTexSample = tex2D( _MainTex, uv );
				half occlusion = lerp( 1.0 , tex2D( _OcclusionMap, uv ).r , _OcclusionStrength);
				half4 mainTexture = ( _Color * mainTexSample * half4( occlusion, occlusion, occlusion, 1.0 ) );
				// 将主纹理/遮蔽与烘焙光或环境光结合，并乘以材质的间接光强度。
				half3 bakedGI = SampleIndirectDiffuse( IN.lightmapUVOrSH.xy, worldNormal);
				Light mainLight = GetMainLight( ShadowCoords );
				MixRealtimeAndBakedGI(mainLight, worldNormal, bakedGI, half4(0,0,0,0));
				half3 indirectDiffuseFactor = lerp( float3( 0,0,0 ) , bakedGI , _IndirectLightStrength);
				half4 indirectDiffuseLight = ( mainTexture * half4( indirectDiffuseFactor , 0.0 ) );
				half4 finalDiffuse = ( ( diffuseShadingPlus * mainTexture ) + indirectDiffuseLight );
				// 世界空间视线方向
				float3 worldViewDir = normalize( _WorldSpaceCameraPos.xyz - WorldPosition );
				// 镜面高光：附加光源 + 主光源 Blinn-Phong，再按设置色阶化。
				half smoothness = ( tex2D( _SpecGlossMap, uv ).r * _Glossiness );
				half additionalSmoothness = ( smoothness * ( 2.0 - _AdditionalLightsSmoothnessMultiplier ) );
				half specularSteps = round( _SpecularPosterizeSteps );
				half3 additionalSpecular = AdditionalLightsSpecular( WorldPosition , worldNormal , worldViewDir , half3(1,1,1) , additionalSmoothness , specularSteps , _SpecularFaloff );
				half3 mainLightDir = normalize( _MainLightPosition.xyz );
				half3 halfDir = normalize( ( mainLightDir + worldViewDir ) );
				half halfDotN = dot( halfDir , worldNormal );
				half mainSpecularPower = ( exp2( ( ( smoothness * 10.0 * ( 2.0 - _SmoothnessMultiplier ) ) + -2.0 ) ) * 2.0 );
				half mainSpecularInput = ( pow( max( halfDotN , 0.0 ) , mainSpecularPower ) * ( _SmoothnessMultiplier == 0.0 ? 0.0 : 1.0 ) );
				half mainSpecular = PosterizeFaloff( mainSpecularInput , _SpecularFaloff , specularSteps );
				// 阴影遮罩和材质关键字共同决定镜面光保留的强度。
				#ifdef _USESPECULAR_ON
				half4 specularFinal = ( half4( ( ( additionalSpecular * _AdditionalLightsIntesity * ( _AdditionalLightsSmoothnessMultiplier == 0.0 ? 0.0 : 1.0 ) ) + ( _MainLightColor.rgb * _MainLightIntesity * mainSpecular ) ) , 0.0 ) * smoothness * _SpecColor );
				#else
				half4 specularFinal = float4( 0,0,0,0 );
				#endif
				// 暗部通过镜面光阴影遮罩削弱高光。
				half diffuseLuminance = dot(diffuseShadingPlus.rgb, float3(0.299,0.587,0.114));
				half specularShadowMask = lerp( 1.0 , diffuseLuminance , _SpecularShadowMask);
				// 环境反射（可选）
				float3 reflectVector = reflect( -worldViewDir, worldNormal );
				float3 indirectSpecular = GlossyEnvironmentReflection( reflectVector, 1.0 - smoothness, 0.75 );
				#ifdef _USEENVIRONMENTREFLETION_ON
				half3 indirectSpecularFinal = ( indirectSpecular * _Strength * smoothness );
				#else
				half3 indirectSpecularFinal = float3( 0,0,0 );
				#endif
				half4 lightingColor = ( finalDiffuse + ( specularFinal * specularShadowMask ) + half4( indirectSpecularFinal , 0.0 ) );
				// Fresnel 风格的边缘光强调背向摄像机的轮廓边缘；
				// 分色关键字决定边缘光颜色与漫反射颜色的关系。
				half4 diffuseNoAdditional = diffuseShading;
				half diffuseLum = Luminance(diffuseNoAdditional.rgb);
				half4 rimShadowLerp = lerp( _RimShadowColor , _RimColor , diffuseLum);
				#if defined(_RIMSPLITCOLOR_NOSPLIT)
				half4 rimColor = _RimColor;
				#elif defined(_RIMSPLITCOLOR_MULTIPLYWITHDIFFUSE)
				half4 rimColor = ( _RimColor * diffuseNoAdditional );
				#elif defined(_RIMSPLITCOLOR_USESECONDCOLOR)
				half4 rimColor = rimShadowLerp;
				#else
				half4 rimColor = _RimColor;
				#endif
				half fresnelNDotV = dot( normalize( worldNormal ), worldViewDir );
				half fresnelValue = ( 0.0 + _RimThickness * pow( max( 1.0 - fresnelNDotV , 0.0001 ), _RimPower ) );
				half rimSmooth = smoothstep( ( ( 1.0 - _RimSmoothness ) - 0.5 ) , 0.5 , fresnelValue);
				#ifdef _USERIMLIGHT_ON
				half rimLight = rimSmooth;
				#else
				half rimLight = 0.0;
				#endif
				half4 litColorFinal = lerp( lightingColor , rimColor , rimLight);
				// 自发光
				half4 emission = ( _UseEmission == 1.0 ? ( tex2D( _EmissionMap, uv ) * _EmissionColor ) : float4( 0,0,0,0 ) );

				half mainTexAlpha = ( mainTexture.a * 1.0 );

				float3 Color = ( litColorFinal + emission ).rgb;
				float Alpha = mainTexAlpha;
				float AlphaClipThreshold = _Cutoff;

				#ifdef _ALPHATEST_ON
					clip( Alpha - AlphaClipThreshold );
				#endif

				#ifdef LOD_FADE_CROSSFADE
					LODDitheringTransition( IN.clipPos.xyz, unity_LODFade.x );
				#endif

				Color = MixFog( Color, IN.fogFactor );

				return half4( Color, Alpha );
			}

			ENDHLSL
		}

		Pass
		{
			// ShadowCaster Pass 只向光源阴影贴图写入深度。
			// 它重复材质的 Alpha 测试，避免镂空像素投射出完整实心轮廓。
			Name "ShadowCaster"
			Tags { "LightMode"="ShadowCaster" "Queue"="Geometry" }

			ZWrite On
			ZTest LEqual
			AlphaToMask Off
			ColorMask 0

			HLSLPROGRAM

			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#pragma multi_compile_instancing
			#pragma multi_compile _ DOTS_INSTANCING_ON
			#define _ALPHATEST_ON 1

			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile _ _CASTING_PUNCTUAL_LIGHT_SHADOW
			#define SHADERPASS SHADERPASS_SHADOWCASTER

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

			#define SHADOWS_SHADOWMASK
			#define LIGHTMAP_SHADOW_MIXING


			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 normalOS : NORMAL;
				float4 uv0 : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				float4 uv : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			half4 _EmissionColor;
			half4 _SpecColor;
			half4 _RimColor;
			half4 _RimShadowColor;
			half4 _Color;
			half4 _ShadowColor;
			half4 _HColor;
			half4 _OcclusionMap_ST;
			half4 _OutlineColor;
			half _RimThickness;
			half _SpecularPosterizeSteps;
			half _Strength;
			half _SpecularShadowMask;
			half _RimPower;
			half _SmoothnessMultiplier;
			half _MainLightIntesity;
			half _AdditionalLightsIntesity;
			half _SpecularFaloff;
			half _RimSmoothness;
			half _AdditionalLightsSmoothnessMultiplier;
			half _AdditionalLightsFaloff;
			half _IndirectLightStrength;
			half _UseEmission;
			half _AdditionalLightsAmount;
			half _DiffusePosterizeSteps;
			half _DiffusePosterizePower;
			half _DiffusePosterizeOffset;
			half _DiffuseWrap;
			half _LightRampOffset;
			half _NormalMapStrength;
			half _StepOffset;
			half _RampSmooth;
			half _Cutoff;
			half _OcclusionStrength;
			half _OutlineTextureStrength;
			half _Thicnkess;
			half _AdaptiveThicnkess;
			half _Glossiness;
			half _RampDiffuseTextureLoaded;
			CBUFFER_END
			sampler2D _MainTex;
			sampler2D _OcclusionMap;


			float3 _LightDirection;
			float3 _LightPosition;
			// 将网格转换到阴影贴图空间，并应用 URP 基于法线的阴影偏移。
			VertexOutput VertexFunction( VertexInput v )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( o );

				o.uv.xy = v.uv0.xy;
				o.uv.zw = 0;

				float3 positionWS = TransformObjectToWorld( v.vertex.xyz );
				float3 normalWS = TransformObjectToWorldDir( v.normalOS );

				#if _CASTING_PUNCTUAL_LIGHT_SHADOW
					float3 lightDirectionWS = normalize(_LightPosition - positionWS);
				#else
					float3 lightDirectionWS = _LightDirection;
				#endif
				float4 clipPos = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
				#if UNITY_REVERSED_Z
					clipPos.z = min(clipPos.z, UNITY_NEAR_CLIP_VALUE);
				#else
					clipPos.z = max(clipPos.z, UNITY_NEAR_CLIP_VALUE);
				#endif

				o.clipPos = clipPos;
				return o;
			}

			VertexOutput vert ( VertexInput v )
			{
				return VertexFunction( v );
			}

			// 阴影片元不输出颜色；Alpha 裁剪决定当前像素是否写入阴影贴图。
			half4 frag(VertexOutput IN  ) : SV_TARGET
			{
				UNITY_SETUP_INSTANCE_ID( IN );
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );

				// 复用可见 Pass 的主纹理/遮蔽 Alpha 计算，保证透明或镂空部分
				// 在阴影中的形状保持一致。
				half2 uv = IN.uv.xy * _OcclusionMap_ST.xy + _OcclusionMap_ST.zw;
				half4 mainTexSample = tex2D( _MainTex, uv );
				half occlusion = lerp( 1.0 , tex2D( _OcclusionMap, uv ).r , _OcclusionStrength);
				half4 mainTexture = ( _Color * mainTexSample * half4( occlusion, occlusion, occlusion, 1.0 ) );
				half mainTexAlpha = ( mainTexture.a * 1.0 );

				float Alpha = mainTexAlpha;
				float AlphaClipThreshold = _Cutoff;
				float AlphaClipThresholdShadow = 0.5;

				#ifdef _ALPHATEST_ON
					#ifdef _ALPHATEST_SHADOW_ON
						clip(Alpha - AlphaClipThresholdShadow);
					#else
						clip(Alpha - AlphaClipThreshold);
					#endif
				#endif

				#ifdef LOD_FADE_CROSSFADE
					LODDitheringTransition( IN.clipPos.xyz, unity_LODFade.x );
				#endif
				return 0;
			}

			ENDHLSL
		}

		Pass
		{
			// DepthOnly Pass 填充摄像机深度纹理，但不写入颜色。
			// Alpha 裁剪保证深度、屏幕空间效果和镂空几何体保持一致。

			Name "DepthOnly"
			Tags { "LightMode"="DepthOnly" "Queue"="Geometry" }

			ZWrite On
			ColorMask 0
			AlphaToMask Off

			HLSLPROGRAM

			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#pragma multi_compile_instancing
			#pragma multi_compile _ DOTS_INSTANCING_ON
			#define _ALPHATEST_ON 1

			#pragma vertex vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

			#define SHADOWS_SHADOWMASK
			#define LIGHTMAP_SHADOW_MIXING


			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 normalOS : NORMAL;
				float4 uv0 : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				float4 uv : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			half4 _EmissionColor;
			half4 _SpecColor;
			half4 _RimColor;
			half4 _RimShadowColor;
			half4 _Color;
			half4 _ShadowColor;
			half4 _HColor;
			half4 _OcclusionMap_ST;
			half4 _OutlineColor;
			half _RimThickness;
			half _SpecularPosterizeSteps;
			half _Strength;
			half _SpecularShadowMask;
			half _RimPower;
			half _SmoothnessMultiplier;
			half _MainLightIntesity;
			half _AdditionalLightsIntesity;
			half _SpecularFaloff;
			half _RimSmoothness;
			half _AdditionalLightsSmoothnessMultiplier;
			half _AdditionalLightsFaloff;
			half _IndirectLightStrength;
			half _UseEmission;
			half _AdditionalLightsAmount;
			half _DiffusePosterizeSteps;
			half _DiffusePosterizePower;
			half _DiffusePosterizeOffset;
			half _DiffuseWrap;
			half _LightRampOffset;
			half _NormalMapStrength;
			half _StepOffset;
			half _RampSmooth;
			half _Cutoff;
			half _OcclusionStrength;
			half _OutlineTextureStrength;
			half _Thicnkess;
			half _AdaptiveThicnkess;
			half _Glossiness;
			half _RampDiffuseTextureLoaded;
			CBUFFER_END
			sampler2D _MainTex;
			sampler2D _OcclusionMap;


			// 将对象顶点转换到裁剪空间，并传递 Alpha 测试所需的 UV。
			VertexOutput VertexFunction( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				o.uv.xy = v.uv0.xy;
				o.uv.zw = 0;

				float3 positionWS = TransformObjectToWorld( v.vertex.xyz );
				o.clipPos = TransformWorldToHClip( positionWS );
				return o;
			}

			VertexOutput vert ( VertexInput v )
			{
				return VertexFunction( v );
			}

			// 这里仅关心 Alpha 测试；ColorMask 0 会丢弃片元颜色，
			// 但深度缓冲区仍会接收该片元的深度值。
			half4 frag(VertexOutput IN  ) : SV_TARGET
			{
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );

				half2 uv = IN.uv.xy * _OcclusionMap_ST.xy + _OcclusionMap_ST.zw;
				half4 mainTexSample = tex2D( _MainTex, uv );
				half occlusion = lerp( 1.0 , tex2D( _OcclusionMap, uv ).r , _OcclusionStrength);
				half4 mainTexture = ( _Color * mainTexSample * half4( occlusion, occlusion, occlusion, 1.0 ) );
				half mainTexAlpha = ( mainTexture.a * 1.0 );

				float Alpha = mainTexAlpha;
				float AlphaClipThreshold = _Cutoff;

				#ifdef _ALPHATEST_ON
					clip(Alpha - AlphaClipThreshold);
				#endif

				#ifdef LOD_FADE_CROSSFADE
					LODDitheringTransition( IN.clipPos.xyz, unity_LODFade.x );
				#endif
				return 0;
			}
			ENDHLSL
		}
	}

	CustomEditor "CartoonEditor"
	Fallback "Hidden/InternalErrorShader"
}
