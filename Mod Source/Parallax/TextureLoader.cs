using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        const int DDS_HEADER_SIZE = 128;

        public struct TextureLoaderData
        {
            public int width, height;
            public TextureFormat format;
            public bool mips;
            public bool linear;
            public bool unreadable;
        }

        public static Texture2D LoadTexture(string path, bool linear, bool markUnreadable = true)
        {
            ParallaxDebug.Log("Loading Parallax Texture: " + path);
            Texture2D output;
            string filePath = ConfigLoader.GameDataPath + path;
            if (path.EndsWith(".dds"))
            {
                output = TextureLoader.LoadDDSTexture(filePath, linear, markUnreadable);
            }
            else
            {
                output = TextureLoader.LoadPNGTexture(filePath, linear, markUnreadable);
            }
            return output;
        }
        public static byte[] LoadTextureData(string path, bool linear, bool unreadable, out TextureLoaderData textureData)
        {
            string filePath = ConfigLoader.GameDataPath + path;
            if (path.EndsWith(".dds"))
            {
                return TextureLoader.LoadDDSTextureData(filePath, linear, unreadable, out textureData);
            }
            else
            {
                //return TextureLoader.LoadPNGTextureData(filePath);
                textureData = new TextureLoaderData();
                return null;
            }
        }
        public static Cubemap LoadCubeTexture(string name, bool linear)
        {
            ParallaxDebug.Log("Loading Parallax Cubemap: " + name);
            Texture2D texture = LoadTexture(name, linear, false);
            Cubemap result = CubemapFromTexture2D(texture);
            return result;
        }
        public static Texture2D LoadPNGTexture(string url, bool linear, bool markUnreadable)
        {
            Texture2D tex;
            tex = new Texture2D(2, 2, TextureFormat.ARGB32, true, linear);
            tex.LoadRawTextureData(File.ReadAllBytes(url));
            tex.Apply(true, markUnreadable);
            return tex;
        }
        public static byte[] LoadDDSTextureData(string url, bool linear, bool markUnreadable, out TextureLoaderData textureData)
        {
            textureData = new TextureLoaderData();

            byte[] data = File.ReadAllBytes(url);
            if (data.Length < 128)
            {
                ParallaxDebug.LogError("This DDS texture is invalid - File is too small to contain a valid header.");
                return null;
            }

            byte ddsSizeCheck = data[4];
            if (ddsSizeCheck != 124)
            {
                ParallaxDebug.LogError("This DDS texture is invalid - Header size check failed.");
                return null;
            }

            int height = BitConverter.ToInt32(data, 12);
            int width = BitConverter.ToInt32(data, 16);

            const int DDS_HEADER_SIZE = 128;
            byte[] rawData = new byte[data.Length - DDS_HEADER_SIZE];

            Buffer.BlockCopy(data, DDS_HEADER_SIZE, rawData, 0, data.Length - DDS_HEADER_SIZE);

            int mipMapCount = BitConverter.ToInt32(data, 28);
            uint pixelFormatFlags = BitConverter.ToUInt32(data, 80);
            uint fourCC = BitConverter.ToUInt32(data, 84);

            TextureFormat format;

            if ((pixelFormatFlags & 0x4) != 0) // DDPF_FOURCC
            {
                if (fourCC == 0x31545844) // 'DXT1'
                {
                    format = TextureFormat.DXT1;
                }
                else if (fourCC == 0x35545844) // 'DXT5'
                {
                    format = TextureFormat.DXT5;
                }
                else
                {
                    return null;
                }
            }
            else if ((pixelFormatFlags & 0x40) != 0 && fourCC == 0) // DDPF_ALPHAPIXELS (standard L8)
            {
                format = TextureFormat.Alpha8; // Equivalent to L8
            }
            else if ((pixelFormatFlags & 0x20000) != 0 && fourCC == 0) // DDPF_FOURCC with no FourCC (alternate L8)
            {
                format = TextureFormat.R8; // Equivalent to L8
            }
            else
            {
                return null;
            }

            // Create the Texture2D with or without mipmaps based on the header
            textureData.width = width;
            textureData.height = height;
            textureData.format = format;
            textureData.mips = mipMapCount > 1;
            textureData.linear = linear;
            textureData.unreadable = markUnreadable;

            return rawData;
        }
        public static Texture2D LoadDDSTexture(string url, bool linear, bool markUnreadable)
        {
            using var inFlight = InFlightLoadData.Start(url, linear, markUnreadable);
            inFlight?.Materialize();
            return inFlight?.Complete();
        }

        public struct TextureLoadRequest()
        {
            public string path;
            public bool linear;
            public bool unreadable = true;
        }

        class InFlightLoadData : IDisposable
        {
            public Texture2D texture;
            public TextureLoaderData textureData;
            public SafeReadHandle handle;
            public NativeArray<byte> data;
            public NativeArray<byte>? rawTextureData;

            private InFlightLoadData() { }

            public static unsafe InFlightLoadData Start(string path, bool linear, bool unreadable)
            {
                const int DDS_HEADER_SIZE = 128;
                byte[] header = new byte[DDS_HEADER_SIZE];
                long length;
                using (var file = File.OpenRead(path))
                {
                    length = file.Length;
                    if (length < 128)
                    {
                        ParallaxDebug.LogError("This DDS texture is invalid - File is too small to contain a valid header.");
                        return null;
                    }

                    if (length > int.MaxValue)
                    {
                        ParallaxDebug.LogError("This DDS texture is too large to load");
                        return null;
                    }

                    if (file.Read(header, 0, DDS_HEADER_SIZE) < DDS_HEADER_SIZE)
                    {
                        ParallaxDebug.LogError("This DDS texture is invalid - File is too small to contain a valid header.");
                        return null;
                    }
                }

                byte ddsSizeCheck = header[4];
                if (ddsSizeCheck != 124)
                {
                    ParallaxDebug.LogError("This DDS texture is invalid - Header size check failed.");
                    return null;
                }

                if (GetTextureLoaderData(header, linear, unreadable) is not TextureLoaderData textureData)
                    return null;

                var inFlight = new InFlightLoadData()
                {
                    texture = CreateUninitializedTexture(in textureData),
                    textureData = textureData,
                    data = new NativeArray<byte>(
                        (int)(length - DDS_HEADER_SIZE),
                        // Specifically use TempJob here because Allocator.Temp is a bump
                        // allocator and these are large allocations.
                        Allocator.TempJob,
                        NativeArrayOptions.UninitializedMemory
                    )
                };
                
                ReadCommand command = new()
                {
                    Buffer = inFlight.data.GetUnsafePtr(),
                    Offset = DDS_HEADER_SIZE,
                    Size = length - DDS_HEADER_SIZE
                };
                inFlight.handle = new SafeReadHandle(AsyncReadManager.Read(path, &command, 1));
                return inFlight;
            }

            public void Materialize()
            {
                var format = textureData.format;
                bool useRawData = format == TextureFormat.DXT5
                    || format == TextureFormat.DXT1
                    || format == TextureFormat.R8
                    || format == TextureFormat.Alpha8
                    || format == TextureFormat.R16;

                if (useRawData)
                    rawTextureData = texture.GetRawTextureData<byte>();
            }

            public Texture2D Complete()
            {
                handle.JobHandle.Complete();
                if (handle.Status == ReadStatus.Failed)
                {
                    ParallaxDebug.LogError($"Error reading DDS file from disk");
                    return null;
                }

                try
                {
                    if (rawTextureData is NativeArray<byte> rawData)
                        rawData.CopyFrom(data);
                    else
                        texture.LoadRawTextureData(data);

                    texture.Apply(false, textureData.unreadable);
                }
                catch (Exception e)
                {
                    ParallaxDebug.LogError($"Error loading texture data: {e.Message}");
                    return null;
                }

                return texture;
            }

            public void Dispose()
            {
                handle.Dispose();
                data.Dispose();
                rawTextureData?.Dispose();
            }
        }

        public static Texture2D[] BulkLoadTextures(TextureLoadRequest[] requests)
        {
            var textures = new Texture2D[requests.Length];
            var inFlight = new InFlightLoadData[requests.Length];

            try
            {
                // Start all disk reads before doing anything else
                for (int i = 0; i < requests.Length; ++i)
                {
                    var req = requests[i];
                    if (!req.path.EndsWith(".dds"))
                        continue;

                    inFlight[i] = InFlightLoadData.Start(req.path, req.linear, req.unreadable);
                }

                // Now get the raw texture pointers, if applicable.
                //
                // For initial texture load this tends to be very slow since it has
                // make a copy of the backing texture data for us to write into and
                // that seems to cause a ton of page faults.
                //
                // Later texture loads do not have this issue, but they still need
                // to perform a rather large memcpy and that takes time.
                for (int i = 0; i < requests.Length; ++i)
                    inFlight[i]?.Materialize();

                // Handle any pngs while disk reads are happening
                for (int i = 0; i < requests.Length; ++i)
                {
                    var req = requests[i];
                    if (req.path.EndsWith(".png"))
                        continue;

                    textures[i] = LoadPNGTexture(req.path, req.linear, req.unreadable);
                }

                // Now we actually complete and copy in the relevant data.
                for (int i = 0; i < requests.Length; ++i)
                {
                    if (inFlight[i] is null)
                        continue;

                    textures[i] = inFlight[i].Complete();
                }

                return textures;
            }
            finally
            {
                for (int i = 0; i < inFlight.Length; ++i)
                    inFlight[i]?.Dispose();
            }
        }

        static TextureLoaderData? GetTextureLoaderData(byte[] header, bool linear, bool markUnreadable)
        {
            
            int height = BitConverter.ToInt32(header, 12);
            int width = BitConverter.ToInt32(header, 16);

            int mipMapCount = BitConverter.ToInt32(header, 28);
            uint pixelFormatFlags = BitConverter.ToUInt32(header, 80);
            uint fourCC = BitConverter.ToUInt32(header, 84);
            uint BitCount = BitConverter.ToUInt32(header, 88); // Get bitdepth

            TextureFormat format;

            if ((pixelFormatFlags & 0x4) != 0) // DDPF_FOURCC
            {
                if (fourCC == 0x31545844) // 'DXT1'
                {
                    format = TextureFormat.DXT1;
                }
                else if (fourCC == 0x35545844) // 'DXT5'
                {
                    format = TextureFormat.DXT5;
                }
                else
                {
                    return null;
                }
            }
            else if ((pixelFormatFlags & 0x40) != 0 && fourCC == 0) // DDPF_ALPHAPIXELS (standard L8)
            {
                format = TextureFormat.Alpha8;
            }
            else if ((pixelFormatFlags & 0x20000) != 0 && fourCC == 0) // DDPF_FOURCC with no FourCC (alternate L8)
            {
                if (BitCount == 16)
                {
                    format = TextureFormat.R16;
                }
                else
                {
                    format = TextureFormat.R8;
                }
            }
            else
            {
                return null;
            }

            // Create the Texture2D with or without mipmaps based on the header
            return new TextureLoaderData
            {
                width = width,
                height = height,
                format = format,
                mips = mipMapCount > 1,
                linear = linear,
                unreadable = markUnreadable
            };
        }

        // This uses a bunch of undocumented flags to prevent unity from
        // initializing or sharing the texture before we apply it.
        static Texture2D CreateUninitializedTexture(in TextureLoaderData data)
        {
            var flags = TextureCreationFlags.DontInitializePixels;
            if (data.mips)
                flags |= TextureCreationFlags.MipChain;
            if (GraphicsFormatUtility.IsCrunchFormat(data.format))
                flags |= TextureCreationFlags.Crunch;

            var format = GraphicsFormatUtility.GetGraphicsFormat(data.format, !data.linear);
            var uflags = (UnityEngine.Experimental.Rendering.TextureCreationFlags)flags;
            
            return new Texture2D(data.width, data.height, format, uflags);
        }

        // This reflects the actual creation flags in
        // https://github.com/Unity-Technologies/UnityCsReference/blob/59b03b8a0f179c0b7e038178c90b6c80b340aa9f/Runtime/Export/Graphics/GraphicsEnums.cs#L626
        //
        // Most of the extra ones here are completely undocumented.
        [Flags]
        enum TextureCreationFlags
        {
            None,
            MipChain = 1 << 0,
            DontInitializePixels = 1 << 2,
            DontDestroyTexture = 1 << 3,
            DontCreateSharedTextureData = 1 << 4,
            APIShareable = 1 << 5,
            Crunch = 1 << 6,
        }

        // This takes care of completing the read and then disposing of it
        // appropriately, which saves us a whole bunch of try-catch blocks.
        readonly struct SafeReadHandle : IDisposable
        {
            readonly ReadHandle handle;

            public ReadStatus Status => handle.Status;
            public JobHandle JobHandle => handle.JobHandle;

            public SafeReadHandle(ReadHandle handle) => this.handle = handle; 

            public void Dispose()
            {
                if (!handle.IsValid())
                    return;
                if (handle.Status == ReadStatus.InProgress)
                    handle.JobHandle.Complete();
                
                handle.Dispose();
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
