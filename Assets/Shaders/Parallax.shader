Shader "Custom/Parallax"
{
    Properties
    {
        [Header(Tessellation Settings)]
        [Space(10)]
        _MaxTessellation("Max Tessellation", Range(1, 64)) = 1
        _TessellationEdgeLength("Tessellation Edge Length (Pixels)", Range(0.01, 100)) = 1

        [Space(10)]
        [Header(Textures)]
        [Space(10)]
        _MainTex("Texture", 2D) = "white" {}
        _BumpMap("Bump Map", 2D) = "bump" {}
        _DisplacementMap("Displacement Map", 2D) = "black" {}

        [Space(10)]
        [Header(Texture Parameters)]
        [Space(10)]
        _Tiling("Texture Tiling", Range(0.001, 30)) = 0.2
        _DisplacementScale("Displacement Scale", Range(0, 0.3)) = 0
        _BiplanarBlendFactor("Biplanar Blend Factor", Range(0.01, 8)) = 1

        [Space(10)]
        [Header(Lighting Parameters)]
        [Space(10)]
        [PowerSlider(3.0)]
        _SpecularPower("Specular Power", Range(0.001, 1000)) = 1
        _SpecularIntensity("Specular Intensity", Range(0.0, 5.0)) = 1
        _FresnelPower("Fresnel Power", Range(0.001, 20)) = 1
        _EnvironmentMapFactor("Environment Map Factor", Range(0.0, 2.0)) = 1
        _RefractionIntensity("Refraction Intensity", Range(0, 2)) = 1

        [Space(10)]
        [Header(Other Params)]
        [Space(10)]
        _TerrainShaderOffset("Terrain Shader Offset", VECTOR) = (0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex Vertex_Shader
            #pragma hull Hull_Shader
            #pragma domain Domain_Shader
            #pragma fragment Frag_Shader
            
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "ParallaxVariables.cginc"
            #include "ParallaxUtils.cginc"

            // Input
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            // Vertex to hull shader
            struct TessellationControlPoint
            {
                float4 pos : SV_POSITION;
                float3 worldPos : INTERNALTESSPOS;
                float3 worldNormal : NORMAL;
                float3 viewDir : TEXCOORD1;
            };

            // Patch constant function
            struct TessellationFactors
            {
                float edge[3] : SV_TessFactor;
                float inside : SV_InsideTessFactor;
            };

            // Interpolators to frag shader
            struct Interpolators
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : NORMAL;
                float3 viewDir : TEXCOORD1;

                float3 wPos1 : TEXCOORD2;
                float3 wPos2 : TEXCOORD3;
                float blend : TEXCOORD4;
            };
            
            // Do expensive shit here!!
            // Before tessellation!!
            TessellationControlPoint Vertex_Shader (appdata v)
            {
                TessellationControlPoint o;

                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.worldNormal = normalize(mul(unity_ObjectToWorld, v.normal).xyz);
                o.viewDir = _WorldSpaceCameraPos - o.worldPos;
                return o;
            }

            TessellationFactors PatchConstantFunction(InputPatch<TessellationControlPoint, 3> patch) 
            {
                TessellationFactors f;

                if (ShouldClipPatch(patch[0].pos, patch[1].pos, patch[2].pos, patch[0].worldNormal, patch[1].worldNormal, patch[2].worldNormal, patch[0].worldPos, patch[1].worldPos, patch[2].worldPos))
                {
                    // Cull the patch - This should be set to 1 in the shadow caster
                    f.edge[0] = f.edge[1] = f.edge[2] = f.inside = 0;
                } 
                else 
                {
                    float tessFactor0 =  EdgeTessellationFactor(_TessellationEdgeLength.x, 0, patch[1].worldPos, patch[1].pos, patch[2].worldPos, patch[2].pos);
                    float tessFactor1 =  EdgeTessellationFactor(_TessellationEdgeLength.x, 0, patch[2].worldPos, patch[2].pos, patch[0].worldPos, patch[0].pos);
                    float tessFactor2 =  EdgeTessellationFactor(_TessellationEdgeLength.x, 0, patch[0].worldPos, patch[0].pos, patch[1].worldPos, patch[1].pos);

                    f.edge[0] = min(tessFactor0, _MaxTessellation);
                    f.edge[1] = min(tessFactor1, _MaxTessellation);
                    f.edge[2] = min(tessFactor2, _MaxTessellation);
                    f.inside  = min((tessFactor0 + tessFactor1 + tessFactor2) * 0.333f, _MaxTessellation);
                }
                return f;
            }

            [domain("tri")]
            [outputcontrolpoints(3)]
            [outputtopology("triangle_cw")]
            [patchconstantfunc("PatchConstantFunction")]
            [partitioning("fractional_odd")]
            TessellationControlPoint Hull_Shader(InputPatch<TessellationControlPoint, 3> patch, uint id : SV_OutputControlPointID)
            {
                return patch[id];
            }

            // Domain shader
            [domain("tri")]
            Interpolators Domain_Shader(TessellationFactors factors, OutputPatch<TessellationControlPoint, 3> patch, float3 barycentricCoordinates : SV_DomainLocation)
            {
                Interpolators o;

                o.worldPos = BARYCENTRIC_INTERPOLATE(worldPos);
                o.worldNormal = normalize(BARYCENTRIC_INTERPOLATE(worldNormal));
                o.viewDir = BARYCENTRIC_INTERPOLATE(viewDir);

                float terrainDistance = length(o.viewDir);
                DO_WORLD_UV_CALCULATIONS(terrainDistance / 5, o.worldPos)

                VertexBiplanarParams params;
                GET_VERTEX_BIPLANAR_PARAMS(params, worldUVs, o.worldNormal);

                o.wPos1 = worldUVsLevel0;
                o.wPos2 = worldUVsLevel1;
                //float3 worldUVsLevel0

                float4 displacement = SampleBiplanarTextureLOD(_DisplacementMap, params, worldUVsLevel0, worldUVsLevel1, o.worldNormal, texLevelBlend);
                float3 displacedWorldPos = o.worldPos + displacement.g * o.worldNormal * _DisplacementScale;

                o.blend = displacement.g;
                o.pos = UnityWorldToClipPos(displacedWorldPos);
            
                return o;
            }

            fixed4 Frag_Shader (Interpolators i) : SV_Target
            {   
                i.worldNormal = normalize(i.worldNormal);
                // Calculate scaling params
                float terrainDistance = length(i.viewDir);
                //terrainDistance = min(terrainDistance, 5);
                // Retrieve UV levels for texture sampling
                DO_WORLD_UV_CALCULATIONS( terrainDistance / 5, i.worldPos)
                
                float3 viewDir = normalize(i.viewDir);

                PixelBiplanarParams params;
                GET_PIXEL_BIPLANAR_PARAMS(params, i.worldPos, worldUVsLevel1, i.worldNormal, texScale0, texScale1, terrainDistance * PARALLAX_SHARPENING_FACTOR);

                fixed4 col = SampleBiplanarTexture(_MainTex, params, worldUVsLevel0, worldUVsLevel1, i.worldNormal, texLevelBlend);
                float3 normal = SampleBiplanarNormal(_BumpMap, params, worldUVsLevel0, worldUVsLevel1, i.worldNormal, texLevelBlend);

                float3 result = CalculateLighting(col, normal, viewDir);

                //float3 wpos2 = worldUVsLevel0 + dpdy1;

                //return float4(params.dpdx0 * 5 + params.dpdy0 * 5, 1);

                //params.dpdx0 = ddx(i.worldPos) * (_Tiling / texScale0);
                //params.dpdy0 = ddy(i.worldPos) * (_Tiling / texScale0);

                //params.dpdx0 = ddx(worldUVsLevel0);
                //params.dpdy0 = ddy(worldUVsLevel0);

                //float calibratedMipLevel = floor(GetMipLevel(worldUVsLevel0 * 4096.0f, params.dpdx0, params.dpdy0)) / 5;
                //return calibratedMipLevel;
                //return GetMipLevel()

                //col = lerp(tex2D(_MainTex, worldUVsLevel0.zx), tex2D(_MainTex, worldUVsLevel1.zx), texLevelBlend);
                //normal = lerp(UnpackNormal(tex2D(_BumpMap, worldUVsLevel0.zx)), UnpackNormal(tex2D(_BumpMap, worldUVsLevel1.zx)), texLevelBlend);
                //normal.rgb = normal.rbg;
                
                //return float4(worldUVsLevel0, 1);

                //return i.blend;

                return col * dot(normal, _WorldSpaceLightPos0);
                //return float4(test + test2, 1);
                return float4(result, 1);
            }
            ENDCG
        }
    }
}