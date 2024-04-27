using Kopernicus.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Parallax
{
    public class LoadOnDemand
    {
        public static void OnDominantBodyChange(CelestialBody from, CelestialBody to)
        {
            ParallaxDebug.Log("Body changed: " + from?.name + " to " + to?.name);

            // Safely unload everything before loading the new textures
            UnloadAll();

            if (to != null)
            {
                LoadBody(ConfigLoader.parallaxTerrainBodies[to.name]);
            }
        }
        public static void LoadBody(ParallaxTerrainBody body)
        {
            body.Load(true);
        }
        public static void UnloadAll()
        {
            foreach (KeyValuePair<string, ParallaxTerrainBody> body in ConfigLoader.parallaxTerrainBodies)
            {
                if (body.Value.loaded)
                {
                    body.Value.Unload();
                }
            }
        }
    }
}
