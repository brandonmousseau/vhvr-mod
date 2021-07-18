Shader "Custom/StencilMask" {
	Properties {
		_StencilMask("Stencil mask", Int) = 0
	}

	SubShader {
		Tags {
			"RenderType" = "Opaque"
			"Queue" = "Geometry-100"
		}

		ColorMask 0
		Cull Off
		ZTest Off
		ZWrite Off

		Stencil {
			Ref[_StencilMask]
			Comp always
			Pass replace
		}

		Pass {
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			struct appdata {
				float4 vertex : POSITION;
			};

			struct v2f {
				float4 pos : SV_POSITION;
			};

			v2f vert(appdata v) {
				v2f o;
				o.pos = float4(v.vertex.xy, 0.0, 0.5);
				return o;
			}

			half4 frag(v2f i) : COLOR {
				return half4(1, 1, 1, 1);
			}

			ENDCG
		}
	}
}