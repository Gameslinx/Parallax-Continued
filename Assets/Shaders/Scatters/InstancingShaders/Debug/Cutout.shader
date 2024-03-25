﻿Shader "Custom/InstancedCutout" {
    Properties
    {
        _MainTex("Main Tex", 2D) = "white" {}
        _BumpMap("Bump Map", 2D) = "bump" {}
        _Color("Color", COLOR) = (0,0,0)
        _Cutoff("_Cutoff", Range(0, 1)) = 0.5
        _MaxBrightness("_MaxBrightness", float) = 1
        _WindMap("_WindMap", 2D) = "white" {}
        _WorldSize("_WorldSize", vector) = (0,0,0)
        _WindSpeed("Wind Speed", vector) = (1, 1, 1, 1)
        _WaveSpeed("Wave Speed", float) = 1.0
        _WaveAmp("Wave Amp", float) = 1.0
        _HeightCutoff("Height Cutoff", Range(-1, 1)) = -100
        _HeightFactor("HeightFactor", Range(0, 4)) = 1
        _Metallic("_Metallic", Range(0.001, 100)) = 1
        _Hapke("_Hapke", Range(0.3, 5)) = 1
        _MetallicTint("_MetallicTint", COLOR) = (1,1,1)
        _Gloss("_Gloss", Range(0, 250)) = 0
        _ShaderOffset("_ShaderOffset", vector) = (0,0,0)
        _DitherFactor("_DitherFactor", Range(0, 1)) = 1
        _InitialTime("_InitialTime", float) = 0
        _CurrentTime("_CurrentTime", float) = 0
        _FresnelPower("_FresnelPower", Range(0.001, 20)) = 1
		_FresnelColor("_FresnelColor", COLOR) = (0,0,0)
        _Transmission("_Transmission", Range(-1.0, 1.0)) = 0.0
    }
    SubShader
    {
        ZWrite On
        Cull Off
        Tags {"Queue" = "AlphaTest" "IgnoreProjector" = "True" "RenderType" = "TransparentCutout"}
        Pass
        {
            Tags{ "LightMode" = "ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
#pragma multi_compile ATMOSPHERE

#include "UnityCG.cginc"
#include "AutoLight.cginc"
#include "Lighting.cginc"

float3 _PlanetOrigin;
float3 _ShaderOffset;
float3 _WindSpeed;
float _WaveAmp;
float _HeightCutoff;
float _HeightFactor;
float _WaveSpeed;
sampler2D _WindMap;

sampler2D _MainTex;
float2 _MainTex_ST;
sampler2D _BumpMap;

float _DitherFactor;
float _InitialTime;
float _CurrentTime;

float _Transmission;
float _Hapke = 1;
float _Gloss = 1;
float _Metallic = 1;
float3 _MetallicTint;
float4 _Color;

float3 _SunDir;

struct GrassData
{
    float4x4 mat;
};

StructuredBuffer<GrassData> _InstanceData;

float3 Wind(float4x4 mat, float3 world_vertex, float localVertexHeight)
{
    float3 bf = normalize(abs(normalize(world_vertex - _PlanetOrigin)));
    bf /= dot(bf, (float3) 1);
    float2 xz = world_vertex.zx * bf.y;
    float2 xy = world_vertex.xy * bf.z;
    float2 zy = world_vertex.yz * bf.x;
                
    float2 samplePosXZ = xz;
    samplePosXZ += _Time.x * _WindSpeed.xz;
    samplePosXZ = (samplePosXZ) * _WaveAmp;
                
    float2 samplePosXY = xy;
    samplePosXY += _Time.x * _WindSpeed.xy;
    samplePosXY = (samplePosXY) * _WaveAmp;
                
    float2 samplePosZY = zy;
    samplePosZY += _Time.x * _WindSpeed.zy;
    samplePosZY = (samplePosZY) * _WaveAmp;
                
    float2 wind = (samplePosXZ + samplePosXY + samplePosZY) / 3;
                
    float heightFactor = localVertexHeight > _HeightCutoff;
    heightFactor = heightFactor * pow(localVertexHeight, _HeightFactor);
    if (localVertexHeight < 0)
    {
        heightFactor = 0;
    }
                
    float2 windSample = -tex2Dlod(_WindMap, float4(wind, 0, 0));
    
    float3 positionOffset = mul(mat, float3(windSample.x, 0, windSample.y));
                
    return sin(_WaveSpeed * positionOffset) * heightFactor;
}
float InterleavedGradientNoise(float alpha, float2 uv)
{
    float timeLim = alpha + 0.75;
    float ditherFactor = (_Time.y - alpha) / (timeLim - alpha);
    return frac(sin(dot(uv / 10, float2(12.9898, 78.233))) * 43758.5453123) > ditherFactor;
}
float4 BlinnPhongAlbedo(float3 normal, float3 basicNormal, float3 terrainNormal, float4 diffuseCol, float3 lightDir, float3 viewDir, float3 attenCol)
{

    half3 halfDir = normalize(lightDir + viewDir);

    // Dot
    half NdotL = max(0, dot(normal, lightDir));
    NdotL = pow(NdotL, _Hapke);

    // Fake shadows from terrain
    NdotL *= saturate(dot(terrainNormal, normalize(_WorldSpaceLightPos0)) * 4);

    half NdotH = max(0, dot(normal, halfDir)); //
    // Color
    fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb * diffuseCol.rgb;
    fixed3 diffuse =  attenCol * diffuseCol.rgb * NdotL;
    fixed3 specular = pow(NdotH, _Gloss * diffuseCol.a) * _Metallic * diffuseCol.a;
    
    float angle = saturate(dot(normalize(basicNormal), _WorldSpaceLightPos0));
    angle = 1 - pow(1 - angle, 7);
    specular *= saturate(angle - 0.2);
    
    specular = specular * _MetallicTint.rgb;
    fixed4 color = fixed4(ambient + diffuse + specular, 1.0);

    return color;
}
float4 BlinnPhongLight(float3 normal, float3 basicNormal, float4 diffuseCol, float3 lightDir, float3 viewDir, float3 attenCol)
{

    half3 halfDir = normalize(lightDir + viewDir);

    // Dot
    half NdotL = max(0, dot(normal, lightDir));
    NdotL = pow(NdotL, _Hapke);

    half NdotH = max(0, dot(normal, halfDir)); //
    // Color
    fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb * diffuseCol.rgb;
    fixed3 diffuse =  attenCol * diffuseCol.rgb * NdotL;
    fixed3 specular = pow(NdotH, _Gloss * diffuseCol.a) * _Metallic * diffuseCol.a;
    
    float angle = saturate(dot(normalize(basicNormal), _WorldSpaceLightPos0));
    angle = 1 - pow(1 - angle, 7);
    specular *= saturate(angle - 0.2);
    
    specular = specular * _MetallicTint.rgb;
    fixed4 color = fixed4(ambient + diffuse + specular, 1.0);

    return color;
}
float3 Fresnel(float3 normal, float3 viewDir, float smoothness, float3 color)
{
    float fresnel = dot(normal, viewDir);
    fresnel = saturate(1 - fresnel);
    fresnel = pow(fresnel, smoothness);
    return fresnel * color;
}
void Billboard(inout float4 vertex, float4x4 mat)
{
    float4x4 localMat = mul(mat, unity_WorldToObject);

    const float3 local = float3(vertex.x, vertex.y, vertex.z); // this is the quad verts as generated by MakeMesh.cs in the localPos list.
    const float3 offset = 0;//vertex.xyz - local;
    
    const float3 upVector = float3(0, 1, 0);
    const float3 forwardVector = mul(UNITY_MATRIX_IT_MV[2].xyz, localMat); // camera forward   
    const float3 rightVector = normalize(cross(forwardVector, upVector));
 
    float3 position = 0;
    position += local.x * rightVector;
    position += local.y * upVector;
    position += local.z * forwardVector;
 
    const float3x3 rotMat = float3x3(upVector, forwardVector, rightVector);
    
    vertex = float4(offset + position, 1);
}
#define PARALLAX_LIGHT_ATTENUATION(v2f) attenuation = LIGHT_ATTENUATION(v2f); attenuation = 1 - pow(1 - attenuation, 3);
#define PARALLAX_UP_VECTOR(mat) o.up = normalize(mul(mat, float3(0, 1, 0)) - mul(mat, float3(0, 0, 0)));

struct appdata_t
{
    float4 vertex : POSITION;
    fixed4 color : COLOR;
    float2 uv : TEXCOORD0;
    float3 normal : NORMAL;
    float4 tangent : TANGENT;
};
struct v2f
{
    float4 pos : SV_POSITION;
    fixed4 color : COLOR;
    float2 uv : TEXCOORD0;
    float3 normal : NORMAL;
    float3 worldNormal : TEXCOORD1;
    float3 world_vertex : TEXCOORD2;

    float3 tangentWorld : TEXCOORD6;
    float3 binormalWorld : TEXCOORD7;

    float3 viewDir : TEXCOORD8;
    float3 lightDir : TEXCOORD9;
    
    float3 up : TEXCOORD11;
    LIGHTING_COORDS(3, 4)
};
struct v2f_lighting
{
    float4 pos : SV_POSITION;
    fixed4 color : COLOR;
    float2 uv : TEXCOORD0;
    float3 normal : NORMAL;
    float3 worldNormal : TEXCOORD1;
    float3 world_vertex : TEXCOORD2;

    float3 tangentWorld : TEXCOORD6;
    float3 binormalWorld : TEXCOORD7;

    float3 viewDir : TEXCOORD8;
    float3 lightDir : TEXCOORD9;
};
struct shadow_appdata_t
{
    float4 vertex : POSITION;
    float2 uv : TEXCOORD0;
};
struct shadow_v2f
{
    float4 pos : SV_POSITION;
    float2 uv : TEXCOORD0;
    float4 color : COLOR;
};

            
            float _Cutoff;
            float3 _FresnelColor;
            float _FresnelPower;

            v2f vert(appdata_t i, uint instanceID: SV_InstanceID) {
                v2f o;
                UNITY_INITIALIZE_OUTPUT(v2f, o);

                float4x4 mat = _InstanceData[instanceID].mat;
                float4 pos = mul(mat, i.vertex) + float4(_ShaderOffset, 0);
                float3 world_vertex = pos.xyz;

                // Get the terrain normal from the transformation matrix

                pos.xyz += Wind(mat, world_vertex, i.vertex.y);

                o.pos = UnityObjectToClipPos(pos);
                o.color = 1;//_InstanceData[instanceID].color;

                o.uv = i.uv;
                o.normal = i.normal;
                o.worldNormal = normalize(mul(mat, i.normal));
                o.world_vertex = world_vertex; 
                o.tangentWorld = normalize(mul(mat, i.tangent).xyz);
                o.binormalWorld = normalize(cross(o.worldNormal, o.tangentWorld));

                o.viewDir =  normalize(_WorldSpaceCameraPos.xyz - o.world_vertex.xyz);
                o.lightDir = normalize(_WorldSpaceLightPos0.xyz);

                o.up = PARALLAX_UP_VECTOR(mat);
                TRANSFER_VERTEX_TO_FRAGMENT(o);
                return o;
            }

            fixed4 frag(v2f i, float facing : VFACE) : SV_Target
            {

                float faceSign = ( facing >= 0 ? 1 : -1 );
                i.worldNormal *= lerp(faceSign, 1, _Transmission);  //Get subtle shading. Transmission of 1 means lighting on both sides of face

                float4 col = tex2D(_MainTex, i.uv * _MainTex_ST) * float4(i.color.rgb, 1) * _Color;
                clip(col.a - _Cutoff);

                float3 normalMap = UnpackNormal(tex2D(_BumpMap, i.uv * _MainTex_ST));
                float3x3 TBN = float3x3(normalize(i.tangentWorld), normalize(i.binormalWorld), i.worldNormal);
                TBN = transpose(TBN);
                float3 worldNormal = mul(TBN, normalMap);

                float attenuation = PARALLAX_LIGHT_ATTENUATION(i);
                float3 attenColor = attenuation * _LightColor0.rgb;

                //float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.world_vertex.xyz);
                float4 color = BlinnPhongAlbedo(worldNormal, i.worldNormal, i.up, col, i.lightDir, i.viewDir, attenColor);
                float3 fresnelCol = Fresnel(worldNormal, normalize(i.viewDir), _FresnelPower, _FresnelColor) * saturate(dot(i.worldNormal, _WorldSpaceLightPos0)) * attenColor;
                color.rgb += fresnelCol;
                //return 0;
                return float4(color);
            }

            ENDCG
        }
    }
    Fallback "Cutout"
}