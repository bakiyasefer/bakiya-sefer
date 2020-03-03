// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "Custom/Spherical_World_Multiply"
{
	Properties
	{
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_QOffset ("Offset", Vector) = (0,0,0,0)
		_Sphere("Sphere", Vector) = (0,0,0,0)
	}
	
	SubShader
	{
		Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
		Blend Zero SrcColor
		AlphaTest Greater .01
		ColorMask RGB
		Cull Off Lighting Off ZWrite Off Fog { Color (0,0,0,0) }

		Pass
		{			
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
				o.uv = v.uv;
				o.pos = mul( unity_ObjectToWorld, v.pos );
				//vv.xz -= _WorldSpaceCameraPos.xz;
				float zOff = o.pos.z * 0.01;
				float xOff = o.pos.y * 0.1;
				float yOff = o.pos.x * 0.1;
				o.pos += _QOffset*zOff*zOff;
				o.pos.x -= _Sphere.x*xOff*yOff;
				o.pos.y += _Sphere.y*yOff*yOff;
				o.pos = mul (UNITY_MATRIX_VP, o.pos);
				return o;
			}
			half4 frag (v2f i) : COLOR
			{
			  	half4 prev = i.color * tex2D(_MainTex, i.uv);
				return lerp(half4(1,1,1,1), prev, prev.a);
			}
			ENDCG
		}
	}
	
	FallBack "Diffuse"
}
