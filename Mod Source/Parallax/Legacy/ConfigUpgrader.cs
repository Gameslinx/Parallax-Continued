using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Parallax
{
    /// <summary>
    /// Upgrades legacy 2.0.x configs to the Rewritten configs. Note that this is not a 1:1 conversion and will not be perfect, but will provide a starting point for further tweaks.
    /// </summary>
    //[KSPAddon(KSPAddon.Startup.Instantly, true)]
    //public class ConfigUpgrader : MonoBehaviour
    //{
    //    public static string configVersion = "2.0";
    //    public void Start()
    //    {
    //        UrlDir.UrlConfig[] configs = ConfigLoader.GetConfigsByName("ParallaxScatters");
    //        Debug.Log("Replacer: " + configs.Length + " configs");
    //        foreach (UrlDir.UrlConfig rootNode in configs)
    //        {
    //            ConfigNode node = rootNode.config;
    //            string bodyName = node.GetValue("body");
    //            string minimumSubdivision = node.GetValue("minimumSubdivision");
    //
    //            // Check if we're processing an up to date config
    //            string fileConfigVersion = node.GetValue("configVersion");
    //            if (fileConfigVersion != null)
    //            {
    //                ParallaxDebug.Log("Skipping up-to-date body: " + bodyName);
    //                break;
    //            }
    //
    //            // I really don't know why we need to do this, but it's required for the root node to show up...
    //            ConfigNode baseNode = new ConfigNode("ParallaxScatters");
    //            ConfigNode result = new ConfigNode("ParallaxScatters-Upgraded", "Upgraded scatter config from old syntax. Remove the '-Upgraded' part to activate, and replace the old config.");
    //            result.AddValue("configVersion", configVersion);
    //            result.AddValue("body", bodyName);
    //            result.AddValue("minimumSubdivision", minimumSubdivision);
    //
    //            // Process scatter nodes
    //            ConfigNode[] scatterNodes = node.GetNodes("Scatter");
    //            Debug.Log("Replacer: " + configs.Length + " scatter nodes");
    //            foreach (ConfigNode scatterNode in  scatterNodes)
    //            {
    //                ConfigNode resultingScatterNode = new ConfigNode("Scatter");
    //                RecursiveReplace(scatterNode, resultingScatterNode);
    //                result.AddNode(resultingScatterNode);
    //            }
    //
    //            // Process shared scatter nodes
    //            ConfigNode[] sharedScatterNodes = node.GetNodes("SharedScatter");
    //            foreach (ConfigNode sharedScatterNode in sharedScatterNodes)
    //            {
    //                ConfigNode resultingScatterNode = new ConfigNode("SharedScatter");
    //                resultingScatterNode.AddValue("parentName", sharedScatterNode.GetValue("parent"));
    //                resultingScatterNode.AddValue("name", sharedScatterNode.GetValue("name"));
    //                resultingScatterNode.AddValue("model", sharedScatterNode.GetValue("model"));
    //
    //                resultingScatterNode.AddNode("Distribution", sharedScatterNode.GetNode("Distribution"));
    //                resultingScatterNode.AddNode("Material", sharedScatterNode.GetNode("Material"));
    //                result.AddNode(resultingScatterNode);
    //            }
    //
    //            Debug.Log("Saving config to " + bodyName + ".cfg");
    //            // Write node to file
    //            baseNode.AddNode(result);
    //            baseNode.Save(KSPUtil.ApplicationRootPath + "GameData/Parallax/Exports/LegacyUpgrades/" + bodyName + ".cfg");
    //        }
    //    }
    //    /// <summary>
    //    /// Runs a find and replace on the entire config, updating it to the new version
    //    /// </summary>
    //    /// <param name="node"></param>
    //    /// <param name="result"></param>
    //    public void RecursiveReplace(ConfigNode node, ConfigNode result)
    //    {
    //        // Process values on this node
    //        string[] valueNames = node.values.DistinctNames();
    //        foreach (string valueName in valueNames)
    //        {
    //            
    //            // If this value exists in the updated configs
    //            if (IsValueNameEligible(valueName))
    //            {
    //                string newName = PerformValueNameChecks(node.name, valueName);
    //                string[] values = node.GetValues(valueName);
    //                foreach (string thisValue in values)
    //                {
    //                    string newValue = AdjustValueIfRequired(newName, thisValue);
    //                    result.AddValue(newName, newValue);
    //                }
    //            }
    //        }
    //
    //        // Add any new values to this node, if eligible, and initialize defaults
    //        AddNewValues(node.name, result);
    //        
    //        // Add any new nodes to this node, if eligible, and initialize defaults
    //        AddNewNodes(node.name, result, node);
    //
    //        // Process nodes
    //        ConfigNode[] nodes = node.GetNodes();
    //        if (nodes != null && nodes.Length > 0)
    //        {
    //            foreach (ConfigNode childNode in nodes)
    //            {
    //                if (IsNodeEligible(childNode.name))
    //                {
    //                    ConfigNode resultChildNode = new ConfigNode(RenameNodeIfRequired(childNode.name));
    //                    RecursiveReplace(childNode, resultChildNode);
    //                    result.AddNode(resultChildNode);
    //                }
    //            }
    //        }
    //        
    //    }
    //    public string PerformValueNameChecks(string nodeName, string valueName)
    //    {
    //        // Name is of the form _MyVariable
    //        valueName = RemoveUnderscoreDecapitalise(nodeName, valueName);
    //        // Name is of the form myVariable
    //        valueName = CheckForRenames(valueName);
    //
    //        return valueName;
    //    }
    //    // _MyVariable -> myVariable
    //    public string RemoveUnderscoreDecapitalise(string nodeName, string input)
    //    {
    //        if (nodeName == "Material")
    //        {
    //            return input;
    //        }
    //        if (string.IsNullOrEmpty(input))
    //        {
    //            return input;
    //        }
    //
    //        if (input.StartsWith("_"))
    //        {
    //            // Remove the underscore
    //            input = input.Substring(1);
    //
    //            // Decapitalize the first letter
    //            if (!string.IsNullOrEmpty(input) && char.IsUpper(input[0]))
    //            {
    //                input = char.ToLower(input[0]) + input.Substring(1);
    //            }
    //        }
    //
    //        return input;
    //    }
    //    // Ideally would like to check a config for the before-after conversion but this is mainly used as a helper-tool, it doesn't need to be robust
    //    // and frankly my time is better spent elsewhere
    //    public string CheckForRenames(string valueName)
    //    {
    //        // None yet
    //        if (valueName == "normalDeviance")
    //        {
    //            return "maxNormalDeviance";
    //        }
    //        return valueName;
    //    }
    //    public void AddNewValues(string nodeName, ConfigNode resultNode)
    //    {
    //        if (nodeName == "Distribution")
    //        {
    //            resultNode.AddValue("scaleRandomness", 0.5f);
    //            resultNode.AddValue("alignToTerrainNormal", true);
    //            resultNode.AddValue("altitudeFadeRange", 20.0f);
    //        }
    //    }
    //    public string AdjustValueIfRequired(string valueName, string value)
    //    {
    //        if (valueName == "_AltitudeFadeRange")
    //        {
    //            float num = float.Parse(value) * 2;
    //            value = num.ToString("F3");
    //        }
    //
    //        return value;
    //    }
    //    public bool IsValueNameEligible(string valueName)
    //    {
    //        switch (valueName)
    //        {
    //            case "_RangePow":
    //                return false;
    //            case "_SizeNoiseStrength":
    //                return false;
    //            case "_AltitudeFadeRange":
    //                return false;
    //            case "updateFPS":
    //                return false;
    //            // These are moved to OptimizationSettings node
    //            case "cullingRange":
    //                return false;
    //            case "cullingLimit":
    //                return false;
    //            // This is moved to DistributionSettings node
    //            case "alignToTerrainNormal":
    //                return false;
    //        }
    //        return true;
    //    }
    //    public bool IsNodeEligible(string nodeName)
    //    {
    //        switch (nodeName)
    //        {
    //            case "SubdivisionSettings":
    //                return false;
    //            case "DistributionNoise":
    //                return false;
    //            case "LOD":
    //                return false;
    //            case "Material":
    //                return false;
    //            case "SubObjects":
    //                return false;
    //        }
    //        return true;
    //    }
    //    public string RenameNodeIfRequired(string nodeName)
    //    {
    //        // None yet
    //        return nodeName;
    //    }
    //    public void AddNewNodes(string parentNodeName, ConfigNode result, ConfigNode node)
    //    {
    //        // Add distribution noise
    //        if (parentNodeName == "Scatter")
    //        {
    //            ConfigNode newNode = result.AddNode("DistributionNoise");
    //            // Init defaults
    //            newNode.AddValue("noiseType", "simplexPerlin");
    //            newNode.AddValue("inverted", false);
    //            newNode.AddValue("frequency", 1000);
    //            newNode.AddValue("octaves", 2);
    //            newNode.AddValue("lacunarity", 2);
    //            newNode.AddValue("seed", 0.0f);
    //        }
    //
    //        if (parentNodeName == "Scatter")
    //        {
    //            ConfigNode newNode = result.AddNode("SubdivisionSettings");
    //
    //            newNode.AddValue("subdivisionRangeMode", "noSubdivision");
    //        }
    //
    //        if (parentNodeName == "Scatter")
    //        {
    //            ConfigNode newNode = result.AddNode("Optimizations");
    //
    //            newNode.AddValue("frustumCullingStartRange", 0.0f);
    //            newNode.AddValue("frustumCullingScreenMargin", 0.0f);
    //            newNode.AddValue("maxObjects", 15000);
    //        }
    //
    //        if (parentNodeName == "LODs")
    //        {
    //            ConfigNode lod1 = node.GetNodes("LOD")[0];
    //            ConfigNode lod2 = node.GetNodes("LOD")[1];
    //
    //            ProcessLOD(lod1, result);
    //            ProcessLOD(lod2, result);
    //        }
    //
    //        if (parentNodeName == "Scatter")
    //        {
    //            ConfigNode materialNode = node.GetNode("Material");
    //            ConfigNode newMaterialNode = result.AddNode("Material");
    //            newMaterialNode.AddValue("shader", "Custom/ParallaxInstancedSolid", "Upgraded materials will not have all shader properties set. See what they are in the Shader Bank in the Parallax folder");
    //
    //            newMaterialNode.AddValue("_MainTex", materialNode.GetValue("_MainTex"));
    //            if (materialNode.GetValue("_BumpMap") != null)
    //            {
    //                newMaterialNode.AddValue("_BumpMap", materialNode.GetValue("_BumpMap"));
    //            }
    //        }
    //    }
    //    void ProcessLOD(ConfigNode node, ConfigNode result)
    //    {
    //        ConfigNode lod = result.AddNode("LOD");
    //
    //        //
    //        // LOD node
    //        //
    //
    //        lod.AddValue("model", node.GetValue("model"));
    //        lod.AddValue("range", node.GetValue("range"));
    //
    //        bool lodIsBillboard = node.GetValue("billboard") != null ? bool.Parse(node.GetValue("billboard")) : false;
    //
    //        ConfigNode lodMaterialOrOverrideNode;
    //
    //        if (lodIsBillboard)
    //        {
    //            lodMaterialOrOverrideNode = lod.AddNode("Material");
    //            lodMaterialOrOverrideNode.AddValue("shader", "Custom/ParallaxInstancedSolid");
    //            ConfigNode lodMaterialKeywordsNode = lodMaterialOrOverrideNode.AddNode("Keywords");
    //            lodMaterialKeywordsNode.AddValue("name", "BILLBOARD");
    //        }
    //        else
    //        {
    //            lodMaterialOrOverrideNode = lod.AddNode("MaterialOverride");
    //        }
    //
    //        // Process old overrides
    //
    //        bool lodHasMainTex = node.GetValue("_MainTex") != null && node.GetValue("_MainTex") != "parent";
    //        bool lodHasBumpMap = node.GetValue("_BumpMap") != null && node.GetValue("_BumpMap") != "parent";
    //
    //        if (lodHasMainTex)
    //        {
    //            lodMaterialOrOverrideNode.AddValue("_MainTex", node.GetValue("_MainTex"));
    //        }
    //        if (lodHasBumpMap)
    //        {
    //            lodMaterialOrOverrideNode.AddValue("_BumpMap", node.GetValue("_BumpMap"));
    //        }
    //    }
    //}
}
