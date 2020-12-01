#if false || UNITY_2021_1_OR_NEWER
#define BEE_COMPILATION_PIPELINE
#endif

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Needle.EditorGUIUtility;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEditorInternal;
using Assembly = UnityEditor.Compilation.Assembly;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

#if BEE_COMPILATION_PIPELINE
using IterativeCompilationData = Needle.CompilationVisualizer.CompilationData.IterativeCompilationData;
#else
using IterativeCompilationData = Needle.CompilationVisualizer.CompilationAnalysis.IterativeCompilationData;
#endif

namespace Needle.CompilationVisualizer
{
    internal class CompilationTimelineWindow : EditorWindow, IHasCustomMenu
    {
        [MenuItem("Window/Analysis/Compilation Timeline")]
        static void Init() {
            var win = GetWindow<CompilationTimelineWindow>();
            win.titleContent = new GUIContent("↻ Compilation Timeline");
            win.Show();
        }

        // public bool allowRefresh = true;
        private bool AllowRefresh => !windowLockState.IsLocked;
        
        public EditorWindowLockState windowLockState = new EditorWindowLockState();
        public bool compactDrawing = true;
        public int threadCountMultiplier = 1;
        public IterativeCompilationData data;
        
        private void OnEnable() {
            #if UNITY_2019_1_OR_NEWER
            CompilationPipeline.compilationFinished += CompilationFinished;
            #endif
            AssemblyReloadEvents.afterAssemblyReload += AfterAssemblyReload;
            CompilationPipeline.assemblyCompilationFinished += AssemblyCompilationFinished;

            // Helper: find EditorCompilationInterface
            /*
            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            foreach (var a in assemblies) {
                try {
                    var t = a.GetTypes();
                    foreach (var t2 in t) {
                        if(t2.Name.Contains("EditorCompilationInterface")) {
                            Debug.Log(t2.AssemblyQualifiedName);
                            break;
                        }
                    }
                } catch { 
                    // ignore
                }
            }
            */
            
            windowLockState.lockStateChanged.AddListener(OnLockStateChanged);

            if (threadCountMultiplier > 1) {
                // EXPERIMENT: set thread count for compilation "UnityEditor.Scripting.ScriptCompilation.EditorCompilationInterface"
                var eci = Type.GetType(
                    "UnityEditor.Scripting.ScriptCompilation.EditorCompilationInterface, UnityEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
                if (eci != null) {
                    var instanceProp = eci.GetProperty("Instance", (BindingFlags) (-1));
                    if (instanceProp != null) {
                        var instance = instanceProp.GetValue(null);
                        var setComp = instance.GetType().GetMethod("SetMaxConcurrentCompilers", (BindingFlags) (-1));
                        setComp?.Invoke(instance, new object[] {SystemInfo.processorCount * threadCountMultiplier});
                    }
                }
            }

            Refresh();
        }

        private void OnLockStateChanged(bool locked)
        {
            if(!locked)
                data = CompilationData.GetAll();
        }

        private bool AllowLogging {
            get => CompilationAnalysis.AllowLogging;
            set => CompilationAnalysis.AllowLogging = value;
        }  
        private bool ShowAssemblyReloads {
            get => CompilationAnalysis.ShowAssemblyReloads;
            set => CompilationAnalysis.ShowAssemblyReloads = value;
        }

        private WindowStyles styles;
        private WindowStyles Styles => styles ?? (styles = new WindowStyles());

        class WindowStyles
        {
            // public GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel);
            public readonly GUIStyle background = "ProfilerGraphBackground";
            public readonly GUIStyle miniLabel = EditorStyles.miniLabel;
            public readonly GUIStyle overflowMiniLabel = new GUIStyle(EditorStyles.miniLabel) {
                clipping = TextClipping.Overflow
            };
            public readonly GUIStyle rightAlignedLabel = new GUIStyle(EditorStyles.miniLabel) {
                clipping = TextClipping.Overflow,
                alignment = TextAnchor.UpperRight
            };
            public readonly GUIStyle lockButton = "IN LockButton";
        }

        private void OnDisable() {
            #if UNITY_2019_1_OR_NEWER
            CompilationPipeline.compilationFinished -= CompilationFinished;
            #endif
            AssemblyReloadEvents.afterAssemblyReload -= AfterAssemblyReload;
        }

        private void Refresh() {
            // Debug.Log("should refresh, allowed: " + allowRefresh);
            if (AllowRefresh) {
                data = CompilationData.GetAll();
                Repaint();
            }
        }

        private void AssemblyCompilationFinished(string arg1, CompilerMessage[] arg2) {
            Refresh();
        }

        private List<Assembly> assemblies = new List<Assembly>();

        private List<Assembly> Assemblies {
            get {
                if (assemblies.Any()) return assemblies;
                assemblies = CompilationPipeline.GetAssemblies().ToList();
                return assemblies;
            }
        }

        private Dictionary<string, Assembly> assemblyDependencyDict = new Dictionary<string, Assembly>();

        private Dictionary<string, Assembly> AssemblyDependencyDict {
            get {
                if (assemblyDependencyDict.Any()) return assemblyDependencyDict;
                assemblyDependencyDict = Assemblies.ToDictionary(x => x.outputPath);
                return assemblyDependencyDict;
            }
        }

        private readonly Dictionary<string, List<Assembly>> assemblyDependantDict =
            new Dictionary<string, List<Assembly>>();

        private Dictionary<string, List<Assembly>> AssemblyDependantDict {
            get {
                if (assemblyDependantDict.Any()) return assemblyDependantDict;
                foreach (var asm in Assemblies) {
                    if (!assemblyDependantDict.ContainsKey(asm.outputPath))
                        assemblyDependantDict.Add(asm.outputPath, new List<Assembly>());
                }

                foreach (var asm in Assemblies) {
                    foreach (var dep in asm.assemblyReferences) {
                        assemblyDependantDict[dep.outputPath].Add(asm);
                    }
                }

                return assemblyDependantDict;
            }
        }

        private void CompilationFinished(object obj) {
            Refresh();
            ClearCaches();
        }

        private void AfterAssemblyReload() {
            Refresh();
            ClearCaches();
        }

        private void ClearCaches() {
            assemblies.Clear();
            assemblyDependencyDict.Clear();
            assemblyDependantDict.Clear();

            // clear selection if not in result data
            if (selectedEntry != null &&
                !data.iterations.Any(c => c.compilationData.Any(x => selectedEntry.Equals(x.assembly, StringComparison.Ordinal))))
                selectedEntry = null;
        }

        private float k_LineHeight = 20;
        private float k_SkippedHeight = 4;
        private float lastTotalHeight = 200;

        internal enum ColorMode {
            CompilationDuration,
            DependantCount,
            DependencyCount
        }

        [SerializeField]
        internal ColorMode colorMode = ColorMode.CompilationDuration;
        
        // private Vector2 normalizedTimeView = new Vector2(0, 1);

        // slot ID (height index) to current end time
        private static readonly Dictionary<int, float> GraphSlots = new Dictionary<int, float>();

        private void Clear()
        {
            #if !BEE_COMPILATION_PIPELINE
            CompilationAnalysis.CompilationData.Clear();
            #endif
        }
        
        private void OnGUI()
        {
            // data = CompilationAnalysis.CompilationData.GetAll();
            
            var gotData = data != null && data.iterations != null && data.iterations.Count > 0;
            var gotSelection = !string.IsNullOrEmpty(selectedEntry);
            
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUI.BeginDisabledGroup(EditorApplication.isCompiling);
            if (GUILayout.Button("Recompile", EditorStyles.toolbarButton))
            {
                if(AllowRefresh)
                    Clear();
                RecompileEverything();
                // TODO recompile separate scripts or AsmDefs or packages by selection, by setting them dirty
            }
            
            //// For Testing on 2021
            // if (GUILayout.Button("Fetch Trace", EditorStyles.toolbarButton))
            // {
            //     data = CompilationData.GetAll();
            // }
            
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.Space();

            compactDrawing = GUILayout.Toggle(compactDrawing, "Compact", EditorStyles.toolbarButton);
            AllowLogging = GUILayout.Toggle(AllowLogging, new GUIContent("Logging", "Log additional compilation data to the console on compilation"), EditorStyles.toolbarButton);
            ShowAssemblyReloads = GUILayout.Toggle(ShowAssemblyReloads, new GUIContent("Show Reloads", "Show or hide assembly reloads in the timeline."), EditorStyles.toolbarButton);
            colorMode = (ColorMode) EditorGUILayout.EnumPopup(colorMode, GUILayout.ExpandWidth(false));
            
            var totalSpan = TimeSpan.Zero;
            var totalCompilationSpan = TimeSpan.Zero;
            var totalCompiledAssemblyCount = 0;
            
            GUILayout.FlexibleSpace();
            if(gotData && data.iterations.Count > 0) {
                totalSpan = data.iterations.Last().AfterAssemblyReload - data.iterations.First().CompilationStarted;
                if (totalSpan.TotalSeconds < 0) // timespan adjusted during compilation
                    totalSpan = DateTime.Now - data.iterations.First().CompilationStarted;
                
                // workaround for Editor restart issues where compilation events are not complete
                if(totalSpan.TotalSeconds > 7200) {
                    Clear();
                    return; // need to cancel drawing here, otherwise we end up in an infinite loop
                }
                
                totalCompilationSpan = data.iterations
                    .Select(item => item.CompilationFinished - item.CompilationStarted)
                    .Aggregate((result, item) => result + item);
                           
                if (totalCompilationSpan.TotalSeconds < 0) // timespan adjusted during compilation
                    totalCompilationSpan = DateTime.Now - data.iterations.First().CompilationStarted;

                var totalReloadSpan = data.iterations
                    .Select(item => item.AfterAssemblyReload - item.BeforeAssemblyReload)
                    .Aggregate((result, item) => result + item);

                totalCompiledAssemblyCount = data.iterations.Select(x => x.compilationData.Count).Sum();
                
                GUILayout.Label("Total: " + totalSpan.TotalSeconds.ToString("F2") + "s");
                GUILayout.Label("Compilation: " + totalCompilationSpan.TotalSeconds.ToString("F2") + "s");
                GUILayout.Label("Reload: " + totalReloadSpan.TotalSeconds.ToString("F2") + "s");
                GUILayout.Label("Compiled Assemblies: " + totalCompiledAssemblyCount);
                if (data.iterations.Count > 1)
                    GUILayout.Label("Iterations: " + data.iterations.Count);
                GUILayout.FlexibleSpace();
            }
            GUILayout.EndHorizontal();

            if (!gotData || data.iterations.Count == 0) return;

            
            var yMax = GUILayoutUtility.GetLastRect().yMax;

            // start of draw area rect
            var rect = new Rect(0, 0, position.width, position.height) {yMin = yMax};

            // var totalWidth = rect.width;
            var totalSeconds = ShowAssemblyReloads ? totalSpan.TotalSeconds : totalCompilationSpan.TotalSeconds;

            var viewRect = rect;
            viewRect.yMax = viewRect.yMin + k_LineHeight * totalCompiledAssemblyCount;
            // totalWidth -= 15; // scrollbar
            viewRect.width -= 15;
            // viewRect.width *= normalizedTimeView.y;
            var totalWidth = viewRect.width;

            if (compactDrawing && GraphSlots.Any()) {
                viewRect.yMax = viewRect.yMin + k_LineHeight * (GraphSlots.Last().Key + 1);
            }
            else if (!compactDrawing && gotSelection) {
                viewRect.yMax = viewRect.yMin + lastTotalHeight;
            }

            if (Event.current.type == EventType.Repaint) {
                // draw time header
                var backgroundRect = rect;
                // not sure why, but Profiler background style is weird pre-New UI
                #if !UNITY_2019_3_OR_NEWER
                backgroundRect.xMin -= 200;
                backgroundRect.width += 200;
                #endif
                Styles.background.Draw(backgroundRect, false, false, false, false);
                DrawTimeHeader(viewRect, scrollPosition, (float) totalSeconds * 1000f);
            }

            rect.yMin += 20;
            viewRect.height = Mathf.Max(viewRect.height + k_LineHeight, rect.height); // one extra line for reload indicator
            
            if (gotSelection) {
                selectedScrollPosition = GUI.BeginScrollView(rect, selectedScrollPosition, viewRect);
                scrollPosition.x = selectedScrollPosition.x; // sync X scroll
            }
            else {
                scrollPosition = GUI.BeginScrollView(rect, scrollPosition, viewRect);
                selectedScrollPosition.x = scrollPosition.x; // sync X scroll
            }

            DrawVerticalLines(viewRect, (float) totalSeconds * 1000f);
            // GUI.DrawTexture(rect, Texture2D.whiteTexture);

            GraphSlots.Clear();
            entryRects.Clear();

            // naive first pass: paint colored textures
            int nonSkippedIndex = 0;
            float currentHeight = yMax;
            DateTime firstCompilationStarted = data.iterations.First().CompilationStarted;
            DateTime lastSectionEndTime = firstCompilationStarted;
            
            foreach(var iterationData in data.iterations) {
                foreach (var c in iterationData.compilationData) {
                    bool skip = gotSelection;
                    // skip in selection mode
                    if (skip && selectedEntry != null) {
                        if (selectedEntry == c.assembly) skip = false;

                        if (skip) {
                            if (AssemblyDependantDict.TryGetValue(c.assembly, out var dependantAssemblyList)) {
                                foreach (var a in dependantAssemblyList) {
                                    if (a == null) continue;
                                    if (selectedEntry.Equals(a.outputPath, StringComparison.Ordinal)) {
                                        skip = false;
                                        break;
                                    }
                                }
                            }
                        }

                        if (skip) {
                            if (AssemblyDependencyDict.TryGetValue(c.assembly, out var assembly)) {
                                foreach (var a in assembly.assemblyReferences) {
                                    if (a == null) continue;
                                    if (selectedEntry.Equals(a.outputPath, StringComparison.Ordinal)) {
                                        skip = false;
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    // if (skip) continue;

                    var entryHeight = skip ? k_SkippedHeight : k_LineHeight;
                    var xSpan = (c.StartTime - iterationData.CompilationStarted) + (lastSectionEndTime - firstCompilationStarted);
                    var wSpan = c.EndTime - c.StartTime;

                    // continuous drawing during compilation - looks nicer
                    if (wSpan.TotalSeconds < 0)
                        wSpan = DateTime.Now - c.startTime;

                    var x = (float) (xSpan.TotalSeconds / totalSeconds) * totalWidth;
                    var w = (float) (wSpan.TotalSeconds / totalSeconds) * totalWidth;

                    var color = Color.white;
                    switch (colorMode)
                    {
                        case ColorMode.DependantCount:
                            if (AssemblyDependantDict.TryGetValue(c.assembly, out var dependantAssemblyList)) {
                                color = ColorFromValue(dependantAssemblyList.Count, 0f, 5f, 0.3f);
                            }
                            break;
                        case ColorMode.DependencyCount:
                            if (AssemblyDependencyDict.TryGetValue(c.assembly, out var assembly)) {
                                color = ColorFromValue(assembly.assemblyReferences.Length, 0f, 10f, 0.3f);
                            }
                            break;
                        default:
                            color = ColorFromValue((float) wSpan.TotalSeconds, 0.5f, 5f, 0.3f);
                            break;
                    }

                    // stacking: find free slots to place entries
                    var freeSlots = GraphSlots.Where(slot => slot.Value + 0 < x).ToList();
                    int freeSlot;
                    if (freeSlots.Any()) {
                        freeSlot = freeSlots.OrderByDescending(slot => x - slot.Value).First().Key;
                    }
                    else {
                        if (GraphSlots.Any())
                            freeSlot = GraphSlots.Last().Key + 1;
                        else
                            freeSlot = 0;
                        GraphSlots.Add(freeSlot, x + w);
                    }

                    GraphSlots[freeSlot] = x + w;

                    var localRect = new Rect(x, k_LineHeight * (compactDrawing ? freeSlot : nonSkippedIndex) + yMax, w,
                        entryHeight);
                    if (gotSelection) {
                        if (compactDrawing) {
                            if (skip) {
                                localRect.height = k_SkippedHeight;
                                localRect.y += k_LineHeight - k_SkippedHeight;
                            }
                        }
                        else
                            localRect.y = currentHeight;
                    }

                    currentHeight += entryHeight;
                    nonSkippedIndex++;
                    entryRects[c.assembly] = localRect;
                    DrawEntry(compilationData: iterationData, c, localRect, color, !compactDrawing || (compactDrawing && gotSelection));
                }

                var oldLastSection = lastSectionEndTime;
                lastSectionEndTime = ShowAssemblyReloads ? iterationData.AfterAssemblyReload : iterationData.CompilationFinished;
                
                // reload indicator at the end of each iteration
                var xSpan2 = (iterationData.CompilationFinished - iterationData.CompilationStarted) + (oldLastSection - firstCompilationStarted);
                var wSpan2 = lastSectionEndTime - iterationData.CompilationFinished;
                var x2 = (float) (xSpan2.TotalSeconds / totalSeconds) * totalWidth;
                var w2 = (float) (wSpan2.TotalSeconds / totalSeconds) * totalWidth;
                DrawReloadIndicator(viewRect, ShowAssemblyReloads, x2, w2, (float) (iterationData.AfterAssemblyReload - iterationData.CompilationFinished).TotalSeconds);
            }
            lastTotalHeight = currentHeight;

            foreach(var iterationData in data.iterations)
            foreach (var c in iterationData.compilationData) {
                if (c.assembly != selectedEntry) continue;

                var localRect = entryRects[c.assembly];
                DrawConnectors(c, localRect);
            }

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition)) {
                // context click: remove selection
                // Debug.Log("context click!");
                selectedEntry = null;
                Repaint();
            }

            if (Event.current.type == EventType.ScrollWheel) {
                /*
                
                // TODO: implement zooming into view to make names better readable
                // check if scrollbar is visible (then we need to catch Y scroll and only process here with a modifier pressed)
                
                // we want to zoom around the current X position:
                var mPosX = Event.current.mousePosition.x;
                var scrollDelta = Event.current.delta.y;
                var lerp = 1 - mPosX / totalWidth;
    
                var cursorBeforeScroll = mPosX + scrollPosition.x;
                
                // Debug.Log("Scroll delta: " + Event.current.delta + ", X: " + (mPosX + scrollPosition.x) / totalWidth);
    
                // normalizedTimeView.x = (1 + scrollDelta * 0.01f * lerp) * normalizedTimeView.x;
                // normalizedTimeView.y = (1 - scrollDelta * 0.01f * lerp) * normalizedTimeView.y;
                normalizedTimeView.y = (1 - scrollDelta * 0.01f) * normalizedTimeView.y;
    
                var cursorAfterScroll = mPosX + scrollPosition.x;// position.width * normalizedTimeView.y;
    
                scrollPosition.x += cursorAfterScroll - cursorBeforeScroll;
                
                // mouse cursor relative pos should stay invariant
                // so we need to solve:
                // scrollPosition.x + mousePosition.x = f(scrollPosition.x + mousePosition.x)
                // scrollPosition.x = f(scrollPosition.x + mousePosition.x) - mousePosition.x
                // 
                // scrollPosition.x -= (scrollDelta * 0.01f * (1 - lerp)) * viewRect.width;
                
                Repaint();
                
                */
            }

            GUI.EndScrollView();
        }

        void RecompileEverything()
        {
#if UNITY_2021_1_OR_NEWER || BEE_COMPILATION_PIPELINE
            if(Directory.Exists("Library/Bee")) {
                try {
                    Directory.Delete("Library/Bee", true);
                }
                catch(IOException) {}
            }
            AssetDatabase.Refresh();
#endif
#if UNITY_2019_3_OR_NEWER
            CompilationPipeline.RequestScriptCompilation();
#elif UNITY_2017_1_OR_NEWER
             var editorAssembly = System.Reflection.Assembly.GetAssembly(typeof(Editor));
             var editorCompilationInterfaceType = editorAssembly?.GetType("UnityEditor.Scripting.ScriptCompilation.EditorCompilationInterface");
             var dirtyAllScriptsMethod = editorCompilationInterfaceType?.GetMethod("DirtyAllScripts", BindingFlags.Static | BindingFlags.Public);
             dirtyAllScriptsMethod?.Invoke(editorCompilationInterfaceType, null);
#endif
        }
        
        private void DrawReloadIndicator(Rect viewRect, bool showAssemblyReloads, float x, float width, float reloadDuration)
        {
            if (showAssemblyReloads)
                GUI.color = new Color(1, 0, 0, 0.05f);
            else
                GUI.color = new Color(1, 0, 0, 0.5f);
            GUI.DrawTexture(new Rect(x, viewRect.yMin, Mathf.Max(1, width), viewRect.height), Texture2D.whiteTexture);
            GUI.color = Color.red;
            GUI.Label(new Rect(x - 100, viewRect.yMax - 14, 100, 14), "Reload: " + reloadDuration.ToString("0.###s"), Styles.rightAlignedLabel);
            GUI.color = Color.white;
        }
        
        void DrawTimeHeader(Rect viewRect, Vector2 scroll, float totalMilliseconds) {
            var totalSeconds = totalMilliseconds / 1000f;

            var multiplier = 1f;

            if (totalSeconds < 1.5f)
                multiplier = 50f;
            if (totalSeconds < 5)
                multiplier = 10f;
            if (totalSeconds > 50)
                multiplier = 0.5f;
            if (totalSeconds > 100)
                multiplier = 0.2f;

            var linesPerSecond = 1f * multiplier;
            var lineCount = (int) (totalSeconds * linesPerSecond) + 2;
            var lineDistance = viewRect.width / (totalSeconds * linesPerSecond);

            var vr = viewRect;
            vr.x -= scroll.x;
            viewRect = vr;

            // height: 20
            var tenthRect = new Rect(viewRect.x, viewRect.yMin + 17, viewRect.width, 3);
            DrawVerticalLines(tenthRect, totalMilliseconds, 10 * multiplier);

            var fourthRect = new Rect(viewRect.x, viewRect.yMin + 14, viewRect.width, 3);
            DrawVerticalLines(fourthRect, totalMilliseconds, 5f * multiplier);

            var onesRect = new Rect(viewRect.x, viewRect.yMin + 0, viewRect.width, 20);
            DrawVerticalLines(onesRect, totalMilliseconds, 1f * multiplier);

            for (int i = 0; i < lineCount; i++) {
                GUI.Label(new Rect(i * lineDistance - 50 + viewRect.xMin, viewRect.yMin, 100, 14),
                    (i / multiplier).ToString("0.###s"), EditorStyles.centeredGreyMiniLabel);
            }
        }

        void DrawVerticalLines(Rect viewRect, float totalMilliseconds, float linesPerSecond = 1f) {
            if (Event.current.type != EventType.Repaint) return;

            // just draw a line per second
            // left is always 0
            var totalSeconds = totalMilliseconds / 1000f;
            var lineCount = (int) (totalSeconds * linesPerSecond) + 2;
            var lineDistance = viewRect.width / (totalSeconds * linesPerSecond);

            GUI.color = new Color(1, 1, 1, 0.1f);
            for (int i = 0; i < lineCount; i++) {
                GUI.DrawTexture(new Rect(i * lineDistance + viewRect.xMin, viewRect.yMin, 1, viewRect.height), Texture2D.whiteTexture);
            }

            GUI.color = Color.white;
        }

        Color ColorFromValue(float value, float min = 0.5f, float max = 5f, float hueRange = 0.25f) {
            return Color.HSVToRGB(Mathf.InverseLerp(max, min, value) * hueRange, 0.3f, 0.3f);
        }

        public Vector2 scrollPosition;
        public Vector2 selectedScrollPosition;
        public string selectedEntry;

        readonly Dictionary<string, Rect> entryRects = new Dictionary<string, Rect>();

        private void DrawEntry(CompilationData compilationData, AssemblyCompilationData c, Rect localRect, Color color,
            bool overflowLabel) {
            localRect.xMin += 0.5f;
            localRect.xMax -= 0.5f;
            localRect.yMin += 0.5f;
            localRect.yMax -= 0.5f;

            if (Event.current.type == EventType.Repaint) {
                GUI.color = color;
                GUI.DrawTexture(localRect, Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            // localRect.width = Mathf.Max(localRect.width, 500); // make space for the label
            if (localRect.height > 5 && GUI.Button(localRect, GetGUIContent(compilationData, c),
                overflowLabel ? Styles.overflowMiniLabel : Styles.miniLabel)) {
                if (!string.IsNullOrEmpty(selectedEntry) &&
                    selectedEntry.Equals(c.assembly, StringComparison.Ordinal)) {
                    selectedEntry = null;
                    return;
                }

                selectedEntry = c.assembly;

                if(AllowLogging) {
                    try {
                        var path = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(c.assembly);
                        var asm = Assemblies.FirstOrDefault(x => x.outputPath == c.assembly);

                        if (asm == null) {
                            Debug.LogError("Assembly is null for " + path + " from " + c.assembly);
                            return;
                        }

                        var asmDefAsset = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(path);
                        
                        var pi = string.IsNullOrEmpty(path) ?
                            null :
                            #if UNITY_2019_2_OR_NEWER
                            PackageInfo.FindForAssetPath(path);
                            #else
                            default(PackageInfo);
                            #endif

                        var editorPath = Path.GetDirectoryName(EditorApplication.applicationPath) + Path.DirectorySeparatorChar + "Data" + Path.DirectorySeparatorChar;
                        var logString = "<b>" + Path.GetFileName(path) + "</b>" + " in " + (pi?.name ?? "Assets") +
                                  "\n\n<i>Assembly References</i>:\n- " +
                                  string.Join("\n- ",
                                      asm.assemblyReferences.Select(x => x.name)
                                  ) +
                                  "\n\n<i>Defines</i>:\n- " +
                                  string.Join("\n- ",
                                      asm.defines
                                          .OrderBy(x => x)
                                  ) +
                                  "\n\n<i>Compiled Assembly References</i>:\n- " +
                                  string.Join("\n- ",
                                      asm.compiledAssemblyReferences.Select(x =>
                                          Path.GetFileName(x) + "   <color=#ffffff" + "55>" + Path.GetDirectoryName(x).Replace(editorPath, "") + "</color>")
                                  );
                        // Workaround for console log length limitations
                        const int MaxLogLength = 15000;
                        if (logString.Length > MaxLogLength) {
                            var colorMarker = "</color>";
                            logString = logString.Substring(0, MaxLogLength);
                            int substringLength = logString.LastIndexOf(colorMarker, StringComparison.Ordinal) + colorMarker.Length;
                            if(substringLength <= logString.Length)
                                logString = logString.Substring(0,  substringLength) + "\n\n<b>(truncated)</b>";
                        }
                        
                        Debug.Log(logString, asmDefAsset);
                    }
                    catch {}
                }
                // EditorGUIUtility.PingObject(asmDefAsset);
            }
        }

        private static readonly Color ConnectorColor = new Color(1f, 1f, 0.7f, 0.4f);
        private static readonly Color ConnectorColor2 = new Color(0.7f, 1f, 1f, 0.4f);

        private void DrawConnectors(AssemblyCompilationData c,
            Rect originalRect) {
            void DrawConnector(int i, IList<Assembly> assemblyList, bool alignRight, Color color) {
                // target asm
                if (entryRects.TryGetValue(assemblyList[i].outputPath, out var referenceRect)) {
                    float lrp = (i + 0.5f) / assemblyList.Count;
                    var lineRect = originalRect;
                    lineRect.yMin = Mathf.Lerp(originalRect.yMin, originalRect.yMax, lrp);
                    lineRect.yMax = lineRect.yMin + 1;
                    var mat = GUI.matrix;
                    var col = GUI.color;
                    GUI.color = color;

                    // horizontal line
                    // randomize X pos along node (makes deps easier to see when there are many)
                    lineRect.xMin = Mathf.Lerp(referenceRect.xMin, referenceRect.xMax, lrp);
                    // override for now, we don't need this if we have a selection
                    lineRect.xMin = referenceRect.xMin;
                    lineRect.xMax = alignRight ? originalRect.xMax : originalRect.xMin;
                    GUI.DrawTexture(lineRect, Texture2D.whiteTexture);

                    // vertical line
                    lineRect.xMax = lineRect.xMin + 1;
                    lineRect.yMin = referenceRect.center.y;
                    GUI.DrawTexture(lineRect, Texture2D.whiteTexture);

                    // end point
                    lineRect.width = 5;
                    lineRect.height = 5;
                    lineRect.x -= 2f;
                    lineRect.y -= 2f;
                    GUI.DrawTexture(lineRect, Texture2D.whiteTexture);
                    GUI.matrix = mat;
                    GUI.color = col;
                }

                /*
                else {
                    stackRect.y += 10;
                    GUI.DrawTexture(stackRect, Texture2D.whiteTexture);
                }
                */
            }

            if (Event.current.type != EventType.Repaint) return;
            // draw connectors
            if (AssemblyDependencyDict.TryGetValue(c.assembly, out var assembly)) {
                var adjustedRect = originalRect;
                adjustedRect.x = adjustedRect.xMin + 1;
                adjustedRect.width = adjustedRect.height = 1;
                for (int i = 0; i < assembly.assemblyReferences.Length; i++) {
                    DrawConnector(i, assembly.assemblyReferences, false, ConnectorColor);
                }
            }

            if (AssemblyDependantDict.TryGetValue(c.assembly, out var dependantList)) {
                var adjustedRect = originalRect;
                adjustedRect.x = adjustedRect.xMin + 1;
                adjustedRect.width = adjustedRect.height = 1;
                for (int i = 0; i < dependantList.Count; i++) {
                    DrawConnector(i, dependantList, true, ConnectorColor2);
                }
            }
        }

        const string FormatString = "HH:mm:ss.fff";
        private const string SpanFormatString = "0.###s";
        GUIContent GetGUIContent(CompilationData compilationData, AssemblyCompilationData c) {
            var shortName = Path.GetFileName(c.assembly);
            // var pi = PackageInfo.FindForAssembly()
            return new GUIContent(shortName,
                shortName + "\n" +
                (c.EndTime - c.StartTime).TotalSeconds.ToString(SpanFormatString) + "\n" + 
                "From " + c.StartTime.ToString(FormatString) + " to " + c.EndTime.ToString(FormatString) + "\n" +
                "From " + (c.StartTime - compilationData.CompilationStarted).TotalSeconds.ToString(SpanFormatString) + " to " + (c.EndTime - compilationData.CompilationStarted).TotalSeconds.ToString(SpanFormatString));
        }

        public void AddItemsToMenu(GenericMenu menu) {
            windowLockState.AddItemsToMenu(menu);
        }
        
        protected void ShowButton(Rect r) {
            windowLockState.ShowButton(r, Styles.lockButton);
        }
    }
}