using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Upgradeables;

namespace Parallax.Structures
{
    /// <summary>
    /// Replaces the ground shaders of structures (the KSC, launch sites, KK statics) with the Parallax terrain shader
    /// </summary>
    [KSPAddon(KSPAddon.Startup.FlightAndKSC, false)]
    public class StructureGroundTerrain : MonoBehaviour
    {
        const string groundShaderName = "Custom/ParallaxKSCTerrain";

        static readonly string[] altitudeKeywords =
        {
            "PARALLAX_SINGLE_LOW", "PARALLAX_SINGLE_MID", "PARALLAX_SINGLE_HIGH", "PARALLAX_DOUBLE_LOWMID", "PARALLAX_DOUBLE_MIDHIGH", "PARALLAX_FULL"
        };

        static readonly int terrainColorID = Shader.PropertyToID("_TerrainColor");
        static readonly int blendMaskUsesUV0ID = Shader.PropertyToID("_BlendMaskUsesUV0");

        // Shader replacements persist across scenes so these also hold the materials replaced in previous scenes
        static readonly Dictionary<Material, CelestialBody> groundMaterials = new Dictionary<Material, CelestialBody>();
        static readonly Dictionary<Material, (double min, double max)> groundMaterialAltitudes = new Dictionary<Material, (double min, double max)>();
        static readonly Dictionary<(Material, CelestialBody), Material> bodyMaterialClones = new Dictionary<(Material, CelestialBody), Material>();

        static readonly Dictionary<Renderer, GroundRendererProperties> groundRenderers = new Dictionary<Renderer, GroundRendererProperties>();
        static readonly Dictionary<Transform, Color> structureTerrainColors = new Dictionary<Transform, Color>();

        // KK statics and KSC facility upgrades spawn their renderers after Start and statics can activate outside these scenes
        // So we need to queue up these
        static readonly HashSet<Renderer> queuedRenderers = new HashSet<Renderer>();

        static MaterialPropertyBlock propertyBlock;

        // Accumulates what each batch of AddRenderer calls did so the batch can log its cost
        static ReplacementStats stats;
        static readonly System.Diagnostics.Stopwatch terrainColorStopwatch = new System.Diagnostics.Stopwatch();

        Shader groundShader;

        // Leaving a body releases its textures
        CelestialBody appliedBody;

        // Building a terrain colour reads the body's PQS maps, which would load them if the body is unloaded
        // So colours are only built for the current main body
        CelestialBody terrainColorBody;

        struct GroundRendererProperties
        {
            public CelestialBody body;
            public Color terrainColor;
            public bool hasTerrainColor;
            public bool blendMaskUsesUV0;
        }

        struct ReplacementStats
        {
            public int renderers;
            public int groundRenderers;
            public int shaderReplacements;
            public int bodyClones;
            public int terrainColors;
        }

        public void Start()
        {
            if (!AssetBundleLoader.parallaxTerrainShaders.TryGetValue(groundShaderName, out groundShader))
            {
                ParallaxDebug.LogError(groundShaderName + " is missing from the terrain shader bundle, structure ground will not be replaced");
                enabled = false;
                return;
            }

            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            System.Diagnostics.Stopwatch scanStopwatch = new System.Diagnostics.Stopwatch();
            int bodies = 0;

            RemoveDestroyed();

            foreach (CelestialBody body in FlightGlobals.Bodies)
            {
                if (body.pqsController == null || !ConfigLoader.parallaxTerrainBodies.ContainsKey(body.name))
                {
                    continue;
                }
                bodies++;

                // Terrain quads are the only renderers under a body that aren't structures
                // TODO: This could be brittle
                scanStopwatch.Start();
                Renderer[] renderers = body.GetComponentsInChildren<Renderer>(true);
                scanStopwatch.Stop();

                foreach (Renderer renderer in renderers)
                {
                    scanStopwatch.Start();
                    bool isQuad = renderer.GetComponent<PQ>() != null;
                    scanStopwatch.Stop();

                    if (!isQuad)
                    {
                        AddRenderer(renderer);
                    }
                }
            }

            GameEvents.OnUpgradeableObjLevelChange.Add(OnUpgradeableObjLevelChange);

            LogStats("Scene start (" + HighLogic.LoadedScene + ", " + bodies + " bodies, renderer scan " + scanStopwatch.Elapsed.TotalMilliseconds.ToString("F2") + " ms)", sw);
        }

        public void OnDestroy()
        {
            GameEvents.OnUpgradeableObjLevelChange.Remove(OnUpgradeableObjLevelChange);
        }

        public void Update()
        {
            if (queuedRenderers.Count > 0)
            {
                System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                int queuedCount = queuedRenderers.Count;

                foreach (Renderer renderer in queuedRenderers)
                {
                    if (renderer != null)
                    {
                        AddRenderer(renderer);
                    }
                }
                queuedRenderers.Clear();

                LogStats("Queued renderers (" + queuedCount + " queued)", sw);
            }

            CelestialBody mainBody = FlightGlobals.currentMainBody;
            if (mainBody != terrainColorBody)
            {
                terrainColorBody = mainBody;
                if (mainBody != null)
                {
                    BuildPendingTerrainColors(mainBody);
                }
            }

            if (mainBody != appliedBody)
            {
                appliedBody = null;
            }
            if (appliedBody == null && mainBody != null && ConfigLoader.parallaxTerrainBodies.TryGetValue(mainBody.name, out ParallaxTerrainBody terrainBody) && terrainBody.Loaded)
            {
                System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                int appliedCount = 0;

                foreach (KeyValuePair<Material, CelestialBody> groundMaterial in groundMaterials)
                {
                    if (groundMaterial.Value == mainBody && groundMaterial.Key != null)
                    {
                        ApplyTerrainProperties(groundMaterial.Key, terrainBody);
                        appliedCount++;
                    }
                }
                appliedBody = mainBody;

                ParallaxDebug.Log("[Structure ground] Applying " + mainBody.name + " terrain properties to " + appliedCount + " materials took " + sw.Elapsed.TotalMilliseconds.ToString("F2") + " ms");
            }
        }

        public static void QueueRenderers(Component structure)
        {
            queuedRenderers.UnionWith(structure.GetComponentsInChildren<Renderer>(true));
        }

        // Highlighting a KSC facility replaces the whole property block on its renderers, including its ground
        public static void ReapplyRendererProperties(Renderer[] renderers)
        {
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null && groundRenderers.TryGetValue(renderer, out GroundRendererProperties properties))
                {
                    ApplyRendererProperties(renderer, properties);
                }
            }
        }

        void OnUpgradeableObjLevelChange(UpgradeableObject upgradeableObject, int level)
        {
            if (upgradeableObject.FacilityTransform != null)
            {
                QueueRenderers(upgradeableObject.FacilityTransform);
            }
        }

        static void LogStats(string batch, System.Diagnostics.Stopwatch sw)
        {
            ParallaxDebug.Log("[Structure ground] " + batch + " took " + sw.Elapsed.TotalMilliseconds.ToString("F2") + " ms: "
                + stats.renderers + " renderers checked, "
                + stats.groundRenderers + " ground renderers, "
                + stats.shaderReplacements + " shaders replaced, "
                + stats.bodyClones + " body clones, "
                + stats.terrainColors + " terrain colours built in " + terrainColorStopwatch.Elapsed.TotalMilliseconds.ToString("F2") + " ms");

            stats = default;
            terrainColorStopwatch.Reset();
        }

        void AddRenderer(Renderer renderer)
        {
            stats.renderers++;

            CelestialBody body = GetBody(renderer.transform);
            if (body == null || !ConfigLoader.parallaxTerrainBodies.TryGetValue(body.name, out ParallaxTerrainBody terrainBody))
            {
                return;
            }

            Material[] materials = renderer.sharedMaterials;
            bool hasGroundMaterial = false;
            bool materialsChanged = false;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null || !IsGroundMaterial(material))
                {
                    continue;
                }

                if (groundMaterials.TryGetValue(material, out CelestialBody materialBody) && materialBody != body)
                {
                    material = GetBodyClone(material, body);
                    materials[i] = material;
                    materialsChanged = true;
                }

                ReplaceGroundShader(material, body);
                UpdateAltitudeKeyword(material, renderer, body, terrainBody);
                if (appliedBody == body)
                {
                    ApplyTerrainProperties(material, terrainBody);
                }
                hasGroundMaterial = true;
            }

            if (!hasGroundMaterial)
            {
                return;
            }
            stats.groundRenderers++;
            if (materialsChanged)
            {
                renderer.sharedMaterials = materials;
            }

            if (!groundRenderers.TryGetValue(renderer, out GroundRendererProperties properties))
            {
                Mesh mesh = GetMesh(renderer);
                properties = new GroundRendererProperties()
                {
                    body = body,
                    terrainColor = Color.white,
                    blendMaskUsesUV0 = mesh != null && !mesh.HasVertexAttribute(VertexAttribute.TexCoord1)
                };
            }
            if (!properties.hasTerrainColor && body == FlightGlobals.currentMainBody)
            {
                properties.terrainColor = GetStructureTerrainColor(renderer.transform, body);
                properties.hasTerrainColor = true;
            }
            groundRenderers[renderer] = properties;
            ApplyRendererProperties(renderer, properties);
        }

        static void BuildPendingTerrainColors(CelestialBody body)
        {
            List<Renderer> pendingRenderers = groundRenderers.Where(groundRenderer => groundRenderer.Key != null && groundRenderer.Value.body == body && !groundRenderer.Value.hasTerrainColor).Select(groundRenderer => groundRenderer.Key).ToList();
            if (pendingRenderers.Count == 0)
            {
                return;
            }

            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            foreach (Renderer renderer in pendingRenderers)
            {
                GroundRendererProperties properties = groundRenderers[renderer];
                properties.terrainColor = GetStructureTerrainColor(renderer.transform, body);
                properties.hasTerrainColor = true;
                groundRenderers[renderer] = properties;
                ApplyRendererProperties(renderer, properties);
            }

            LogStats("Pending terrain colours on " + body.name + " (" + pendingRenderers.Count + " renderers)", sw);
        }

        bool IsGroundMaterial(Material material)
        {
            return material.shader == groundShader || ConfigLoader.structureGroundShaders.Contains(material.shader.name) || ConfigLoader.structureGroundMaterials.Contains(material.name);
        }

        void ReplaceGroundShader(Material material, CelestialBody body)
        {
            if (material.shader != groundShader)
            {
                int renderQueue = material.renderQueue;
                material.shader = groundShader;
                material.renderQueue = renderQueue;
                stats.shaderReplacements++;
            }
            groundMaterials[material] = body;
        }

        // Each body applies its own terrain textures so a material can only belong to one body
        static Material GetBodyClone(Material material, CelestialBody body)
        {
            if (!bodyMaterialClones.TryGetValue((material, body), out Material clone) || clone == null)
            {
                clone = new Material(material);
                clone.name = material.name + " (" + body.name + ")";
                bodyMaterialClones[(material, body)] = clone;
                stats.bodyClones++;
            }
            return clone;
        }

        static void ApplyRendererProperties(Renderer renderer, GroundRendererProperties properties)
        {
            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(terrainColorID, properties.terrainColor);
            propertyBlock.SetFloat(blendMaskUsesUV0ID, properties.blendMaskUsesUV0 ? 1 : 0);
            renderer.SetPropertyBlock(propertyBlock);
        }

        static CelestialBody GetBody(Transform transform)
        {
            for (; transform != null; transform = transform.parent)
            {
                CelestialBody body = transform.GetComponent<CelestialBody>();
                if (body != null)
                {
                    return body;
                }
            }
            return null;
        }

        // The structure is the topmost object below the body or its PQS (a PQSCity or a Kerbal Konstructs group), so its ground tiles share one colour
        static Color GetStructureTerrainColor(Transform transform, CelestialBody body)
        {
            Transform pqsTransform = body.pqsController.transform;
            while (transform.parent != null && transform.parent != body.transform && transform.parent != pqsTransform)
            {
                transform = transform.parent;
            }

            if (!structureTerrainColors.TryGetValue(transform, out Color terrainColor))
            {
                terrainColorStopwatch.Start();
                terrainColor = GetTerrainColor(body.pqsController, transform.position);
                terrainColorStopwatch.Stop();

                structureTerrainColors.Add(transform, terrainColor);
                stats.terrainColors++;
            }
            return terrainColor;
        }

        static Mesh GetMesh(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
            {
                return skinnedMeshRenderer.sharedMesh;
            }

            MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
            return meshFilter != null ? meshFilter.sharedMesh : null;
        }

        static void UpdateAltitudeKeyword(Material material, Renderer renderer, CelestialBody body, ParallaxTerrainBody terrainBody)
        {
            (double min, double max) range = GetAltitudeRange(renderer, body);
            if (groundMaterialAltitudes.TryGetValue(material, out (double min, double max) existingRange))
            {
                range = (Math.Min(range.min, existingRange.min), Math.Max(range.max, existingRange.max));
            }
            groundMaterialAltitudes[material] = range;

            SetAltitudeKeyword(material, GetAltitudeKeyword(terrainBody, range.min, range.max));
        }

        static (double min, double max) GetAltitudeRange(Renderer renderer, CelestialBody body)
        {
            Mesh mesh = GetMesh(renderer);
            if (mesh == null)
            {
                return (double.MinValue, double.MaxValue);
            }

            Bounds localBounds = mesh.bounds;
            double min = double.MaxValue;
            double max = double.MinValue;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 cornerSign = new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1);
                double altitude = body.GetAltitude(renderer.transform.TransformPoint(localBounds.center + Vector3.Scale(localBounds.extents, cornerSign)));

                min = Math.Min(min, altitude);
                max = Math.Max(max, altitude);
            }
            return (min, max);
        }

        static string GetAltitudeKeyword(ParallaxTerrainBody terrainBody, double minAltitude, double maxAltitude)
        {
            Dictionary<string, float> shaderFloats = terrainBody.terrainShaderProperties.shaderFloats;

            bool usesLow = minAltitude < shaderFloats["_LowMidBlendEnd"];
            bool usesMid = maxAltitude > shaderFloats["_LowMidBlendStart"] && minAltitude < shaderFloats["_MidHighBlendEnd"];
            bool usesHigh = maxAltitude > shaderFloats["_MidHighBlendStart"];

            if (usesLow && usesHigh)
            {
                return "PARALLAX_FULL";
            }
            if (usesLow && usesMid)
            {
                return "PARALLAX_DOUBLE_LOWMID";
            }
            if (usesMid && usesHigh)
            {
                return "PARALLAX_DOUBLE_MIDHIGH";
            }
            if (usesLow)
            {
                return "PARALLAX_SINGLE_LOW";
            }
            if (usesHigh)
            {
                return "PARALLAX_SINGLE_HIGH";
            }
            return "PARALLAX_SINGLE_MID";
        }

        static void SetAltitudeKeyword(Material material, string altitudeKeyword)
        {
            foreach (string keyword in altitudeKeywords)
            {
                SetKeyword(material, keyword, keyword == altitudeKeyword);
            }
        }

        static void ApplyTerrainProperties(Material material, ParallaxTerrainBody terrainBody)
        {
            ShaderProperties terrainShaderProperties = terrainBody.terrainShaderProperties;
            Material terrainMaterial = terrainBody.parallaxMaterials.parallaxFull;
            TerrainGlobalSettings terrainGlobalSettings = ConfigLoader.parallaxGlobalSettings.terrainGlobalSettings;

            foreach (string textureName in terrainShaderProperties.shaderTextures.Keys)
            {
                material.SetTexture(textureName, terrainMaterial.GetTexture(textureName));
            }
            foreach (KeyValuePair<string, float> floatValue in terrainShaderProperties.shaderFloats)
            {
                material.SetFloat(floatValue.Key, floatValue.Value);
            }
            foreach (KeyValuePair<string, Vector3> vectorValue in terrainShaderProperties.shaderVectors)
            {
                material.SetVector(vectorValue.Key, vectorValue.Value);
            }
            foreach (KeyValuePair<string, Color> colorValue in terrainShaderProperties.shaderColors)
            {
                material.SetColor(colorValue.Key, colorValue.Value);
            }
            foreach (KeyValuePair<string, int> intValue in terrainShaderProperties.shaderInts)
            {
                material.SetInt(intValue.Key, intValue.Value);
            }

            material.EnableKeyword("INFLUENCE_MAPPING");
            SetKeyword(material, "ADVANCED_BLENDING", terrainGlobalSettings.advancedTextureBlending);
            SetKeyword(material, "AMBIENT_OCCLUSION", terrainGlobalSettings.ambientOcclusion);
            SetKeyword(material, "EMISSION", terrainBody.emissive);
        }

        static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled)
            {
                material.EnableKeyword(keyword);
            }
            else
            {
                material.DisableKeyword(keyword);
            }
        }

        static Color GetTerrainColor(PQS pqs, Vector3 worldPosition)
        {
            if (pqs.mods == null)
            {
                return Color.white;
            }

            PQS.VertexBuildData data = new PQS.VertexBuildData();
            data.directionFromCenter = ((Vector3d)pqs.transform.InverseTransformPoint(worldPosition)).normalized;
            data.vertHeight = pqs.radius;

            // Third party mods aren't expecting to build vertices outside of a quad build
            try
            {
                pqs.Mod_OnVertexBuildHeight(data, true);
                foreach (PQSMod mod in pqs.mods)
                {
                    mod.OnVertexBuild(data);
                }
            }
            catch (Exception e)
            {
                ParallaxDebug.LogError("Failed to build the structure terrain colour on " + pqs.name + ": " + e.Message);
                return Color.white;
            }

            return data.vertColor;
        }

        static void RemoveDestroyed()
        {
            RemoveDestroyedKeys(groundMaterials);
            RemoveDestroyedKeys(groundMaterialAltitudes);
            RemoveDestroyedKeys(groundRenderers);
            RemoveDestroyedKeys(structureTerrainColors);

            foreach ((Material, CelestialBody) key in bodyMaterialClones.Where(clone => clone.Value == null).Select(clone => clone.Key).ToList())
            {
                bodyMaterialClones.Remove(key);
            }
        }

        static void RemoveDestroyedKeys<TKey, TValue>(Dictionary<TKey, TValue> dictionary) where TKey : UnityEngine.Object
        {
            foreach (TKey key in dictionary.Keys.Where(key => key == null).ToList())
            {
                dictionary.Remove(key);
            }
        }
    }
}
