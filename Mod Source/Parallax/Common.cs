using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Parallax
{
    // Holds ParallaxGlobalSettings.cfg values for terrain shader and scatters
    public class ParallaxSettings
    {
        public TerrainGlobalSettings terrainGlobalSettings = new TerrainGlobalSettings();
    }
    public struct TerrainGlobalSettings
    {
        public float maxTessellation;
        public float tessellationEdgeLength;
        public float maxTessellationRange;
    }
    // Stores the loaded values from the configs for each planet, except for the textures which are stored via file path
    // Textures are loaded On-Demand and stored in loadedTextures, where they are unloaded on scene change
    public class ParallaxBody
    {
        public string planetName;
        public Dictionary<string, Texture2D> loadedTextures;

        public ShaderProperties terrainShaderProperties;
        public ParallaxBody(string planetName)
        {
            this.planetName = planetName;
        }
    }
    // Stores the names of the variables, then the types as defined in the ShaderPropertiesTemplate.cfg
    public class ShaderProperties : ICloneable
    {
        public Dictionary<string, string> shaderTextures = new Dictionary<string, string>();
        public Dictionary<string, float> shaderFloats = new Dictionary<string, float>();
        public Dictionary<string, Vector3> shaderVectors = new Dictionary<string, Vector3>();
        public Dictionary<string, Color> shaderColors = new Dictionary<string, Color>();

        public object Clone()
        {
            var clone = new ShaderProperties();
            foreach (var textureValue in shaderTextures)
            {
                clone.shaderTextures.Add(textureValue.Key, textureValue.Value);
            }
            foreach (var floatValue in shaderFloats)
            {
                clone.shaderFloats.Add(floatValue.Key, floatValue.Value);
            }
            foreach (var vectorValue in shaderVectors)
            {
                clone.shaderVectors.Add(vectorValue.Key, vectorValue.Value);
            }
            foreach (var colorValue in shaderColors)
            {
                clone.shaderColors.Add(colorValue.Key, colorValue.Value);
            }
            return clone;
        }
    }
}
