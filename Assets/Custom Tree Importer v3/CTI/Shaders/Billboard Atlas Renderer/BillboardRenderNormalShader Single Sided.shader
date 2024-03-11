Shader "Custom/BillboardRenderNormalShader Single Sided" {
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_Cutoff ("CutOff", Range(0,1)) = 0.3
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		
		//	Leaves
		[NoScaleOffset] _BumpSpecMap("Normalmap (GA) Specular (B)", 2D) = "bump" {} // Shadow Offset (B)
		[NoScaleOffset] _TranslucencyMap("AO (G) Translucency (B) Smoothness (A)", 2D) = "white" {}
		//	Bark
		[NoScaleOffset] _BumpSpecAOMap("Normalmap (GA) Specular (R) AO (B)", 2D) = "bump" {}

		_IsBark ("is bark", Float) = 0.0
	}
	SubShader {

		Tags { "RenderType"="Opaque" }
		LOD 200
		Cull Off
		
		CGPROGRAM
		#pragma surface surf Lambert
		#pragma target 3.0

		sampler2D _MainTex;
		sampler2D _BumpSpecMap;
		sampler2D _TranslucencyMap;
		sampler2D _BumpSpecAOMap;
		float _Cutoff;
		float _IsBark;

		struct Input {
			float2 uv_MainTex;
			float FacingSign : FACE;
			float3 worldNormal;
			INTERNAL_DATA
		};

		void vert (inout appdata_full v) {
			if (_IsBark)
				v.vertex.xyz += v.normal * 2;
		}


		void surf (Input IN, inout SurfaceOutput o) {
			fixed4 c = tex2D (_MainTex, IN.uv_MainTex);
			float3 worldNormal;
			half smoothness;
			// leaves
			if(_IsBark == 0) {
				clip (c.a - _Cutoff);
				half4 norspc = tex2D (_BumpSpecMap, IN.uv_MainTex);
				o.Normal = UnpackNormalDXT5nm(norspc);
				worldNormal = WorldNormalVector (IN, o.Normal * IN.FacingSign);
				smoothness = tex2D(_TranslucencyMap, IN.uv_MainTex).a;
			}
			// bark
			else {
				o.Normal = UnpackNormal(tex2D(_BumpSpecAOMap, IN.uv_MainTex));
				worldNormal = WorldNormalVector(IN, o.Normal);
				smoothness = c.a;
			}
			worldNormal = normalize(worldNormal);
			//	green is up;
			//	o.Albedo = float3( -worldNormal.x, worldNormal.y, worldNormal.z) * 0.5 + 0.5;
			o.Albedo = float3 (float2(-worldNormal.x, worldNormal.y) * 0.5 + 0.5, smoothness);
			o.Alpha = 1;
		}
		ENDCG
	} 
	FallBack "Diffuse"
}
