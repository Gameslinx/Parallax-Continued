using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Parallax
{
    //
    //  Loader Common
    //

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
        public Dictionary<string, Texture2D> loadedTextures = new Dictionary<string, Texture2D>();
        public ParallaxMaterials parallaxMaterials = new ParallaxMaterials();

        public ShaderProperties terrainShaderProperties;
        public bool loaded = false;
        public ParallaxBody(string planetName)
        {
            this.planetName = planetName;
        }
        // Create materials and set most properties, except the textures which use load on demand
        public void LoadInitial()
        {
            Material baseMaterial = new Material(AssetBundleLoader.parallaxTerrainShaders["Custom/Parallax"]);
            baseMaterial.EnableKeyword("INFLUENCE_MAPPING");

            foreach (KeyValuePair<string, float> floatValue in terrainShaderProperties.shaderFloats)
            {
                baseMaterial.SetFloat(floatValue.Key, floatValue.Value);
            }
            foreach (KeyValuePair<string, Vector3> vectorValue in terrainShaderProperties.shaderVectors)
            {
                baseMaterial.SetVector(vectorValue.Key, vectorValue.Value);
            }
            foreach (KeyValuePair<string, Color> colorValue in terrainShaderProperties.shaderColors)
            {
                baseMaterial.SetColor(colorValue.Key, colorValue.Value);
            }

            baseMaterial.SetFloat("_MaxTessellation", ConfigLoader.parallaxGlobalSettings.terrainGlobalSettings.maxTessellation);
            baseMaterial.SetFloat("_TessellationEdgeLength", ConfigLoader.parallaxGlobalSettings.terrainGlobalSettings.tessellationEdgeLength);
            baseMaterial.SetFloat("_MaxTessellationRange", ConfigLoader.parallaxGlobalSettings.terrainGlobalSettings.maxTessellationRange);

            // Instantiate materials - Keywords are set in the Parallax PQSMod
            // These are then updated at runtime with the incoming textures
            parallaxMaterials.parallaxLow = UnityEngine.Object.Instantiate(baseMaterial);
            parallaxMaterials.parallaxMid = UnityEngine.Object.Instantiate(baseMaterial);
            parallaxMaterials.parallaxHigh = UnityEngine.Object.Instantiate(baseMaterial);

            parallaxMaterials.parallaxLowMid = UnityEngine.Object.Instantiate(baseMaterial);
            parallaxMaterials.parallaxMidHigh = UnityEngine.Object.Instantiate(baseMaterial);

            parallaxMaterials.parallaxFull = UnityEngine.Object.Instantiate(baseMaterial);
        }
        public void Load(bool loadTextures)
        {
            if (loadTextures)
            {
                foreach (KeyValuePair<string, string> textureValue in terrainShaderProperties.shaderTextures)
                {
                    bool linear = false;
                    Debug.Log("Attempting load: " + textureValue.Key + " at " + textureValue.Value);
                    if (loadedTextures.ContainsKey(textureValue.Key))
                    {
                        if (loadedTextures[textureValue.Key] != null)
                        {
                            Debug.Log("This texture is already loaded!");
                            continue;
                        }
                        else
                        {
                            Debug.Log("The key exists, but the texture is null");
                        }
                    }
                    // Bump maps need to be linear, while everything else sRGB
                    if (textureValue.Key.Contains("Bump") || textureValue.Key.Contains("Displacement") || textureValue.Key.Contains("Influence")) { linear = true; }
                    Texture2D tex = TextureLoader.LoadTexture(textureValue.Value, linear);

                    parallaxMaterials.parallaxLow.SetTexture(textureValue.Key, tex);
                    parallaxMaterials.parallaxMid.SetTexture(textureValue.Key, tex);
                    parallaxMaterials.parallaxHigh.SetTexture(textureValue.Key, tex);

                    parallaxMaterials.parallaxLowMid.SetTexture(textureValue.Key, tex);
                    parallaxMaterials.parallaxMidHigh.SetTexture(textureValue.Key, tex);

                    parallaxMaterials.parallaxFull.SetTexture(textureValue.Key, tex);

                    loadedTextures.Add(textureValue.Key, tex);
                    // Add to active textures
                }
            }
            loaded = true;
        }
        public void Unload()
        {
            // Unload all textures
            Texture2D[] textures = loadedTextures.Values.ToArray();
            for (int i = 0 ; i < textures.Length; i++)
            {
                UnityEngine.Object.Destroy(textures[i]);
            }
            loadedTextures.Clear();
            loaded = false;
        }
    }
    // Stores all material quality variants
    public class ParallaxMaterials
    {
        public Material parallaxLow;
        public Material parallaxMid;
        public Material parallaxHigh;

        public Material parallaxLowMid;
        public Material parallaxMidHigh;

        public Material parallaxFull;
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
