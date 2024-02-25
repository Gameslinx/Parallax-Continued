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
                o.worldNormal = BARYCENTRIC_INTERPOLATE(worldNormal);
                o.viewDir = BARYCENTRIC_INTERPOLATE(viewDir);

                float3 worldUVs = (o.worldPos - _TerrainShaderOffset) * _Tiling;

                VertexBiplanarParams params;
                GET_VERTEX_BIPLANAR_PARAMS(params, worldUVs, o.worldNormal);

                float terrainDistance = length(o.viewDir);

                float4 displacement = SampleBiplanarTextureLOD(_DisplacementMap, params, worldUVs, o.worldNormal, 0);
                float3 displacedWorldPos = o.worldPos + displacement.g * o.worldNormal * _DisplacementScale;

                o.pos = UnityWorldToClipPos(displacedWorldPos);
            
                return o;
            }

            fixed4 Frag_Shader (Interpolators i) : SV_Target
            {   
                // Calculate scaling params
                float terrainDistance = length(i.viewDir);
                float logDistance = floor(log2(terrainDistance * 0.2 + 4));
                float exponent = pow(2, logDistance);
                float texScale0 = exponent;
                float texScale1 = exponent * 0.5;

                float3 worldUVsLevel0 = (i.worldPos - _TerrainShaderOffset) * _Tiling / texScale0;
                float3 worldUVsLevel1 = (i.worldPos - _TerrainShaderOffset) * _Tiling / texScale1;
                
                float3 viewDir = normalize(i.viewDir);

                PixelBiplanarParams params;
                GET_PIXEL_BIPLANAR_PARAMS(params, worldUVsLevel0, worldUVsLevel1, i.worldNormal);
                
                fixed4 col = SampleBiplanarTexture(_MainTex, params, worldUVsLevel0, worldUVsLevel1, i.worldNormal, 0);
                float3 normal = SampleBiplanarNormal(_BumpMap, params, worldUVsLevel0, worldUVsLevel1, i.worldNormal, 0);

                float3 result = CalculateLighting(col, normal, viewDir);
                return float4(result, 1);
            }
            ENDCG
        }
    }
}