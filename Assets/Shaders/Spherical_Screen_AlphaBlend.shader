Shader "Custom/Spherical_Screen_AlphaBlend" {
Properties {
	_MainTex ("Particle Texture", 2D) = "white" {}
	_QOffset ("Offset", Vector) = (0,0,0,0)
	_Sphere("Sphere", Vector) = (0,0,0,0)
}

Category {
	Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
	Blend SrcAlpha OneMinusSrcAlpha
	AlphaTest Greater .01
	ColorMask RGB
	Cull Off Lighting Off ZWrite Off

	SubShader {
		Pass {
		
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_particles
			
			#include "UnityCG.cginc"

			sampler2D _MainTex;
			float4 _QOffset;
			float4 _Sphere;
			
			struct appdata_t {
				float4 pos : POSITION;
				fixed4 color : COLOR;
				float2 uv : TEXCOORD0;
			};

			struct v2f {
				float4 pos : SV_POSITION;
				fixed4 color : COLOR;
				float2 uv : TEXCOORD0;
			};

			v2f vert (appdata_t v)
			{
				v2f o;
				o.pos = mul (UNITY_MATRIX_MV, v.pos);
				float zOff = o.pos.z * 0.01;
				float xOff = o.pos.y * 0.1;
				float yOff = o.pos.x * 0.1;
				o.pos += _QOffset*zOff*zOff;
				o.pos.x -= _Sphere.x*xOff*yOff;
				o.pos.y += _Sphere.y*yOff*yOff;
				o.pos = mul (UNITY_MATRIX_P, o.pos);
				o.color = v.color;
				o.uv = v.uv;
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				return i.color * tex2D(_MainTex, i.uv);
			}
			ENDCG 
		}
	}	
}
}
