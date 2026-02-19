Shader "Custom/TriplanarWall"
{
    Properties
    {
        _MainTex ("Wall Texture", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _RoughnessMap ("Roughness Map", 2D) = "white" {}
        _OcclusionMap ("Occlusion Map", 2D) = "white" {}
        _Tiling ("Texture Tiling", Float) = 1.0
        _Sharpness ("Blend Sharpness", Range(1, 16)) = 4.0
        _Color ("Tint Color", Color) = (1, 1, 1, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.2
        _RoughnessStrength ("Roughness Strength", Range(0, 1)) = 1.0
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1.0
        _NormalStrength ("Normal Strength", Range(0, 2)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float  fogFactor   : TEXCOORD2;
                float3 tangentWS   : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            TEXTURE2D(_RoughnessMap);
            SAMPLER(sampler_RoughnessMap);
            TEXTURE2D(_OcclusionMap);
            SAMPLER(sampler_OcclusionMap);

            CBUFFER_START(UnityPerMaterial)
                float _Tiling;
                float _Sharpness;
                float4 _Color;
                float _Smoothness;
                float _RoughnessStrength;
                float _OcclusionStrength;
                float _NormalStrength;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS  = posInputs.positionCS;
                output.positionWS  = posInputs.positionWS;
                output.normalWS    = normInputs.normalWS;
                output.tangentWS   = normInputs.tangentWS;
                output.bitangentWS = normInputs.bitangentWS;
                output.fogFactor   = ComputeFogFactor(posInputs.positionCS.z);

                return output;
            }

            // Triplanar sampling — projects texture from X, Y, Z axes
            float4 TriplanarSample(TEXTURE2D_PARAM(tex, samp), float3 worldPos, float3 blendWeights)
            {
                float2 uvX = worldPos.zy * _Tiling;
                float2 uvY = worldPos.xz * _Tiling;
                float2 uvZ = worldPos.xy * _Tiling;

                float4 colX = SAMPLE_TEXTURE2D(tex, samp, uvX);
                float4 colY = SAMPLE_TEXTURE2D(tex, samp, uvY);
                float4 colZ = SAMPLE_TEXTURE2D(tex, samp, uvZ);

                return colX * blendWeights.x + colY * blendWeights.y + colZ * blendWeights.z;
            }

            // Triplanar normal sampling with proper tangent-space blending
            float3 TriplanarNormal(TEXTURE2D_PARAM(tex, samp), float3 worldPos, float3 blendWeights)
            {
                float2 uvX = worldPos.zy * _Tiling;
                float2 uvY = worldPos.xz * _Tiling;
                float2 uvZ = worldPos.xy * _Tiling;

                float3 nX = UnpackNormal(SAMPLE_TEXTURE2D(tex, samp, uvX));
                float3 nY = UnpackNormal(SAMPLE_TEXTURE2D(tex, samp, uvY));
                float3 nZ = UnpackNormal(SAMPLE_TEXTURE2D(tex, samp, uvZ));

                nX = float3(nX.xy * _NormalStrength, nX.z);
                nY = float3(nY.xy * _NormalStrength, nY.z);
                nZ = float3(nZ.xy * _NormalStrength, nZ.z);

                // Whiteout blending for proper triplanar normals
                nX = float3(nX.xy + float2(0, 0), nX.z);
                nY = float3(nY.xy + float2(0, 0), nY.z);
                nZ = float3(nZ.xy + float2(0, 0), nZ.z);

                return normalize(nX * blendWeights.x + nY * blendWeights.y + nZ * blendWeights.z);
            }

            float4 frag(Varyings input) : SV_Target
            {
                // Calculate blend weights from world normal
                float3 absNormal = abs(input.normalWS);
                float3 blendWeights = pow(absNormal, _Sharpness);
                blendWeights /= (blendWeights.x + blendWeights.y + blendWeights.z + 0.0001);

                // Sample albedo via triplanar
                float4 albedo = TriplanarSample(
                    TEXTURE2D_ARGS(_MainTex, sampler_MainTex),
                    input.positionWS, blendWeights
                );
                albedo *= _Color;

                // Sample normal via triplanar
                float3 triNormal = TriplanarNormal(
                    TEXTURE2D_ARGS(_NormalMap, sampler_NormalMap),
                    input.positionWS, blendWeights
                );

                // Build TBN matrix and transform normal to world space
                float3x3 TBN = float3x3(
                    normalize(input.tangentWS),
                    normalize(input.bitangentWS),
                    normalize(input.normalWS)
                );
                float3 normalWS = normalize(mul(triNormal, TBN));

                // URP Lighting
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                #ifdef _MAIN_LIGHT_SHADOWS
                    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                    inputData.shadowCoord = float4(0, 0, 0, 0);
                #endif

                // Sample roughness via triplanar (invert to smoothness)
                float roughness = TriplanarSample(
                    TEXTURE2D_ARGS(_RoughnessMap, sampler_RoughnessMap),
                    input.positionWS, blendWeights
                ).r;
                float finalSmoothness = _Smoothness * (1.0 - roughness * _RoughnessStrength);

                // Sample occlusion via triplanar
                float occlusion = TriplanarSample(
                    TEXTURE2D_ARGS(_OcclusionMap, sampler_OcclusionMap),
                    input.positionWS, blendWeights
                ).r;
                float finalOcclusion = lerp(1.0, occlusion, _OcclusionStrength);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo.rgb;
                surfaceData.alpha = 1.0;
                surfaceData.metallic = 0.0;
                surfaceData.smoothness = finalSmoothness;
                surfaceData.normalTS = triNormal;
                surfaceData.occlusion = finalOcclusion;

                float4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, input.fogFactor);

                return color;
            }
            ENDHLSL
        }

        // Shadow caster pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            float3 _LightDirection;

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output;
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, normWS, _LightDirection));

                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                return output;
            }

            float4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
