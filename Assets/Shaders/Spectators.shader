// The spectators in the stands, as billboards. Every vertex of a spectator sits at its seat, and the shader
// builds the quad there: turned to face the player around its own vertical (the same way for both eyes, from
// _CrowdViewer), swaying in its seat, and jumping up with its arms in the air when the crowd is excited.
// The texture holds masks, not colors (Spectators.png from tools/textures): R the shading, G the part (clothes,
// hair or skin), B what covers it (metal, the light cloth of a toga, or nothing) and A the cutout. Clothes take the
// vertex color; hair and skin are picked from the spectator's random numbers. Set by the Spectators component
// every frame:
//   _CrowdState  x the time, y how excited the whole crowd is (0 to 1)
//   _CrowdKill   x the angle around the arena of the last kill, y when it was
//   _CrowdOla    x when the wave around the stands started, y its speed in radians per second, z how long it lasts
Shader "RuneArena/Spectators"
{
    Properties
    {
        _BaseMap ("Spectators (R shade, G part, A cutout)", 2D) = "white" {}
        _People ("People in the texture", Float) = 8
        _Metal ("Helmets and armor", Color) = (0.62, 0.64, 0.68, 1)
        _Toga ("Togas and veils", Color) = (0.9, 0.87, 0.8, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #define TAU 6.28318530718

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _People;
                half4 _Metal;
                half4 _Toga;
            CBUFFER_END

            float4 _CrowdViewer;
            float4 _CrowdState;
            float4 _CrowdKill;
            float4 _CrowdOla;

            struct Attributes
            {
                float4 positionOS : POSITION;
                // xy the corner of the quad (0 to 1), z which person, w a random number
                float4 uv : TEXCOORD0;
                // x its angle around the arena, y its height, z and w more random numbers (skin, hair)
                float4 data : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 clothes : TEXCOORD1;
                half3 skin : TEXCOORD2;
                half3 hair : TEXCOORD3;
                half fog : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float AngleGap(float a, float b)
            {
                float d = fmod(abs(a - b), TAU);
                return min(d, TAU - d);
            }

            // How worked up the crowd is where this spectator sits, from 0 to 1.
            float Excitement(float angle)
            {
                float now = _CrowdState.x;
                float excited = _CrowdState.y;

                float sinceKill = now - _CrowdKill.y;
                float near = exp(-pow(AngleGap(angle, _CrowdKill.x) / 0.35, 2));
                excited = max(excited, near * saturate(1 - sinceKill / 2.5) * step(0, sinceKill));

                float sinceOla = now - _CrowdOla.x;
                if (sinceOla > 0 && sinceOla < _CrowdOla.z)
                    excited = max(excited, exp(-pow(AngleGap(angle, fmod(sinceOla * _CrowdOla.y, TAU)) / 0.25, 2)));
                return excited;
            }

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float2 corner = input.uv.xy;
                float seed = input.uv.w;
                float height = input.data.y;
                float now = _CrowdState.x;

                // Up on its feet once the excitement passes its own threshold: more of them the louder it gets.
                float cheering = step(0.05 + seed * 0.9, Excitement(input.data.x));
                float sway = sin(now * 1.7 + seed * 40) * 0.012;
                float jump = cheering * abs(sin(now * 7 + seed * 30)) * 0.12;

                float3 seat = TransformObjectToWorld(input.positionOS.xyz);
                float3 toViewer = _CrowdViewer.xyz - seat;
                toViewer.y = 0;
                float3 right = normalize(cross(toViewer + float3(0, 0, 0.0001), float3(0, 1, 0)));
                float3 positionWS = seat
                    + right * ((corner.x - 0.5) * height * 0.5 + sway)
                    + float3(0, corner.y * height + jump, 0);

                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = float2((input.uv.z + corner.x) / _People, (cheering + corner.y) * 0.5);
                output.clothes = input.color.rgb;
                output.skin = lerp(half3(0.92, 0.74, 0.6), half3(0.42, 0.28, 0.18), input.data.z);
                output.hair = lerp(half3(0.35, 0.22, 0.12), half3(0.06, 0.05, 0.05), input.data.w);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 masks = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip(masks.a - 0.5);
                half3 albedo = masks.g > 0.75 ? input.clothes : masks.g > 0.25 ? input.hair : input.skin;
                albedo = masks.b > 0.75 ? _Metal.rgb : masks.b > 0.25 ? _Toga.rgb : albedo;

                // Lit as if facing up into the sky: a flat card can't take the sun from the side.
                Light light = GetMainLight();
                half3 color = albedo * masks.r * (SampleSH(half3(0, 1, 0)) + light.color * 0.5);
                color = MixFog(color, input.fog);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
