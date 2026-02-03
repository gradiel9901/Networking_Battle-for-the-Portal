Shader "Custom/StylizedIce"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Main Color", Color) = (1,1,1,1)
        _SparkleSpeed ("Sparkle Speed", Float) = 1.5
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Blend SrcAlpha OneMinusSrcAlpha // Normal Alpha Blending for Ice/Mist
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
            float _SparkleSpeed;

            struct appdata_t {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            float4 _MainTex_ST;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.texcoord);
                
                // Sparkle Effect: Time-based glint based on world position
                float sparkle = sin(_Time.y * _SparkleSpeed + i.worldPos.x * 10.0 + i.worldPos.y * 10.0);
                // Map -1..1 to 0.8..1.2
                sparkle = 0.8 + (sparkle * 0.5 + 0.5) * 0.4;
                
                col.rgb *= sparkle;
                col *= i.color;
                
                return col;
            }
            ENDCG
        }
    }
}
