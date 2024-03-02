
#include "AutoLight.cginc"

float3 _PlanetOrigin;
float _PlanetRadius;
float _LowStart;
float _LowEnd;
float _HighStart;
float _HighEnd;
//float3 _WorldSpaceLightPos0;
float3 _MetallicTint;
float _Metallic;
float3 _LightColor0;
float4 _ReflectionMask;
float _NormalSpecularInfluence;
float _HasEmission;
float _Hapke;
float _Gloss;
float _BumpStrength;
float _SteepContrast;
float _SteepMidpoint;

float zoom1 = 20;
float zoom2 = 60;
float zoom3 = 140;
float zoom4 = 250;
float zoom5 = 600;
float zoom6 = 1200;
float zoom7 = 2400;
float zoom8 = 3200;
float zoom9 = 4600;
float zoom10 = 7800;
float zoom11 = 10000;
float zoom12 = 12000;
//float3 _LightPos;

float3 lerpReflectionsLow(float slope, float3 reflColor, float3 specColor)
{
	float3 col;
	col = lerp(reflColor * _ReflectionMask.r, reflColor * _ReflectionMask.a, 1 - slope);
	return col;
}
float Fresnel(float3 normal, float3 viewDir, float smoothness)
{
    float fresnel = dot(normal, viewDir);
    fresnel = saturate(1 - fresnel);
    fresnel = pow(fresnel, smoothness);
    return fresnel;
}
float3 lerpReflections(float midPoint, float slope, float blendLow, float blendHigh, float3 reflColor, float3 specColor)
{
	float3 col;
	if (midPoint < 0.5)
	{
		col = lerp(reflColor * _ReflectionMask.r, reflColor * _ReflectionMask.g, 1 - blendLow);
	}
	else
	{
		col = lerp(reflColor * _ReflectionMask.g, reflColor * _ReflectionMask.b, blendHigh);
	}
	col = lerp(col, reflColor * _ReflectionMask.a, 1 - slope);
	return col;
}
fixed4 lerpSurfaceColor(fixed4 low, fixed4 mid, fixed4 high, fixed4 steep, float midPoint, float slope, float blendLow, float blendHigh)
{
	fixed4 col;
	if (midPoint < 0.5)
	{
		col = lerp(low, mid, 1 - blendLow);
	}
	else
	{
		col = lerp(mid, high, blendHigh);
		
	}
	col = lerp(col, steep, 1 - slope);
	return col;
}
float heightBlendLow(float3 worldPos)
{
	float terrainHeight = distance(worldPos, _PlanetOrigin) - _PlanetRadius;

	float blendLow = saturate((terrainHeight - _LowEnd) / (_LowStart - _LowEnd));
	return blendLow;
}
float heightBlendHigh(float3 worldPos)
{
	float terrainHeight = distance(worldPos, _PlanetOrigin) - _PlanetRadius;

	float blendHigh = saturate((terrainHeight - _HighStart) / (_HighEnd - _HighStart));
	return blendHigh;
}
float HeightBlendDOUBLE(float3 worldPos)	//Uses LOW
{
	float terrainHeight = distance(worldPos, _PlanetOrigin) - _PlanetRadius;

	float blendLow = saturate((terrainHeight - _LowEnd) / (_LowStart - _LowEnd));
	return blendLow;
}
float CalculateSlope(float slope)
{
	slope = saturate((slope - _SteepMidpoint) * _SteepContrast + _SteepMidpoint);
	return slope;
}
fixed4 SampleDisplacementBiplanarTexture(sampler2D sam, float3 p, float3 n, float2 scale, float3 surfaceUVs, float3x3 biplanarCoords, float3 dpdx, float3 dpdy, float UVDistortion, float nextUVDist, float percentage, float slope)
{
    n = normalize(abs(n));
    float blendLow = heightBlendLow(p);
	float blendHigh = heightBlendHigh(p);
	float midPoint = (distance(p, _PlanetOrigin) - _PlanetRadius) / (_HighStart + _LowEnd);


	float4 x = tex2Dlod(sam, float4(float2(p[biplanarCoords[0].y] * scale.x - surfaceUVs[biplanarCoords[0].y], p[biplanarCoords[0].z] * scale.y - surfaceUVs[biplanarCoords[0].z]) / UVDistortion, 0, 0));
	float4 y = tex2Dlod(sam, float4(float2(p[biplanarCoords[2].y] * scale.x - surfaceUVs[biplanarCoords[2].y], p[biplanarCoords[2].z] * scale.y - surfaceUVs[biplanarCoords[2].z]) / UVDistortion, 0, 0));

	float4 x1 = tex2Dlod(sam, float4(float2(p[biplanarCoords[0].y] * scale.x - surfaceUVs[biplanarCoords[0].y], p[biplanarCoords[0].z] * scale.y - surfaceUVs[biplanarCoords[0].z]) / nextUVDist, 0, 0));
	float4 y1 = tex2Dlod(sam, float4(float2(p[biplanarCoords[2].y] * scale.x - surfaceUVs[biplanarCoords[2].y], p[biplanarCoords[2].z] * scale.y - surfaceUVs[biplanarCoords[2].z]) / nextUVDist, 0, 0));

	float4 n1 = lerp(x, x1, percentage);
	float4 n2 = lerp(y, y1, percentage);
	float2 w = float2(n[biplanarCoords[0].x], n[biplanarCoords[2].x]);

	w = saturate((w - 0.5773) / (1 - 0.5773));

	// Blending
	float4 finalCol = ((n1 * w.x + n2 * w.y) / (w.x + w.y));
	fixed4 finalColLow = finalCol.r;
	fixed4 finalColMid = finalCol.g;
	fixed4 finalColHigh = finalCol.b;
	fixed4 finalColSteep = finalCol.a;

	fixed4 displacement = lerpSurfaceColor(finalColLow, finalColMid, finalColHigh, finalColSteep, midPoint, slope, blendLow, blendHigh);
	return displacement;
}
fixed4 SampleSingleSteepTexture(sampler2D sam, float3 p, float3 n, float k, float2 scale, float3 surfaceUVs, float3x3 biplanarCoords, float3 dpdx, float3 dpdy, float UVDistortion, float nextUVDist, float percentage, float slope)
{
    n = normalize(abs(n));


	float4 x = tex2Dlod(sam, float4(float2(p[biplanarCoords[0].y] * scale.x - surfaceUVs[biplanarCoords[0].y], p[biplanarCoords[0].z] * scale.y - surfaceUVs[biplanarCoords[0].z]) / UVDistortion, 0, 0));
	float4 y = tex2Dlod(sam, float4(float2(p[biplanarCoords[2].y] * scale.x - surfaceUVs[biplanarCoords[2].y], p[biplanarCoords[2].z] * scale.y - surfaceUVs[biplanarCoords[2].z]) / UVDistortion, 0, 0));

	float4 x1 = tex2Dlod(sam, float4(float2(p[biplanarCoords[0].y] * scale.x - surfaceUVs[biplanarCoords[0].y], p[biplanarCoords[0].z] * scale.y - surfaceUVs[biplanarCoords[0].z]) / nextUVDist, 0, 0));
	float4 y1 = tex2Dlod(sam, float4(float2(p[biplanarCoords[2].y] * scale.x - surfaceUVs[biplanarCoords[2].y], p[biplanarCoords[2].z] * scale.y - surfaceUVs[biplanarCoords[2].z]) / nextUVDist, 0, 0));

	float4 n1 = lerp(x, x1, percentage);
	float4 n2 = lerp(y, y1, percentage);
	float2 w = float2(n[biplanarCoords[0].x], n[biplanarCoords[2].x]);

	w = saturate((w - 0.5773) / (1 - 0.5773));

	// Blending
	float4 finalCol = ((n1 * w.x + n2 * w.y) / (w.x + w.y));
	fixed4 finalColLow = finalCol.r;
	fixed4 finalColMid = finalCol.g;
	fixed4 finalColHigh = finalCol.b;
	fixed4 finalColSteep = finalCol.a;

	fixed4 displacement = lerp(finalColMid, finalColSteep, 1 - slope);
	return displacement;
}
bool TriangleIsBelowClipPlane(
	float3 p0, float3 p1, float3 p2, int planeIndex, float bias, float3 normal
) {
	float4 plane = mul(unity_WorldToObject, normal);//
	plane = unity_CameraWorldClipPlanes[planeIndex];
	return
		dot(float4(p0, 1), plane) < bias &&
		dot(float4(p1, 1), plane) < bias &&
		dot(float4(p2, 1), plane) < bias;
	
}

bool TriangleIsCulled(float3 p0, float3 p1, float3 p2, float bias, float3 normal) {
	return
		TriangleIsBelowClipPlane(p0, p1, p2, 0, bias, normal) ||
		TriangleIsBelowClipPlane(p0, p1, p2, 1, bias, normal) ||
		TriangleIsBelowClipPlane(p0, p1, p2, 2, bias, normal) ||
		TriangleIsBelowClipPlane(p0, p1, p2, 3, bias, normal);
}
float3x3 GetBiplanarCoordinates(float3 p, float3 n)
{
	// grab coord derivatives for texturing
    
    n = abs(n);



	 //Major axis (in x; yz are following axis)
	uint3 ma = (n.x > n.y && n.x > n.z) ? uint3(0, 1, 2) :
		(n.y > n.z) ? uint3(1, 2, 0) :
		uint3(2, 0, 1);
	
	// Minor axis (in x; yz are following axis)
	uint3 mi = (n.x < n.y&& n.x < n.z) ? uint3(0, 1, 2) :
		(n.y < n.z) ? uint3(1, 2, 0) :
		uint3(2, 0, 1);
	
	// Median axis (in x; yz are following axis)
	uint3 me = 3 - mi - ma;
	return float3x3(ma, mi, me);
	//We do not need to return mi, but i want a fucking square matrix lad
}
float4 BiplanarNormal_float
(sampler2D tex, float3 dpdx, float3 dpdy, float UVDistortion, float nextUVDist, float percentage, float3 surfaceUVs, float3x3 biplanarCoords, float3 wpos, float3 wnrm, float2 scale)
{
	// Coordinate derivatives for texturing
	float3 p = wpos;
	float3 n = abs(wnrm);
	//float3 dpdx = ddx(p);
	//float3 dpdy = ddy(p);

	uint3 ma = biplanarCoords[0];
	uint3 me = biplanarCoords[2];

	float4 x = tex2D(tex, float2(p[ma.y] * scale.x - surfaceUVs[ma.y], p[ma.z] * scale.y - surfaceUVs[ma.z]) / UVDistortion, float2(dpdx[biplanarCoords[0].y], dpdx[biplanarCoords[0].z]) * (scale / UVDistortion), float2(dpdy[biplanarCoords[0].y], dpdy[biplanarCoords[0].z]) * (scale / UVDistortion));
	float4 y = tex2D(tex, float2(p[me.y] * scale.x - surfaceUVs[me.y], p[me.z] * scale.y - surfaceUVs[me.z]) / UVDistortion, float2(dpdx[biplanarCoords[0].y], dpdx[biplanarCoords[0].z]) * (scale / UVDistortion), float2(dpdy[biplanarCoords[0].y], dpdy[biplanarCoords[0].z]) * (scale / UVDistortion));

	float4 x1 = tex2D(tex, float2(p[ma.y] * scale.x - surfaceUVs[ma.y], p[ma.z] * scale.y - surfaceUVs[ma.z]) / nextUVDist, float2(dpdx[biplanarCoords[0].y], dpdx[biplanarCoords[0].z]) * (scale / nextUVDist), float2(dpdy[biplanarCoords[0].y], dpdy[biplanarCoords[0].z]) * (scale / nextUVDist));
	float4 y1 = tex2D(tex, float2(p[me.y] * scale.x - surfaceUVs[me.y], p[me.z] * scale.y - surfaceUVs[me.z]) / nextUVDist, float2(dpdx[biplanarCoords[0].y], dpdx[biplanarCoords[0].z]) * (scale / nextUVDist), float2(dpdy[biplanarCoords[0].y], dpdy[biplanarCoords[0].z]) * (scale / nextUVDist));

	

	float3 n1 = UnpackNormal(x);
	float3 n2 = UnpackNormal(y);
	float3 n3 = UnpackNormal(x1);
	float3 n4 = UnpackNormal(y1);
	//n1.g = -n1.g;
	//n2.g = -n2.g;
	//n3.g = -n3.g;
	//n4.g = -n4.g;
	n1 = lerp(n1, n3, percentage);
	n2 = lerp(n2, n4, percentage);
	n1 = normalize(float3(n1.y + wnrm[ma.z], n1.x + wnrm[ma.y], wnrm[ma.x]));
	n2 = normalize(float3(n2.y + wnrm[me.z], n2.x + wnrm[me.y], wnrm[me.x]));
	n1 = float3(n1[ma.z], n1[ma.y], n1[ma.x]);
	n2 = float3(n2[me.z], n2[me.y], n2[me.x]);
	
	// Blend factors
	float2 w = float2(n[ma.x], n[me.x]);
	// Make local support
	w = saturate((w - 0.5773) / (1 - 0.5773));
	// Blending
	float3 output = normalize((n1 * w.x + n2 * w.y) / (w.x + w.y));	//This is the normal map after biplanar sampling

	return float4((output), 1);
}
float4 EmissiveBiplanarNormal_float
(sampler2D tex, float3 dpdx, float3 dpdy, float UVDistortion, float nextUVDist, float percentage, float3 surfaceUVs, float3x3 biplanarCoords, float3 wpos, float3 wnrm, float2 scale)
{
	// Coordinate derivatives for texturing
	float3 p = wpos;
	float3 n = abs(wnrm);
	//float3 dpdx = ddx(p);
	//float3 dpdy = ddy(p);

	uint3 ma = biplanarCoords[0];
	uint3 me = biplanarCoords[2];

	float4 x = tex2D(tex, float2(p[ma.y] * scale.x - surfaceUVs[ma.y], p[ma.z] * scale.y - surfaceUVs[ma.z]) / UVDistortion, float2(dpdx[biplanarCoords[0].y], dpdx[biplanarCoords[0].z]) * (scale / UVDistortion), float2(dpdy[biplanarCoords[0].y], dpdy[biplanarCoords[0].z]) * (scale / UVDistortion));
	float4 y = tex2D(tex, float2(p[me.y] * scale.x - surfaceUVs[me.y], p[me.z] * scale.y - surfaceUVs[me.z]) / UVDistortion, float2(dpdx[biplanarCoords[0].y], dpdx[biplanarCoords[0].z]) * (scale / UVDistortion), float2(dpdy[biplanarCoords[0].y], dpdy[biplanarCoords[0].z]) * (scale / UVDistortion));

	float4 x1 = tex2D(tex, float2(p[ma.y] * scale.x - surfaceUVs[ma.y], p[ma.z] * scale.y - surfaceUVs[ma.z]) / nextUVDist, float2(dpdx[biplanarCoords[0].y], dpdx[biplanarCoords[0].z]) * (scale / nextUVDist), float2(dpdy[biplanarCoords[0].y], dpdy[biplanarCoords[0].z]) * (scale / nextUVDist));
	float4 y1 = tex2D(tex, float2(p[me.y] * scale.x - surfaceUVs[me.y], p[me.z] * scale.y - surfaceUVs[me.z]) / nextUVDist, float2(dpdx[biplanarCoords[0].y], dpdx[biplanarCoords[0].z]) * (scale / nextUVDist), float2(dpdy[biplanarCoords[0].y], dpdy[biplanarCoords[0].z]) * (scale / nextUVDist));

	float2 w = float2(n[ma.x], n[me.x]);
	// Make local support
	w = saturate((w - 0.5773) / (1 - 0.5773));
	

	// Normal vector extraction
	float4 n1a = float4(UnpackNormal(x).rgb, x.a);
	float4 n2a = float4(UnpackNormal(y).rgb, y.a);
	float4 n3a = float4(UnpackNormal(x1).rgb, x1.a);
	float4 n4a = float4(UnpackNormal(y1).rgb, y1.a);
	//return float4(x);
	n1a = lerp(n1a, n3a, percentage);
	n2a = lerp(n2a, n4a, percentage);
	float alpha = ((n1a.a * w.x + n2a.a * w.y) / (w.x + w.y));

	// Do UDN-style normal blending in the tangent space then bring the result
	// back to the world space. To make the space conversion simpler, we use
	// reverse-order swizzling, which brings us back to the original space by
	// applying twice.
	float3 n1 = normalize(float3(n1a.y + wnrm[ma.z], n1a.x + wnrm[ma.y], wnrm[ma.x])).rgb;
	float3 n2 = normalize(float3(n2a.y + wnrm[me.z], n2a.x + wnrm[me.y], wnrm[me.x])).rgb;
	n1 = float3(n1[ma.z], n1[ma.y], n1[ma.x]);
	n2 = float3(n2[me.z], n2[me.y], n2[me.x]);

	// Blend factors
	
	// Blending
	float3 output = normalize((n1 * w.x + n2 * w.y) / (w.x + w.y));	//This is the normal map after biplanar sampling
	
	//return float4(wnrm, 1);
	//return float4(me, 1);
	//return float4(w, 0.5, 1);
	return float4((output), alpha);

	//output = (mul(float3x3(wtan, wbtn, wnrm), output));	//Transform to tangent space
	//float3x3 a = transpose(float3x3(wtan, wbtn, wnrm));	//Transform back to world space
	//output = mul(a, output);
	//return float4((output), 1);
}
float4 BiplanarTexture_float
(sampler2D tex, float3 dpdx, float3 dpdy, float UVDistortion, float nextUVDist, float percentage, float3 surfaceUVs, float3x3 biplanarCoords, float3 wpos, float3 wnrm, float2 scale)
{
	// Coordinate derivatives for texturing
	float3 p = wpos;
	float3 n = abs(wnrm);
	uint3 ma = biplanarCoords[0];
	uint3 me = biplanarCoords[2];


	// Project + fetch
	float4 x = tex2D(tex, float2(p[ma.y] * scale.x - surfaceUVs[ma.y], p[ma.z] * scale.y - surfaceUVs[ma.z]) / UVDistortion, float2(dpdx[biplanarCoords[0].y], dpdx[biplanarCoords[0].z]) * (scale / UVDistortion), float2(dpdy[biplanarCoords[0].y], dpdy[biplanarCoords[0].z]) * (scale / UVDistortion));
	float4 y = tex2D(tex, float2(p[me.y] * scale.x - surfaceUVs[me.y], p[me.z] * scale.y - surfaceUVs[me.z]) / UVDistortion, float2(dpdx[biplanarCoords[0].y], dpdx[biplanarCoords[0].z]) * (scale / UVDistortion), float2(dpdy[biplanarCoords[0].y], dpdy[biplanarCoords[0].z]) * (scale / UVDistortion));

	float4 x1 = tex2D(tex, float2(p[ma.y] * scale.x - surfaceUVs[ma.y], p[ma.z] * scale.y - surfaceUVs[ma.z]) / nextUVDist, float2(dpdx[biplanarCoords[0].y], dpdx[biplanarCoords[0].z]) * (scale / nextUVDist), float2(dpdy[biplanarCoords[0].y], dpdy[biplanarCoords[0].z]) * (scale / nextUVDist));
	float4 y1 = tex2D(tex, float2(p[me.y] * scale.x - surfaceUVs[me.y], p[me.z] * scale.y - surfaceUVs[me.z]) / nextUVDist, float2(dpdx[biplanarCoords[0].y], dpdx[biplanarCoords[0].z]) * (scale / nextUVDist), float2(dpdy[biplanarCoords[0].y], dpdy[biplanarCoords[0].z]) * (scale / nextUVDist));

	float4 n1 = lerp(x, x1, percentage);
	float4 n2 = lerp(y, y1, percentage);

	// Do UDN-style normal blending in the tangent space then bring the result
	// back to the world space. To make the space conversion simpler, we use
	// reverse-order swizzling, which brings us back to the original space by
	// applying twice.
	//n1 = float3(n1[ma.z], n1[ma.y], n1[ma.x]);
	//n2 = float3(n2[me.z], n2[me.y], n2[me.x]);

	// Blend factors
	float2 w = float2(n[ma.x], n[me.x]);

	// Make local support
	w = saturate((w - 0.5773) / (1 - 0.5773));

	float4 output = ((n1 * w.x + n2 * w.y) / (w.x + w.y));	//Everything up to here is correct

	return output;
}
float4 BiplanarTexture_float2
(sampler2D tex, float3 dpdx, float3 dpdy, float UVDistortion, float nextUVDist, float percentage, float3 surfaceUVs, float3x3 biplanarCoords, float3 wpos, float3 wnrm, float2 scale, float mipmap)
{
	// Coordinate derivatives for texturing
	float3 p = wpos;
	float3 n = abs(wnrm);
	uint3 ma = biplanarCoords[0];
	uint3 me = biplanarCoords[2];


	// Project + fetch
	float4 x = tex2Dlod(tex, float4(float2(p[ma.y] * scale.x - surfaceUVs[ma.y], p[ma.z] * scale.y - surfaceUVs[ma.z] / UVDistortion), 0, mipmap));
	float4 y = tex2Dlod(tex, float4(float2(p[me.y] * scale.x - surfaceUVs[me.y], p[me.z] * scale.y - surfaceUVs[me.z] / UVDistortion), 0, mipmap));
	float4 n1 = x;
	float4 n2 = y;

	// Do UDN-style normal blending in the tangent space then bring the result
	// back to the world space. To make the space conversion simpler, we use
	// reverse-order swizzling, which brings us back to the original space by
	// applying twice.
	//n1 = float3(n1[ma.z], n1[ma.y], n1[ma.x]);
	//n2 = float3(n2[me.z], n2[me.y], n2[me.x]);

	// Blend factors
	float2 w = float2(n[ma.x], n[me.x]);

	// Make local support
	w = saturate((w - 0.5773) / (1 - 0.5773));

	float4 output = ((n1 * w.x + n2 * w.y) / (w.x + w.y));	//Everything up to here is correct

	return output;
}
float4 TessTexture_float
(sampler2D tex, float3 surfaceUVs, float3x3 biplanarCoords, float3 wpos, float3 wnrm, float2 scale, int mipLevel)
{
	// Coordinate derivatives for texturing
	float3 p = wpos;
	float3 n = abs(wnrm);
	uint3 ma = biplanarCoords[0];
	uint3 me = biplanarCoords[2];


	// Project + fetch
	float4 x = tex2Dlod(tex, float4(float2(p[ma.y] * scale.x - surfaceUVs[ma.y], p[ma.z] * scale.y - surfaceUVs[ma.z]), mipLevel, mipLevel));
	float4 y = tex2Dlod(tex, float4(float2(p[me.y] * scale.x - surfaceUVs[me.y], p[me.z] * scale.y - surfaceUVs[me.z]), mipLevel, mipLevel));


	// Do UDN-style normal blending in the tangent space then bring the result
	// back to the world space. To make the space conversion simpler, we use
	// reverse-order swizzling, which brings us back to the original space by
	// applying twice.
	//n1 = float3(n1[ma.z], n1[ma.y], n1[ma.x]);
	//n2 = float3(n2[me.z], n2[me.y], n2[me.x]);

	// Blend factors
	float2 w = float2(n[ma.x], n[me.x]);

	// Make local support
	w = saturate((w - 0.5773) / (1 - 0.5773));

	float4 output = ((x * w.x + y * w.y) / (w.x + w.y));	//Everything up to here is correct

	return output;
}

float4 lerpSurfaceNormal(float4 low, float4 mid, float4 high, float4 steep, float midPoint, float slope, float blendLow, float blendHigh)
{
	float4 col;
	if (midPoint < 0.5)
	{
		col = lerp(low, mid, 1 - blendLow);
	}
	else
	{
		col = lerp(mid, high, blendHigh);
	}
	col = lerp(col, steep, 1 - slope);
	return col;
}


float fogUV(float3 normal, float3 planetNormal)
{
	float sunDirection = (1 + dot(planetNormal, _WorldSpaceLightPos0.xyz)) * 0.5;
	return clamp(sunDirection, 0.01, 0.99);
}
float CalculateSaturation(float4 surfaceCol)
{
	float saturation = (max(surfaceCol.r, max(surfaceCol.g, surfaceCol.b)) - min(surfaceCol.r, min(surfaceCol.g, surfaceCol.b))) / max(surfaceCol.r, max(surfaceCol.g, surfaceCol.b));
	return saturation;
}
float3 BoxProjection(
	float3 direction, float3 position,
	float4 cubemapPosition, float3 boxMin, float3 boxMax
) {
	if (cubemapPosition.w > 0) {
		float3 factors =
			((direction > 0 ? boxMax : boxMin) - position) / direction;
		float scalar = min(min(factors.x, factors.y), factors.z);
		direction = direction * scalar + (position - cubemapPosition);
	}
	return direction;
}
//float4 BlinnPhong(float3 world_vertex, float3 lightDir, float3 worldNormal, float3 basicNormal, float alpha, float3 col, float3 lightAttenuation)
//{
//	float3 normal = lerp(basicNormal, worldNormal, _NormalSpecularInfluence);
//	float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - world_vertex.xyz);
//	float3 halfDirection = normalize(viewDirection + lightDir);
//	float NdotL = max(0, dot(worldNormal, lightDir));
//	float NdotV = max(0, dot(normal, halfDirection));
//	float3 specularity = lightAttenuation * pow(NdotV, _Gloss * alpha) * _Metallic * _MetallicTint.rgb * alpha;
//	float3 lightingModel = col * lightAttenuation + specularity;
//	float3 attenColor = _LightColor0.rgb;
//	float4 finalDiffuse = float4(lightingModel * attenColor, 0);
//	return finalDiffuse;
//}

float4 BlinnPhong(float3 world_vertex, float3 lightDir, float3 worldNormal, float3 basicNormal, float alpha, float3 col, float3 lightAttenuation)
{
	float3 normal = lerp(basicNormal, worldNormal, _NormalSpecularInfluence);
	float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - world_vertex.xyz);
	float3 halfDirection = normalize(viewDirection + lightDir);
	float NdotL = max(0, dot(worldNormal, lightDir));
	float NdotV = max(0, dot(normal, halfDirection));

	NdotL = pow(NdotL, _Hapke);

	//Specular calculations
	float3 specularity = pow(NdotV, _Gloss * alpha) * _Metallic * _MetallicTint.rgb * alpha;
	float angle = saturate(dot(normalize(worldNormal), lightDir));

	float3 lightingModel = NdotL * col + specularity;

	float3 attenColor = lightAttenuation * _LightColor0.rgb;
	float4 finalDiffuse = float4(lightingModel * attenColor, 0);
	return finalDiffuse;
}
float4 BlinnPhongObject(float3 world_vertex, float3 lightDir, float3 worldNormal, float3 basicNormal, float alpha, float3 col, float3 lightAttenuation)
{
	_WorldSpaceCameraPos = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos.xyz, 1));
	float3 normal = lerp(worldNormal, basicNormal, _NormalSpecularInfluence);
	float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - world_vertex.xyz);
	float3 halfDirection = normalize(viewDirection + lightDir);
	float NdotL = max(0, dot(worldNormal, lightDir));
	float NdotV = max(0, dot(normal, halfDirection));

	NdotL = pow(NdotL, _Hapke);

	//Specular calculations
	float3 specularity = pow(NdotV, _Gloss * alpha) * _Metallic * _MetallicTint.rgb * alpha;
	float angle = saturate(dot(normalize(worldNormal), lightDir));

	float3 lightingModel = NdotL * col + specularity;

	float3 attenColor = lightAttenuation * _LightColor0.rgb;
	float4 finalDiffuse = float4(lightingModel * attenColor, 0);
	return finalDiffuse;
}
float GetZoomLevel(float cameraDist)
{
	float ZoomLevel = log2(cameraDist / 1.333f);
	if (ZoomLevel < 4)
	{
		ZoomLevel = 4;
	}
	return ZoomLevel;
}
float GetZoomLevelAsteroid(float cameraDist)
{
	float ZoomLevel = log2(cameraDist / 4);
	if (ZoomLevel < 4)
	{
		ZoomLevel = 4;
	}
	return ZoomLevel;
}
float4 SampleUVTexVert(sampler2D tex, float2 uv, float2 scale, float uvDistortion, float nextUVDist, float percentage)
{
	float4 tex1 = tex2Dlod(tex, float4(float2((uv * scale) / uvDistortion), 0, 0));
	float4 tex2 = tex2Dlod(tex, float4(float2((uv * scale) / nextUVDist), 0, 0));

	float4 result = lerp(tex1, tex2, percentage);
	return result;
}
float4 SampleUVTex(sampler2D tex, float2 uv, float2 scale, float uvDistortion, float nextUVDist, float percentage)
{
	float4 tex1 = tex2D(tex, float2(uv * scale / uvDistortion));
	float4 tex2 = tex2D(tex, float2(uv * scale / nextUVDist));

	float4 result = lerp(tex1, tex2, percentage);
	return result;
}
float4 SampleUVNormal(sampler2D tex, float2 uv, float2 scale, float uvDistortion, float nextUVDist, float percentage)
{
	float3 tex1 = UnpackNormal(tex2D(tex, float2(uv * scale / uvDistortion), 0, 0));
	float3 tex2 = UnpackNormal(tex2D(tex, float2(uv * scale / nextUVDist), 0, 0));

	float4 result = float4(lerp(tex1, tex2, percentage), 0);
	return result;
}
float4 SampleUVNormalEmission(sampler2D tex, float2 uv, float2 scale, float uvDistortion, float nextUVDist, float percentage)
{
	float4 tex1 = tex2D(tex, float2(uv * scale / uvDistortion), 0, 0);
	float4 tex2 = tex2D(tex, float2(uv * scale / nextUVDist), 0, 0);
	float4 nrm1 = float4(UnpackNormal(tex1), tex1.a);
	float4 nrm2 = float4(UnpackNormal(tex2), tex2.a);

	float4 result = lerp(nrm1, nrm2, percentage);
	return result;
}