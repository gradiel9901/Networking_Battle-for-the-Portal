Shader "Custom/GlowingPortal"
{
    Properties
    {
        // colors for the portal, [HDR] means can go above 1 for bloom
        [HDR] _InnerColor        ("Inner Core Color",    Color) = (0.1, 0.8, 1.0, 1.0)
        [HDR] _OuterColor        ("Outer Ring Color",    Color) = (0.0, 0.2, 1.0, 1.0)
        [HDR] _RimColor          ("Rim / Edge Color",    Color) = (0.5, 0.0, 1.0, 1.0)
        [HDR] _EnergyColor       ("Energy Arc Color",    Color) = (1.0, 0.4, 0.0, 1.0)

        // how big the circle is and how the edge looks
        _PortalRadius            ("Portal Radius",         Range(0.01, 0.5)) = 0.45
        _RimWidth                ("Rim Width",             Range(0.001, 0.15)) = 0.06
        _RimSharpness            ("Rim Sharpness",         Range(1.0, 64.0))  = 16.0

        // controls the spinning swirl effect
        _VortexSpeed             ("Vortex Rotation Speed", Range(-10.0, 10.0)) = 2.5
        _VortexTwist             ("Vortex Twist Amount",   Range(0.0, 20.0))   = 8.0
        _VortexScale             ("Vortex Noise Scale",    Range(0.5, 8.0))    = 3.0

        // noise stuff, makes it look less flat
        _NoiseScale              ("Noise Scale",           Range(0.5, 16.0))  = 4.0
        _NoiseSpeed              ("Noise Scroll Speed",    Range(0.0, 4.0))   = 0.6
        _NoiseStrength           ("Noise Strength",        Range(0.0, 1.0))   = 0.35
        _DistortionStrength      ("Distortion Strength",   Range(0.0, 0.5))   = 0.12

        // the rings that pulse outward
        _RingCount               ("Ring Count",            Range(1.0, 12.0))  = 5.0
        _RingSpeed               ("Ring Pulse Speed",      Range(0.0, 6.0))   = 1.8
        _RingWidth               ("Ring Width",            Range(0.001, 0.2))  = 0.04
        _RingIntensity           ("Ring Intensity",        Range(0.0, 4.0))   = 1.5

        // brightness and glow stuff
        _CoreBrightness          ("Core Brightness",       Range(0.0, 8.0))   = 3.5
        _GlowFalloff             ("Glow Falloff Power",    Range(0.5, 6.0))   = 2.2
        _ChromaticAberration     ("Chromatic Aberration",  Range(0.0, 0.05))  = 0.012

        _Alpha                   ("Overall Alpha",         Range(0.0, 1.0))   = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Transparent"  // render after opaque objects
            "IgnoreProjector"= "True"
        }

        Pass
        {
            Name "PortalForward"
            Tags { "LightMode" = "UniversalForward" }

            // additive blend makes it glow brighter on top of things
            Blend One One
            ZWrite Off   // dont write to depth, its transparent
            Cull Off     // render both sides just in case

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // need to put all material properties in here for URP batching
            CBUFFER_START(UnityPerMaterial)
                float4 _InnerColor;
                float4 _OuterColor;
                float4 _RimColor;
                float4 _EnergyColor;

                float  _PortalRadius;
                float  _RimWidth;
                float  _RimSharpness;

                float  _VortexSpeed;
                float  _VortexTwist;
                float  _VortexScale;

                float  _NoiseScale;
                float  _NoiseSpeed;
                float  _NoiseStrength;
                float  _DistortionStrength;

                float  _RingCount;
                float  _RingSpeed;
                float  _RingWidth;
                float  _RingIntensity;

                float  _CoreBrightness;
                float  _GlowFalloff;
                float  _ChromaticAberration;
                float  _Alpha;
            CBUFFER_END

            // vertex input from the mesh
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            // what we pass from vert to frag
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float  fogFactor  : TEXCOORD1;
            };

            // random-ish number from a 2d coordinate, used for noise
            float hash2(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p.yx + 19.19);
                return frac((p.x + p.y) * p.x);
            }

            // smooth noise using 4 corner hashes and interpolating between them
            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f); // smoothstep curve

                float a = hash2(i + float2(0,0));
                float b = hash2(i + float2(1,0));
                float c = hash2(i + float2(0,1));
                float d = hash2(i + float2(1,1));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // fractal brownian motion - basically layered noise
            // each loop adds a smaller but faster version of the noise on top
            float FBM(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                float2x2 rot = float2x2(1.6,  1.2, -1.2, 1.6); // rotates the noise each octave so it doesnt tile

                UNITY_UNROLL
                for (int i = 0; i < 4; i++)
                {
                    v += a * ValueNoise(p);
                    p  = mul(rot, p);
                    a *= 0.5; // each layer is half as strong
                }
                return v;
            }

            // warps the uv to look like its spinning/swirling
            float2 ApplyVortex(float2 uv, float dist, float time)
            {
                float angle = atan2(uv.y, uv.x); // convert xy to angle
                float radius = dist;

                // more twist in the center, less near the edge
                float twist = _VortexTwist * (1.0 - saturate(radius / _PortalRadius));
                angle += twist + time * _VortexSpeed;

                float2 warpedUV;
                sincos(angle, warpedUV.y, warpedUV.x); // back to xy from angle
                warpedUV *= radius * _VortexScale;

                return warpedUV;
            }

            // draws the pulsing rings that move outward from the center
            float EnergyRings(float dist, float time)
            {
                float rings = 0.0;
                float normalizedDist = dist / _PortalRadius; // 0 at center, 1 at edge

                UNITY_LOOP
                for (int r = 0; r < (int)_RingCount; r++)
                {
                    float ringPhase  = (float)r / _RingCount;
                    float ringRadius = frac(ringPhase - time * _RingSpeed * 0.1); // moves outward over time
                    float ringDist   = abs(normalizedDist - ringRadius);
                    float ring       = smoothstep(_RingWidth, 0.0, ringDist);
                    rings           += ring * (1.0 - ringRadius); // fade out rings as they expand
                }
                return saturate(rings);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = posInputs.positionCS;
                OUT.uv         = IN.uv;
                OUT.fogFactor  = ComputeFogFactor(posInputs.positionCS.z);
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float time = _Time.y;

                // remap uv from 0..1 to -1..1 so center is (0,0)
                float2 centeredUV = IN.uv * 2.0 - 1.0;
                float dist = length(centeredUV) * 0.5;

                // throw away pixels outside the portal circle + a small glow area
                // this hides the quad corners so you dont see the rectangle
                float haloExtent = _PortalRadius + 0.07;
                clip(haloExtent - dist);

                // 1 inside the circle, 0 outside
                float portalMask = 1.0 - smoothstep(_PortalRadius - 0.005, _PortalRadius + 0.005, dist);

                // the bright ring around the edge of the portal
                float rimMin = _PortalRadius - _RimWidth;
                float rimFactor = smoothstep(rimMin, _PortalRadius, dist)
                                * smoothstep(_PortalRadius + 0.01, _PortalRadius, dist);
                rimFactor = pow(saturate(rimFactor), max(0.5, 32.0 / _RimSharpness));

                // get warped uvs for sampling the noise
                float2 vortexUV = ApplyVortex(centeredUV, dist, time);

                // chromatic aberration = shift r and b channels slightly apart
                float  caOffset = _ChromaticAberration;
                float2 noiseUV  = vortexUV + time * _NoiseSpeed;

                float noiseR = FBM(noiseUV * _NoiseScale + float2( caOffset, 0));
                float noiseG = FBM(noiseUV * _NoiseScale);
                float noiseB = FBM(noiseUV * _NoiseScale + float2(-caOffset, 0));

                float3 chromaNoise = float3(noiseR, noiseG, noiseB);

                // extra distortion layer to make the edge look wobbly
                float2 distortUV = vortexUV * _NoiseScale * 0.8 + time * 0.4;
                float  distort   = FBM(distortUV) * 2.0 - 1.0;
                float2 distortedCentered = centeredUV + distort * _DistortionStrength * portalMask;
                float  distortedDist = length(distortedCentered) * 0.5;
                float  distortedMask = 1.0 - smoothstep(_PortalRadius - 0.005, _PortalRadius + 0.01, distortedDist);

                float noiseStrength = FBM(noiseUV * _NoiseScale) * _NoiseStrength;

                // 0 at edge, 1 at center
                float normalizedDist = saturate(dist / _PortalRadius);
                float coreGlow   = pow(1.0 - normalizedDist, _GlowFalloff) * _CoreBrightness;
                float innerBlend = pow(1.0 - normalizedDist, 1.5);

                // mix the two main colors based on how close to center we are
                float3 baseColor = lerp(_OuterColor.rgb, _InnerColor.rgb, innerBlend);
                baseColor       += chromaNoise * noiseStrength * _InnerColor.rgb * 2.5;

                float  rings     = EnergyRings(dist, time);
                float3 ringColor = _EnergyColor.rgb * rings * _RingIntensity;

                // arc sparks that spin around using angle-based noise
                float  angle     = atan2(centeredUV.y, centeredUV.x);
                float  arcNoise  = FBM(float2(angle * 2.0 + time * 3.0, normalizedDist * 4.0));
                float  arcFactor = pow(saturate(arcNoise - 0.4), 2.0) * (normalizedDist * (1.0 - normalizedDist) * 4.0);
                float3 arcColor  = _EnergyColor.rgb * arcFactor * 2.5;

                // makes the portal look like its breathing
                float pulse = 0.85 + 0.15 * sin(time * 2.3 + normalizedDist * 6.28);

                // combine everything together
                float3 finalColor  = baseColor * coreGlow * pulse * distortedMask;
                finalColor        += ringColor * distortedMask;
                finalColor        += arcColor  * distortedMask;
                finalColor        += _RimColor.rgb * rimFactor * _RimColor.a * 4.0;

                // soft glow just outside the circle edge
                float haloFade = saturate(1.0 - (dist - _PortalRadius) / 0.07);
                float halo     = exp(-max(0.0, dist - _PortalRadius) * 28.0) * 0.6 * haloFade;
                finalColor    += lerp(_RimColor.rgb, _OuterColor.rgb, 0.5) * halo;

                float alpha = saturate(portalMask + rimFactor + halo) * _Alpha;

                // for additive blending, fog should go to black not white
                finalColor = MixFogColor(finalColor, float3(0, 0, 0), IN.fogFactor);

                return float4(finalColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
