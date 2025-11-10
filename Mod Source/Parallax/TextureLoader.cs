using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Parallax.Loading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.IO.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Parallax
{
    public class TextureLoader
    {

        struct ScatterTextureEntry
        {
            public string name;
            public string path;
            public TextureLoadManager.LoadHandle handle;
        }

        internal static void LoadScatterTexturesImmediate(
            IEnumerable<KeyValuePair<string, string>> textureValues,
            Dictionary<string, Texture> cache,
            Action<string, Texture> onLoaded
        )
        {
            // Make sure to launch all the different async loads before we start
            // blocking on them.
            HashSet<string> known = [];
            List<ScatterTextureEntry> handles = [];
            foreach (var (name, path) in textureValues)
            {
                TextureLoadManager.LoadHandle handle;
                if (known.Contains(path))
                {
                    continue;
                }
                else if (cache.TryGetValue(path, out var tex))
                {
                    onLoaded(name, tex);
                    continue;
                }
                else
                {
                    bool linear = TextureUtils.IsLinear(name);
                    handle = TextureLoadManager.LoadTextureAsync(path, linear);
                }

                handles.Add(new() { name = name, path = path, handle = handle});
                known.Add(path);
            }

            // Now we block on the individual loads as we go.
            foreach (var entry in handles)
            {
                Texture2D tex2d;
                try
                {
                    entry.handle.Complete();
                    tex2d = entry.handle.Texture;
                }
                catch (Exception e)
                {
                    ParallaxDebug.LogError($"Failed to load Parallax Texture: {entry.handle.Path}");
                    Debug.LogException(e);
                    continue;
                }

                Texture tex = tex2d;
                if (TextureUtils.IsCube(entry.name))
                    tex = CubemapFromTexture2D(tex2d);

                cache.Add(entry.path, tex);
                onLoaded(entry.name, tex);
            }
        }

        /// <summary>
        /// Load a batch of textures immediately, reusing ones in the cache
        /// if present, and otherwise doing as much work in parallel as
        /// possible.
        /// </summary>
        /// <param name="textureValues"></param>
        /// <param name="loadedTextures"></param>
        /// <param name="onLoaded"></param>
        internal static void LoadTexturesImmediateWithCache(
            IEnumerable<KeyValuePair<string, string>> textureValues,
            Dictionary<string, Texture2D> loadedTextures,
            Action<string, Texture2D> onLoaded
        )
        {
            // Make sure to launch all the different async loads before we start
            // blocking on them.
            HashSet<string> known = [];
            List<KeyValuePair<string, TextureLoadManager.LoadHandle>> handles = [];
            foreach (var (name, path) in textureValues)
            {
                TextureLoadManager.LoadHandle handle;
                if (known.Contains(name))
                {
                    continue;
                }
                else if (loadedTextures.TryGetValue(name, out var tex))
                {
                    handle = TextureLoadManager.CreateCompletedHandle(tex, path);
                }
                else
                {
                    bool linear = TextureUtils.IsLinear(name);
                    handle = TextureLoadManager.LoadTextureAsync(path, linear);
                }

                handles.Add(new(name, handle));
                known.Add(name);
            }

            // Now we block on the individual loads as we go.
            foreach (var (name, handle) in handles)
            {
                Texture2D tex;
                try
                {
                    handle.Complete();
                    tex = handle.Texture;
                }
                catch (Exception e)
                {
                    ParallaxDebug.LogError($"Failed to load Parallax Texture: {handle.Path}");
                    Debug.LogException(e);
                    continue;
                }

                if (!loadedTextures.ContainsKey(name))
                    loadedTextures.Add(name, tex);
                onLoaded(name, tex);
            }
        }
        

        /// <summary>
        /// Start loading a batch of textures immediately and call
        /// <paramref name="onLoaded"/> as each texture finishes loading.
        /// This may happen immediately or spread out over a few frames,
        /// depending on how much overhead there is when calling
        /// <see cref="Texture2D.GetRawTextureData{byte}"/>.
        /// </summary>
        /// <param name="textureValues"></param>
        /// <param name="loadedTextures"></param>
        /// <param name="onLoaded"></param>
        internal static IEnumerable LoadTexturesAsyncWithCache(
            IEnumerable<KeyValuePair<string, string>> textureValues,
            Dictionary<string, Texture2D> loadedTextures,
            Action<string, Texture2D> onLoaded
        )
        {
            // Make sure to launch all the different async loads before we start
            // blocking on them.
            HashSet<string> known = [];
            List<KeyValuePair<string, TextureLoadManager.LoadHandle>> handles = [];
            foreach (var (name, path) in textureValues)
            {
                TextureLoadManager.LoadHandle handle;
                if (known.Contains(name))
                {
                    continue;
                }
                else if (loadedTextures.TryGetValue(name, out var tex))
                {
                    handle = TextureLoadManager.CreateCompletedHandle(tex, path);
                }
                else
                {
                    bool linear = TextureUtils.IsLinear(name);
                    handle = TextureLoadManager.LoadTextureAsync(path, linear);
                }

                handles.Add(new(name, handle));
            }

            // Now we block on the individual loads as we go.
            foreach (var (name, handle) in handles)
            {
                if (!handle.IsComplete)
                    yield return new WaitUntil(() => handle.IsComplete);

                Texture2D tex;
                try
                {
                    handle.Complete();
                    tex = handle.Texture;
                }
                catch (Exception e)
                {
                    ParallaxDebug.LogError($"Failed to load Parallax Texture: {handle.Path}");
                    Debug.LogException(e);
                    continue;
                }

                if (!loadedTextures.ContainsKey(name))
                    loadedTextures.Add(name, tex);
                onLoaded(name, tex);
            }
        }

        // Helper function
        public static Cubemap CubemapFromTexture2D(Texture2D texture)
        {
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            int cubedim = texture.width / 4;
            Cubemap cube = new Cubemap(cubedim, TextureFormat.ARGB32, false);
            cube.SetPixels(texture.GetPixels(2 * cubedim, 2 * cubedim, cubedim, cubedim), CubemapFace.NegativeY);
            cube.SetPixels(texture.GetPixels(3 * cubedim, cubedim, cubedim, cubedim), CubemapFace.PositiveX);
            cube.SetPixels(texture.GetPixels(2 * cubedim, cubedim, cubedim, cubedim), CubemapFace.PositiveZ);
            cube.SetPixels(texture.GetPixels(cubedim, cubedim, cubedim, cubedim), CubemapFace.NegativeX);
            cube.SetPixels(texture.GetPixels(0, cubedim, cubedim, cubedim), CubemapFace.NegativeZ);
            cube.SetPixels(texture.GetPixels(2 * cubedim, 0, cubedim, cubedim), CubemapFace.PositiveY);
            cube.Apply(true, true);
            stopwatch.Stop();
            ParallaxDebug.Log("Cubemap conversion took: " + stopwatch.Elapsed.TotalMilliseconds.ToString("F5") + " ms");
            return cube;
        }
    }
}
