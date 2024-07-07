Shader "Custom/ShowDensity"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : NORMAL;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            
            float2 WorldPosToLatLong(float3 pos)
            {
                // Normalize the position to ensure it's on the unit sphere
                pos = normalize(pos);

                // Compute the longitude (theta)
                float longitude = atan2(pos.z, pos.x);  // atan2 returns the angle in radians

                // Compute the latitude (phi)
                float latitude = asin(pos.y);  // asin returns the angle in radians

                // Convert radians to degrees if needed
                float radToDeg = 180.0 / 3.14159265358979323846;
                longitude *= radToDeg;
                latitude *= radToDeg;

                // Return the result as a float2 where x is longitude and y is latitude
                return float2(longitude, latitude);
            }

            float GetMult(float3 worldNormal)
            {
                float3 segments = fmod(worldNormal, 0.5);
                return dot(segments, float3(0, 1, 1));
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.worldNormal = mul(unity_ObjectToWorld, v.normal);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                float2 col = WorldPosToLatLong(i.worldPos);
                //
                float latitude = col.y;
                float longitude = col.x;
                //
                //
                //
                //float latitude2 = abs(fmod(latitude, 35.25)) / 35.25;   //(abs(fmod(latitude, 45.0f)) - 22.5f) / 45;
                //if (abs(latitude) > 35.25)
                //{
                //    latitude2 = 1 - latitude2;
                //}
                //if (latitude > 70.5)
                //{
                //    latitude2 = 0;
                //}
                
                //return latitude2;
                //longitude = 1 - abs((fmod(longitude + 180, 90) / 90) - 0.5) * 2;

                //float3 worldNormal = normalize(i.worldNormal);
                //float mult = GetMult(worldNormal);
                //return mult;

                //return pow((latitude2 + longitude) * 0.5f, 3);
                //return float4(latitude2, longitude, 0, 0);

                float lat = fmod(abs(latitude), 45.0) - 22.5;
                float lon = fmod(abs(longitude), 45.0) - 22.5;   //From -22.5 to 22.5 where 0 we want the highest density and -22.5 we want 1/3 density

                //Now from -1 to 1, with 0 being where we want most density and -1 where we want 1/3
                lat /= 22.5;
                lon /= 22.5;        

                //Now from 0 to 1. 1 when at a corner
                lat = abs(lat);
                lon = abs(lon);    

                float factor = length(float2(lat, lon));//(lat + lon) / 2;
                float multiplier = saturate(lerp(1.0f, 0.333333f, pow(factor, 2.0f)));
                return multiplier;
            }
            ENDCG
        }
    }
}