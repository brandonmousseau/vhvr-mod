Shader "BowBendingShader"
{
    Properties
    {
        _HandleVector("Handle Vector", Vector) = (0, 0, 0, 1)
        _HandleTopHeight("Handle Top Height", Float) = 0
        _HandleBottomHeight("_Handle Bottom Height", Float) = 0
        _MainTex ("Texture", 2D) = "white" {}
        _BumpMap ("Bumpmap", 2D) = "bump" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        CGPROGRAM
        #pragma surface surf Lambert vertex:vert

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

        struct Input {
          float2 uv_MainTex;
          float2 uv_BumpMap;
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

        void surf (Input IN, inout SurfaceOutput o) {
           o.Albedo = tex2D (_MainTex, IN.uv_MainTex).rgb;
           o.Normal = UnpackNormal (tex2D (_BumpMap, IN.uv_BumpMap));
        }

        ENDCG
    }
    Fallback "Diffuse"
}
