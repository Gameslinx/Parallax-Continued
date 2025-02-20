using Parallax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

// todo: second project for this

//namespace ParallaxInstallationChecker
//{
//    [KSPAddon(KSPAddon.Startup.Instantly, true)]
//    class InstallationVerifyer : MonoBehaviour
//    {
//        void Awake()
//        {
//            string gameDataPath = KSPUtil.ApplicationRootPath + "GameData/";
//
//            bool hasKopernicus = Exists(gameDataPath + "Kopernicus", true);
//            bool hasHarmony = Exists(gameDataPath + "000_Harmony", true);
//            bool hasBurst = Exists(gameDataPath + "000_KSPBurst", true);
//            bool hasMFI = Exists(gameDataPath + "ModularFlightIntegrator", true);
//            
//            if (!hasKopernicus)
//            {
//                PopupDependencyError("Kopernicus");
//            }
//            if (!hasHarmony)
//            {
//                PopupDependencyError("Harmony");
//            }
//            if (!hasBurst)
//            {
//                PopupDependencyError("KSP Burst");
//            }
//            if (!hasMFI)
//            {
//                PopupDependencyError("Modular Flight Integrator");
//            }
//
//            // Cba to check for module manager most people just forget burst
//        }
//        bool Exists(string path, bool isDirectory)
//        {
//            if (isDirectory)
//            {
//                if (Directory.Exists(path))
//                {
//                    return true;
//                }
//                else
//                {
//                    return false;
//                }
//            }
//            else
//            {
//                if (File.Exists(path))
//                {
//                    return true;
//                }
//                else
//                {
//                    return false;
//                }
//            }
//        }
//        void PopupDependencyError(string name)
//        {
//            ParallaxDebug.LogCritical("Installation Error. You're missing a required dependency mod: " + name + ". This mod is required for Parallax to function. Do not report this to the author - you have not read the installation instructions correctly.");
//        }
//    }
//}
