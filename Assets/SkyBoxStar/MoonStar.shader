// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Skybox/TAPro/MoonQueueStar"
{
	Properties
	{
		_Noise1PowScale("Noise1PowScale", Vector) = (1,1,0,0)
		_Noise1Scale("Noise1Scale", Float) = 0
		_Noise2PowScale("Noise2PowScale", Vector) = (1,1,0,0)
		_Noise2Scale("Noise2Scale", Float) = 0
		_Noise2RotateSpeed("Noise2RotateSpeed", Float) = 1
		_Noise1RotateSpeed("Noise1RotateSpeed", Float) = 1
		_NoiseMaskPowScale("NoiseMaskPowScale", Vector) = (1,1,0,0)
		_NoiseMaskScale("NoiseMaskScale", Float) = 0
		_NoiseMaskRotateSpeed("NoiseMaskRotateSpeed", Float) = 1
		_GradientColorIntensity("GradientColorIntensity", Range( 0 , 1)) = 0
		_Gradient("Gradient", 2D) = "white" {}
		_PosMin("PosMin", Range( 0 , 1)) = 0
		_PosMax("PosMax", Range( 0 , 1)) = 0
		_BGTint1("BGTint1", Color) = (0,0,0,0)
		_BGTint2("BGTint2", Color) = (0.6132076,0.6132076,0.6132076,0)

	}
	
	SubShader
	{
		
		
		Tags { "RenderType"="Opaque" }
	LOD 100

		CGINCLUDE
		#pragma target 3.0
		ENDCG
		Blend Off
		AlphaToMask Off
		Cull Back
		ColorMask RGBA
		ZWrite On
		ZTest LEqual
		Offset 0 , 0
		
		
		
		Pass
		{
			Name "Unlit"
			Tags { "LightMode"="ForwardBase" }
			CGPROGRAM

			

			#ifndef UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX
			//only defining to not throw compilation error over Unity 5.5
			#define UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input)
			#endif
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			#include "UnityCG.cginc"
			#include "UnityShaderVariables.cginc"
			#define ASE_NEEDS_FRAG_POSITION


			struct MeshData
			{
				float4 vertex : POSITION;
				float4 color : COLOR;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};
			
			struct V2FData
			{
				float4 vertex : SV_POSITION;
				#ifdef ASE_NEEDS_FRAG_WORLD_POSITION
				float3 worldPos : TEXCOORD0;
				#endif
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			uniform float _Noise1RotateSpeed;
			uniform float _Noise1Scale;
			uniform float2 _Noise1PowScale;
			uniform float _Noise2RotateSpeed;
			uniform float _Noise2Scale;
			uniform float2 _Noise2PowScale;
			uniform float _NoiseMaskRotateSpeed;
			uniform float _NoiseMaskScale;
			uniform float2 _NoiseMaskPowScale;
			uniform sampler2D _Gradient;
			uniform float _GradientColorIntensity;
			uniform float4 _BGTint1;
			uniform float4 _BGTint2;
			uniform float _PosMin;
			uniform float _PosMax;
			float3 RotateAroundAxis( float3 center, float3 original, float3 u, float angle )
			{
				original -= center;
				float C = cos( angle );
				float S = sin( angle );
				float t = 1 - C;
				float m00 = t * u.x * u.x + C;
				float m01 = t * u.x * u.y - S * u.z;
				float m02 = t * u.x * u.z + S * u.y;
				float m10 = t * u.x * u.y + S * u.z;
				float m11 = t * u.y * u.y + C;
				float m12 = t * u.y * u.z - S * u.x;
				float m20 = t * u.x * u.z - S * u.y;
				float m21 = t * u.y * u.z + S * u.x;
				float m22 = t * u.z * u.z + C;
				float3x3 finalMatrix = float3x3( m00, m01, m02, m10, m11, m12, m20, m21, m22 );
				return mul( finalMatrix, original ) + center;
			}
			
			float3 mod3D289( float3 x ) { return x - floor( x / 289.0 ) * 289.0; }
			float4 mod3D289( float4 x ) { return x - floor( x / 289.0 ) * 289.0; }
			float4 permute( float4 x ) { return mod3D289( ( x * 34.0 + 1.0 ) * x ); }
			float4 taylorInvSqrt( float4 r ) { return 1.79284291400159 - r * 0.85373472095314; }
			float snoise( float3 v )
			{
				const float2 C = float2( 1.0 / 6.0, 1.0 / 3.0 );
				float3 i = floor( v + dot( v, C.yyy ) );
				float3 x0 = v - i + dot( i, C.xxx );
				float3 g = step( x0.yzx, x0.xyz );
				float3 l = 1.0 - g;
				float3 i1 = min( g.xyz, l.zxy );
				float3 i2 = max( g.xyz, l.zxy );
				float3 x1 = x0 - i1 + C.xxx;
				float3 x2 = x0 - i2 + C.yyy;
				float3 x3 = x0 - 0.5;
				i = mod3D289( i);
				float4 p = permute( permute( permute( i.z + float4( 0.0, i1.z, i2.z, 1.0 ) ) + i.y + float4( 0.0, i1.y, i2.y, 1.0 ) ) + i.x + float4( 0.0, i1.x, i2.x, 1.0 ) );
				float4 j = p - 49.0 * floor( p / 49.0 );  // mod(p,7*7)
				float4 x_ = floor( j / 7.0 );
				float4 y_ = floor( j - 7.0 * x_ );  // mod(j,N)
				float4 x = ( x_ * 2.0 + 0.5 ) / 7.0 - 1.0;
				float4 y = ( y_ * 2.0 + 0.5 ) / 7.0 - 1.0;
				float4 h = 1.0 - abs( x ) - abs( y );
				float4 b0 = float4( x.xy, y.xy );
				float4 b1 = float4( x.zw, y.zw );
				float4 s0 = floor( b0 ) * 2.0 + 1.0;
				float4 s1 = floor( b1 ) * 2.0 + 1.0;
				float4 sh = -step( h, 0.0 );
				float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
				float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;
				float3 g0 = float3( a0.xy, h.x );
				float3 g1 = float3( a0.zw, h.y );
				float3 g2 = float3( a1.xy, h.z );
				float3 g3 = float3( a1.zw, h.w );
				float4 norm = taylorInvSqrt( float4( dot( g0, g0 ), dot( g1, g1 ), dot( g2, g2 ), dot( g3, g3 ) ) );
				g0 *= norm.x;
				g1 *= norm.y;
				g2 *= norm.z;
				g3 *= norm.w;
				float4 m = max( 0.6 - float4( dot( x0, x0 ), dot( x1, x1 ), dot( x2, x2 ), dot( x3, x3 ) ), 0.0 );
				m = m* m;
				m = m* m;
				float4 px = float4( dot( x0, g0 ), dot( x1, g1 ), dot( x2, g2 ), dot( x3, g3 ) );
				return 42.0 * dot( m, px);
			}
			

			
			V2FData vert ( MeshData v )
			{
				V2FData o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				UNITY_TRANSFER_INSTANCE_ID(v, o);

				o.ase_texcoord1 = v.vertex;
				o.ase_texcoord2.xy = v.ase_texcoord.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				o.ase_texcoord2.zw = 0;
				float3 vertexValue = float3(0, 0, 0);
				#if ASE_ABSOLUTE_VERTEX_POS
				vertexValue = v.vertex.xyz;
				#endif
				vertexValue = vertexValue;
				#if ASE_ABSOLUTE_VERTEX_POS
				v.vertex.xyz = vertexValue;
				#else
				v.vertex.xyz += vertexValue;
				#endif
				o.vertex = UnityObjectToClipPos(v.vertex);

				#ifdef ASE_NEEDS_FRAG_WORLD_POSITION
				o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
				#endif
				return o;
			}
			
			fixed4 frag (V2FData i ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(i);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
				fixed4 finalColor;
				#ifdef ASE_NEEDS_FRAG_WORLD_POSITION
				float3 WorldPosition = i.worldPos;
				#endif
				float3 rotatedValue55 = RotateAroundAxis( float3( 0,0,0 ), i.ase_texcoord1.xyz, float3(1,0,0), ( _Time.x * _Noise1RotateSpeed ) );
				float simplePerlin3D2 = snoise( ( rotatedValue55 * _Noise1Scale ) );
				simplePerlin3D2 = simplePerlin3D2*0.5 + 0.5;
				float4 temp_cast_0 = (( pow( simplePerlin3D2 , _Noise1PowScale.x ) * _Noise1PowScale.y )).xxxx;
				float3 rotatedValue48 = RotateAroundAxis( float3( 0,0,0 ), i.ase_texcoord1.xyz, float3(1,1,0), ( _Time.x * _Noise2RotateSpeed ) );
				float simplePerlin3D11 = snoise( ( rotatedValue48 * _Noise2Scale ) );
				simplePerlin3D11 = simplePerlin3D11*0.5 + 0.5;
				float3 rotatedValue20 = RotateAroundAxis( float3( 0,0,0 ), i.ase_texcoord1.xyz, float3(1,1,0), ( _Time.x * _NoiseMaskRotateSpeed ) );
				float simplePerlin3D19 = snoise( ( rotatedValue20 * _NoiseMaskScale ) );
				simplePerlin3D19 = simplePerlin3D19*0.5 + 0.5;
				float4 color38 = IsGammaSpace() ? float4(1,1,1,0) : float4(1,1,1,0);
				float2 texCoord54 = i.ase_texcoord2.xy * float2( 1,1 ) + float2( 0,0 );
				float2 appendResult52 = (float2(texCoord54.x , frac( ( i.ase_texcoord2.xy.x + _Time.x ) )));
				float4 lerpResult37 = lerp( color38 , tex2D( _Gradient, appendResult52 ) , _GradientColorIntensity);
				float4 temp_cast_1 = (10.0).xxxx;
				float4 Star64 = max( temp_cast_0 , min( ( ( pow( simplePerlin3D11 , _Noise2PowScale.x ) * _Noise2PowScale.y ) * ( pow( simplePerlin3D19 , _NoiseMaskPowScale.x ) * _NoiseMaskPowScale.y ) * lerpResult37 ) , temp_cast_1 ) );
				float smoothstepResult69 = smoothstep( _PosMin , _PosMax , i.ase_texcoord1.xyz.y);
				float4 lerpResult68 = lerp( _BGTint1 , _BGTint2 , smoothstepResult69);
				
				
				finalColor = ( Star64 + lerpResult68 );
				return finalColor;
			}
			ENDCG
		}
	}
	CustomEditor "ASEMaterialInspector"
	
	
}
/*ASEBEGIN
Version=18800
63.42857;386.8571;1907.429;692.1429;5609.971;-83.35245;3.037532;True;False
Node;AmplifyShaderEditor.CommentaryNode;66;-3515.927,-1142.417;Inherit;False;2901.56;2594.347;Star;48;43;44;28;30;25;22;23;24;45;35;47;29;46;20;42;54;41;31;56;58;48;60;57;10;59;17;52;51;19;40;11;55;38;39;4;37;12;2;7;32;63;62;36;13;14;15;6;64;Star;1,1,1,1;0;0
Node;AmplifyShaderEditor.TimeNode;44;-3426.238,-285.7054;Inherit;False;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TimeNode;22;-3409.577,250.164;Inherit;False;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;43;-3465.927,-134.1389;Inherit;False;Property;_Noise2RotateSpeed;Noise2RotateSpeed;4;0;Create;True;0;0;0;False;0;False;1;-0.07;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;25;-3449.266,402.7302;Inherit;False;Property;_NoiseMaskRotateSpeed;NoiseMaskRotateSpeed;8;0;Create;True;0;0;0;False;0;False;1;0.1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TexCoordVertexDataNode;28;-2707.497,1148.072;Inherit;False;0;2;0;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TimeNode;30;-2687.497,1274.072;Inherit;False;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;47;-3203.927,-249.1384;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;24;-3187.266,286.731;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.PosVertexDataNode;35;-3200.563,438.5132;Inherit;False;0;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.PosVertexDataNode;45;-3217.224,-97.35591;Inherit;False;0;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector3Node;46;-3201.783,-418.8643;Inherit;False;Constant;_Vector1;Vector 1;6;0;Create;True;0;0;0;False;0;False;1,1,0;0,0,0;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.SimpleAddOpNode;29;-2470.496,1168.072;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector3Node;23;-3186.122,118.0051;Inherit;False;Constant;_Vector0;Vector 0;6;0;Create;True;0;0;0;False;0;False;1,1,0;0,0,0;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.TimeNode;56;-3422.474,-939.7138;Inherit;False;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RotateAboutAxisNode;48;-2912.238,-301.7054;Inherit;False;False;4;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;58;-3462.163,-788.1473;Inherit;False;Property;_Noise1RotateSpeed;Noise1RotateSpeed;5;0;Create;True;0;0;0;False;0;False;1;-0.035;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;54;-2411.692,994.7192;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;42;-2791.638,381.7092;Inherit;False;Property;_NoiseMaskScale;NoiseMaskScale;7;0;Create;True;0;0;0;False;0;False;0;2;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RotateAboutAxisNode;20;-2895.577,235.164;Inherit;False;False;4;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.FractNode;31;-2330.496,1170.072;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;41;-2781.837,-149.6774;Inherit;False;Property;_Noise2Scale;Noise2Scale;3;0;Create;True;0;0;0;False;0;False;0;100;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.Vector3Node;60;-3187.347,-1092.417;Inherit;False;Constant;_Vector2;Vector 2;6;0;Create;True;0;0;0;False;0;False;1,0,0;0,0,0;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.PosVertexDataNode;59;-3213.46,-751.3644;Inherit;False;0;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;10;-2466.799,-264.5186;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.DynamicAppendNode;52;-2123.273,1012.635;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;17;-2452.434,233.4249;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;57;-3200.163,-903.1469;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;13;-2220.799,-68.51789;Inherit;False;Property;_Noise2PowScale;Noise2PowScale;2;0;Create;True;0;0;0;False;0;False;1,1;150,1000;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.RotateAboutAxisNode;55;-2934.866,-974.2207;Inherit;False;False;4;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;40;-2746.556,-811.5886;Inherit;False;Property;_Noise1Scale;Noise1Scale;1;0;Create;True;0;0;0;False;0;False;0;200;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.NoiseGeneratorNode;11;-2252.799,-292.5186;Inherit;True;Simplex3D;True;False;2;0;FLOAT3;0,0,0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;39;-1947.853,1192.86;Inherit;False;Property;_GradientColorIntensity;GradientColorIntensity;9;0;Create;True;0;0;0;False;0;False;0;1;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.NoiseGeneratorNode;19;-2232.852,201.1864;Inherit;True;Simplex3D;True;False;2;0;FLOAT3;0,0,0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;38;-1901.916,804.4041;Inherit;False;Constant;_Color0;Color 0;7;0;Create;True;0;0;0;False;0;False;1,1,1,0;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;51;-1963.58,987.8132;Inherit;True;Property;_Gradient;Gradient;10;0;Create;True;0;0;0;False;0;False;-1;None;e1aae396f87117940a2136fa5edeecf7;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector2Node;15;-2209.852,432.1862;Inherit;False;Property;_NoiseMaskPowScale;NoiseMaskPowScale;6;0;Create;True;0;0;0;False;0;False;1,1;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.LerpOp;37;-1605.952,971.1862;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;4;-2438.872,-935.1197;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.FunctionNode;14;-1940.851,217.1864;Inherit;False;PowerScale;-1;;9;5ba70760a40e0a6499195a0590fd2e74;0;3;1;FLOAT;1;False;2;FLOAT;1;False;3;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;12;-1942.798,-278.5186;Inherit;False;PowerScale;-1;;10;5ba70760a40e0a6499195a0590fd2e74;0;3;1;FLOAT;1;False;2;FLOAT;1;False;3;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;7;-2208.872,-721.1197;Inherit;False;Property;_Noise1PowScale;Noise1PowScale;0;0;Create;True;0;0;0;False;0;False;1,1;100,50;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.RangedFloatNode;63;-1377.473,236.264;Inherit;False;Constant;_Float1;Float 1;11;0;Create;True;0;0;0;False;0;False;10;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;32;-1464.754,54.9934;Inherit;False;3;3;0;FLOAT;0;False;1;FLOAT;0;False;2;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.NoiseGeneratorNode;2;-2247.872,-936.1197;Inherit;True;Simplex3D;True;False;2;0;FLOAT3;0,0,0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMinOpNode;62;-1254.677,57.7;Inherit;False;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.FunctionNode;6;-1928.684,-924.2894;Inherit;False;PowerScale;-1;;11;5ba70760a40e0a6499195a0590fd2e74;0;3;1;FLOAT;1;False;2;FLOAT;1;False;3;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMaxOpNode;36;-1128.429,-96.20215;Inherit;False;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.PosVertexDataNode;67;785.8835,916.3069;Inherit;False;0;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;70;688.2289,1073.385;Inherit;False;Property;_PosMin;PosMin;11;0;Create;True;0;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;72;683.2289,1172.385;Inherit;False;Property;_PosMax;PosMax;12;0;Create;True;0;0;0;False;0;False;0;0.083;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;75;1019.328,580.4853;Inherit;False;Property;_BGTint1;BGTint1;13;0;Create;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RegisterLocalVarNode;64;-824.7955,-85.72701;Inherit;False;Star;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SmoothstepOpNode;69;1075.23,949.1854;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;74;1014.328,769.4853;Inherit;False;Property;_BGTint2;BGTint2;14;0;Create;True;0;0;0;False;0;False;0.6132076,0.6132076,0.6132076,0;0.6478873,0.6197183,1,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GetLocalVarNode;65;1554.506,511.599;Inherit;False;64;Star;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.LerpOp;68;1370.529,853.6855;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleAddOpNode;76;1849.641,636.6402;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;0;2065.878,655.9629;Float;False;True;-1;2;ASEMaterialInspector;100;1;Skybox/TAPro/MoonQueueStar;0770190933193b94aaa3065e307002fa;True;Unlit;0;0;Unlit;2;True;0;1;False;-1;0;False;-1;0;1;False;-1;0;False;-1;True;0;False;-1;0;False;-1;False;False;False;False;False;False;True;0;False;-1;True;0;False;-1;True;True;True;True;True;0;False;-1;False;False;False;True;False;255;False;-1;255;False;-1;255;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;True;1;False;-1;True;3;False;-1;True;True;0;False;-1;0;False;-1;True;1;RenderType=Opaque=RenderType;True;2;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=ForwardBase;False;0;;0;0;Standard;1;Vertex Position,InvertActionOnDeselection;1;0;1;True;False;;False;0
WireConnection;47;0;44;1
WireConnection;47;1;43;0
WireConnection;24;0;22;1
WireConnection;24;1;25;0
WireConnection;29;0;28;1
WireConnection;29;1;30;1
WireConnection;48;0;46;0
WireConnection;48;1;47;0
WireConnection;48;3;45;0
WireConnection;20;0;23;0
WireConnection;20;1;24;0
WireConnection;20;3;35;0
WireConnection;31;0;29;0
WireConnection;10;0;48;0
WireConnection;10;1;41;0
WireConnection;52;0;54;1
WireConnection;52;1;31;0
WireConnection;17;0;20;0
WireConnection;17;1;42;0
WireConnection;57;0;56;1
WireConnection;57;1;58;0
WireConnection;55;0;60;0
WireConnection;55;1;57;0
WireConnection;55;3;59;0
WireConnection;11;0;10;0
WireConnection;19;0;17;0
WireConnection;51;1;52;0
WireConnection;37;0;38;0
WireConnection;37;1;51;0
WireConnection;37;2;39;0
WireConnection;4;0;55;0
WireConnection;4;1;40;0
WireConnection;14;1;19;0
WireConnection;14;2;15;1
WireConnection;14;3;15;2
WireConnection;12;1;11;0
WireConnection;12;2;13;1
WireConnection;12;3;13;2
WireConnection;32;0;12;0
WireConnection;32;1;14;0
WireConnection;32;2;37;0
WireConnection;2;0;4;0
WireConnection;62;0;32;0
WireConnection;62;1;63;0
WireConnection;6;1;2;0
WireConnection;6;2;7;1
WireConnection;6;3;7;2
WireConnection;36;0;6;0
WireConnection;36;1;62;0
WireConnection;64;0;36;0
WireConnection;69;0;67;2
WireConnection;69;1;70;0
WireConnection;69;2;72;0
WireConnection;68;0;75;0
WireConnection;68;1;74;0
WireConnection;68;2;69;0
WireConnection;76;0;65;0
WireConnection;76;1;68;0
WireConnection;0;0;76;0
ASEEND*/
//CHKSM=224C7CAE72F0046ABD3E6CE451DDCA9DC7A81808