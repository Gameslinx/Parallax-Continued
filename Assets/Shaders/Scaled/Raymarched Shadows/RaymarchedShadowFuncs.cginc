#ifdef SHADOWS_CUBE
    #define PARALLAX_TRANSFER_SHADOW(a) a._ShadowCoord = worldPos.xyz - _LightPositionRange.xyz;
#endif

#if defined (SHADOWS_DEPTH) && defined (SPOT)
    #define PARALLAX_TRANSFER_SHADOW(a) a._ShadowCoord = mul (unity_WorldToShadow[0], float4(worldPos, 1));
#endif

#if defined (SHADOWS_SCREEN)
    #if defined (UNITY_NO_SCREENSPACE_SHADOWS)
        #define PARALLAX_TRANSFER_SHADOW(a) a._ShadowCoord = mul( unity_WorldToShadow[0], float4(worldPos, 1) );
    #else
        #define PARALLAX_TRANSFER_SHADOW(a) a._ShadowCoord = ComputeScreenPos(a.pos);
    #endif
#endif

// No shadows
#if !defined (SHADOWS_SCREEN) && !defined (SHADOWS_DEPTH) && !defined (SHADOWS_CUBE)
    #define PARALLAX_TRANSFER_SHADOW(a)
#endif

// Fix unity's pointless re-transform to world space which doesn't work for custom o2w matrices anyway
// These are all identical however any additional functionality can go here in the future if required
#ifdef SPOT
    #define PARALLAX_TRANSFER_VERTEX_TO_FRAGMENT(a) a._LightCoord = mul(unity_WorldToLight, float4(worldPos, 1));     PARALLAX_TRANSFER_SHADOW(a)
#endif
#ifdef POINT
    #define PARALLAX_TRANSFER_VERTEX_TO_FRAGMENT(a) a._LightCoord = mul(unity_WorldToLight, float4(worldPos, 1)).xyz; PARALLAX_TRANSFER_SHADOW(a)
#endif
#ifdef POINT_COOKIE
    #define PARALLAX_TRANSFER_VERTEX_TO_FRAGMENT(a) a._LightCoord = mul(unity_WorldToLight, float4(worldPos, 1)).xyz; PARALLAX_TRANSFER_SHADOW(a)
#endif
#ifdef DIRECTIONAL_COOKIE
    #define PARALLAX_TRANSFER_VERTEX_TO_FRAGMENT(a) a._LightCoord = mul(unity_WorldToLight, float4(worldPos, 1)).xyz; PARALLAX_TRANSFER_SHADOW(a)
#endif
#ifdef DIRECTIONAL
    #define PARALLAX_TRANSFER_VERTEX_TO_FRAGMENT(a) PARALLAX_TRANSFER_SHADOW(a)
#endif

// More efficient version of unity's UnityClipSpaceShadowCasterPos which avoids re-transforming to world space
float4 ParallaxClipSpaceShadowCasterPos(float3 wPos, float3 wNormal)
{
    if (unity_LightShadowBias.z != 0.0)
    {
        float3 wLight = normalize(UnityWorldSpaceLightDir(wPos.xyz));

        // apply normal offset bias (inset position along the normal)
        // bias needs to be scaled by sine between normal and light direction
        // (http://the-witness.net/news/2013/09/shadow-mapping-summary-part-1/)
        //
        // unity_LightShadowBias.z contains user-specified normal offset amount
        // scaled by world space texel size.

        float shadowCos = dot(wNormal, wLight);
        float shadowSine = sqrt(1 - shadowCos * shadowCos);
        float normalBias = unity_LightShadowBias.z * shadowSine;

        wPos.xyz -= wNormal * normalBias;
    }

    return mul(UNITY_MATRIX_VP, float4(wPos, 1));
}