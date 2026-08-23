Shader "URP/Custom/HalfLambert_Outline_SmoothNormal_Noise"
{
    Properties
    {
        _BaseMap ("Main Texture", 2D) = "white" {}
        _MainTex ("Legacy Main Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _Color ("Legacy Base Color", Color) = (1,1,1,1)
        [HDR] _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width", Range(0.0, 0.5)) = 0.02
        [Toggle] _UseBakedSmoothNormal ("Use Baked Smooth Normal (UV2)", Float) = 1
        _LambertStrength ("Half Lambert Strength", Range(0, 2)) = 1
        _AmbientStrength ("Ambient Strength", Range(0, 1)) = 0.2
        [Header(Outline Noise)]
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _NoiseScale ("Noise Scale", Range(0.1, 20)) = 4
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.2
        _NoiseScroll ("Noise Scroll (XY)", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }

        Pass
        {
            Name "Forward"
            Cull Back
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _BaseMap;
            float4 _BaseMap_ST;
            fixed4 _BaseColor;
            half _LambertStrength;
            half _AmbientStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.normalWS = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 albedo = tex2D(_BaseMap, i.uv) * _BaseColor;
                half3 normalWS = normalize(i.normalWS);
                half3 lightDirection = normalize(half3(0.35h, 0.8h, 0.45h));
                half halfLambert = dot(normalWS, lightDirection) * 0.5h + 0.5h;
                half lighting = lerp(_AmbientStrength, 1.0h, saturate(halfLambert * _LambertStrength));
                return fixed4(albedo.rgb * lighting, albedo.a);
            }
            ENDCG
        }

        Pass
        {
            Name "Outline"
            Cull Front
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vertOutline
            #pragma fragment fragOutline
            #include "UnityCG.cginc"

            sampler2D _NoiseTex;
            fixed4 _OutlineColor;
            half _OutlineWidth;
            half _UseBakedSmoothNormal;
            half _NoiseScale;
            half _NoiseStrength;
            float4 _NoiseScroll;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 uv2 : TEXCOORD1;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vertOutline(appdata v)
            {
                v2f o;
                float3 outlineNormal = normalize(v.normal);
                if (_UseBakedSmoothNormal > 0.5h && dot(v.uv2.xyz, v.uv2.xyz) > 0.0001f)
                    outlineNormal = normalize(v.uv2.xyz);

                float3 positionWS = mul(unity_ObjectToWorld, v.vertex).xyz;
                float2 noiseUV = positionWS.xz * _NoiseScale + _NoiseScroll.xy * _Time.y;
                half noise = tex2Dlod(_NoiseTex, float4(noiseUV, 0, 0)).r;
                half finalWidth = max(0.0h, _OutlineWidth * (1.0h + (noise * 2.0h - 1.0h) * _NoiseStrength));
                float4 expandedVertex = v.vertex;
                expandedVertex.xyz += outlineNormal * finalWidth;
                o.pos = UnityObjectToClipPos(expandedVertex);
                return o;
            }

            fixed4 fragOutline(v2f i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }
    }

    FallBack "Unlit/Texture"
}
