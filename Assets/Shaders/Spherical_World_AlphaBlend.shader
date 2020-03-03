// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "Custom/Spherical_World_AlphaBlend"
{
	Properties
	{
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_QOffset ("Offset", Vector) = (0,0,0,0)
		_Sphere("Sphere", Vector) = (0,0,0,0)

		_FogColor ("Fog Color", Color) = (1.0, 1.0, 1.0, 1.0)
		_FogStart ("Fog Start", Float) = 0.0
		_FogEnd ("Fog End", Float) = 1000.0
	}
	
	SubShader
	{
		Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
		Blend SrcAlpha OneMinusSrcAlpha
		AlphaTest Greater .01
		ColorMask RGB
		Cull Back Lighting Off ZWrite Off Fog { Color (0,0,0,0) }

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
			float4 _FogColor;
			float _FogStart;
			float _FogEnd;
			
			struct appdata_t {
				float4 pos : POSITION;
				fixed4 color : COLOR;
				float2 uv : TEXCOORD0;
			};

			struct v2f {
				float4 pos : SV_POSITION;
				fixed4 color : COLOR;
				float2 uv : TEXCOORD0;				
			    float fog : COLOR1;
			};

			v2f vert (appdata_t v)
			{
				v2f o;
				o.uv = v.uv;
				o.color = v.color;
				o.pos = mul( unity_ObjectToWorld, v.pos );
				//vv.xz -= _WorldSpaceCameraPos.xz;
				float zOff = o.pos.z * 0.01;
				float xOff = o.pos.y * 0.1;
				float yOff = o.pos.x * 0.1;
				o.pos += _QOffset*zOff*zOff;
				o.pos.x -= _Sphere.x*xOff*yOff;
				o.pos.y += _Sphere.y*yOff*yOff;
				o.pos = mul (UNITY_MATRIX_VP, o.pos);				
				o.fog = saturate((o.pos.z + _FogStart) / (_FogStart - _FogEnd));
				return o;
			}
			half4 frag (v2f i) : COLOR
			{				
				float4 colour = tex2D(_MainTex, i.uv);
				colour.rgb = lerp(_FogColor.rgb, colour.rgb, 1 - i.fog);
				
				return colour;
			}
			ENDCG
		}
	}
	
	FallBack "Diffuse"
}
