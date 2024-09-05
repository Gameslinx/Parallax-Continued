using Expansions.Missions;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Parallax.Legacy
{
    public class ParallaxPropertyHolder
    {
        //Holds all possible shader properties


    }
    public class AsteroidMaterial
    {
        public Material asteroidMaterial { get; set; }
        public string shaderName;
        public string[] shaderVars;
    }

    public class ParallaxAsteroidMed : AsteroidMaterial
    {
        public ParallaxAsteroidMed()
        {
            shaderName = "Custom/ParallaxAsteroidMed";
            shaderVars = new string[]
            {
                "_SurfaceTexture",
                "_BumpMap",
                "_SurfaceTextureScale",
                "_Hapke",
                "_NormalSpecularInfluence",
                "_Metallic",
                "_MetallicTint",
                "_Gloss",
                "_EmissionColor",
                "_SteepContrast",
                "_SteepMidpoint",
                "_SteepTex",
                "_BumpMapSteep",
                "_SteepPower"
            };
        }
    }
    public class ParallaxAsteroidUltra : AsteroidMaterial
    {
        public ParallaxAsteroidUltra()
        {
            shaderName = "Custom/ParallaxAsteroidUltra";
            shaderVars = new string[]
            {
                "_SurfaceTexture",
                "_BumpMap",
                "_DispTex",
                "_SurfaceTextureScale",
                "_Hapke",
                "_NormalSpecularInfluence",
                "_Metallic",
                "_MetallicTint",
                "_Gloss",
                "_displacement_scale",
                "_displacement_offset",
                "_EmissionColor",
                "_SteepContrast",
                "_SteepMidpoint",
                "_SteepTex",
                "_BumpMapSteep",
                "_SteepPower"
            };
        }
    }
    public class Parallax
    {
        public Material parallaxMaterial { get; set; }
        public string shaderName;
        public string[] shaderVars;
        public string[] globalVars;
        public Dictionary<string, string> specificVars; //Dictionary<ValueToReplace, Value> - Replace _SurfaceTexture with _SurfaceTextureMid when filling out the parallax bodies
        public static Parallax DetermineVersion(bool scaled, bool part, int quality)
        {
            if (scaled == true)
            {


            }
            if (part == true)
            {

            }
            if (scaled == false && part == false)
            {
                //if (quality == 0)
                //{
                //    return new ParallaxLow();
                //}
                //if (quality == 1 || quality == 2)
                //{
                //    return new ParallaxMed();
                //}
                if (quality == 3)
                {
                    //return new ParallaxUltra();

                    //Determine Parallax version for the quad and return it here
                }
            }
            //ParallaxLog.Log("Unable to determine shader quality - Automatically setting shader quality to low");
            return new ParallaxFullUltra();
        }
    }
    public class ParallaxFull : Parallax
    {
        //Can contain ultra, med and low
    }
    public class ParallaxSingle : Parallax
    {

    }
    public class ParallaxSingleSteep : Parallax
    {

    }
    public class ParallaxDoubleLow : Parallax
    {

    }
    public class ParallaxDoubleHigh : Parallax
    {

    }
    public class ParallaxFullUltra : ParallaxFull
    {
        //Shader Variables
        public ParallaxFullUltra()
        {
            shaderName = "Custom/ParallaxFULLUltra";
            specificVars = new Dictionary<string, string>();
            shaderVars = new string[]
            {
                "_SurfaceTexture",
                "_SurfaceTextureMid",
                "_SurfaceTextureHigh",
                "_SurfaceTextureScale",
                "_SteepTex",
                "_BumpMap",
                "_BumpMapMid",
                "_BumpMapHigh",
                "_BumpMapSteep",
                "_DispTex",
                "_InfluenceMap",
                "_Metallic",
                "_MetallicTint",
                "_FresnelPower",
                "_FresnelColor",
                "_Gloss",
                "_NormalSpecularInfluence",
                "_SteepPower",
                "_SteepContrast",
                "_SteepMidpoint",
                "_displacement_scale",
                "_displacement_offset",
                "_Hapke",
                "_LowStart",
                "_LowEnd",
                "_HighStart",
                "_HighEnd",
                "_EmissionColor"
            };
            globalVars = new string[]
            {
                "_TessellationEdgeLength",
                "_TessellationRange",
                "_MaxTessellation"
            };
            //ParallaxLog.Log("Using Parallax (Ultra)");
        }
    }
    public class ParallaxSingleUltra : ParallaxSingle
    {
        public ParallaxSingleUltra(string altitude)
        {
            shaderName = "Custom/ParallaxSINGLEUltra";
            if (altitude == "low")
            {
                specificVars = new Dictionary<string, string>()
                {
                    { "_SurfaceTexture", "_SurfaceTexture" },    //Don't replace for low
                    { "_BumpMap", "_BumpMap" }
                };
            }
            if (altitude == "mid")
            {
                specificVars = new Dictionary<string, string>()
                {
                    { "_SurfaceTexture", "_SurfaceTextureMid" },
                    { "_BumpMap", "_BumpMapMid" }
                };
            }
            if (altitude == "high")
            {
                specificVars = new Dictionary<string, string>()
                {
                    { "_SurfaceTexture", "_SurfaceTextureHigh" },
                    { "_BumpMap", "_BumpMapHigh" }
                };
            }
            shaderVars = new string[]
            {
                "_SurfaceTexture",
                "_SurfaceTextureScale",
                "_BumpMap",
                "_DispTex",
                "_InfluenceMap",
                "_Metallic",
                "_MetallicTint",
                "_FresnelPower",
                "_FresnelColor",
                "_Gloss",
                "_NormalSpecularInfluence",
                "_displacement_scale",
                "_displacement_offset",
                "_Hapke",
                "_LowStart",
                "_LowEnd",
                "_HighStart",
                "_HighEnd",
                "_EmissionColor"
            };
            globalVars = new string[]
            {
                "_TessellationEdgeLength",
                "_TessellationRange",
                "_MaxTessellation"
            };
        }
    }
    public class ParallaxSingleSteepUltra : ParallaxSingleSteep
    {
        public ParallaxSingleSteepUltra(string altitude)
        {
            shaderName = "Custom/ParallaxSINGLESTEEPUltra";
            if (altitude == "low")
            {
                specificVars = new Dictionary<string, string>()
                {
                    { "_SurfaceTexture", "_SurfaceTexture" },    //Don't replace for low
                    { "_BumpMap", "_BumpMap" }
                };
            }
            if (altitude == "mid")
            {
                specificVars = new Dictionary<string, string>()
                {
                    { "_SurfaceTexture", "_SurfaceTextureMid" },
                    { "_BumpMap", "_BumpMapMid" }
                };
            }
            if (altitude == "high")
            {
                specificVars = new Dictionary<string, string>()
                {
                    { "_SurfaceTexture", "_SurfaceTextureHigh" },
                    { "_BumpMap", "_BumpMapHigh" }
                };
            }
            shaderVars = new string[]
            {
                "_SurfaceTexture",
                "_SteepTex",
                "_SurfaceTextureScale",
                "_BumpMap",
                "_BumpMapSteep",
                "_DispTex",
                "_InfluenceMap",
                "_Metallic",
                "_MetallicTint",
                "_FresnelPower",
                "_FresnelColor",
                "_Gloss",
                "_NormalSpecularInfluence",
                "_SteepPower",
                "_SteepContrast",
                "_SteepMidpoint",
                "_displacement_scale",
                "_displacement_offset",
                "_Hapke",
                "_LowStart",
                "_LowEnd",
                "_HighStart",
                "_HighEnd",
                "_EmissionColor"
            };
            globalVars = new string[]
            {
                "_TessellationEdgeLength",
                "_TessellationRange",
                "_MaxTessellation"
            };
        }
    }
    public class ParallaxDoubleLowUltra : ParallaxDoubleLow
    {
        public ParallaxDoubleLowUltra()
        {
            shaderName = "Custom/ParallaxDOUBLELOWUltra";
            specificVars = new Dictionary<string, string>()
            {
                //{ "_SurfaceTextureLower", "_SurfaceTexture" },
                //{ "_SurfaceTextureHigher", "_SurfaceTextureMid" },
                //{ "_BumpMapLower", "_BumpMap" },
                //{ "_BumpMapHigher", "_BumpMapMid" }
            };
            shaderVars = new string[]
            {
                "_SurfaceTexture",
                "_SurfaceTextureMid",
                "_SteepTex",
                "_SurfaceTextureScale",
                "_BumpMap",
                "_BumpMapMid",
                "_BumpMapSteep",
                "_DispTex",
                "_InfluenceMap",
                "_Metallic",
                "_MetallicTint",
                "_FresnelPower",
                "_FresnelColor",
                "_Gloss",
                "_NormalSpecularInfluence",
                "_SteepPower",
                "_SteepContrast",
                "_SteepMidpoint",
                "_displacement_scale",
                "_displacement_offset",
                "_Hapke",
                "_LowStart",
                "_LowEnd",
                "_HighStart",
                "_HighEnd",
                "_EmissionColor"
            };
            globalVars = new string[]
            {
                "_TessellationEdgeLength",
                "_TessellationRange",
                "_MaxTessellation"
            };
        }
    }
    public class ParallaxDoubleHighUltra : ParallaxDoubleHigh
    {
        public ParallaxDoubleHighUltra()
        {
            shaderName = "Custom/ParallaxDOUBLEHIGHUltra";
            specificVars = new Dictionary<string, string>()
            {
                //{ "_SurfaceTextureLower", "_SurfaceTextureMid" },
                //{ "_SurfaceTextureHigher", "_SurfaceTextureHigh" },
                //{ "_BumpMapLower", "_BumpMapMid" },
                //{ "_BumpMapHigher", "_BumpMapHigh" }
            };
            shaderVars = new string[]
            {
                "_SurfaceTextureMid",
                "_SurfaceTextureHigh",
                "_SteepTex",
                "_SurfaceTextureScale",
                "_BumpMapMid",
                "_BumpMapHigh",
                "_BumpMapSteep",
                "_DispTex",
                "_InfluenceMap",
                "_Metallic",
                "_MetallicTint",
                "_FresnelPower",
                "_FresnelColor",
                "_Gloss",
                "_NormalSpecularInfluence",
                "_SteepPower",
                "_SteepContrast",
                "_SteepMidpoint",
                "_displacement_scale",
                "_displacement_offset",
                "_Hapke",
                "_LowStart",
                "_LowEnd",
                "_HighStart",
                "_HighEnd",
                "_EmissionColor"
            };
            globalVars = new string[]
            {
                "_TessellationEdgeLength",
                "_TessellationRange",
                "_MaxTessellation"
            };
        }
    }

    //ParallaxMed
    public class ParallaxFullMed : ParallaxFull
    {
        //Shader Variables
        public ParallaxFullMed()
        {
            shaderName = "Custom/ParallaxFULLMed";
            specificVars = new Dictionary<string, string>();
            shaderVars = new string[]
            {
            "_SurfaceTexture",
            "_SurfaceTextureMid",
            "_SurfaceTextureHigh",
            "_SurfaceTextureScale",
            "_SteepTex",
            "_BumpMap",
            "_BumpMapMid",
            "_BumpMapHigh",
            "_BumpMapSteep",
            "_InfluenceMap",
            "_Metallic",
            "_MetallicTint",
            "_FresnelPower",
            "_FresnelColor",
            "_Gloss",
            "_NormalSpecularInfluence",
            "_SteepPower",
            "_SteepContrast",
            "_SteepMidpoint",
            "_Hapke",
            "_LowStart",
            "_LowEnd",
            "_HighStart",
            "_HighEnd",
            "_EmissionColor"
            };
            globalVars = new string[]
            {

            };
            //ParallaxLog.Log("Using Parallax (Med)");
        }
    }
    public class ParallaxSingleMed : ParallaxSingle
    {
        public ParallaxSingleMed(string altitude)
        {
            shaderName = "Custom/ParallaxSINGLEMed";
            if (altitude == "low")
            {
                specificVars = new Dictionary<string, string>()
            {
                { "_SurfaceTexture", "_SurfaceTexture" },    //Don't replace for low
                { "_BumpMap", "_BumpMap" }
            };
            }
            if (altitude == "mid")
            {
                specificVars = new Dictionary<string, string>()
            {
                { "_SurfaceTexture", "_SurfaceTextureMid" },
                { "_BumpMap", "_BumpMapMid" }
            };
            }
            if (altitude == "high")
            {
                specificVars = new Dictionary<string, string>()
            {
                { "_SurfaceTexture", "_SurfaceTextureHigh" },
                { "_BumpMap", "_BumpMapHigh" }
            };
            }
            shaderVars = new string[]
            {
            "_SurfaceTexture",
            "_SurfaceTextureScale",
            "_BumpMap",
            "_InfluenceMap",
            "_Metallic",
            "_MetallicTint",
            "_FresnelPower",
            "_FresnelColor",
            "_Gloss",
            "_NormalSpecularInfluence",
            "_Hapke",
            "_LowStart",
            "_LowEnd",
            "_HighStart",
            "_HighEnd",
            "_EmissionColor"
            };
            globalVars = new string[]
            {

            };
        }
    }
    public class ParallaxSingleSteepMed : ParallaxSingleSteep
    {
        public ParallaxSingleSteepMed(string altitude)
        {
            shaderName = "Custom/ParallaxSINGLESTEEPMed";
            if (altitude == "low")
            {
                specificVars = new Dictionary<string, string>()
            {
                { "_SurfaceTexture", "_SurfaceTexture" },    //Don't replace for low
                { "_BumpMap", "_BumpMap" }
            };
            }
            if (altitude == "mid")
            {
                specificVars = new Dictionary<string, string>()
            {
                { "_SurfaceTexture", "_SurfaceTextureMid" },
                { "_BumpMap", "_BumpMapMid" }
            };
            }
            if (altitude == "high")
            {
                specificVars = new Dictionary<string, string>()
            {
                { "_SurfaceTexture", "_SurfaceTextureHigh" },
                { "_BumpMap", "_BumpMapHigh" }
            };
            }
            shaderVars = new string[]
            {
            "_SurfaceTexture",
            "_SteepTex",
            "_SurfaceTextureScale",
            "_BumpMap",
            "_BumpMapSteep",
            "_InfluenceMap",
            "_Metallic",
            "_MetallicTint",
            "_FresnelPower",
            "_FresnelColor",
            "_Gloss",
            "_NormalSpecularInfluence",
            "_SteepPower",
            "_SteepContrast",
            "_SteepMidpoint",
            "_Hapke",
            "_LowStart",
            "_LowEnd",
            "_HighStart",
            "_HighEnd",
            "_EmissionColor"
            };
            globalVars = new string[]
            {

            };
        }
    }
    public class ParallaxDoubleLowMed : ParallaxDoubleLow
    {
        public ParallaxDoubleLowMed()
        {
            shaderName = "Custom/ParallaxDOUBLELOWMed";
            specificVars = new Dictionary<string, string>()
            {
                //{ "_SurfaceTextureLower", "_SurfaceTexture" },
                //{ "_SurfaceTextureHigher", "_SurfaceTextureMid" },
                //{ "_BumpMapLower", "_BumpMap" },
                //{ "_BumpMapHigher", "_BumpMapMid" }
            };
            shaderVars = new string[]
            {
            "_SurfaceTexture",
            "_SurfaceTextureMid",
            "_SteepTex",
            "_SurfaceTextureScale",
            "_BumpMap",
            "_BumpMapMid",
            "_BumpMapSteep",
            "_InfluenceMap",
            "_Metallic",
            "_MetallicTint",
            "_FresnelPower",
            "_FresnelColor",
            "_Gloss",
            "_NormalSpecularInfluence",
            "_SteepPower",
            "_SteepContrast",
            "_SteepMidpoint",
            "_Hapke",
            "_LowStart",
            "_LowEnd",
            "_HighStart",
            "_HighEnd",
            "_EmissionColor"
            };
            globalVars = new string[]
            {

            };
        }
    }
    public class ParallaxDoubleHighMed : ParallaxDoubleHigh
    {
        public ParallaxDoubleHighMed()
        {
            shaderName = "Custom/ParallaxDOUBLEHIGHMed";
            specificVars = new Dictionary<string, string>()
            {
                //{ "_SurfaceTextureLower", "_SurfaceTextureMid" },
                //{ "_SurfaceTextureHigher", "_SurfaceTextureHigh" },
                //{ "_BumpMapLower", "_BumpMapMid" },
                //{ "_BumpMapHigher", "_BumpMapHigh" }
            };
            shaderVars = new string[]
            {
            "_SurfaceTextureMid",
            "_SurfaceTextureHigh",
            "_SteepTex",
            "_SurfaceTextureScale",
            "_BumpMapMid",
            "_BumpMapHigh",
            "_BumpMapSteep",
            "_InfluenceMap",
            "_Metallic",
            "_MetallicTint",
            "_FresnelPower",
            "_FresnelColor",
            "_Gloss",
            "_NormalSpecularInfluence",
            "_SteepPower",
            "_SteepContrast",
            "_SteepMidpoint",
            "_Hapke",
            "_LowStart",
            "_LowEnd",
            "_HighStart",
            "_HighEnd",
            "_EmissionColor"
            };
            globalVars = new string[]
            {

            };
        }


    }



    //ParallaxLow




    public class ParallaxFullLow : ParallaxFull
    {
        //Shader Variables
        public ParallaxFullLow()
        {
            shaderName = "Custom/ParallaxFULLLow";
            specificVars = new Dictionary<string, string>();
            shaderVars = new string[]
            {
                "_SurfaceTexture",
                "_SurfaceTextureMid",
                "_SurfaceTextureHigh",
                "_SurfaceTextureScale",
                "_SteepTex",
                "_BumpMap",
                "_BumpMapMid",
                "_BumpMapHigh",
                "_BumpMapSteep",
                "_Metallic",
                "_MetallicTint",
                "_Gloss",
                "_NormalSpecularInfluence",
                "_SteepPower",
                "_SteepContrast",
                "_SteepMidpoint",
                "_Hapke",
                "_LowStart",
                "_LowEnd",
                "_HighStart",
                "_HighEnd",
                "_EmissionColor"
            };
            globalVars = new string[]
            {

            };
            //ParallaxLog.Log("Using Parallax (Low)");
        }
    }
    public class ParallaxSingleLow : ParallaxSingle
    {
        public ParallaxSingleLow(string altitude)
        {
            shaderName = "Custom/ParallaxSINGLELow";
            if (altitude == "low")
            {
                specificVars = new Dictionary<string, string>()
                {
                    { "_SurfaceTexture", "_SurfaceTexture" },    //Don't replace for low
                    { "_BumpMap", "_BumpMap" }
                };
            }
            if (altitude == "mid")
            {
                specificVars = new Dictionary<string, string>()
                {
                    { "_SurfaceTexture", "_SurfaceTextureMid" },
                    { "_BumpMap", "_BumpMapMid" }
                };
            }
            if (altitude == "high")
            {
                specificVars = new Dictionary<string, string>()
                {
                    { "_SurfaceTexture", "_SurfaceTextureHigh" },
                    { "_BumpMap", "_BumpMapHigh" }
                };
            }
            shaderVars = new string[]
            {
                "_SurfaceTexture",
                "_SurfaceTextureScale",
                "_BumpMap",
                "_Metallic",
                "_MetallicTint",
                "_Gloss",
                "_NormalSpecularInfluence",
                "_Hapke",
                "_LowStart",
                "_LowEnd",
                "_HighStart",
                "_HighEnd",
                "_EmissionColor"
            };
            globalVars = new string[]
            {

            };
        }
    }
    public class ParallaxSingleSteepLow : ParallaxSingleSteep
    {
        public ParallaxSingleSteepLow(string altitude)
        {
            shaderName = "Custom/ParallaxSINGLESTEEPLow";
            if (altitude == "low")
            {
                specificVars = new Dictionary<string, string>()
                {
                    { "_SurfaceTexture", "_SurfaceTexture" },    //Don't replace for low
                    { "_BumpMap", "_BumpMap" }
                };
            }
            if (altitude == "mid")
            {
                specificVars = new Dictionary<string, string>()
                {
                    { "_SurfaceTexture", "_SurfaceTextureMid" },
                    { "_BumpMap", "_BumpMapMid" }
                };
            }
            if (altitude == "high")
            {
                specificVars = new Dictionary<string, string>()
                {
                    { "_SurfaceTexture", "_SurfaceTextureHigh" },
                    { "_BumpMap", "_BumpMapHigh" }
                };
            }
            shaderVars = new string[]
            {
                "_SurfaceTexture",
                "_SteepTex",
                "_SurfaceTextureScale",
                "_BumpMap",
                "_BumpMapSteep",
                "_Metallic",
                "_MetallicTint",
                "_Gloss",
                "_NormalSpecularInfluence",
                "_SteepPower",
                "_SteepContrast",
                "_SteepMidpoint",
                "_Hapke",
                "_LowStart",
                "_LowEnd",
                "_HighStart",
                "_HighEnd",
                "_EmissionColor"
            };
            globalVars = new string[]
            {

            };
        }
    }
    public class ParallaxDoubleLowLow : ParallaxDoubleLow
    {
        public ParallaxDoubleLowLow()
        {
            shaderName = "Custom/ParallaxDOUBLELOWLow";
            specificVars = new Dictionary<string, string>()
            {
                //{ "_SurfaceTextureLower", "_SurfaceTexture" },
                //{ "_SurfaceTextureHigher", "_SurfaceTextureMid" },
                //{ "_BumpMapLower", "_BumpMap" },
                //{ "_BumpMapHigher", "_BumpMapMid" }
            };
            shaderVars = new string[]
            {
                "_SurfaceTexture",
                "_SurfaceTextureMid",
                "_SteepTex",
                "_SurfaceTextureScale",
                "_BumpMap",
                "_BumpMapMid",
                "_BumpMapSteep",
                "_Metallic",
                "_MetallicTint",
                "_Gloss",
                "_NormalSpecularInfluence",
                "_SteepPower",
                "_SteepContrast",
                "_SteepMidpoint",
                "_Hapke",
                "_LowStart",
                "_LowEnd",
                "_HighStart",
                "_HighEnd",
                "_EmissionColor"
            };
            globalVars = new string[]
            {

            };
        }
    }
    public class ParallaxDoubleHighLow : ParallaxDoubleHigh
    {
        public ParallaxDoubleHighLow()
        {
            shaderName = "Custom/ParallaxDOUBLEHIGHLow";
            specificVars = new Dictionary<string, string>()
            {
                //{ "_SurfaceTextureLower", "_SurfaceTextureMid" },
                //{ "_SurfaceTextureHigher", "_SurfaceTextureHigh" },
                //{ "_BumpMapLower", "_BumpMapMid" },
                //{ "_BumpMapHigher", "_BumpMapHigh" }
            };
            shaderVars = new string[]
            {
                "_SurfaceTextureMid",
                "_SurfaceTextureHigh",
                "_SteepTex",
                "_SurfaceTextureScale",
                "_BumpMapMid",
                "_BumpMapHigh",
                "_BumpMapSteep",
                "_Metallic",
                "_MetallicTint",
                "_Gloss",
                "_NormalSpecularInfluence",
                "_SteepPower",
                "_SteepContrast",
                "_SteepMidpoint",
                "_Hapke",
                "_LowStart",
                "_LowEnd",
                "_HighStart",
                "_HighEnd",
                "_EmissionColor"
            };
            globalVars = new string[]
            {

            };
        }
    }



    public class ParallaxBody
    {
        public string bodyName = "Unnamed";
        public Parallax full { get; set; }
        public Parallax singleLow { get; set; }
        public Parallax singleMid { get; set; }
        public Parallax singleHigh { get; set; }
        public Parallax singleSteepLow { get; set; }
        public Parallax singleSteepMid { get; set; }
        public Parallax singleSteepHigh { get; set; }
        public Parallax doubleLow { get; set; }
        public Parallax doubleHigh { get; set; }

        public string _SurfaceTexture { get; set; }
        public string _SurfaceTextureMid { get; set; }
        public string _SurfaceTextureHigh { get; set; }
        public string _SteepTex { get; set; }

        public string _BumpMap { get; set; }
        public string _BumpMapMid { get; set; }
        public string _BumpMapHigh { get; set; }
        public string _BumpMapSteep { get; set; }

        public string _InfluenceMap { get; set; }
        public string _DispTex { get; set; }

        public float _SurfaceTextureScale { get; set; }

        public float _LowStart { get; set; }
        public float _LowEnd { get; set; }
        public float _HighStart { get; set; }
        public float _HighEnd { get; set; }

        public float _displacement_scale { get; set; }
        public float _displacement_offset { get; set; }

        public float _Metallic { get; set; }
        public float _Gloss { get; set; }
        public float _FresnelPower { get; set; }
        public Color _MetallicTint { get; set; }
        public Color _FresnelColor { get; set; }
        public float _NormalSpecularInfluence { get; set; }
        public float _SteepPower { get; set; }
        public float _SteepContrast { get; set; }
        public float _SteepMidpoint { get; set; }
        public float _Hapke { get; set; }
        public Color _EmissionColor { get; set; }

        public bool hasEmission = false;

        public ParallaxBody(string name, int qualityLevel)
        {
            bodyName = name;
            //Set the shader vars, then use these vars to set the materials
            if (qualityLevel == 3)
            {
                full = new ParallaxFullUltra();
                singleLow = new ParallaxSingleUltra("low");
                singleMid = new ParallaxSingleUltra("mid");
                singleHigh = new ParallaxSingleUltra("high");
                singleSteepLow = new ParallaxSingleSteepUltra("low");
                singleSteepMid = new ParallaxSingleSteepUltra("mid");
                singleSteepHigh = new ParallaxSingleSteepUltra("high");
                doubleLow = new ParallaxDoubleLowUltra();
                doubleHigh = new ParallaxDoubleHighUltra();
            }
            if (qualityLevel == 2 || qualityLevel == 1)
            {
                full = new ParallaxFullMed();
                singleLow = new ParallaxSingleMed("low");
                singleMid = new ParallaxSingleMed("mid");
                singleHigh = new ParallaxSingleMed("high");
                singleSteepLow = new ParallaxSingleSteepMed("low");
                singleSteepMid = new ParallaxSingleSteepMed("mid");
                singleSteepHigh = new ParallaxSingleSteepMed("high");
                doubleLow = new ParallaxDoubleLowMed();
                doubleHigh = new ParallaxDoubleHighMed();
            }
            if (qualityLevel == 0)
            {
                full = new ParallaxFullLow();
                singleLow = new ParallaxSingleLow("low");
                singleMid = new ParallaxSingleLow("mid");
                singleHigh = new ParallaxSingleLow("high");
                singleSteepLow = new ParallaxSingleSteepLow("low");
                singleSteepMid = new ParallaxSingleSteepLow("mid");
                singleSteepHigh = new ParallaxSingleSteepLow("high");
                doubleLow = new ParallaxDoubleLowLow();
                doubleHigh = new ParallaxDoubleHighLow();
            }
            ParallaxLog.Log("Created body: " + bodyName);
        }

        public ConfigNode ToUpgradedTerrainNode()
        {
            ConfigNode node = new ConfigNode("ShaderProperties");

            // textures
            node.AddValue("_MainTexLow", ModifyTexturePath(_SurfaceTexture));
            node.AddValue("_MainTexMid", ModifyTexturePath(_SurfaceTextureMid));
            node.AddValue("_MainTexHigh", ModifyTexturePath(_SurfaceTextureHigh));
            node.AddValue("_MainTexSteep", ModifyTexturePath(_SteepTex));

            node.AddValue("_BumpMapLow", ModifyTexturePath(_BumpMap));
            node.AddValue("_BumpMapMid", ModifyTexturePath(_BumpMapMid));
            node.AddValue("_BumpMapHigh", ModifyTexturePath(_BumpMapHigh));
            node.AddValue("_BumpMapSteep", ModifyTexturePath(_BumpMapSteep));

            // params
            node.AddValue("_DisplacementMap", ModifyTexturePath(_DispTex));
            node.AddValue("_InfluenceMap", ModifyTexturePath(_InfluenceMap));

            node.AddValue("_SteepPower", _SteepPower);
            node.AddValue("_SteepContrast", _SteepContrast);
            node.AddValue("_SteepMidpoint", _SteepMidpoint);

            node.AddValue("_DisplacementScale", _displacement_scale);
            node.AddValue("_DisplacementOffset", _displacement_offset);

            node.AddValue("_SpecularIntensity", _Metallic);
            node.AddValue("_SpecularPower", _Gloss);

            node.AddValue("_EnvironmentMapFactor", 0.5f);
            node.AddValue("_Hapke", _Hapke);

            node.AddValue("_EmissionColor", _EmissionColor);
            node.AddValue("_FresnelColor", _FresnelColor);
            node.AddValue("_FresnelPower", _FresnelPower);

            node.AddValue("_BumpScale", 1.0f);
            node.AddValue("_RefractionIntensity", 0.0f);

            node.AddValue("_LowMidBlendStart", _LowStart);
            node.AddValue("_LowMidBlendEnd", _LowEnd);
            node.AddValue("_MidHighBlendStart", _HighStart);
            node.AddValue("_MidHighBlendEnd", _HighEnd);

            // Rough conversion
            node.AddValue("_Tiling", _SurfaceTextureScale / 12.0f);

            return node;
        }
        string ModifyTexturePath(string path)
        {
            if (path.Contains("Parallax_StockTextures"))
            {
                return path.Replace("Parallax_StockTextures", "Parallax_StockTerrainTextures");
            }
            else
            {
                return path;
            }
        }
    }

    public static class ParallaxLog
    {
        public static void Log(string msg)
        {
            Debug.Log("[Parallax] " + msg);
        }
        public static void SubLog(string msg)
        {
            Debug.Log("[Parallax] - " + msg);
        }
    }

}