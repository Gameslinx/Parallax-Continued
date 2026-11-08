#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

public static class ShaderBundleBuilder
{
    struct BundleDefinition
    {
        public string name;
        public string folder;
        public string filter;
        public string[] excludedFolders;

        public BundleDefinition(string name, string folder, string filter, params string[] excludedFolders)
        {
            this.name = name;
            this.folder = folder;
            this.filter = filter;
            this.excludedFolders = excludedFolders;
        }
    }

    struct PlatformDefinition
    {
        public BuildTarget target;
        public string suffix;
        public GraphicsDeviceType[] graphicsAPIs;

        public PlatformDefinition(BuildTarget target, string suffix, params GraphicsDeviceType[] graphicsAPIs)
        {
            this.target = target;
            this.suffix = suffix;
            this.graphicsAPIs = graphicsAPIs;
        }
    }

    static readonly BundleDefinition[] bundles =
    {
        new BundleDefinition("ParallaxTerrain", "Assets/Shaders/Terrain", "t:Shader"),
        new BundleDefinition("ParallaxScatters", "Assets/Shaders/Scatters/InstancingShaders", "t:Shader"),
        new BundleDefinition("ParallaxCompute", "Assets/Shaders/Scatters", "t:ComputeShader"),
        new BundleDefinition("ParallaxScaled", "Assets/Shaders/Scaled", "t:Shader"),
        new BundleDefinition("ParallaxDebug", "Assets/Shaders/Debug", "t:Shader", "Assets/Shaders/Debug/Old Parallax"),
    };

#if UNITY_6000_0_OR_NEWER
    const string versionInfix = "-unity6";
    const BuildAssetBundleOptions options = BuildAssetBundleOptions.ForceRebuildAssetBundle;
    static readonly PlatformDefinition[] platforms =
    {
        new PlatformDefinition(BuildTarget.StandaloneWindows64, "-windows", GraphicsDeviceType.Direct3D11, GraphicsDeviceType.Direct3D12, GraphicsDeviceType.Vulkan),
        new PlatformDefinition(BuildTarget.StandaloneOSX, "-macosx", GraphicsDeviceType.Metal),
        new PlatformDefinition(BuildTarget.StandaloneLinux64, "-linux", GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLCore),
    };
#else
    const string versionInfix = "";
#pragma warning disable CS0618
    const BuildAssetBundleOptions options = BuildAssetBundleOptions.DeterministicAssetBundle | BuildAssetBundleOptions.ForceRebuildAssetBundle;
#pragma warning restore CS0618
    static readonly PlatformDefinition[] platforms =
    {
        new PlatformDefinition(BuildTarget.StandaloneWindows, "-windows"),
        new PlatformDefinition(BuildTarget.StandaloneOSX, "-macosx"),
        new PlatformDefinition(BuildTarget.StandaloneLinux64, "-linux"),
    };
#endif

    const string outputOption = "-parallaxOutput";

    static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');

    static string[] FindBundleAssets(BundleDefinition bundle)
    {
        return AssetDatabase.FindAssets(bundle.filter, new[] { bundle.folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => !bundle.excludedFolders.Any(excluded => path.StartsWith(excluded + "/", StringComparison.OrdinalIgnoreCase)))
            .Distinct()
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    static void BuildBundles(string outputDirectory)
    {
        string stagingDirectory = ProjectRoot + "/Temp/ParallaxShaderBundles";
        Directory.CreateDirectory(outputDirectory);

        foreach (PlatformDefinition platform in platforms)
        {
            if (platform.graphicsAPIs.Length > 0)
            {
                PlayerSettings.SetUseDefaultGraphicsAPIs(platform.target, false);
                PlayerSettings.SetGraphicsAPIs(platform.target, platform.graphicsAPIs);
            }

            List<AssetBundleBuild> builds = new List<AssetBundleBuild>();
            foreach (BundleDefinition bundle in bundles)
            {
                string[] assets = FindBundleAssets(bundle);
                if (assets.Length == 0)
                {
                    throw new Exception("No assets found for shader bundle " + bundle.name + " in " + bundle.folder);
                }
                builds.Add(new AssetBundleBuild
                {
                    assetBundleName = (bundle.name + versionInfix + platform.suffix + ".unity3d").ToLowerInvariant(),
                    assetNames = assets
                });
            }

            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, true);
            }
            Directory.CreateDirectory(stagingDirectory);

            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(stagingDirectory, builds.ToArray(), options, platform.target);
            if (manifest == null)
            {
                throw new Exception("Shader bundle build failed for " + platform.target);
            }

            foreach (BundleDefinition bundle in bundles)
            {
                string fileName = bundle.name + versionInfix + platform.suffix + ".unity3d";
                File.Copy(stagingDirectory + "/" + fileName.ToLowerInvariant(), outputDirectory + "/" + fileName, true);
                Debug.Log("[Parallax] Built shader bundle " + outputDirectory + "/" + fileName);
            }
        }

        Directory.Delete(stagingDirectory, true);
    }

#if UNITY_6000_0_OR_NEWER
    public static void BuildFromCommandLine()
    {
        try
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, outputOption);
            if (index < 0 || index + 1 >= args.Length)
            {
                throw new Exception("Missing " + outputOption + " <directory> argument");
            }

            KeepInstancingVariants();
            BuildBundles(args[index + 1]);
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    static void KeepInstancingVariants()
    {
        UnityEngine.Object graphicsSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset").FirstOrDefault();
        if (graphicsSettings == null)
        {
            throw new Exception("Unable to load ProjectSettings/GraphicsSettings.asset");
        }

        SerializedObject serializedSettings = new SerializedObject(graphicsSettings);
        SerializedProperty instancingStripping = serializedSettings.FindProperty("m_InstancingStripping");
        if (instancingStripping != null && instancingStripping.intValue != 2)
        {
            instancingStripping.intValue = 2;
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }
    }
#else
    const string editorPathKey = "Parallax.ShaderBundles.Unity6EditorPath";
    const string projectPathKey = "Parallax.ShaderBundles.Unity6ProjectPath";
    const string hubEditorDirectory = "C:/Program Files/Unity/Hub/Editor";

    static string OutputDirectory => ProjectRoot + "/Mod Source/GameData/ParallaxContinued/Shaders";

    [MenuItem("Parallax/Build Shader Bundles/All")]
    static void BuildAll()
    {
        if (BuildUnity2019())
        {
            BuildUnity6();
        }
    }

    [MenuItem("Parallax/Build Shader Bundles/Unity 2019 (KSP)")]
    static void BuildUnity2019MenuItem()
    {
        BuildUnity2019();
    }

    [MenuItem("Parallax/Build Shader Bundles/Unity 6 (KSP Retooled)")]
    static void BuildUnity6MenuItem()
    {
        BuildUnity6();
    }

    [MenuItem("Parallax/Build Shader Bundles/Configure Unity 6 Paths...")]
    static void ConfigureUnity6()
    {
        string editorPath = EditorUtility.OpenFilePanel("Select the Unity 6 editor executable", Path.GetDirectoryName(DefaultEditorPath()) ?? hubEditorDirectory, "exe");
        if (string.IsNullOrEmpty(editorPath))
        {
            return;
        }
        string projectPath = EditorUtility.OpenFolderPanel("Select the Unity 6 shader build project", Path.GetDirectoryName(ProjectRoot), "");
        if (string.IsNullOrEmpty(projectPath))
        {
            return;
        }
        EditorPrefs.SetString(editorPathKey, editorPath);
        EditorPrefs.SetString(projectPathKey, projectPath);
        Debug.Log("[Parallax] Unity 6 editor: " + editorPath + ", project: " + projectPath);
    }

    static bool BuildUnity2019()
    {
        try
        {
            BuildBundles(OutputDirectory);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Shader bundle build failed", e.Message, "OK");
            return false;
        }
    }

    static string DefaultEditorPath()
    {
        if (!Directory.Exists(hubEditorDirectory))
        {
            return null;
        }
        return Directory.GetDirectories(hubEditorDirectory, "6000.*")
            .OrderByDescending(path => path, StringComparer.Ordinal)
            .Select(path => path.Replace('\\', '/') + "/Editor/Unity.exe")
            .FirstOrDefault(File.Exists);
    }

    static string DefaultProjectPath()
    {
        string sibling = Path.GetDirectoryName(ProjectRoot).Replace('\\', '/') + "/" + Path.GetFileName(ProjectRoot) + " 6.7";
        return Directory.Exists(sibling) ? sibling : null;
    }

    static bool TryGetUnity6Paths(out string editorPath, out string projectPath)
    {
        editorPath = EditorPrefs.GetString(editorPathKey, DefaultEditorPath());
        projectPath = EditorPrefs.GetString(projectPathKey, DefaultProjectPath());
        if (!File.Exists(editorPath) || !Directory.Exists(projectPath + "/ProjectSettings"))
        {
            ConfigureUnity6();
            editorPath = EditorPrefs.GetString(editorPathKey, null);
            projectPath = EditorPrefs.GetString(projectPathKey, null);
        }
        return File.Exists(editorPath) && Directory.Exists(projectPath + "/ProjectSettings");
    }

    static bool IsProjectOpen(string projectPath)
    {
        string lockFile = projectPath + "/Temp/UnityLockfile";
        if (!File.Exists(lockFile))
        {
            return false;
        }
        try
        {
            using (new FileStream(lockFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                return false;
            }
        }
        catch (IOException)
        {
            return true;
        }
    }

    static void BuildUnity6()
    {
        if (!TryGetUnity6Paths(out string editorPath, out string projectPath))
        {
            EditorUtility.DisplayDialog("Unity 6 shader bundles", "The Unity 6 editor or project path is not set or doesn't exist. Use Parallax > Build Shader Bundles > Configure Unity 6 Paths.", "OK");
            return;
        }
        if (IsProjectOpen(projectPath))
        {
            EditorUtility.DisplayDialog("Unity 6 shader bundles", "The Unity 6 project is open in another editor. Close it and try again.\n\n" + projectPath, "OK");
            return;
        }

        try
        {
            SyncProject(projectPath);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Unity 6 shader bundles", "Failed to copy the shaders to the Unity 6 project: " + e.Message, "OK");
            return;
        }

        string logPath = ProjectRoot + "/Logs/ParallaxUnity6ShaderBuild.log";
        string arguments = "-batchmode -quit"
            + " -projectPath \"" + projectPath + "\""
            + " -executeMethod " + nameof(ShaderBundleBuilder) + ".BuildFromCommandLine"
            + " " + outputOption + " \"" + OutputDirectory + "\""
            + " -logFile \"" + logPath + "\"";

        Stopwatch stopwatch = Stopwatch.StartNew();
        Process process = Process.Start(new ProcessStartInfo(editorPath, arguments) { UseShellExecute = false, CreateNoWindow = true });
        bool cancelled = false;
        try
        {
            while (!process.WaitForExit(250))
            {
                float progress = (float)(stopwatch.Elapsed.TotalSeconds % 60.0 / 60.0);
                if (EditorUtility.DisplayCancelableProgressBar("Building Unity 6 shader bundles", "Running Unity 6 in batch mode (" + stopwatch.Elapsed.ToString(@"mm\:ss") + ")", progress))
                {
                    cancelled = true;
                    process.Kill();
                    process.WaitForExit();
                    break;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (cancelled)
        {
            Debug.LogWarning("[Parallax] Unity 6 shader bundle build cancelled");
            return;
        }

        if (process.ExitCode == 0)
        {
            Debug.Log("[Parallax] Unity 6 shader bundles built in " + stopwatch.Elapsed.TotalSeconds.ToString("F1") + " s. Log: " + logPath);
        }
        else
        {
            string tail = File.Exists(logPath) ? string.Join("\n", File.ReadAllLines(logPath).Reverse().Take(40).Reverse()) : "No log file was written";
            Debug.LogError("[Parallax] Unity 6 shader bundle build failed with exit code " + process.ExitCode + ". Log: " + logPath + "\n" + tail);
            EditorUtility.DisplayDialog("Unity 6 shader bundles", "The build failed with exit code " + process.ExitCode + ". See the console or " + logPath, "OK");
        }
    }

    static void SyncProject(string projectPath)
    {
        MirrorDirectory(ProjectRoot + "/Assets/Shaders", projectPath + "/Assets/Shaders");

        string scriptPath = AssetDatabase.FindAssets(nameof(ShaderBundleBuilder) + " t:MonoScript")
            .Select(AssetDatabase.GUIDToAssetPath)
            .First(path => Path.GetFileNameWithoutExtension(path) == nameof(ShaderBundleBuilder));
        Directory.CreateDirectory(projectPath + "/Assets/Editor");
        CopyIfChanged(ProjectRoot + "/" + scriptPath, projectPath + "/Assets/Editor/" + nameof(ShaderBundleBuilder) + ".cs");
    }

    static bool IsMeta(string path) => path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase);

    static void CopyIfChanged(string source, string destination)
    {
        FileInfo sourceInfo = new FileInfo(source);
        FileInfo destinationInfo = new FileInfo(destination);
        if (destinationInfo.Exists && destinationInfo.Length == sourceInfo.Length && destinationInfo.LastWriteTimeUtc == sourceInfo.LastWriteTimeUtc)
        {
            return;
        }
        File.Copy(source, destination, true);
        File.SetLastWriteTimeUtc(destination, sourceInfo.LastWriteTimeUtc);
    }

    static void MirrorDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        HashSet<string> sourceFiles = new HashSet<string>(Directory.GetFiles(source).Where(path => !IsMeta(path)).Select(Path.GetFileName), StringComparer.OrdinalIgnoreCase);
        HashSet<string> sourceDirectories = new HashSet<string>(Directory.GetDirectories(source).Select(Path.GetFileName), StringComparer.OrdinalIgnoreCase);

        foreach (string file in Directory.GetFiles(destination))
        {
            string name = Path.GetFileName(file);
            string assetName = IsMeta(name) ? name.Substring(0, name.Length - 5) : name;
            if (!sourceFiles.Contains(assetName) && !sourceDirectories.Contains(assetName))
            {
                File.Delete(file);
            }
        }
        foreach (string directory in Directory.GetDirectories(destination))
        {
            if (!sourceDirectories.Contains(Path.GetFileName(directory)))
            {
                Directory.Delete(directory, true);
            }
        }

        foreach (string name in sourceFiles)
        {
            CopyIfChanged(source + "/" + name, destination + "/" + name);
        }
        foreach (string name in sourceDirectories)
        {
            MirrorDirectory(source + "/" + name, destination + "/" + name);
        }
    }
#endif
}
#endif
