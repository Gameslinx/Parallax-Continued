#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

// Dumps a frame (and its neighbours) from the loaded profiler capture into a Markdown report
// Frame numbers everywhere are the ones the Profiler window displays, which are frame index + 1
public class ProfilerFrameAnalyzer : EditorWindow
{
    public enum ThreadScope
    {
        MainThread,
        MainAndRender,
        AllThreads
    }

    [Serializable]
    public class Settings
    {
        public int frame = 1;
        public int radius = 2;
        public ThreadScope threads = ThreadScope.AllThreads;
        public float minHierarchyMs = 0.05f;
        public int maxHierarchyDepth = 14;
        public int topCount = 30;
        public bool hierarchyForEveryFrame = false;
    }

    class MarkerSample
    {
        public string thread;
        public string name;
        public float selfMs;
        public float inclusiveMs;
        public float calls;
        public float selfGcBytes;
    }

    class FrameResult
    {
        public int frame;
        public float cpuMs;
        public float gpuMs;
        public float fps;
        public float mainGcBytes;
        public Dictionary<string, MarkerSample> markers = new Dictionary<string, MarkerSample>();
        public StringBuilder hierarchy;

        public MarkerSample Get(string thread, string name)
        {
            string key = thread + "\n" + name;
            if (!markers.TryGetValue(key, out MarkerSample sample))
            {
                sample = new MarkerSample { thread = thread, name = name };
                markers.Add(key, sample);
            }
            return sample;
        }
    }

    class WalkContext
    {
        public HierarchyFrameDataView view;
        public string threadKey;
        public FrameResult result;
        public Settings settings;
        public StringBuilder hierarchy;
        public HashSet<string> namesOnPath = new HashSet<string>();
    }

    const int MaxThreads = 256;
    const int MaxConsecutiveMissingThreads = 8;
    const HierarchyFrameDataView.ViewModes ViewMode = HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName;
    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    [SerializeField] Settings settings = new Settings();
    [SerializeField] string capturePath = "";
    [SerializeField] string lastReportPath = "";
    string lastReport = "";

    [MenuItem("Parallax/Profiler Frame Analyzer")]
    static void Initialize()
    {
        GetWindow<ProfilerFrameAnalyzer>("Profiler Frame Analyzer").Show();
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Capture", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Last loaded file", string.IsNullOrEmpty(capturePath) ? "(none, using Profiler window data)" : capturePath);
        if (GUILayout.Button("Load capture (.data)..."))
        {
            string path = EditorUtility.OpenFilePanel("Load profiler capture", Path.GetDirectoryName(capturePath) ?? "", "data");
            if (!string.IsNullOrEmpty(path))
            {
                if (ProfilerDriver.LoadProfile(path, false))
                {
                    capturePath = path;
                }
                else
                {
                    EditorUtility.DisplayDialog("Profiler Frame Analyzer", "Failed to load " + path, "OK");
                }
            }
        }

        bool hasFrames = ProfilerDriver.lastFrameIndex >= 0;
        EditorGUILayout.LabelField("Frames available", hasFrames ? $"{ProfilerDriver.firstFrameIndex + 1} - {ProfilerDriver.lastFrameIndex + 1}" : "None");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Frame selection", EditorStyles.boldLabel);
        settings.frame = EditorGUILayout.IntField("Frame (as in Profiler)", settings.frame);
        using (new EditorGUI.DisabledScope(!hasFrames))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Use Profiler selection"))
            {
                int index = GetProfilerWindowFrameIndex();
                if (index >= 0)
                {
                    settings.frame = index + 1;
                }
            }
            if (GUILayout.Button("Find slowest frame"))
            {
                settings.frame = FindSlowestFrame();
            }
            EditorGUILayout.EndHorizontal();
        }
        settings.radius = EditorGUILayout.IntSlider("Frames either side", settings.radius, 0, 30);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Report", EditorStyles.boldLabel);
        settings.threads = (ThreadScope)EditorGUILayout.EnumPopup("Threads", settings.threads);
        settings.minHierarchyMs = Mathf.Max(0, EditorGUILayout.FloatField("Hierarchy min ms", settings.minHierarchyMs));
        settings.maxHierarchyDepth = Mathf.Max(1, EditorGUILayout.IntField("Hierarchy max depth", settings.maxHierarchyDepth));
        settings.topCount = Mathf.Max(1, EditorGUILayout.IntField("Rows per table", settings.topCount));
        settings.hierarchyForEveryFrame = EditorGUILayout.Toggle("Hierarchy for every frame", settings.hierarchyForEveryFrame);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(!hasFrames))
        {
            if (GUILayout.Button("Analyze", GUILayout.Height(28)))
            {
                try
                {
                    lastReport = Analyze(settings, capturePath);
                    lastReportPath = WriteReport(lastReport, capturePath, settings);
                    Debug.Log("[ProfilerFrameAnalyzer] Report written to " + lastReportPath);
                }
                catch (Exception e)
                {
                    EditorUtility.DisplayDialog("Profiler Frame Analyzer", e.Message, "OK");
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            }
        }

        if (!string.IsNullOrEmpty(lastReportPath) && File.Exists(lastReportPath))
        {
            EditorGUILayout.LabelField("Last report", lastReportPath);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Open"))
            {
                EditorUtility.OpenWithDefaultApp(lastReportPath);
            }
            if (GUILayout.Button("Reveal"))
            {
                EditorUtility.RevealInFinder(lastReportPath);
            }
            if (GUILayout.Button("Copy to clipboard"))
            {
                EditorGUIUtility.systemCopyBuffer = string.IsNullOrEmpty(lastReport) ? File.ReadAllText(lastReportPath) : lastReport;
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    // Usage: Unity.exe -batchmode -projectPath <project> -executeMethod ProfilerFrameAnalyzer.RunFromCommandLine
    //   -profilerCapture <file.data> -profilerFrame <number|slowest> [-profilerRadius <n>] [-profilerThreads main|render|all] [-profilerOutput <file.md>]
    public static void RunFromCommandLine()
    {
        try
        {
            string[] args = Environment.GetCommandLineArgs();
            string capture = GetArg(args, "-profilerCapture");
            if (string.IsNullOrEmpty(capture) || !ProfilerDriver.LoadProfile(capture, false))
            {
                throw new ArgumentException("Could not load capture from -profilerCapture: " + capture);
            }

            var cliSettings = new Settings();
            string frame = GetArg(args, "-profilerFrame") ?? "slowest";
            cliSettings.frame = frame == "slowest" ? FindSlowestFrame() : int.Parse(frame, Invariant);

            string radius = GetArg(args, "-profilerRadius");
            if (radius != null)
            {
                cliSettings.radius = int.Parse(radius, Invariant);
            }

            string threads = GetArg(args, "-profilerThreads");
            if (threads == "main") cliSettings.threads = ThreadScope.MainThread;
            else if (threads == "render") cliSettings.threads = ThreadScope.MainAndRender;

            string report = Analyze(cliSettings, capture);
            string output = GetArg(args, "-profilerOutput");
            if (string.IsNullOrEmpty(output))
            {
                output = WriteReport(report, capture, cliSettings);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
                File.WriteAllText(output, report);
            }
            Debug.Log("[ProfilerFrameAnalyzer] Report written to " + output);
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    static string GetArg(string[] args, string name)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    // ProfilerWindow is internal in 2019.4, so the selected frame is only reachable through reflection
    static int GetProfilerWindowFrameIndex()
    {
        Type windowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.ProfilerWindow");
        MethodInfo method = windowType?.GetMethod("GetActiveVisibleFrameIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        UnityEngine.Object window = windowType == null ? null : Resources.FindObjectsOfTypeAll(windowType).FirstOrDefault();
        if (method == null || window == null)
        {
            Debug.LogWarning("[ProfilerFrameAnalyzer] Profiler window is not open");
            return -1;
        }
        return (int)method.Invoke(window, null);
    }

    public static int FindSlowestFrame()
    {
        int first = ProfilerDriver.firstFrameIndex;
        int last = ProfilerDriver.lastFrameIndex;
        int slowest = first;
        float slowestMs = -1;
        try
        {
            for (int i = first; i <= last; i++)
            {
                if ((i - first) % 50 == 0)
                {
                    EditorUtility.DisplayProgressBar("Profiler Frame Analyzer", $"Scanning frame {i + 1}", (float)(i - first) / Mathf.Max(1, last - first));
                }
                using (var view = ProfilerDriver.GetHierarchyFrameDataView(i, 0, ViewMode, HierarchyFrameDataView.columnDontSort, false))
                {
                    if (view.valid && view.frameTimeMs > slowestMs)
                    {
                        slowestMs = view.frameTimeMs;
                        slowest = i;
                    }
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
        return slowest + 1;
    }

    public static string Analyze(Settings s, string capturePath)
    {
        int first = ProfilerDriver.firstFrameIndex;
        int last = ProfilerDriver.lastFrameIndex;
        if (last < 0)
        {
            throw new InvalidOperationException("No profiler frames are loaded");
        }

        int target = s.frame - 1;
        if (target < first || target > last)
        {
            throw new ArgumentOutOfRangeException(nameof(s.frame), $"Frame {s.frame} is outside the loaded range {first + 1} - {last + 1}");
        }

        int start = Math.Max(first, target - s.radius);
        int end = Math.Min(last, target + s.radius);
        var frames = new List<FrameResult>();
        for (int i = start; i <= end; i++)
        {
            EditorUtility.DisplayProgressBar("Profiler Frame Analyzer", $"Reading frame {i + 1}", (float)(i - start) / Mathf.Max(1, end - start));
            frames.Add(ReadFrame(i, s, s.hierarchyForEveryFrame || i == target));
        }
        EditorUtility.ClearProgressBar();

        FrameResult targetFrame = frames.First(f => f.frame == s.frame);
        var report = new StringBuilder();

        report.AppendLine("# Profiler frame analysis");
        report.AppendLine();
        report.AppendLine($"- Capture: {(string.IsNullOrEmpty(capturePath) ? "(data currently in the Profiler window)" : capturePath)}");
        report.AppendLine($"- Loaded frames: {first + 1} - {last + 1}");
        report.AppendLine($"- Target frame: {s.frame}, analysed frames {start + 1} - {end + 1} ({frames.Count} frames)");
        report.AppendLine($"- Threads: {s.threads}. Job worker threads are merged under their group name in the tables");
        report.AppendLine($"- Times are in ms. Self time excludes children, inclusive time counts each marker once per call path (outermost occurrence)");
        report.AppendLine();

        AppendFrameSummary(report, frames, s.frame);

        var stats = AggregateStats(frames);
        if (frames.Count > 1)
        {
            AppendTargetComparison(report, targetFrame, frames, stats, s.topCount);
        }
        AppendTopTable(report, $"Top markers by average self time ({frames.Count} frames)", stats, frames.Count, true, s.topCount);
        AppendTopTable(report, $"Top markers by average inclusive time ({frames.Count} frames)", stats, frames.Count, false, s.topCount);

        foreach (FrameResult frame in frames.Where(f => f.hierarchy != null))
        {
            report.AppendLine($"## Hierarchy, frame {frame.frame}{(frame.frame == s.frame ? " (target)" : "")}");
            report.AppendLine();
            report.AppendLine($"Items below {Format(s.minHierarchyMs)} ms or deeper than {s.maxHierarchyDepth} levels are left out. Columns: total ms / self ms / calls / GC alloc");
            report.AppendLine();
            report.Append(frame.hierarchy);
        }

        return report.ToString();
    }

    static FrameResult ReadFrame(int frameIndex, Settings s, bool buildHierarchy)
    {
        var result = new FrameResult { frame = frameIndex + 1, hierarchy = buildHierarchy ? new StringBuilder() : null };
        var rootChildren = new List<int>();

        int missing = 0;
        for (int thread = 0; thread < MaxThreads && missing < MaxConsecutiveMissingThreads; thread++)
        {
            if (s.threads == ThreadScope.MainThread && thread > 0)
            {
                break;
            }

            using (var view = ProfilerDriver.GetHierarchyFrameDataView(frameIndex, thread, ViewMode, HierarchyFrameDataView.columnTotalTime, false))
            {
                if (!view.valid)
                {
                    missing++;
                    continue;
                }
                missing = 0;

                if (thread == 0)
                {
                    result.cpuMs = view.frameTimeMs;
                    result.gpuMs = view.frameGpuTimeMs;
                    result.fps = view.frameFps;
                }
                else if (s.threads == ThreadScope.MainAndRender && view.threadName != "Render Thread")
                {
                    continue;
                }

                int root = view.GetRootItemID();
                view.GetItemChildren(root, rootChildren);
                if (rootChildren.Count == 0)
                {
                    continue;
                }

                var context = new WalkContext
                {
                    view = view,
                    threadKey = string.IsNullOrEmpty(view.threadGroupName) ? view.threadName : view.threadGroupName + " (all threads)",
                    result = result,
                    settings = s,
                    hierarchy = buildHierarchy ? new StringBuilder() : null
                };

                float threadTotalMs = 0;
                foreach (int child in rootChildren)
                {
                    threadTotalMs += view.GetItemColumnDataAsSingle(child, HierarchyFrameDataView.columnTotalTime);
                    if (thread == 0)
                    {
                        result.mainGcBytes += view.GetItemColumnDataAsSingle(child, HierarchyFrameDataView.columnGcMemory);
                    }
                    Walk(context, child, 0, true);
                }

                if (buildHierarchy && context.hierarchy.Length > 0)
                {
                    string group = string.IsNullOrEmpty(view.threadGroupName) ? "" : $" [{view.threadGroupName}]";
                    result.hierarchy.AppendLine($"### {view.threadName}{group} (thread {thread}, {Format(threadTotalMs)} ms sampled)");
                    result.hierarchy.AppendLine();
                    result.hierarchy.AppendLine("```");
                    result.hierarchy.Append(context.hierarchy);
                    result.hierarchy.AppendLine("```");
                    result.hierarchy.AppendLine();
                }
            }
        }

        return result;
    }

    static void Walk(WalkContext ctx, int id, int depth, bool parentPrinted)
    {
        HierarchyFrameDataView view = ctx.view;
        string name = view.GetItemName(id);
        float totalMs = view.GetItemColumnDataAsSingle(id, HierarchyFrameDataView.columnTotalTime);
        float selfMs = view.GetItemColumnDataAsSingle(id, HierarchyFrameDataView.columnSelfTime);
        float calls = view.GetItemColumnDataAsSingle(id, HierarchyFrameDataView.columnCalls);
        float gcBytes = view.GetItemColumnDataAsSingle(id, HierarchyFrameDataView.columnGcMemory);

        var children = new List<int>();
        view.GetItemChildren(id, children);

        // The GC column is inclusive of children
        float childGcBytes = 0;
        foreach (int child in children)
        {
            childGcBytes += view.GetItemColumnDataAsSingle(child, HierarchyFrameDataView.columnGcMemory);
        }

        MarkerSample sample = ctx.result.Get(ctx.threadKey, name);
        sample.selfMs += selfMs;
        sample.calls += calls;
        sample.selfGcBytes += Mathf.Max(0, gcBytes - childGcBytes);

        bool outermost = ctx.namesOnPath.Add(name);
        if (outermost)
        {
            sample.inclusiveMs += totalMs;
        }

        bool printed = ctx.hierarchy != null && parentPrinted && depth < ctx.settings.maxHierarchyDepth && totalMs >= ctx.settings.minHierarchyMs;
        if (printed)
        {
            ctx.hierarchy.Append(' ', depth * 2);
            ctx.hierarchy.Append(name);
            ctx.hierarchy.Append("  ");
            ctx.hierarchy.Append(Format(totalMs)).Append(" / ").Append(Format(selfMs)).Append(" / ").Append(calls.ToString("0", Invariant));
            if (gcBytes > 0)
            {
                ctx.hierarchy.Append(" / ").Append(FormatBytes(gcBytes));
            }
            ctx.hierarchy.AppendLine();
        }

        foreach (int child in children)
        {
            Walk(ctx, child, depth + 1, printed);
        }

        if (outermost)
        {
            ctx.namesOnPath.Remove(name);
        }
    }

    class MarkerStats
    {
        public string thread;
        public string name;
        public float selfSum;
        public float selfMax;
        public int selfMaxFrame;
        public float inclusiveSum;
        public float inclusiveMax;
        public int inclusiveMaxFrame;
        public float callsSum;
        public float gcSum;
    }

    static Dictionary<string, MarkerStats> AggregateStats(List<FrameResult> frames)
    {
        var stats = new Dictionary<string, MarkerStats>();
        foreach (FrameResult frame in frames)
        {
            foreach (KeyValuePair<string, MarkerSample> pair in frame.markers)
            {
                MarkerSample sample = pair.Value;
                if (!stats.TryGetValue(pair.Key, out MarkerStats stat))
                {
                    stat = new MarkerStats { thread = sample.thread, name = sample.name };
                    stats.Add(pair.Key, stat);
                }
                stat.selfSum += sample.selfMs;
                stat.inclusiveSum += sample.inclusiveMs;
                stat.callsSum += sample.calls;
                stat.gcSum += sample.selfGcBytes;
                if (sample.selfMs > stat.selfMax)
                {
                    stat.selfMax = sample.selfMs;
                    stat.selfMaxFrame = frame.frame;
                }
                if (sample.inclusiveMs > stat.inclusiveMax)
                {
                    stat.inclusiveMax = sample.inclusiveMs;
                    stat.inclusiveMaxFrame = frame.frame;
                }
            }
        }
        return stats;
    }

    static void AppendFrameSummary(StringBuilder report, List<FrameResult> frames, int targetFrame)
    {
        report.AppendLine("## Frames");
        report.AppendLine();
        report.AppendLine("| Frame | CPU ms | GPU ms | FPS | Main thread GC alloc |");
        report.AppendLine("|---:|---:|---:|---:|---:|");
        foreach (FrameResult frame in frames)
        {
            string label = frame.frame == targetFrame ? $"**{frame.frame}**" : frame.frame.ToString(Invariant);
            report.AppendLine($"| {label} | {Format(frame.cpuMs)} | {Format(frame.gpuMs)} | {frame.fps.ToString("0.0", Invariant)} | {FormatBytes(frame.mainGcBytes)} |");
        }

        if (frames.Count > 1)
        {
            var others = frames.Where(f => f.frame != targetFrame).ToList();
            FrameResult target = frames.First(f => f.frame == targetFrame);
            float otherAvg = others.Average(f => f.cpuMs);
            report.AppendLine();
            report.AppendLine($"Target CPU time {Format(target.cpuMs)} ms against a neighbour average of {Format(otherAvg)} ms ({FormatSigned(target.cpuMs - otherAvg)} ms)");
        }
        report.AppendLine();
    }

    static void AppendTargetComparison(StringBuilder report, FrameResult target, List<FrameResult> frames, Dictionary<string, MarkerStats> stats, int topCount)
    {
        int neighbours = frames.Count - 1;
        var rows = stats.Select(pair =>
        {
            target.markers.TryGetValue(pair.Key, out MarkerSample t);
            float targetSelf = t?.selfMs ?? 0;
            float targetInclusive = t?.inclusiveMs ?? 0;
            float targetCalls = t?.calls ?? 0;
            return new
            {
                stat = pair.Value,
                targetSelf,
                targetInclusive,
                targetCalls,
                avgSelf = (pair.Value.selfSum - targetSelf) / neighbours,
                avgInclusive = (pair.Value.inclusiveSum - targetInclusive) / neighbours,
                avgCalls = (pair.Value.callsSum - targetCalls) / neighbours
            };
        }).ToList();

        report.AppendLine($"## Target frame {target.frame} against its {neighbours} neighbours: biggest self time increases");
        report.AppendLine();
        report.AppendLine("| Thread | Marker | Target self | Neighbour avg self | Delta | Target calls | Neighbour avg calls |");
        report.AppendLine("|---|---|---:|---:|---:|---:|---:|");
        foreach (var row in rows.Where(r => r.targetSelf - r.avgSelf > 0).OrderByDescending(r => r.targetSelf - r.avgSelf).Take(topCount))
        {
            report.AppendLine($"| {Escape(row.stat.thread)} | {Escape(row.stat.name)} | {Format(row.targetSelf)} | {Format(row.avgSelf)} | {FormatSigned(row.targetSelf - row.avgSelf)} | {row.targetCalls.ToString("0", Invariant)} | {row.avgCalls.ToString("0.#", Invariant)} |");
        }
        report.AppendLine();

        report.AppendLine($"## Target frame {target.frame} against its {neighbours} neighbours: biggest inclusive time increases");
        report.AppendLine();
        report.AppendLine("| Thread | Marker | Target inclusive | Neighbour avg inclusive | Delta |");
        report.AppendLine("|---|---|---:|---:|---:|");
        foreach (var row in rows.Where(r => r.targetInclusive - r.avgInclusive > 0).OrderByDescending(r => r.targetInclusive - r.avgInclusive).Take(topCount))
        {
            report.AppendLine($"| {Escape(row.stat.thread)} | {Escape(row.stat.name)} | {Format(row.targetInclusive)} | {Format(row.avgInclusive)} | {FormatSigned(row.targetInclusive - row.avgInclusive)} |");
        }
        report.AppendLine();
    }

    static void AppendTopTable(StringBuilder report, string title, Dictionary<string, MarkerStats> stats, int frameCount, bool bySelf, int topCount)
    {
        report.AppendLine("## " + title);
        report.AppendLine();
        report.AppendLine("| Thread | Marker | Avg ms | Max ms | Max at frame | Avg calls | Avg self GC alloc |");
        report.AppendLine("|---|---|---:|---:|---:|---:|---:|");
        IEnumerable<MarkerStats> sorted = bySelf ? stats.Values.OrderByDescending(m => m.selfSum) : stats.Values.OrderByDescending(m => m.inclusiveSum);
        foreach (MarkerStats m in sorted.Take(topCount))
        {
            float avg = (bySelf ? m.selfSum : m.inclusiveSum) / frameCount;
            float max = bySelf ? m.selfMax : m.inclusiveMax;
            int maxFrame = bySelf ? m.selfMaxFrame : m.inclusiveMaxFrame;
            report.AppendLine($"| {Escape(m.thread)} | {Escape(m.name)} | {Format(avg)} | {Format(max)} | {maxFrame} | {(m.callsSum / frameCount).ToString("0.#", Invariant)} | {FormatBytes(m.gcSum / frameCount)} |");
        }
        report.AppendLine();
    }

    static string WriteReport(string report, string capturePath, Settings s)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string folder = Path.Combine(projectRoot, "Logs", "ProfilerAnalysis");
        Directory.CreateDirectory(folder);
        string captureName = string.IsNullOrEmpty(capturePath) ? "profiler" : Path.GetFileNameWithoutExtension(capturePath);
        string path = Path.Combine(folder, $"{captureName}_frame{s.frame}_r{s.radius}.md");
        File.WriteAllText(path, report);
        return path;
    }

    static string Format(float ms) => ms.ToString("0.000", Invariant);

    static string FormatSigned(float ms) => (ms >= 0 ? "+" : "") + ms.ToString("0.000", Invariant);

    static string FormatBytes(float bytes)
    {
        if (bytes < 1024) return bytes.ToString("0", Invariant) + " B";
        if (bytes < 1024 * 1024) return (bytes / 1024).ToString("0.0", Invariant) + " KB";
        return (bytes / (1024 * 1024)).ToString("0.00", Invariant) + " MB";
    }

    static string Escape(string text) => text.Replace("|", "\\|");
}
#endif
