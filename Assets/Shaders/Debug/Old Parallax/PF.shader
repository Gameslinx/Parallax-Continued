// Upgrade NOTE: replaced 'defined EMISSION_ON' with 'defined (EMISSION_ON)'

// Upgrade NOTE: replaced 'defined REFL_ON' with 'defined (REFL_ON)'

// Upgrade NOTE: replaced 'defined TESS_ON' with 'defined (TESS_ON)'



Shader "Custom/ParallaxFULLUltra"
{
	Properties
	{
		_SurfaceTexture("_SurfaceTexture", 2D) = "white" {}
		[NoScaleOffset] _SurfaceTextureMid("_SurfaceTextureMid", 2D) = "white" {}
		[NoScaleOffset] _SurfaceTextureHigh("_SurfaceTextureHigh", 2D) = "white" {}
		[NoScaleOffset] _SteepTex("_SteepTex", 2D) = "white" {}
		[NoScaleOffset] _InfluenceMap("_InfluenceMap", 2D) = "white" {}

		[NoScaleOffset] _BumpMap("_BumpMap", 2D) = "bump" {}
		[NoScaleOffset] _BumpMapMid("_BumpMapMid", 2D) = "bump" {}
		[NoScaleOffset] _BumpMapHigh("_BumpMapHigh", 2D) = "bump" {}
		[NoScaleOffset] _BumpMapSteep("_BumpMapSteep", 2D) = "bump" {}
		

		[NoScaleOffset] _DispTex("Displacement Texture", 2D) = "white" {}
		_LowStart("_LowStart", float) = 0
		_LowEnd("_LowEnd", float) = 1
		_HighStart("_HighStart", float) = 2
		_HighEnd("_HighEnd", float) = 3

		_displacement_scale("Displacement Scale", Range(0, 7)) = 1
		_displacement_offset("_displacement_offset", Range(-1, 1)) = 0
		_SteepPower("_SteepPower", Range(0.01, 50)) = 1
		_SteepContrast("_SteepContrast", Range(0, 10)) = 1
		_SteepMidpoint("_SteepMidpoint", Range(0, 1)) = 0.5
		_Strength("_Strength", Range(0.001, 100)) = 4
		_LightPos("_LightPos", vector) = (0,0,0)

		_Metallic("_Metallic", Range(0.001, 20)) = 0.2
		_Gloss("_Gloss", Range(0, 250)) = 0.2
		_MetallicTint("_MetallicTint", COLOR) = (0,0,0)

		_PlanetOrigin("_PlanetOrigin", vector) = (0,0,0)
		_PlanetRadius("_PlanetRadius", float) = 100000
		_LowStart("_LowStart", float) = 0
		_LowEnd("_LowEnd", float) = 1
		_HighStart("_HighStart", float) = 2
		_HighEnd("_HighEnd", float) = 3
		_TessellationEdgeLength("_TessellationEdgeLength", Range(0.0001, 25)) = 25
		_TessellationRange("_TessellationRange", Range(1, 10000)) = 99
		_TessellationMax("_TessellationMax", Range(4, 64)) = 64
		_NormalSpecularInfluence("_NormalSpecularInfluence", Range(0, 1)) = 1

		_SurfaceTextureUVs("_SurfaceTextureUVs", vector) = (0,0,0)
		_Debug("_Debug", COLOR) = (1,1,1,1)

		_SpecColor("Specular Color", Color) = (1.0, 1.0, 1.0, 1.0)
		_Shininess("Shininess", float) = 10
		_Hapke("_Hapke", Range(0.3, 5)) = 1
		_FadeStart("_FadeStart", float) = 6000
		_FadeEnd("_FadeEnd", float) = 9000
		_PlanetOpacity("_PlanetOpacity", Range(0, 1)) = 1
		_EmissionColor("_EmissionColor", COLOR) = (0,0,0,0)
		_HasEmission("_HasEmission", Range(0, 1)) = 0

		_FresnelColor("Fresnel color", COLOR) = (0,0,0)
		_FresnelPower("Fresnel power", Range(0.001, 10)) = 1
	}
		SubShader
		{
			//Cull Off
			Tags {"RenderType" = "Opaque" "Queue" = "Geometry" }
			Pass
			{
				Tags { "LightMode" = "ForwardBase" }
				Blend SrcAlpha OneMinusSrcAlpha
				Offset 0.0, 1
				CGPROGRAM
			#pragma vertex vertex_shader			
			#pragma hull hull_shader
			#pragma domain domain_shader
			#pragma fragment pixel_shader
			#pragma multi_compile_fwdbase
			#pragma multi_compile EMISSION_ON EMISSION_OFF
			#include "UnityCG.cginc"
			//#include "AutoLight.cginc"
			#include "TessellationProper.cginc"
			//#include "TessellationProper.cginc"

			uniform sampler2D _SurfaceTexture;
			uniform sampler2D _SurfaceTextureMid;
			uniform sampler2D _SurfaceTextureHigh;
			uniform sampler2D _SteepTex;
			float2 _SurfaceTexture_ST;
			uniform sampler2D _DispTex;
			float _tessellation_scale;
			float _displacement_scale;
			float _Strength;
			uniform sampler2D _BumpMap;
			uniform sampler2D _BumpMapMid;
			uniform sampler2D _BumpMapHigh;
			uniform sampler2D _BumpMapSteep;
			float _TessellationEdgeLength;
			uniform sampler2D _InfluenceMap;
			float _SteepPower;
			float3 _SurfaceTextureUVs;
			float4 _Debug;
			float _displacement_offset;
			float _TessellationRange;
			float _TessellationMax;
			float _FadeStart;
			float _FadeEnd;
			float _PlanetOpacity;
			float3 _EmissionColor;
			float3 _FresnelColor;
			float _FresnelPower;

			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float2 uv : TEXCOORD0;
				float4 color : COLOR;
			};

			struct VertexOutput
			{

				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
				float3 world_vertex : TEXCOORD1;
				float3 normal : NORMAL;
				float3 worldNormal : TEXCOORD2;
				float4 color : COLOR;
				float slope : TEXCOORD6;
				LIGHTING_COORDS(4, 5)
			};
			float4 BlinnPhong(VertexOutput i, float3 worldNormal, float alpha, float3 col)
				{
					float3 normal = lerp(i.worldNormal, worldNormal, _NormalSpecularInfluence);
					float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.world_vertex.xyz);
					float3 halfDirection = normalize(viewDirection + _WorldSpaceLightPos0);
					float NdotL = max(0, dot(worldNormal, _WorldSpaceLightPos0));
					float NdotV = max(0, dot(normal, halfDirection));

					NdotL = pow(NdotL, _Hapke);

					//Specular calculations
					float3 specularity = pow(NdotV, _Gloss * alpha) * _Metallic * _MetallicTint.rgb * alpha;
					float angle = saturate(dot(normalize(i.worldNormal), _WorldSpaceLightPos0));
					angle = 1 - pow(1 - angle, 7);
					specularity *= saturate(angle - 0.2);
					float3 lightingModel = NdotL * 1 + specularity;

					float attenuation = LIGHT_ATTENUATION(i);
					attenuation = 1 - pow(1 - attenuation, 3);
					float3 attenColor = attenuation * _LightColor0.rgb;
					

					float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - i.world_vertex.xyz);
					float fresnelMult = Fresnel(worldNormal, normalize(viewDir), _FresnelPower) * saturate(1 - pow(1 - dot(i.worldNormal, _WorldSpaceLightPos0), 4)) * attenuation;

					float4 finalDiffuse = float4(lightingModel * attenColor + ShadeSH9(float4(i.world_vertex, 1)), fresnelMult);

					return finalDiffuse;
				}
			VertexOutput vert(VertexInput v)
			{
				VertexOutput o;
				o.uv = v.uv;
				o.world_vertex = mul(unity_ObjectToWorld, v.vertex);
				o.worldNormal = normalize(mul(unity_ObjectToWorld, v.normal));
				o.color = v.color;
				o.normal = v.normal;
				float slope = abs(dot(normalize(o.world_vertex - _PlanetOrigin), o.worldNormal));
				slope = pow(slope, _SteepPower);
				o.slope = CalculateSlope(slope);
				float3 dpdx = ddx(o.world_vertex );
				float3 dpdy = ddy(o.world_vertex );
				float3x3 biplanarCoords = GetBiplanarCoordinates(o.world_vertex, o.worldNormal);
				
				float cameraDist = distance(_WorldSpaceCameraPos, o.world_vertex);
				float ZoomLevel = GetZoomLevel(cameraDist);
				int ClampedZoomLevel = floor(ZoomLevel);
				float percentage = (ZoomLevel - ClampedZoomLevel);
				float uvDistortion = pow(2, ClampedZoomLevel - 1);
				float nextUVDist = pow(2, ClampedZoomLevel);
				fixed4 displacement = (SampleDisplacementBiplanarTexture(_DispTex, o.world_vertex , o.worldNormal, _SurfaceTexture_ST, _SurfaceTextureUVs, biplanarCoords, dpdx, dpdy, uvDistortion, nextUVDist, percentage, o.slope)) * (lerp(uvDistortion, nextUVDist, percentage) + 1) / 2;
				if (cameraDist < _TessellationRange)
				{
					v.vertex.xyz += ((displacement * _displacement_scale * v.normal) + ((_displacement_offset) * (v.normal) * (ZoomLevel))) * (1 - pow(saturate(cameraDist / (_TessellationRange)), 5));
				}
				
				//o.vertex = v.vertex;

				o.pos = UnityObjectToClipPos(v.vertex);

				return o;
			}
			struct tessellation
			{
				float4 vertex : INTERNALTESSPOS;
				float3 normal : NORMAL;
				float2 uv : TEXCOORD0;
				float4 color : COLOR;
				float3 world_vertex : TEXCOORD1;
			};
			struct OutputPatchConstant
			{
				float edge[3]: SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			tessellation vertex_shader(VertexInput v)
			{
				tessellation o;
				o.vertex = v.vertex;
				o.normal = v.normal;
				o.uv = v.uv;
				o.color = v.color;
				o.world_vertex = mul(unity_ObjectToWorld, v.vertex);

				return o;
			}
			float TessellationEdgeFactor(tessellation cp0, tessellation cp1)
			{
				float3 p0 = mul(unity_ObjectToWorld, float4(cp0.vertex.xyz, 1)).xyz;
				float3 p1 = mul(unity_ObjectToWorld, float4(cp1.vertex.xyz, 1)).xyz;
				float edgeLength = distance(p0, p1);
				float3 edgeCenter = (p0 + p1) * 0.5;

				float viewDistance = distance(edgeCenter, _WorldSpaceCameraPos);
				if (viewDistance < _TessellationRange)
				{
					return min(_TessellationMax, max((edgeLength * pow(_ScreenParams.y / (_TessellationEdgeLength * viewDistance), 2)), 1));
				}
				else
				{
					return 1;
				}
				
			}

			OutputPatchConstant constantsHS(InputPatch<tessellation,3> patch)
			{
				OutputPatchConstant o;
				


				float3 p0 = mul(unity_ObjectToWorld, patch[0].vertex).xyz;
				float3 p1 = mul(unity_ObjectToWorld, patch[1].vertex).xyz;
				float3 p2 = mul(unity_ObjectToWorld, patch[2].vertex).xyz;
				
				o.edge[0] = TessellationEdgeFactor(patch[1], patch[2]);
				o.edge[1] = TessellationEdgeFactor(patch[2], patch[0]);
				o.edge[2] = TessellationEdgeFactor(patch[0], patch[1]);
				o.inside = (o.edge[0] + o.edge[1] + o.edge[2]) * 0.3333;
				float bias = -1;
				bias = -0.5 * _displacement_scale - 1;
				if (TriangleIsCulled(p0, p1, p2, bias, patch[0].normal)) 
				{
					o.edge[0] = o.edge[1] = o.edge[2] = o.inside = 1;
				}

				return o;
			}

			[domain("tri")]
			[partitioning("fractional_even")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("constantsHS")]
			[outputcontrolpoints(3)]
			tessellation hull_shader(InputPatch<tessellation,3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			VertexOutput domain_shader(OutputPatchConstant tessFactors,const OutputPatch<tessellation,3> vs, float3 d:SV_DomainLocation)
			{
				VertexInput v;
				v.vertex = vs[0].vertex * d.x + vs[1].vertex * d.y + vs[2].vertex * d.z;
				v.normal = vs[0].normal * d.x + vs[1].normal * d.y + vs[2].normal * d.z;
				v.uv = vs[0].uv * d.x + vs[1].uv * d.y + vs[2].uv * d.z;
				v.color = vs[0].color * d.x + vs[1].color * d.y + vs[2].color * d.z;
				VertexOutput o = vert(v);

				TRANSFER_VERTEX_TO_FRAGMENT(o);
				return o;
			}
			float4 pixel_shader(VertexOutput ps) : SV_TARGET
			{
				float cameraDist = distance(_WorldSpaceCameraPos, ps.world_vertex);
				
				float ZoomLevel = GetZoomLevel(cameraDist);
				int ClampedZoomLevel = ZoomLevel;
				
				float uvDistortion = pow(2, ClampedZoomLevel - 1);
				float nextUVDist = pow(2, ClampedZoomLevel);
				float percentage = ZoomLevel - ClampedZoomLevel;
				
				float slope = ps.slope;// abs(dot(normalize(ps.world_vertex - _PlanetOrigin), normalize(mul(unity_ObjectToWorld, ps.normal))));
				
				float blendLow = heightBlendLow(ps.world_vertex);
				float blendHigh = heightBlendHigh(ps.world_vertex);
				float midPoint = (distance(ps.world_vertex, _PlanetOrigin) - _PlanetRadius) / (_HighStart + _LowEnd);
				
				fixed4 lowTex;
				fixed4 midTex;
				fixed4 highTex;
				fixed4 steepTex;
				
				float3 dpdx = ddx(ps.world_vertex);
				float3 dpdy = ddy(ps.world_vertex);
				float3x3 biplanarCoords = GetBiplanarCoordinates(ps.world_vertex , ps.worldNormal);
				
				lowTex = BiplanarTexture_float(_SurfaceTexture, dpdx, dpdy, uvDistortion, nextUVDist, percentage, _SurfaceTextureUVs, biplanarCoords, ps.world_vertex,  ps.worldNormal, _SurfaceTexture_ST);
				highTex = BiplanarTexture_float(_SurfaceTextureHigh, dpdx, dpdy, uvDistortion, nextUVDist, percentage, _SurfaceTextureUVs, biplanarCoords, ps.world_vertex,  ps.worldNormal, _SurfaceTexture_ST);
				steepTex = BiplanarTexture_float(_SteepTex, dpdx, dpdy, uvDistortion, nextUVDist, percentage, _SurfaceTextureUVs, biplanarCoords, ps.world_vertex,  ps.worldNormal, _SurfaceTexture_ST);
				midTex = BiplanarTexture_float(_SurfaceTextureMid, dpdx, dpdy, uvDistortion, nextUVDist, percentage, _SurfaceTextureUVs, biplanarCoords, ps.world_vertex,  ps.worldNormal, _SurfaceTexture_ST);
				
				float4 lowNormal = 1;
				float4 midNormal = 1;
				float4 highNormal = 1;
				float4 steepNormal = 1;
				
				#if defined (EMISSION_ON)
				{
					lowNormal = EmissiveBiplanarNormal_float(_BumpMap, dpdx, dpdy, uvDistortion, nextUVDist, percentage, _SurfaceTextureUVs, biplanarCoords, ps.world_vertex,  ps.worldNormal, _SurfaceTexture_ST);
					highNormal = EmissiveBiplanarNormal_float(_BumpMapHigh, dpdx, dpdy, uvDistortion, nextUVDist, percentage, _SurfaceTextureUVs, biplanarCoords, ps.world_vertex,  ps.worldNormal, _SurfaceTexture_ST);
					steepNormal = EmissiveBiplanarNormal_float(_BumpMapSteep, dpdx, dpdy, uvDistortion, nextUVDist, percentage, _SurfaceTextureUVs, biplanarCoords, ps.world_vertex,  ps.worldNormal, _SurfaceTexture_ST);
					midNormal = EmissiveBiplanarNormal_float(_BumpMapMid, dpdx, dpdy, uvDistortion, nextUVDist, percentage, _SurfaceTextureUVs, biplanarCoords, ps.world_vertex,  ps.worldNormal, _SurfaceTexture_ST);
				}
				#else
				{
					lowNormal = BiplanarNormal_float(_BumpMap, dpdx, dpdy, uvDistortion, nextUVDist, percentage, _SurfaceTextureUVs, biplanarCoords, ps.world_vertex,  ps.worldNormal, _SurfaceTexture_ST);
					highNormal = BiplanarNormal_float(_BumpMapHigh, dpdx, dpdy, uvDistortion, nextUVDist, percentage, _SurfaceTextureUVs, biplanarCoords, ps.world_vertex,  ps.worldNormal, _SurfaceTexture_ST);
					steepNormal = BiplanarNormal_float(_BumpMapSteep, dpdx, dpdy, uvDistortion, nextUVDist, percentage, _SurfaceTextureUVs, biplanarCoords, ps.world_vertex,  ps.worldNormal, _SurfaceTexture_ST);
					midNormal = BiplanarNormal_float(_BumpMapMid, dpdx, dpdy, uvDistortion, nextUVDist, percentage, _SurfaceTextureUVs, biplanarCoords, ps.world_vertex,  ps.worldNormal, _SurfaceTexture_ST);
				}
				#endif
				
				
				//return 1;
				
				float luminosityLow = (lowTex.r * 0.21 + lowTex.g * 0.72 + lowTex.b * 0.07) + 0.5f;
				float luminosityMid = (midTex.r * 0.21 + midTex.g * 0.72 + midTex.b * 0.07) + 0.5f;
				float luminosityHigh = (highTex.r * 0.21 + highTex.g * 0.72 + highTex.b * 0.07) + 0.5f;
				float luminositySteep = (steepTex.r * 0.21 + steepTex.g * 0.72 + steepTex.b * 0.07) + 0.5f;
				
				fixed4 influenceTex = BiplanarTexture_float(_InfluenceMap, dpdx, dpdy, uvDistortion, nextUVDist, percentage, _SurfaceTextureUVs, biplanarCoords, ps.world_vertex,  ps.worldNormal, _SurfaceTexture_ST);
				
				
				
				lowTex.rgb = lerp(ps.color.rgb * (luminosityLow), lowTex.rgb, influenceTex.r);
				midTex.rgb = lerp(ps.color.rgb * (luminosityMid), midTex.rgb, influenceTex.g);
				highTex.rgb = lerp(ps.color.rgb * (luminosityHigh), highTex.rgb, influenceTex.b);
				steepTex.rgb = lerp(ps.color.rgb * (luminositySteep), steepTex.rgb, influenceTex.a);

				fixed4 surfaceCol = lerpSurfaceColor(lowTex, midTex, highTex, steepTex, midPoint, slope, blendLow, blendHigh);
				
				float4 surfaceNormal = lerpSurfaceNormal(lowNormal, midNormal, highNormal, steepNormal, midPoint, slope, blendLow, blendHigh);
				
				float4 lightingCol = BlinnPhong(ps, surfaceNormal, surfaceCol.a, surfaceCol.rgb);

				float3 emissionColor = _EmissionColor * (1 - surfaceNormal.a);

				return float4(lightingCol.rgb * surfaceCol.rgb + emissionColor + (lightingCol.a * _FresnelColor.rgb), 1 - _PlanetOpacity);
			}
			ENDCG
		}
			Pass
			{
			
				Tags {"LightMode" = "ShadowCaster"}
				Offset 0.0, 1
				Blend SrcAlpha OneMinusSrcAlpha
				CGPROGRAM
				#pragma vertex vertex_shader			
				#pragma hull hull_shader
				#pragma domain domain_shader
				#pragma fragment pixel_shader
				//#pragma multi_compile_fwdbase
				#include "UnityCG.cginc"
				#include "AutoLight.cginc"
				#include "TessellationProper.cginc"
			
				uniform sampler2D _SurfaceTexture;
				float2 _SurfaceTexture_ST;
				uniform sampler2D _DispTex;
				float2 _DispTex_ST;
				float _tessellation_scale;
				float _displacement_scale;
				float _TessellationEdgeLength;
				float _SteepPower;
				float3 _SurfaceTextureUVs;
				float _displacement_offset;
				float _TessellationRange;
				float _TessellationMax;
				float _PlanetOpacity;
			
				struct VertexInput
				{
					float4 vertex : POSITION;
					float3 normal : NORMAL;
			
				};
			
				struct VertexOutput
				{
			
					float4 pos : SV_POSITION;
					float4 world_vertex : TEXCOORD1;
					float3 normal : NORMAL;
					float3 worldNormal : TEXCOORD5;
				};
			
			
			
				VertexOutput vert(VertexInput v)
				{
					VertexOutput o;
					o.world_vertex = mul(unity_ObjectToWorld, v.vertex);
					o.worldNormal = normalize(mul(unity_ObjectToWorld, v.normal));
					o.normal = v.normal;
					float3 dpdx = ddx(o.world_vertex);
					float3 dpdy = ddy(o.world_vertex);
					float3x3 biplanarCoords = GetBiplanarCoordinates(o.world_vertex, o.worldNormal);
					float slope = abs(dot(normalize(o.world_vertex - _PlanetOrigin), normalize(o.worldNormal)));
					slope = pow(slope, _SteepPower);
					slope = CalculateSlope(slope);
					float cameraDist = distance(_WorldSpaceCameraPos, o.world_vertex);
					float ZoomLevel = GetZoomLevel(cameraDist);
					int ClampedZoomLevel = floor(ZoomLevel);
					float percentage = (ZoomLevel - ClampedZoomLevel);
					float uvDistortion = pow(2, ClampedZoomLevel - 1);
					float nextUVDist = pow(2, ClampedZoomLevel);
					fixed4 displacement = (SampleDisplacementBiplanarTexture(_DispTex, o.world_vertex, o.worldNormal, _SurfaceTexture_ST, _SurfaceTextureUVs, biplanarCoords, dpdx, dpdy, uvDistortion, nextUVDist, percentage, slope)) * (lerp(uvDistortion, nextUVDist, percentage) + 1) / 2;
					//o.displacement = displacement;
					v.vertex.xyz += ((displacement * _displacement_scale * v.normal) + ((_displacement_offset) * (v.normal) * (ZoomLevel))) * (1 - pow(saturate(cameraDist / (_TessellationRange)), 5));
					o.pos = UnityObjectToClipPos(v.vertex);
			
			
			
					return o;
				}
			
				struct tessellation
				{
					float4 vertex : INTERNALTESSPOS;
					float3 normal : NORMAL;
				};
				struct OutputPatchConstant
				{
					float edge[3]: SV_TessFactor;
					float inside : SV_InsideTessFactor;
				};
				tessellation vertex_shader(VertexInput v)
				{
					tessellation o;
					o.vertex = v.vertex;
					o.normal = v.normal;
			
					return o;
				}
				float TessellationEdgeFactor(tessellation cp0, tessellation cp1)
				{
					float3 p0 = mul(unity_ObjectToWorld, float4(cp0.vertex.xyz, 1)).xyz;
					float3 p1 = mul(unity_ObjectToWorld, float4(cp1.vertex.xyz, 1)).xyz;
					float edgeLength = distance(p0, p1);
					float3 edgeCenter = (p0 + p1) * 0.5;
			
					float viewDistance = distance(edgeCenter, _WorldSpaceCameraPos);
					if (viewDistance < _TessellationRange)
					{
					//return 1;
						return min(_TessellationMax, max((edgeLength * pow(_ScreenParams.y / (_TessellationEdgeLength * viewDistance), 2)), 1));
					}
					else
					{
						return 1;
					}
				}
				OutputPatchConstant constantsHS(InputPatch<tessellation, 3> patch)
				{
					OutputPatchConstant o;
					float3 p0 = mul(unity_ObjectToWorld, patch[0].vertex).xyz;
					float3 p1 = mul(unity_ObjectToWorld, patch[1].vertex).xyz;
					float3 p2 = mul(unity_ObjectToWorld, patch[2].vertex).xyz;
					o.edge[0] = TessellationEdgeFactor(patch[1], patch[2]);
					o.edge[1] = TessellationEdgeFactor(patch[2], patch[0]);
					o.edge[2] = TessellationEdgeFactor(patch[0], patch[1]);
					o.inside = (o.edge[0] + o.edge[1] + o.edge[2]) * 0.333;
					float bias = -1;
					bias = -0.5 * _displacement_scale - 1;
					if (TriangleIsCulled(p0, p1, p2, bias, patch[0].normal))
					{
						o.edge[0] = o.edge[1] = o.edge[2] = o.inside = 1;
					}
					
					return o;
				}
			
				[domain("tri")]
				[partitioning("fractional_even")]
				[outputtopology("triangle_cw")]
				[patchconstantfunc("constantsHS")]
				[outputcontrolpoints(3)]
				tessellation hull_shader(InputPatch<tessellation,3> patch, uint id : SV_OutputControlPointID)
				{
					return patch[id];
				}
			
				[domain("tri")]
				VertexOutput domain_shader(OutputPatchConstant tessFactors,const OutputPatch<tessellation,3> vs, float3 d:SV_DomainLocation)
				{
					VertexInput v;
					v.vertex = vs[0].vertex * d.x + vs[1].vertex * d.y + vs[2].vertex * d.z;
					v.normal = vs[0].normal * d.x + vs[1].normal * d.y + vs[2].normal * d.z;
					VertexOutput o = vert(v);
					o.pos = UnityApplyLinearShadowBias(o.pos);
					return o;
				}
				void pixel_shader(VertexOutput ps)
				{
					
				}
				ENDCG
			}
			Pass
			{
				Tags { "LightMode" = "ForwardAdd" "RenderQueue" = "100"}
				Zwrite Off
				Blend One OneMinusSrcAlpha 
				Offset 0, 1
				CGPROGRAM
				#pragma vertex vertex_shader			
				#pragma hull hull_shader
				#pragma domain domain_shader
				#pragma fragment pixel_shader
				#pragma multi_compile HQ_LIGHTS_ON HQ_LIGHTS_OFF
				//#pragma multi_compile_fwdadd
				#pragma multi_compile_lightpass
				#include "UnityCG.cginc"
				#include "AutoLight.cginc"
				#include "TessellationProper.cginc"
				//#include "TessellationProper.cginc"
			
				uniform sampler2D _SurfaceTexture;
				uniform sampler2D _SurfaceTextureMid;
				uniform sampler2D _SurfaceTextureHigh;
				uniform sampler2D _SteepTex;
				float2 _SurfaceTexture_ST;
				uniform sampler2D _DispTex;
				float2 _DispTex_ST;
				float _displacement_scale;
				uniform sampler2D _BumpMap;
				uniform sampler2D _BumpMapMid;
				uniform sampler2D _BumpMapHigh;
				uniform sampler2D _BumpMapSteep;
				float _TessellationEdgeLength;
				uniform sampler2D _InfluenceMap;
				float _SteepPower;
				float3 _SurfaceTextureUVs;
				float4 _Debug;
				float _displacement_offset;
				float _TessellationRange;
				float _TessellationMax;
				float _PlanetOpacity;
				float3 _FresnelColor;
			
				struct VertexInput
				{
					float4 vertex : POSITION;
					float3 normal : NORMAL;
					float2 uv : TEXCOORD0;
					float3 color : COLOR;
				};
			
				struct VertexOutput
				{
			
					float4 pos : SV_POSITION;
					float2 uv : TEXCOORD0;
					float4 world_vertex : TEXCOORD1;
					float3 normal : NORMAL;
					float3 worldNormal : TEXCOORD2;
					float3 color : COLOR;
					float3 lightDir : TEXCOORD4;
					float slope : TEXCOORD6;
			};
			
			
			
			VertexOutput vert(VertexInput v)
			{
				VertexOutput o;
				o.uv = v.uv;
				o.world_vertex = (mul(unity_ObjectToWorld, v.vertex));
				o.worldNormal = normalize(mul(unity_ObjectToWorld, v.normal));
				o.color = v.color;
				o.normal = v.normal;
				float slope = abs(dot(normalize(o.world_vertex - _PlanetOrigin), o.worldNormal));
				slope = pow(slope, _SteepPower);
				o.slope = CalculateSlope(slope);
				o.lightDir = normalize(_WorldSpaceLightPos0.xyz - mul(unity_ObjectToWorld, v.vertex).xyz);
				float3 dpdx = ddx(o.world_vertex);
				float3 dpdy = ddy(o.world_vertex);
				float3x3 biplanarCoords = GetBiplanarCoordinates(o.world_vertex, o.worldNormal);
			
				float cameraDist = distance(_WorldSpaceCameraPos, o.world_vertex);
				float ZoomLevel = GetZoomLevel(cameraDist);
				int ClampedZoomLevel = floor(ZoomLevel);
				float percentage = (ZoomLevel - ClampedZoomLevel);
				float uvDistortion = pow(2, ClampedZoomLevel - 1);
				float nextUVDist = pow(2, ClampedZoomLevel);
				fixed4 displacement = (SampleDisplacementBiplanarTexture(_DispTex, o.world_vertex, o.worldNormal, _SurfaceTexture_ST, _SurfaceTextureUVs, biplanarCoords, dpdx, dpdy, uvDistortion, nextUVDist, percentage, o.slope)) * (lerp(uvDistortion, nextUVDist, percentage) + 1) / 2;

				#if defined (HQ_LIGHTS_ON)
				{
					if (cameraDist < _TessellationRange)
					{
						v.vertex.xyz += ((displacement * _displacement_scale * v.normal) + ((_displacement_offset) * (v.normal) * (ZoomLevel))) * (1 - pow(saturate(cameraDist / (_TessellationRange)), 5));
					}
				}
				#else
				{
					if (cameraDist < _TessellationRange)
					{
						v.vertex.xyz += ((1 * _displacement_scale * v.normal) + ((_displacement_offset) * (v.normal) * (ZoomLevel))) * (1 - pow(saturate(cameraDist / (_TessellationRange)), 5));
					}
				}
				#endif
				o.pos = UnityObjectToClipPos(v.vertex);
			
				return o;
			}
			
			struct tessellation
			{
				float4 vertex : INTERNALTESSPOS;
				float3 normal : NORMAL;
				float2 uv : TEXCOORD0;
				float3 color : COLOR;
				float3 world_vertex : TEXCOORD1;
			};
			
			
			struct OutputPatchConstant
			{
				float edge[3]: SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};
			
			tessellation vertex_shader(VertexInput v)
			{
				tessellation o;
				o.vertex = v.vertex;
				o.normal = v.normal;
				o.uv = v.uv;
				o.color = v.color;
				o.world_vertex = mul(unity_ObjectToWorld, v.vertex);
				return o;
			}
			float TessellationEdgeFactor(tessellation cp0, tessellation cp1)
			{
				#if defined (HQ_LIGHTS_ON)
				{
					float3 p0 = mul(unity_ObjectToWorld, float4(cp0.vertex.xyz, 1)).xyz;
					float3 p1 = mul(unity_ObjectToWorld, float4(cp1.vertex.xyz, 1)).xyz;
					float edgeLength = distance(p0, p1);
					float3 edgeCenter = (p0 + p1) * 0.5;

					float viewDistance = distance(edgeCenter, _WorldSpaceCameraPos);
					if (viewDistance < _TessellationRange)
					{
						return min(_TessellationMax, max((edgeLength * pow(_ScreenParams.y / (_TessellationEdgeLength * viewDistance), 2)), 1));
					}
					else
					{
						return 1;
					}
				}
				#else
				{
					return 1;
				}
				#endif
			}
			OutputPatchConstant constantsHS(InputPatch<tessellation, 3> patch)
			{
				OutputPatchConstant o;
				float3 p0 = mul(unity_ObjectToWorld, patch[0].vertex).xyz;
				float3 p1 = mul(unity_ObjectToWorld, patch[1].vertex).xyz;
				float3 p2 = mul(unity_ObjectToWorld, patch[2].vertex).xyz;
				o.edge[0] = TessellationEdgeFactor(patch[1], patch[2]);
				o.edge[1] = TessellationEdgeFactor(patch[2], patch[0]);
				o.edge[2] = TessellationEdgeFactor(patch[0], patch[1]);
				o.inside = (o.edge[0] + o.edge[1] + o.edge[2]) * 0.3333;
				float bias = -1;
				#if defined (TESS_ON)
				{
					bias = -0.5 * _displacement_scale - 1;
				}
				#endif
				if (TriangleIsCulled(p0, p1, p2, bias, patch[0].normal))
				{
					o.edge[0] = o.edge[1] = o.edge[2] = o.inside = 1;
				}
				return o;
			}
			
			[domain("tri")]
			[partitioning("fractional_even")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("constantsHS")]
			[outputcontrolpoints(3)]
			tessellation hull_shader(InputPatch<tessellation,3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}
			
			[domain("tri")]
			VertexOutput domain_shader(OutputPatchConstant tessFactors,const OutputPatch<tessellation,3> vs, float3 d:SV_DomainLocation)
			{
				VertexInput v;
				v.vertex = vs[0].vertex * d.x + vs[1].vertex * d.y + vs[2].vertex * d.z;
				v.normal = vs[0].normal * d.x + vs[1].normal * d.y + vs[2].normal * d.z;
				v.uv = vs[0].uv * d.x + vs[1].uv * d.y + vs[2].uv * d.z;
				v.color = vs[0].color * d.x + vs[1].color * d.y + vs[2].color * d.z;
				//v.dist = vs[0].dist * d.x + vs[1].dist * d.y + vs[2].dist * d.z;
				//v.vertex.xyz += ((tex2Dlod(_SurfaceTexture,float4(v.uv * _SurfaceTexture_ST,0.0,0.0)).xyz * normalize(v.normal)) * _displacement_scale);	//mcfucking YEET that shit
				VertexOutput o = vert(v);
				return o;
			}
			float4 pixel_shader(VertexOutput ps) : SV_TARGET
			{
				float cameraDist = distance(_WorldSpaceCameraPos, ps.world_vertex);

				float ZoomLevel = GetZoomLevel(cameraDist);
				int ClampedZoomLevel = ZoomLevel;

				float uvDistortion = pow(2, ClampedZoomLevel - 1);
				float nextUVDist = pow(2, ClampedZoomLevel);
				float percentage = ZoomLevel - ClampedZoomLevel;

				float slope = ps.slope;// abs(dot(normalize(ps.world_vertex - _PlanetOrigin), normalize(mul(unity_ObjectToWorld, ps.normal))));

				float blendLow = heightBlendLow(ps.world_vertex);
				float blendHigh = heightBlendHigh(ps.world_vertex);
				float midPoint = (distance(ps.world_vertex, _PlanetOrigin) - _PlanetRadius) / (_HighStart + _LowEnd);

				fixed4 lowTex;
				fixed4 midTex;
				fixed4 highTex;
				fixed4 steepTex;

				float3 dpdx = ddx(ps.world_vertex);
				float3 dpdy = ddy(ps.world_vertex);
				float3x3 biplanarCoords = GetBiplanarCoordinates(ps.world_vertex , ps.worldNormal);

				lowTex = BiplanarTexture_float(_SurfaceTexture, dpdx, dpdy, uvDistortion, nextUVDist, percentage, _SurfaceTextureUVs, biplanarCoords, ps.world_vertex,  ps.worldNormal, _SurfaceTexture_ST);
				highTex = BiplanarTexture_float(_SurfaceTextureHigh, dpdx, dpdy, uvDistortion, nextUVDist, percentage, _SurfaceTextureUVs, biplanarCoords, ps.world_vertex,  ps.worldNormal, _SurfaceTexture_ST);
				steepTex = BiplanarTexture_float(_SteepTex, dpdx, dpdy, uvDistortion, nextUVDist, percentage, _SurfaceTextureUVs, biplanarCoords, ps.world_vertex,  ps.worldNormal, _SurfaceTexture_ST);
				midTex = BiplanarTexture_float(_SurfaceTextureMid, dpdx, dpdy, uvDistortion, nextUVDist, percentage, _SurfaceTextureUVs, biplanarCoords, ps.world_vertex,  ps.worldNormal, _SurfaceTexture_ST);

				float4 lowNormal;
				float4 midNormal;
				float4 highNormal;
				float4 steepNormal;

				//#if defined (EMISSION_ON)
				//{
				//	lowNormal = EmissiveBiplanarNormal_float(_BumpMap, dpdx, dpdy, uvDistortion, nextUVDist, percentage, _SurfaceTextureUVs, biplanarCoords, ps.world_vertex,  ps.worldNormal, _SurfaceTexture_ST);
				//	highNormal = EmissiveBiplanarNormal_float(_BumpMapHigh, dpdx, dpdy, uvDistortion, nextUVDist, percentage, _SurfaceTextureUVs, biplanarCoords, ps.world_vertex,  ps.worldNormal, _SurfaceTexture_ST);
				//	steepNormal = EmissiveBiplanarNormal_float(_BumpMapSteep, dpdx, dpdy, uvDistortion, nextUVDist, percentage, _SurfaceTextureUVs, biplanarCoords, ps.world_vertex,  ps.worldNormal, _SurfaceTexture_ST);
				//	midNormal = EmissiveBiplanarNormal_float(_BumpMapMid, dpdx, dpdy, uvDistortion, nextUVDist, percentage, _SurfaceTextureUVs, biplanarCoords, ps.world_vertex,  ps.worldNormal, _SurfaceTexture_ST);
				//}
				//#else
				//{
					lowNormal = BiplanarNormal_float(_BumpMap, dpdx, dpdy, uvDistortion, nextUVDist, percentage, _SurfaceTextureUVs, biplanarCoords, ps.world_vertex,  ps.worldNormal, _SurfaceTexture_ST);
					highNormal = BiplanarNormal_float(_BumpMapHigh, dpdx, dpdy, uvDistortion, nextUVDist, percentage, _SurfaceTextureUVs, biplanarCoords, ps.world_vertex,  ps.worldNormal, _SurfaceTexture_ST);
					steepNormal = BiplanarNormal_float(_BumpMapSteep, dpdx, dpdy, uvDistortion, nextUVDist, percentage, _SurfaceTextureUVs, biplanarCoords, ps.world_vertex,  ps.worldNormal, _SurfaceTexture_ST);
					midNormal = BiplanarNormal_float(_BumpMapMid, dpdx, dpdy, uvDistortion, nextUVDist, percentage, _SurfaceTextureUVs, biplanarCoords, ps.world_vertex,  ps.worldNormal, _SurfaceTexture_ST);
					//}
					//#endif


					//return 1;

					float luminosityLow = (lowTex.r * 0.21 + lowTex.g * 0.72 + lowTex.b * 0.07) + 0.5f;
					float luminosityMid = (midTex.r * 0.21 + midTex.g * 0.72 + midTex.b * 0.07) + 0.5f;
					float luminosityHigh = (highTex.r * 0.21 + highTex.g * 0.72 + highTex.b * 0.07) + 0.5f;
					float luminositySteep = (steepTex.r * 0.21 + steepTex.g * 0.72 + steepTex.b * 0.07) + 0.5f;

					fixed4 influenceTex = BiplanarTexture_float(_InfluenceMap, dpdx, dpdy, uvDistortion, nextUVDist, percentage, _SurfaceTextureUVs, biplanarCoords, ps.world_vertex,  ps.worldNormal, _SurfaceTexture_ST);



					lowTex.rgb = lerp(ps.color.rgb * (luminosityLow), lowTex.rgb, influenceTex.r);
					midTex.rgb = lerp(ps.color.rgb * (luminosityMid), midTex.rgb, influenceTex.g);
					highTex.rgb = lerp(ps.color.rgb * (luminosityHigh), highTex.rgb, influenceTex.b);
					steepTex.rgb = lerp(ps.color.rgb * (luminositySteep), steepTex.rgb, influenceTex.a);

					fixed4 surfaceCol = lerpSurfaceColor(lowTex, midTex, highTex, steepTex, midPoint, slope, blendLow, blendHigh);

					float4 surfaceNormal = lerpSurfaceNormal(lowNormal, midNormal, highNormal, steepNormal, midPoint, slope, blendLow, blendHigh);
					float3 normalLighting = surfaceNormal;



					//return normalIntensity;
					//return (normalIntensity);

					UNITY_LIGHT_ATTENUATION(lightAttenuation, ps, ps.world_vertex.xyz);
					float4 lightingData = BlinnPhong(ps.world_vertex, ps.lightDir, surfaceNormal, ps.worldNormal, surfaceCol.a, surfaceCol, lightAttenuation);
					return lightingData;
				
			}
			ENDCG
			}
		
	}
}