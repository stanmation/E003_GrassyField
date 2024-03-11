Shader "Custom/BillboardRenderAlbedoShader Single Sided" {
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_Cutoff ("CutOff", Range(0,1)) = 0.3
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		
	//	Leaves
		[NoScaleOffset] _BumpSpecMap("Normalmap (GA) Specular (B)", 2D) = "bump" {} // Shadow Offset (B)
		[NoScaleOffset] _TranslucencyMap("AO (G) Translucency (B) Smoothness (A)", 2D) = "white" {}
	//	Bark
		_MainTexArray ("Albedo Array (RGB) Smoothness (A)", 2DArray) = "white" {}
		[NoScaleOffset] _BumpSpecAOMapArray ("Normalmap (GA) Specular (R) AO (B)", 2DArray) = "bump" {}
		[NoScaleOffset] _BumpSpecAOMap("Normalmap (GA) Specular (R) AO (B)", 2D) = "bump" {}
	//	Bark Secondary Maps
		[NoScaleOffset] _DetailAlbedoMap("Detail Albedo x2 (RGB) Smoothness (A)", 2D) = "gray" {}
		[NoScaleOffset] _DetailNormalMapX("Normal Map (GA) Specular (R) AO (B)", 2D) = "gray" {}
		_DetailNormalMapScale("Normal Strength", Float) = 1.0


		_IsBark ("is bark", Float) = 0.0
		_UseDetails ("use details", Float) = 0.0
		_UseArrays ("use arrays", Float) = 0.0
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200
		Cull Off
		
		CGPROGRAM
		#pragma surface surf Lambert keepalpha vertex:vert
		#pragma target 3.0

		sampler2D _MainTex;
		sampler2D _BumpSpecMap;
		sampler2D _TranslucencyMap;
		sampler2D _BumpSpecAOMap;

		sampler2D _DetailAlbedoMap;
		UNITY_DECLARE_TEX2DARRAY(_MainTexArray);
		UNITY_DECLARE_TEX2DARRAY(_BumpSpecAOMapArray);

		float _Cutoff;
		float _IsBark;
		float _UseDetails;
		float _UseArrays;

		struct Input {
			float2 uv_MainTex;
			float2 uv_MainTexArray;
			float2 ctiuv2_DetailAlbedoMap;
			fixed4 color : COLOR;
			float FacingSign : FACE;
		};

		half _Glossiness;
		half _Metallic;
		fixed4 _Color;

		void vert(inout appdata_full v, out Input o) {
			UNITY_INITIALIZE_OUTPUT(Input, o);
			o.ctiuv2_DetailAlbedoMap = v.texcoord1.zw;
		}

		void surf (Input IN, inout SurfaceOutput o) {
			fixed4 c = tex2D (_MainTex, IN.uv_MainTex);
			
			float3 uvs = float3(IN.uv_MainTexArray, saturate(IN.color.b - 0.999) * 1000 );
			fixed4 array = UNITY_SAMPLE_TEX2DARRAY(_MainTexArray, uvs );

			if (_UseArrays == 0) {
				o.Albedo = c.rgb;
			}
			else {
				o.Albedo = array.rgb;
			}
		
		//	leaves
			if(_IsBark == 0) {
				clip (c.a - _Cutoff);
			}

		// 	bark / check for uv2 textures
			else if (_UseDetails > 0) {
				fixed4 detailAlbedo = tex2D(_DetailAlbedoMap, IN.ctiuv2_DetailAlbedoMap);
				o.Albedo *= detailAlbedo.rgb * unity_ColorSpaceDouble.rgb;
			}
			
			o.Gloss = 0;
			o.Specular = 0;
			o.Alpha = IN.color.a * 0.5 + 0.5; //1;
		}
		ENDCG
	} 
	FallBack "Diffuse"
}
