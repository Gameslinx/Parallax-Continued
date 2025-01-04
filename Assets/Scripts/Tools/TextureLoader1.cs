using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

[CustomEditor(typeof(TextureLoader1))]
class DecalMeshHelperEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (GUILayout.Button("Load DDS"))
        {
            TextureLoader1 instance = (TextureLoader1)target;
            instance.Load();
        }
    }
}
public class TextureLoader1 : MonoBehaviour
{
    // Start is called before the first frame update
    public void Load()
    {
        string filePath = "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Kerbal Space Program\\GameData\\Parallax_StockPlanetTextures\\Kerbin\\PluginData\\Kerbin_Height.dds";
        Texture2D tex = LoadDDSTexture(filePath, false, false);
        gameObject.GetComponent<MeshRenderer>().sharedMaterial.SetTexture("_HeightMap", tex);
    }

    public static Texture2D LoadDDSTexture(string url, bool linear, bool markUnreadable)
    {
        byte[] data = File.ReadAllBytes(url);
        if (data.Length < 128)
        {
            Debug.LogError("This DDS texture is invalid - File is too small to contain a valid header.");
            return null;
        }

        byte ddsSizeCheck = data[4];
        if (ddsSizeCheck != 124)
        {
            Debug.LogError("This DDS texture is invalid - Header size check failed.");
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
                Debug.Log("DXT1 texture detected");
                format = TextureFormat.DXT1;
            }
            else if (fourCC == 0x35545844) // 'DXT5'
            {
                Debug.Log("DXT5 texture detected");
                format = TextureFormat.DXT5;
            }
            else
            {
                Debug.LogError($"Unsupported FourCC code: 0x{fourCC:X}");
                return null;
            }
        }
        else if ((pixelFormatFlags & 0x40) != 0 && fourCC == 0) // DDPF_ALPHAPIXELS (standard L8)
        {
            Debug.Log("Uncompressed L8 (luminance) texture detected (DDPF_ALPHAPIXELS style)");
            format = TextureFormat.Alpha8; // Equivalent to L8
        }
        else if ((pixelFormatFlags & 0x20000) != 0 && fourCC == 0) // DDPF_FOURCC with no FourCC (alternate L8)
        {
            Debug.Log("Uncompressed L8 (luminance) texture detected (DDPF_FOURCC style)");
            format = TextureFormat.R8; // Equivalent to L8
        }
        else
        {
            Debug.LogError($"Unable to determine texture type. PixelFormatFlags: 0x{pixelFormatFlags:X}, FourCC: 0x{fourCC:X}");
            return null;
        }

        // Create the Texture2D with or without mipmaps based on the header
        Texture2D texture = mipMapCount > 1
            ? new Texture2D(width, height, format, true, linear)
            : new Texture2D(width, height, format, false, linear);

        // Load texture data
        try
        {
            texture.LoadRawTextureData(rawData);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error loading texture data: {e.Message}");
            return null;
        }

        // Apply changes to the texture
        texture.Apply(true, markUnreadable);

        return texture;
    }
}
