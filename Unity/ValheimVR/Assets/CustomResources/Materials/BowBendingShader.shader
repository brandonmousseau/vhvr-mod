Shader "BowBendingShader"
{
    Properties
    {
        _HandleVector("Handle Vector", Vector) = (0, 0, 0, 1)
        _HandleTopHeight("Handle Top Height", Float) = 0
        _HandleBottomHeight("Handle Bottom Height", Float) = 0
        _StringRadius("String Radius", Float) = 0
        _MainTex ("Texture", 2D) = "white" {}
        _BumpMap ("Bumpmap", 2D) = "bump" {}
        _MetallicGlossMap ("Metallic Gloss Map", 2D) = "bump" {}
    	_EmissionMap ("Emission Map", 2D) = "black" {}
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags {"Queue"="AlphaTest" "IgnoreProjector"="True" "RenderType"="TransparentCutout"}

        CGPROGRAM
        #pragma surface surf Standard alphatest:_Cutoff vertex:vert

        #include "UnityCG.cginc"
        #include "UnityLightingCommon.cginc" // for _LightColor0

        float4 _HandleVector;
        float _HandleTopHeight;
        float _HandleBottomHeight;
        float _SoftLimbHeight;
        float4x4 _UpperLimbTransform;
        float4x4 _LowerLimbTransform;

        float4 _StringTop;
        float4 _StringTopToBottomDirection; // Must be normalized.
        float _StringLength;
        float _StringRadius;

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
            v.color.a = 1.0;
            if (_StringRadius > 0) {
                float4 stringTopToCurrentVertex = v.vertex - _StringTop;
                float4 projectionOnString = dot(stringTopToCurrentVertex, _StringTopToBottomDirection) * _StringTopToBottomDirection;
                float distanceToString = length((stringTopToCurrentVertex - projectionOnString).xyz);
                if (distanceToString < _StringRadius) {
                   // Hide vanilla string.
                   v.color.a = 0.0;
                }
            }

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
           o.Albedo = tex2D(_MainTex, IN.uv_MainTex).rgb;
           o.Alpha = IN.vertColor.a;
           o.Normal = UnpackNormal (tex2D (_BumpMap, IN.uv_BumpMap));
           o.Metallic = o.Smoothness = tex2D (_MetallicGlossMap, IN.uv_MetallicGlossMap).r;
           o.Emission = IN.vertColor.a > 0 ? tex2D(_EmissionMap, IN.uv_EmissionMap).rgb : fixed3(0, 0, 0);
        }

        ENDCG
    }
    Fallback "Diffuse"
}
