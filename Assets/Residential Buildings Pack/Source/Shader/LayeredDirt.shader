// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "GabroMedia/LayeredDirt"
{
	Properties
	{
		[SingleLineTexture]_Basecolor("Basecolor", 2D) = "white" {}
		[Normal][SingleLineTexture]_Normal("Normal", 2D) = "white" {}
		[SingleLineTexture]_MetallicSmoothness("MetallicSmoothness", 2D) = "white" {}
		[SingleLineTexture]_AO("AO", 2D) = "white" {}
		[SingleLineTexture]_Dirt_Basecolor("Dirt_Basecolor", 2D) = "white" {}
		[Normal][SingleLineTexture]_Dirt_Normal("Dirt_Normal", 2D) = "white" {}
		[SingleLineTexture]_Dirt_MetallicSmoothness("Dirt_MetallicSmoothness", 2D) = "white" {}
		[SingleLineTexture]_Dirt_AO("Dirt_AO", 2D) = "white" {}
		[Toggle(_ENABLE_DIRT_ON)] _Enable_Dirt("Enable_Dirt", Float) = 0
		[Toggle]_MaskSwitch("MaskSwitch", Float) = 0
		[SingleLineTexture]_DirtMasksRG("DirtMasks(RG)", 2D) = "white" {}
		_Dirt_Tiling("Dirt_Tiling", Float) = 1
		_Dirt_Power("Dirt_Power", Float) = 0
		_Dirt_Strength("Dirt_Strength", Float) = 0
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Opaque"  "Queue" = "Geometry+0" }
		Cull Back
		CGPROGRAM
		#include "UnityShaderVariables.cginc"
		#pragma target 3.0
		#pragma shader_feature_local _ENABLE_DIRT_ON
		#pragma surface surf Standard keepalpha addshadow fullforwardshadows 
		struct Input
		{
			float2 uv_texcoord;
			float3 worldPos;
		};

		uniform sampler2D _Dirt_Normal;
		uniform float _Dirt_Tiling;
		uniform sampler2D _Normal;
		uniform float4 _Normal_ST;
		uniform float _MaskSwitch;
		uniform sampler2D _DirtMasksRG;
		uniform float4 _DirtMasksRG_ST;
		uniform float _Dirt_Power;
		uniform sampler2D _AO;
		uniform float4 _AO_ST;
		uniform float _Dirt_Strength;
		uniform sampler2D _Dirt_Basecolor;
		uniform sampler2D _Basecolor;
		uniform float4 _Basecolor_ST;
		uniform sampler2D _Dirt_MetallicSmoothness;
		uniform sampler2D _MetallicSmoothness;
		uniform float4 _MetallicSmoothness_ST;
		uniform sampler2D _Dirt_AO;

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float2 temp_output_49_0 = ( i.uv_texcoord * _Dirt_Tiling );
			float2 uv_Normal = i.uv_texcoord * _Normal_ST.xy + _Normal_ST.zw;
			float2 uv_DirtMasksRG = i.uv_texcoord * _DirtMasksRG_ST.xy + _DirtMasksRG_ST.zw;
			float4 tex2DNode31 = tex2D( _DirtMasksRG, uv_DirtMasksRG );
			float2 uv_AO = i.uv_texcoord * _AO_ST.xy + _AO_ST.zw;
			float4 tex2DNode22 = tex2D( _AO, uv_AO );
			float temp_output_75_0 = pow( tex2DNode22.r , 2.0 );
			float clampResult76 = clamp( ( 1.0 - pow( ( pow( tex2DNode31.r , _Dirt_Power ) + ( 1.0 - temp_output_75_0 ) ) , _Dirt_Strength ) ) , 0.0 , 1.0 );
			float3 ase_vertex3Pos = mul( unity_WorldToObject, float4( i.worldPos , 1 ) );
			float clampResult62 = clamp( ( pow( ase_vertex3Pos.y , 1.0 ) * 0.4 ) , 0.0 , 1.0 );
			float clampResult83 = clamp( pow( ( ( pow( tex2DNode31.g , _Dirt_Power ) * clampResult62 ) * temp_output_75_0 ) , _Dirt_Strength ) , 0.0 , 1.0 );
			#ifdef _ENABLE_DIRT_ON
				float staticSwitch85 = (( _MaskSwitch )?( clampResult83 ):( clampResult76 ));
			#else
				float staticSwitch85 = 1.0;
			#endif
			float3 lerpResult36 = lerp( UnpackNormal( tex2D( _Dirt_Normal, temp_output_49_0 ) ) , UnpackNormal( tex2D( _Normal, uv_Normal ) ) , staticSwitch85);
			o.Normal = lerpResult36;
			float2 uv_Basecolor = i.uv_texcoord * _Basecolor_ST.xy + _Basecolor_ST.zw;
			float4 lerpResult29 = lerp( tex2D( _Dirt_Basecolor, temp_output_49_0 ) , tex2D( _Basecolor, uv_Basecolor ) , staticSwitch85);
			o.Albedo = lerpResult29.rgb;
			float4 tex2DNode39 = tex2D( _Dirt_MetallicSmoothness, temp_output_49_0 );
			float2 uv_MetallicSmoothness = i.uv_texcoord * _MetallicSmoothness_ST.xy + _MetallicSmoothness_ST.zw;
			float4 tex2DNode3 = tex2D( _MetallicSmoothness, uv_MetallicSmoothness );
			float lerpResult37 = lerp( tex2DNode39.r , tex2DNode3.r , staticSwitch85);
			o.Metallic = lerpResult37;
			float lerpResult38 = lerp( tex2DNode39.a , tex2DNode3.a , staticSwitch85);
			o.Smoothness = lerpResult38;
			float lerpResult43 = lerp( tex2D( _Dirt_AO, temp_output_49_0 ).r , tex2DNode22.r , staticSwitch85);
			o.Occlusion = lerpResult43;
			o.Alpha = 1;
		}

		ENDCG
	}
	Fallback "Diffuse"
	CustomEditor "ASEMaterialInspector"
}
/*ASEBEGIN
Version=18000
1934;14;1906;1005;3855.609;2151.605;2.505647;True;True
Node;AmplifyShaderEditor.PosVertexDataNode;64;-4607.353,-847.9797;Inherit;False;0;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.PowerNode;67;-4350.888,-844.246;Inherit;True;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;22;-2204.707,992.7285;Inherit;True;Property;_AO;AO;3;1;[SingleLineTexture];Create;True;0;0;False;0;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;69;-4285.895,-620.6277;Inherit;False;Constant;_Float0;Float 0;11;0;Create;True;0;0;False;0;0.4;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;78;-3276.135,-1571.02;Inherit;False;Property;_Dirt_Power;Dirt_Power;12;0;Create;True;0;0;False;0;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;68;-4085.895,-746.6277;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.PowerNode;75;-1960.854,1206.387;Inherit;False;False;2;0;FLOAT;0;False;1;FLOAT;2;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;31;-3993.476,-1259.839;Inherit;True;Property;_DirtMasksRG;DirtMasks(RG);10;1;[SingleLineTexture];Create;True;0;0;False;0;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.PowerNode;77;-3080.135,-1427.02;Inherit;False;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.PowerNode;84;-3519.686,-1080.215;Inherit;False;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;62;-3860.888,-748.246;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;57;-1716.697,1061.908;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;56;-2850.496,-1195.41;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;70;-3588.173,-756.4965;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;79;-2690.667,-1483.853;Inherit;False;Property;_Dirt_Strength;Dirt_Strength;13;0;Create;True;0;0;False;0;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;74;-3153.596,-766.7537;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.PowerNode;80;-2513.666,-1342.853;Inherit;False;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.PowerNode;81;-2624.129,-909.9313;Inherit;False;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;58;-2353.639,-1099.918;Inherit;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;83;-2284.677,-769.6832;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;76;-2108.422,-1178.371;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;48;-1756.235,-1978.581;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;50;-1748.235,-1702.581;Inherit;False;Property;_Dirt_Tiling;Dirt_Tiling;11;0;Create;True;0;0;False;0;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;86;-1643.708,-1330.13;Inherit;False;Constant;_Float1;Float 1;14;0;Create;True;0;0;False;0;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ToggleSwitchNode;33;-1659.239,-967.7125;Inherit;False;Property;_MaskSwitch;MaskSwitch;9;0;Create;True;0;0;False;0;0;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;49;-1358.235,-1832.581;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SamplerNode;3;-2208.991,764.6897;Inherit;True;Property;_MetallicSmoothness;MetallicSmoothness;2;1;[SingleLineTexture];Create;True;0;0;False;0;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;1;-2212.99,330.6899;Inherit;True;Property;_Basecolor;Basecolor;0;1;[SingleLineTexture];Create;True;0;0;False;0;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;39;-816.2204,-1615.108;Inherit;True;Property;_Dirt_MetallicSmoothness;Dirt_MetallicSmoothness;6;1;[SingleLineTexture];Create;True;0;0;False;0;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.StaticSwitch;85;-1201.809,-970.9434;Inherit;False;Property;_Enable_Dirt;Enable_Dirt;8;0;Create;True;0;0;False;0;0;0;0;True;;Toggle;2;Key0;Key1;Create;True;9;1;FLOAT;0;False;0;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT;0;False;7;FLOAT;0;False;8;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;35;-804.7689,-1833.214;Inherit;True;Property;_Dirt_Normal;Dirt_Normal;5;2;[Normal];[SingleLineTexture];Create;True;0;0;False;0;-1;None;None;True;0;False;white;Auto;True;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;42;-843.584,-1361.048;Inherit;True;Property;_Dirt_AO;Dirt_AO;7;1;[SingleLineTexture];Create;True;0;0;False;0;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;2;-2224.99,548.6898;Inherit;True;Property;_Normal;Normal;1;2;[Normal];[SingleLineTexture];Create;True;0;0;False;0;-1;None;None;True;0;False;white;Auto;True;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;34;-797.8372,-2041.964;Inherit;True;Property;_Dirt_Basecolor;Dirt_Basecolor;4;1;[SingleLineTexture];Create;True;0;0;False;0;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.LerpOp;37;-151.4607,-234.1831;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;29;-205.0341,-647.8326;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.LerpOp;43;-161.7769,94.66333;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;38;-157.3248,-94.29848;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;36;-147.702,-376.3977;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;484.75,-277.7143;Float;False;True;-1;2;ASEMaterialInspector;0;0;Standard;GabroMedia/LayeredDirt;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;Back;0;False;-1;0;False;-1;False;0;False;-1;0;False;-1;False;0;Opaque;0.5;True;True;0;False;Opaque;;Geometry;All;14;all;True;True;True;True;0;False;-1;False;0;False;-1;255;False;-1;255;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;False;2;15;10;25;False;0.5;True;0;0;False;-1;0;False;-1;0;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;-1;-1;0;False;-1;0;0;0;False;0.1;False;-1;0;False;-1;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;67;0;64;2
WireConnection;68;0;67;0
WireConnection;68;1;69;0
WireConnection;75;0;22;1
WireConnection;77;0;31;1
WireConnection;77;1;78;0
WireConnection;84;0;31;2
WireConnection;84;1;78;0
WireConnection;62;0;68;0
WireConnection;57;0;75;0
WireConnection;56;0;77;0
WireConnection;56;1;57;0
WireConnection;70;0;84;0
WireConnection;70;1;62;0
WireConnection;74;0;70;0
WireConnection;74;1;75;0
WireConnection;80;0;56;0
WireConnection;80;1;79;0
WireConnection;81;0;74;0
WireConnection;81;1;79;0
WireConnection;58;0;80;0
WireConnection;83;0;81;0
WireConnection;76;0;58;0
WireConnection;33;0;76;0
WireConnection;33;1;83;0
WireConnection;49;0;48;0
WireConnection;49;1;50;0
WireConnection;39;1;49;0
WireConnection;85;1;86;0
WireConnection;85;0;33;0
WireConnection;35;1;49;0
WireConnection;42;1;49;0
WireConnection;34;1;49;0
WireConnection;37;0;39;1
WireConnection;37;1;3;1
WireConnection;37;2;85;0
WireConnection;29;0;34;0
WireConnection;29;1;1;0
WireConnection;29;2;85;0
WireConnection;43;0;42;1
WireConnection;43;1;22;1
WireConnection;43;2;85;0
WireConnection;38;0;39;4
WireConnection;38;1;3;4
WireConnection;38;2;85;0
WireConnection;36;0;35;0
WireConnection;36;1;2;0
WireConnection;36;2;85;0
WireConnection;0;0;29;0
WireConnection;0;1;36;0
WireConnection;0;3;37;0
WireConnection;0;4;38;0
WireConnection;0;5;43;0
ASEEND*/
//CHKSM=801908284E5FE185432BAA33D0063FE04FB22229