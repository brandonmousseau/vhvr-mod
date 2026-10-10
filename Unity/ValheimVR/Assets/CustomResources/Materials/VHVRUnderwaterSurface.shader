// The underside of the water surface as seen from below, see UnderwaterEffectsUpdater.
// It is transparent, to leave a clear window overhead, so the fog that the game applies as a post process to
// opaque geometry never reaches it. It fades into the fog color by distance itself instead, and is unlit: the
// color it is given is expected to follow the fog color already.
Shader "VHVRUnderwaterSurface"
{
    Properties
    {
        _MainTex ("Window (RGB tint, A opacity)", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _FogColor ("Fog Color", Color) = (0,0,0,1)
        _FogDensity ("Fog Density", Float) = 0.125
        _FogStart ("Fog Start", Float) = 0
        _FogEnd ("Fog End", Float) = 32
        // The value of UnityEngine.FogMode: 1 linear, 2 exponential, 3 exponential squared.
        _FogMode ("Fog Mode", Float) = 3
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _FogColor;
            float _FogDensity;
            float _FogStart;
            float _FogEnd;
            float _FogMode;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float distance : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                // The distance the fog post process works from: the depth in front of the near clip plane.
                o.distance = -UnityObjectToViewPos(v.vertex).z - _ProjectionParams.y;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, i.uv) * _Color;

                // How much of the surface's own color is left, the same way the fog post process computes it.
                float distance = max(i.distance, 0);
                float visibility;
                if (_FogMode < 1.5)
                {
                    visibility = (_FogEnd - distance) / (_FogEnd - _FogStart);
                }
                else if (_FogMode < 2.5)
                {
                    visibility = exp2(-_FogDensity * distance);
                }
                else
                {
                    float fog = _FogDensity * distance;
                    visibility = exp2(-fog * fog);
                }

                color.rgb = lerp(_FogColor.rgb, color.rgb, saturate(visibility));
                return color;
            }
            ENDCG
        }
    }
}
