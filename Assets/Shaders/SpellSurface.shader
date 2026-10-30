// Stylized surface for the spells' bodies (wave, rocks, vines, ice): a color ramp along the mesh's
// v coordinate, banded "toon" lighting from the sun, a rim of light around the edges, and two
// animations driven from scripts or the material: Grow reveals the mesh along v with a glowing edge,
// Wave ripples it along its normals. Lit by the main light and the ambient only, to stay cheap.
Shader "RuneArena/Spell Surface"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Color at v = 0", Color) = (1, 1, 1, 1)
        _TipColor ("Color at v = 1", Color) = (1, 1, 1, 1)
        _Emission ("Self Lit", Range(0, 2)) = 0
        _Bands ("Light Bands (0 = smooth)", Float) = 3
        _RimColor ("Rim Color (alpha = strength)", Color) = (1, 1, 1, 0)
        _RimPower ("Rim Power", Float) = 3
        _Scroll ("Texture Scroll (uv per second)", Vector) = (0, 0, 0, 0)
        _Wave ("Ripple (amplitude, frequency, speed)", Vector) = (0, 0, 0, 0)
        _Grow ("Grow", Range(0, 1)) = 1
        _GrowEdge ("Grow Edge Color", Color) = (1, 1, 1, 1)
        _Alpha ("Alpha", Range(0, 1)) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        [Toggle] _ZWrite ("Z Write", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _TipColor;
            half _Emission;
            half _Bands;
            half4 _RimColor;
            half _RimPower;
            float4 _Scroll;
            float4 _Wave;
            half _Grow;
            half4 _GrowEdge;
            half _Alpha;
        CBUFFER_END

        // Ripple along the normal, running across the mesh's x, stronger toward v = 1.
        float3 Ripple(float3 positionOS, float3 normalOS, float v)
        {
            float phase = positionOS.x * _Wave.y + _Time.y * _Wave.z;
            return positionOS + normalOS * (sin(phase) * _Wave.x * v);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 rawUv : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
                half fog : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionOS = Ripple(input.positionOS.xyz, input.normalOS, input.uv.y);
                VertexPositionInputs position = GetVertexPositionInputs(positionOS);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap) + _Scroll.xy * _Time.y;
                output.rawUv = input.uv;
                output.fog = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 Fragment(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Not grown this far yet.
                half grown = _Grow - input.rawUv.y;
                clip(grown);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * lerp(_BaseColor, _TipColor, saturate(input.rawUv.y));

                float3 normal = normalize(input.normalWS) * IS_FRONT_VFACE(face, 1, -1);
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half diffuse = saturate(dot(normal, light.direction)) * light.shadowAttenuation;
                if (_Bands > 0.5)
                    diffuse = floor(diffuse * _Bands + 0.5) / _Bands;

                half3 color = albedo.rgb * (SampleSH(normal) + light.color * diffuse + _Emission);

                half rim = pow(1 - saturate(dot(normal, GetWorldSpaceNormalizeViewDir(input.positionWS))), _RimPower);
                color += _RimColor.rgb * (rim * _RimColor.a);

                // A bright edge where it is growing, gone once fully grown.
                half edge = saturate(1 - grown * 25) * step(_Grow, 0.999);
                color = lerp(color, _GrowEdge.rgb, edge);

                color = MixFog(color, input.fog);
                return half4(color, albedo.a * _Alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 rawUv : TEXCOORD0;
            };

            Varyings ShadowVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(Ripple(input.positionOS.xyz, input.normalOS, input.uv.y));
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                output.positionCS = positionCS;
                output.rawUv = input.uv;
                return output;
            }

            half4 ShadowFragment(Varyings input) : SV_Target
            {
                clip(_Grow - input.rawUv.y);
                return 0;
            }
            ENDHLSL
        }
    }
}
