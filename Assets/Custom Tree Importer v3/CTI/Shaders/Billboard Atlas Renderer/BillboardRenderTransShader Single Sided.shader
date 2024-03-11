Shader "Custom/BillboardRenderTransShader Single Sided" {
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_Cutoff ("CutOff", Range(0,1)) = 0.3
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		// bark has no _BumpTransSpecMap -> we set it to black
		_TranslucencyMap ("Normal (GA) Trans(R) Smoothness(B)", 2D) = "black" {}
		_TranslucencyStrength ("Translucency Strength", Range(0,1)) = 0.5
		

		_Glossiness ("Smoothness", Range(0,1)) = 0.5
		_Metallic ("Metallic", Range(0,1)) = 0.0
		
		_IsBark ("is bark", Float) = 0.0
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200
		Cull Off
		
		CGPROGRAM

		// Using decal:add means that we render translucency as some kind of invers depth map:
		// Translucency will be darker the fewer triangles overlap - so the billboard shader has to invers the result.
		// The more triangles overlap however will probably cause self shadowing
		// So this means this solution will look ok if mesh trees will receive real time shadows as soon as the pop in
		// See: all the 1-trans

		#pragma surface surf Lambert nometa decal:add vertex:vert
		//alpha:blend
		#pragma target 3.0

		sampler2D _MainTex;
		sampler2D _TranslucencyMap;
		float _TranslucencyStrength;
		//dx9 fix
		float _Cutoff; // = 0.0;

		struct Input {
			float2 uv_MainTex;
		};

		fixed _IsBark;

		void vert (inout appdata_full v) {
			if (_IsBark)
				v.vertex.xyz += v.normal * 0.5;
		}

		void surf(Input IN, inout SurfaceOutput o) {
			fixed4 c = tex2D(_MainTex, IN.uv_MainTex);
			half4 norspc = half4(0, 0, 0, 0);

			o.Albedo = half3(1, 1, 1); // as it gets reverted in the script, should be 0 otherwise
			half4 trans = 1;

			if (_IsBark == 0.0) {
				clip (c.a - _Cutoff);
			//	Translucency is stored in b	
				trans = tex2D (_TranslucencyMap, IN.uv_MainTex);
			//	Invers translucency so we can use alpha blending
				o.Albedo = (1.0 - trans.b);
			}
			if (_IsBark == 0.0) {
				o.Alpha = (1.0 - trans.b) * 0.1;
			}
			else {
				o.Alpha = 0; // as it gets reverted in the script, should be 1 otherwise
			}
		}
		ENDCG
	} 
	FallBack "Diffuse"
}
