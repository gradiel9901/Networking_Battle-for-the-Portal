Shader "Custom/StylizedFire"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Main Color", Color) = (1,1,1,1)
        _DetailScale ("Noise Scale", Float) = 2.0
        _WobbleSpeed ("Wobble Speed", Float) = 2.0
        _WobbleAmount ("Wobble Amount", Float) = 0.1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Blend SrcAlpha One
        Cull Off 
        Lighting Off 
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_particles
            #pragma target 2.0

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            float _DetailScale;
            float _WobbleSpeed;
            float _WobbleAmount;
            
            struct appdata_t {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 noiseUv : TEXCOORD1;
            };

            float4 _MainTex_ST;

            v2f vert (appdata_t v)
            {
                v2f o;
                
                // Simple Vertex Wobble based on time and height (y) to simulate rising heat
                float wobble = sin(_Time.y * _WobbleSpeed + v.vertex.y * 5.0) * _WobbleAmount * v.texcoord.y; // Wobble more at top
                v.vertex.x += wobble;
                
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                
                // Pass UV for procedural noise
                o.noiseUv = v.vertex; 
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Sample soft texture
                fixed4 col = tex2D(_MainTex, i.texcoord);
                
                // Procedural Noise (Simple Sin/Cos approximation)
                float noise = sin(i.texcoord.x * 10.0 + _Time.y * 5.0) * cos(i.texcoord.y * 10.0 + _Time.x * 2.0);
                col.a *= smoothstep(0.0, 1.0, 1.0 - abs(noise * 0.2)); // Subtle breakup

                // Multiply by vertex color (particle color)
                col *= i.color;
                
                return col; // Additive blend (SrcAlpha One) means we return premultiplied-ish
            }
            ENDCG
        }
    }
}
