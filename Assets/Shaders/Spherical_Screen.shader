Shader "Custom/Spherical_Screen"
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
		Tags {"Queue"="Geometry" "IgnoreProjector"="True" "RenderType"="Opaque"}

		Lighting Off
		Cull Back

		Pass
		{			
	        //Blend SrcAlpha OneMinusSrcAlpha 
			CGPROGRAM
			// Upgrade NOTE: excluded shader from DX11 and Xbox360; has structs without semantics (struct v2f members fog)
			//#pragma exclude_renderers d3d11 xbox360
			#pragma vertex vert
			#pragma fragment frag
			#pragma fragmentoption ARB_precision_hint_fastest
			#include "UnityCG.cginc"	

            sampler2D _MainTex;
			float4 _QOffset;
			float4 _Sphere;
			float4 _FogColor;
			float _FogStart;
			float _FogEnd;

			struct appdata_t {
				float4 vertex : POSITION;
				fixed4 color : COLOR;
				float2 texcoord : TEXCOORD0;
			};
			
			struct v2f {
			    float4 pos : SV_POSITION;
			    float2 uv : TEXCOORD0;
			    float fog : COLOR;
			};

			v2f vert (appdata_t v)
			{
				v2f o;
				float4 vPos = mul (UNITY_MATRIX_MV, v.vertex);
				float fogz = vPos.z;
				float zOff = vPos.z * 0.01;
				float xOff = vPos.y * 0.1;
				float yOff = vPos.x * 0.1;
				vPos += _QOffset*zOff*zOff;
				vPos.x -= _Sphere.x*xOff*yOff;
				vPos.y += _Sphere.y*yOff*yOff;
				o.pos = mul (UNITY_MATRIX_P, vPos);
				o.uv = v.texcoord;
				o.fog = saturate((fogz + _FogStart) / (_FogStart - _FogEnd));
				o.fog *= o.fog;
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
