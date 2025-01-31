using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;

namespace Parallax
{
    public class TextureLoader
    {
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
            uint BitCount = BitConverter.ToUInt32(data, 88); // Get bitdepth

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
            TextureLoaderData textureData = new TextureLoaderData();
            textureData.width = width;
            textureData.height = height;
            textureData.format = format;
            textureData.mips = mipMapCount > 1;
            textureData.linear = linear;
            textureData.unreadable = markUnreadable;

            // Create the texture
            Texture2D tex = new Texture2D(textureData.width, textureData.height, textureData.format, textureData.mips, textureData.linear);
            Texture2DFromData(tex, rawData, textureData);

            tex.Apply(false, textureData.unreadable);

            return tex;
        }
        public static Texture2D Texture2DFromData(byte[] bytes, in TextureLoaderData data)
        {
            Texture2D texture = new Texture2D(data.width, data.height, data.format, data.mips, data.linear);

            // Load texture data
            try
            {
                texture.LoadRawTextureData(bytes);
            }
            catch (Exception e)
            {
                ParallaxDebug.LogError($"Error loading texture data: {e.Message}");
                return null;
            }

            return texture;
        }
        public static Texture2D Texture2DFromData(Texture2D texture, byte[] bytes, in TextureLoaderData data)
        {
            //Texture2D texture = new Texture2D(data.width, data.height, data.format, data.mips, data.linear);
            // Load texture data
            try
            {
                if (texture.format == TextureFormat.DXT5 || texture.format == TextureFormat.DXT1 || texture.format == TextureFormat.R8 || texture.format == TextureFormat.Alpha8 || texture.format == TextureFormat.R16)
                {
                    // Could be faster
                    NativeArray<byte> rawData = texture.GetRawTextureData<byte>();
                    rawData.CopyFrom(bytes);
                }
                else
                {
                    texture.LoadRawTextureData(bytes);
                }

            }
            catch (Exception e)
            {
                ParallaxDebug.LogError($"Error loading texture data: {e.Message}");
                return null;
            }

            return texture;
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
