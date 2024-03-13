Shader "Custom/Wireframe" 
{
	Properties 
	{
		_WireColor("WireColor", Color) = (1,0,0,1)
		_Color("Color", Color) = (1,1,1,1)
	}
	SubShader 
	{
	
	    Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
		Blend SrcAlpha OneMinusSrcAlpha

    	Pass 
    	{
			CGPROGRAM
			#include "UnityCG.cginc"
			#pragma vertex vert
			#pragma geometry geom
			#pragma fragment frag
			
			
			half4 _WireColor, _Color;
		
			struct v2g 
			{
    			float4  pos : SV_POSITION;
    			float2  uv : TEXCOORD0;
				float4 color : COLOR;
			};
			
			struct g2f 
			{
    			float4  pos : SV_POSITION;
    			float2  uv : TEXCOORD0;
    			float3 dist : TEXCOORD1;
				float4 color : COLOR;
			};

			struct appdata
			{
				float3 vertex : POSITION;
				float2 texcoord : TEXCOORD0;
				float4 color : COLOR;
			};

			v2g vert(appdata v)
			{
    			v2g OUT;
    			OUT.pos = UnityObjectToClipPos(v.vertex);
    			OUT.uv = v.texcoord;
				OUT.color = v.color;
    			return OUT;
			}
			
			[maxvertexcount(3)]
			void geom(triangle v2g IN[3], inout TriangleStream<g2f> triStream)
			{
			
				float2 WIN_SCALE = float2(_ScreenParams.x/2.0, _ScreenParams.y/2.0);
				
				//frag position
				float2 p0 = WIN_SCALE * IN[0].pos.xy / IN[0].pos.w;
				float2 p1 = WIN_SCALE * IN[1].pos.xy / IN[1].pos.w;
				float2 p2 = WIN_SCALE * IN[2].pos.xy / IN[2].pos.w;
				
				//barycentric position
				float2 v0 = p2-p1;
				float2 v1 = p2-p0;
				float2 v2 = p1-p0;
				//triangles area
				float area = abs(v1.x*v2.y - v1.y * v2.x);
			
				g2f OUT;
				OUT.pos = IN[0].pos;
				OUT.uv = IN[0].uv;
				OUT.dist = float3(area/length(v0),0,0);
				OUT.color = IN[0].color;
				triStream.Append(OUT);

				OUT.pos = IN[1].pos;
				OUT.uv = IN[1].uv;
				OUT.dist = float3(0,area/length(v1),0);
				OUT.color = IN[1].color;
				triStream.Append(OUT);

				OUT.pos = IN[2].pos;
				OUT.uv = IN[2].uv;
				OUT.color = IN[2].color;
				OUT.dist = float3(0,0,area/length(v2));
				triStream.Append(OUT);
				
			}
			
			half4 frag(g2f IN) : COLOR
			{
				//distance of frag from triangles center
				float d = min(IN.dist.x, min(IN.dist.y, IN.dist.z));
				//fade based on dist from center
 				float I = exp2(-4.0*d*d);
 				
 				return lerp(IN.color, _WireColor, I);				
			}
			
			ENDCG

    	}
	}
}