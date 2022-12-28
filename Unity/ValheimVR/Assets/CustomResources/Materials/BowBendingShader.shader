Shader "BowBendingShader"
{
    Properties
    {
        _HandleVector("Handle Vector", Vector) = (0, 0, 0, 1)
        _HandleTopHeight("Handle Top Height", Float) = 0
        _HandleBottomHeight("_Handle Bottom Height", Float) = 0
        _MainTex ("Texture", 2D) = "white" {}
        _BumpMap ("Bumpmap", 2D) = "bump" {}
        _MetallicGlossMap ("Metallic Gloss Map", 2D) = "bump" {}
        _EmissionMap ("Emission Map", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        CGPROGRAM
        #pragma surface surf Standard vertex:vert

        #include "UnityCG.cginc"
        #include "UnityLightingCommon.cginc" // for _LightColor0

        float4 _HandleVector;
        float _HandleTopHeight;
        float _HandleBottomHeight;
        float _SoftLimbHeight;
        float4x4 _UpperLimbTransform;
        float4x4 _LowerLimbTransform;

        sampler2D _MainTex;
        sampler2D _BumpMap;
        sampler2D _MetallicGlossMap;
        sampler2D _EmissionMap;

        struct Input {
          float2 uv_MainTex;
          float2 uv_BumpMap;
          float2 uv_MetallicGlossMap;
          float2 uv_EmissionMap;
          float4 vertColor : COLOR;
        };

        void vert (inout appdata_full v)
        {
            float4 targetPos = v.vertex;
            float height = dot(v.vertex, _HandleVector);
            if (height  > _HandleTopHeight) {
                targetPos = mul(_UpperLimbTransform, v.vertex);
                if (height < _HandleTopHeight + _SoftLimbHeight) {
                    targetPos = lerp(v.vertex, targetPos , (height - _HandleTopHeight) / _SoftLimbHeight);
                }
            } else if (height < _HandleBottomHeight) {
                targetPos = mul(_LowerLimbTransform, v.vertex);
                if (height > _HandleBottomHeight - _SoftLimbHeight) {
                    targetPos  = lerp(v.vertex, targetPos , (_HandleBottomHeight - height) / _SoftLimbHeight);
                }
            }
            v.vertex = targetPos;
        }

        void surf (Input IN, inout SurfaceOutputStandard o) {
           fixed4 c = tex2D (_MainTex, IN.uv_MainTex);
           o.Albedo = c.rgb;
           o.Alpha = c.a;
           o.Normal = UnpackNormal (tex2D (_BumpMap, IN.uv_BumpMap));
           o.Metallic = tex2D (_MetallicGlossMap, IN.uv_MetallicGlossMap).r;
           o.Smoothness = 0.5;
           o.Emission = tex2D(_EmissionMap, IN.uv_EmissionMap).rgb;
        }

        ENDCG
    }
    Fallback "Diffuse"
}
