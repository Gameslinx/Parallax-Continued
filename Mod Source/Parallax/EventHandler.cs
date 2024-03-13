using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Parallax
{
    [KSPAddon(KSPAddon.Startup.Instantly, false)]
    public class EventHandler : MonoBehaviour
    {
        public delegate void QuadRangeCheck();
        public static event QuadRangeCheck OnQuadRangeCheck;
        
        public static EventHandler Instance;
        public static ParallaxBody currentParallaxBody;

        CelestialBody currentBody;
        void Awake()
        {
            GameObject.DontDestroyOnLoad(this);
            Instance = this;
        }
        // This method exists because there's no event that fires on planet change but before PQS build, so there can't be a call to load the textures.
        // This method is called twice, once from PQS to load the textures, and once from body change update() check
        public static void RequestLoad(string planetName, string source)
        {
            Debug.Log("Load requested: " + planetName);
            // First check if the body is a parallax body
            if (!ConfigLoader.parallaxBodies.ContainsKey(planetName))
            {
                return;
            }

            ParallaxBody body = ConfigLoader.parallaxBodies[planetName];

            // This is a new body, we should load it
            if (body.loaded)
            {
                // This route can be taken, and often is taken, by the Update() check because the PQS will usually request this first
                return;
            }
            else
            {
                body.Load(true);
                currentParallaxBody = body;
            }
        }
        public static void RequestUnload(string planetName, string source)
        {
            Debug.Log("Unload requested: " + planetName);
            if (!ConfigLoader.parallaxBodies.ContainsKey(planetName))
            {
                return;
            }

            ParallaxBody body = ConfigLoader.parallaxBodies[planetName];

            // The planet we're trying to unload is the current body
            if (body.loaded)
            {
                body.Unload();
            }
            else
            {
                return;
            }
        }
        void Update()
        {
            // Check if body changed
            if (FlightGlobals.currentMainBody != currentBody)
            {
                if (currentBody != null)
                {
                    EventHandler.RequestUnload(currentBody.name, "EventHandler");
                }
                if (FlightGlobals.currentMainBody != null)
                {
                    EventHandler.RequestLoad(FlightGlobals.currentMainBody.name, "EventHandler");
                }
                currentBody = FlightGlobals.currentMainBody;
            }
        }
        void FixedUpdate()
        {
            if (OnQuadRangeCheck != null)
            {
                OnQuadRangeCheck();
            }
        }
    }
}