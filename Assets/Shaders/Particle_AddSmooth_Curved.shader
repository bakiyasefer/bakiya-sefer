Shader "Custom/ParticleAdditive(Soft)" {
Properties {
	_MainTex ("Particle Texture", 2D) = "white" {}
	_QOffset ("Offset", Vector) = (0,0,0,0)
}

Category {
	Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
	Blend One OneMinusSrcColor
	ColorMask RGB
	Cull Off Lighting Off ZWrite Off Fog { Color (0,0,0,0) }

	SubShader {
		Pass {
		
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_particles

			#include "UnityCG.cginc"

			sampler2D _MainTex;
			float4 _QOffset;
			
			struct appdata_t {
				float4 vertex : POSITION;
				fixed4 color : COLOR;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				fixed4 color : COLOR;
				float2 texcoord : TEXCOORD0;
				#ifdef SOFTPARTICLES_ON
				float4 projPos : TEXCOORD1;
				#endif
			};

			float4 _MainTex_ST;
			
			v2f vert (appdata_t v)
			{
				v2f o;
				float4 vPos = mul (UNITY_MATRIX_MV, v.vertex);
				float zOff = vPos.z * 0.01;
				vPos += _QOffset*zOff*zOff;
				o.vertex = mul (UNITY_MATRIX_P, vPos);
				o.color = v.color;
				o.texcoord = TRANSFORM_TEX(v.texcoord,_MainTex);
				return o;
			}

			sampler2D_float _CameraDepthTexture;
			float _InvFade;
			
			fixed4 frag (v2f i) : SV_Target
			{				
				half4 prev = i.color * tex2D(_MainTex, i.texcoord);
				prev.rgb *= prev.a;
				return prev;
			}
			ENDCG 
		}
	} 
}
}