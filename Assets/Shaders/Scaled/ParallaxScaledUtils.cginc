
// Calculate the heightmap displacement
// Take into account planet radius and terrain min/max value
// Assume normalised heightmap input (0 min, 1 max)
#define CALCULATE_HEIGHTMAP_DISPLACEMENT_SCALED(o, displacement)                                                                                                    \
    o.worldPos = o.worldPos + displacement * o.worldNormal * _HeightDeformity;