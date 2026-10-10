Shader "VHVRVignette"
{
    Properties
    {
        _Color ("Color", Color) = (0,0,0,1)
        // Angles from the eye's forward axis, in radians: fully clear inside _Inner, fully covered outside _Outer.
        _Inner ("Inner Angle", Float) = 0.6
        _Outer ("Outer Angle", Float) = 0.8
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Inner;
            float _Outer;

            struct appdata { float4 vertex : POSITION; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 viewPos : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // The view matrix is the rendering eye's, so the vignette is centered on each eye's own axis
                // whatever the shape and the position of the mesh it is drawn on.
                o.viewPos = UnityObjectToViewPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 direction = normalize(i.viewPos);
                float angle = acos(clamp(-direction.z, -1, 1));
                fixed4 color = _Color;
                color.a *= smoothstep(_Inner, _Outer, angle);
                return color;
            }
            ENDCG
        }
    }
}
