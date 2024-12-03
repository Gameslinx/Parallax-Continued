using UnityEngine;
using System.IO;

public class HighResScreenshot : MonoBehaviour
{
    public int resolutionMultiplier = 2; // Multiplier for the native resolution (e.g., 2x for double the resolution).
    public KeyCode screenshotKey = KeyCode.P; // Key to trigger the screenshot.

    private void Update()
    {
        if (Input.GetKeyDown(screenshotKey))
        {
            TakeScreenshot();
        }
    }

    private void TakeScreenshot()
    {
        string saveFolder = Application.dataPath + "/Textures/Screenshots/";
        Debug.Log(saveFolder);
        // Ensure the save folder exists.
        if (!Directory.Exists(saveFolder))
        {
            Directory.CreateDirectory(saveFolder);
        }

        // Determine screenshot resolution.
        int width = Screen.width * resolutionMultiplier;
        int height = Screen.height * resolutionMultiplier;

        // Create a render texture with the desired resolution.
        RenderTexture rt = new RenderTexture(width, height, 24);
        Camera.main.targetTexture = rt;

        // Render the camera to the texture.
        Camera.main.Render();

        // Set the active RenderTexture and create a Texture2D to capture it.
        RenderTexture.active = rt;
        Texture2D screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
        screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        screenshot.Apply();

        // Reset the camera and cleanup.
        Camera.main.targetTexture = null;
        RenderTexture.active = null;
        Destroy(rt);

        // Generate file name and save the screenshot as a PNG.
        string fileName = Path.Combine(saveFolder, "HighResScreenshot.png");
        File.WriteAllBytes(fileName, screenshot.EncodeToPNG());

        Debug.Log($"Screenshot saved to: {fileName}");
        Destroy(screenshot);
    }
}