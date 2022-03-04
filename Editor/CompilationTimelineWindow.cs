#if UNITY_2021_1_OR_NEWER
#define BEE_COMPILATION_PIPELINE
#endif

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Needle.EditorGUIUtility;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEditor.Compilation;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEditorInternal;
using Assembly = UnityEditor.Compilation.Assembly;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Needle.CompilationVisualizer
{
#if BEE_COMPILATION_PIPELINE
    using IterativeCompilationData = CompilationData.IterativeCompilationData;
#else
    using CompilationData = CompilationAnalysis.CompilationData;
    using IterativeCompilationData = CompilationAnalysis.IterativeCompilationData;
#endif
    
    internal class CompilationTimelineWindow : EditorWindow, IHasCustomMenu
    {
        [MenuItem("Window/Analysis/Compilation Timeline")]
        static void Init() {
            var win = GetWindow<CompilationTimelineWindow>();
            win.titleContent = new GUIContent("↻ Compilation Timeline");
            win.Show();
        }

        private bool AllowRefresh => !windowLockState.IsLocked;
        
        public EditorWindowLockState windowLockState = new EditorWindowLockState();
        public bool compactDrawing = true;
        public int threadCountMultiplier = 1;
        private IterativeCompilationData data;
        
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

            EditorApplication.delayCall += Refresh;
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
        private WindowStyles Styles => styles != null && styles.background ? styles : styles = new WindowStyles();

        class WindowStyles
        {
            public readonly Texture2D background;
            public readonly GUIStyle miniLabel = EditorStyles.miniLabel;
            public readonly GUIStyle overflowMiniLabel = new GUIStyle(EditorStyles.miniLabel) {
                clipping = TextClipping.Overflow
            };
            public readonly GUIStyle rightAlignedLabel = new GUIStyle(EditorStyles.miniLabel) {
                clipping = TextClipping.Overflow,
                alignment = TextAnchor.UpperRight
            };
            public readonly GUIStyle lockButton = "IN LockButton";

            public WindowStyles()
            {
                background = new Texture2D(1, 1);
                background.SetPixel(0,0,UnityEditor.EditorGUIUtility.isProSkin ? new Color(52/255f,52/255f,52/255f) : new Color(207/255f,207/255f,207/255f));
                background.Apply();

                connectorColor    = UnityEditor.EditorGUIUtility.isProSkin ? new Color(1f, 1f, 0.7f, 0.4f) : new Color(0.3f,0.3f,0.1f,0.4f);
                connectorColor2   = UnityEditor.EditorGUIUtility.isProSkin ? new Color(0.7f, 1f, 1f, 0.4f) : new Color(0.1f, 0.3f, 0.3f, 0.4f);
                verticalLineColor = UnityEditor.EditorGUIUtility.isProSkin ? new Color(1, 1, 1, 0.1f) :      new Color(0,0,0,0.1f);
            }

            public readonly Color connectorColor;
            public readonly Color connectorColor2;
            public readonly Color verticalLineColor;
        }

        private void OnDisable() {
            #if UNITY_2019_1_OR_NEWER
            CompilationPipeline.compilationFinished -= CompilationFinished;
            #endif
            AssemblyReloadEvents.afterAssemblyReload -= AfterAssemblyReload;
        }

        private void Refresh() {
            if (AllowRefresh)
            {
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

        private void CompilationFinished(object obj)
        {
            Refresh();
            ClearCaches();
        }

        private void AfterAssemblyReload()
        {
            Refresh();
            ClearCaches();
        }

        private void ClearCaches() {
            assemblies.Clear();
            assemblyDependencyDict.Clear();
            assemblyDependantDict.Clear();

            // clear selection if not in result data
            if (selectedEntry != null && data?.iterations != null && !data.iterations.Any(c => c.compilationData.Any(x => selectedEntry.Equals(x.assembly, StringComparison.Ordinal))))
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

        [SerializeField]
        internal BuildTarget buildTarget = BuildTarget.StandaloneWindows64;
        
        // private Vector2 normalizedTimeView = new Vector2(0, 1);

        // slot ID (height index) to current end time
        private static readonly Dictionary<int, float> graphSlots = new Dictionary<int, float>();

        private void Clear()
        {
            // #if !BEE_COMPILATION_PIPELINE
            CompilationData.Clear();
            data = CompilationData.GetAll();
            // #endif
        }

        public float fromTimeValue = 0f, toTimeValue = 1f;
        private float accumulatedDrag = 0f;
        private void OnGUI()
        {
            if (data?.iterations == null || !data.iterations.Any() || data.iterations.First().compilationData == null || !data.iterations.First().compilationData.Any())
                data = CompilationData.GetAll();
            
            var gotData = data != null && data.iterations != null && data.iterations.Count > 0;
            var gotSelection = !string.IsNullOrEmpty(selectedEntry);
            
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUI.BeginDisabledGroup(EditorApplication.isCompiling);
            
            if (GUILayout.Button("Recompile", EditorStyles.toolbarButton))
            {
                if(AllowRefresh)
                    Clear();
                RecompileEverything();
                GUIUtility.ExitGUI();
                // TODO recompile separate scripts or AsmDefs or packages by selection, by setting them dirty
            }

            if (GUILayout.Button("Compile Player Scripts", EditorStyles.toolbarButton))
            {
                CompilePlayerScripts();
                GUIUtility.ExitGUI();
            }

            var buttonRect = GUILayoutUtility.GetLastRect();
            if (EditorGUILayout.DropdownButton(new GUIContent(""), FocusType.Passive, EditorStyles.toolbarDropDown))
            {
                if (!PlayerScriptsSettingsWindow.ShowAtPosition(buttonRect, this))
                    return;
                GUIUtility.ExitGUI();
            }
            
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.Space();

            compactDrawing = GUILayout.Toggle(compactDrawing, "Compact", EditorStyles.toolbarButton);
            AllowLogging = GUILayout.Toggle(AllowLogging, new GUIContent("Logging", "Log additional compilation data to the console on compilation"), EditorStyles.toolbarButton);
            ShowAssemblyReloads = GUILayout.Toggle(ShowAssemblyReloads, new GUIContent("Show Reloads", "Show or hide assembly reloads in the timeline."), EditorStyles.toolbarButton);
            colorMode = (ColorMode) EditorGUILayout.EnumPopup(colorMode, GUILayout.ExpandWidth(false));
            
            var totalSpan = TimeSpan.Zero;
            var totalCompilationSpan = TimeSpan.Zero;
#if UNITY_2021_1_OR_NEWER
            var firstToLastAssemblyCompilationSpan = TimeSpan.Zero;
#endif
            var totalCompiledAssemblyCount = 0;
            
            GUILayout.FlexibleSpace();
            if(gotData && data.iterations.Count > 0)
            {
                totalSpan = data.iterations.Last().AfterAssemblyReload - data.iterations.First().CompilationStarted;
                if (totalSpan.TotalSeconds < -1)
                    totalSpan = data.iterations.Last().CompilationFinished - data.iterations.First().CompilationStarted;
                if (totalSpan.TotalSeconds <= 0) // timespan adjusted during compilation
                    totalSpan = DateTime.Now - data.iterations.First().CompilationStarted;
                
                // workaround for Editor restart issues where compilation events are not complete
                if(totalSpan.TotalSeconds > 1800) {
                    Clear();
                    GUIUtility.ExitGUI();
                }
                
                totalCompilationSpan = data.iterations
                    .Select(item => item.CompilationFinished - item.CompilationStarted)
                    .Aggregate((result, item) => result + item);
                           
                if (totalCompilationSpan.TotalSeconds < 0) // timespan adjusted during compilation
                    totalCompilationSpan = DateTime.Now - data.iterations.First().CompilationStarted;

#if UNITY_2021_1_OR_NEWER
                var dataToAggregate= data.iterations
                        .Where(x => x is {compilationData: { }} && x.compilationData.Any())
                        .Select(x => x.compilationData.Last().EndTime - x.compilationData.First().StartTime);
                firstToLastAssemblyCompilationSpan = dataToAggregate.Any() ? dataToAggregate.Aggregate((result, item) => result + item) : new TimeSpan(0);
#endif
                
                var totalReloadSpan = data.iterations
                    .Select(item => item.AfterAssemblyReload - item.BeforeAssemblyReload)
                    .Aggregate((result, item) => result + item);

                // another safeguard against broken compilation
                if (totalReloadSpan.TotalSeconds > 1800) {
                    Clear();
                    GUIUtility.ExitGUI();
                }
                if(totalReloadSpan.TotalSeconds < 0)
                    totalReloadSpan = TimeSpan.FromSeconds(0);
                
                totalCompiledAssemblyCount = data.iterations.Select(x => x.compilationData.Count).Sum();
                
                GUILayout.Label("Total: " + totalSpan.TotalSeconds.ToString("F2") + "s");
                GUILayout.Label("Compilation: " + totalCompilationSpan.TotalSeconds.ToString("F2") + "s");
#if UNITY_2021_1_OR_NEWER
                GUILayout.Label("Csc: " + firstToLastAssemblyCompilationSpan.TotalSeconds.ToString("F2") + "s");
#endif
                GUILayout.Label("Reload: " + totalReloadSpan.TotalSeconds.ToString("F2") + "s");
                GUILayout.Label("Compiled Assemblies: " + totalCompiledAssemblyCount);
                if (data.iterations.Count > 1)
                    GUILayout.Label("Iterations: " + data.iterations.Count);
                GUILayout.FlexibleSpace();
            }
            GUILayout.EndHorizontal();
            
            if (!gotData || data.iterations.Count == 0)
            {
                try
                {
                    GUILayout.Label("Waiting for compilation data...", EditorStyles.miniLabel);
                }
                // Unity being weird
                catch (ArgumentException)
                {
                    // ignore
                }
                return;
            }

            var yMax = GUILayoutUtility.GetLastRect().yMax;

            // start of draw area rect
            var rect = new Rect(0, 0, position.width, position.height) {yMin = yMax};

            // var totalWidth = rect.width;
            var totalSeconds = ShowAssemblyReloads ? totalSpan.TotalSeconds : totalCompilationSpan.TotalSeconds;

            var viewRect = rect;
            
            viewRect.yMax = viewRect.yMin + k_LineHeight * totalCompiledAssemblyCount;
            viewRect.width -= 15;
            var totalWidth = viewRect.width;

            if (compactDrawing && graphSlots.Any()) {
                viewRect.yMax = viewRect.yMin + k_LineHeight * (graphSlots.Last().Key + 1);
            }
            else if (!compactDrawing && gotSelection) {
                viewRect.yMax = viewRect.yMin + lastTotalHeight;
            }

            var paintRect = viewRect;
            var fromWidth = viewRect.width * fromTimeValue;
            var toWidth = viewRect.width * toTimeValue;

            paintRect.xMin = Remap(fromWidth, toWidth, 0, viewRect.width, 0);
            paintRect.xMax = Remap(fromWidth, toWidth, 0, viewRect.width, viewRect.width);
            
            var isZoomed = !(Mathf.Approximately(fromTimeValue, 0) && Mathf.Approximately(toTimeValue, 1));
            if (Event.current.type == EventType.Repaint) {
                // draw time header
                var backgroundRect = rect;
                // not sure why, but Profiler background style is weird pre-New UI
                #if !UNITY_2019_3_OR_NEWER
                backgroundRect.xMin -= 200;
                backgroundRect.width += 200;
                #endif
                GUI.DrawTexture(backgroundRect, Styles.background);

                var timeHeader0 = GUI.color;
                var timeHeader1 = GUI.color;
                timeHeader1.a = 0.65f;
                GUI.color = timeHeader1;
                // actual time
                DrawTimeHeader(viewRect, scrollPosition, (float) totalSeconds * 1000f);
                
                timeHeader1.a = 1f;
                GUI.color = timeHeader1;
                // zoomed time
                var scaledTimeHeader = paintRect;
                scaledTimeHeader.yMin += 40;
                DrawTimeHeader(scaledTimeHeader, scrollPosition, (float) totalSeconds * 1000f);

                GUI.color = timeHeader0;
            }

            var sliderRect = viewRect;
            sliderRect.width += 15;
            sliderRect.yMin += 20;
            sliderRect.height = 20;
            
            var c0 = GUI.color;
            var c1 = GUI.color;
            c1.a = 0.3f;
            GUI.color = c1;
            EditorGUI.MinMaxSlider(sliderRect, ref fromTimeValue, ref toTimeValue, 0f, 1f);
            GUI.color = c0;
            
            rect.yMin += 60;
            viewRect.height = Mathf.Max(viewRect.height + k_LineHeight, rect.height); // one extra line for reload indicator
            paintRect.height = viewRect.height;
            
            if (gotSelection) {
                selectedScrollPosition = GUI.BeginScrollView(rect, selectedScrollPosition, viewRect);
                scrollPosition.x = selectedScrollPosition.x; // sync X scroll
            }
            else {
                scrollPosition = GUI.BeginScrollView(rect, scrollPosition, viewRect);
                selectedScrollPosition.x = scrollPosition.x; // sync X scroll
            }

            DrawVerticalLines(paintRect, (float) totalSeconds * 1000f);
            // GUI.DrawTexture(rect, Texture2D.whiteTexture);

            graphSlots.Clear();
            entryRects.Clear();

            if (Event.current.type == EventType.MouseDown)
            {
                accumulatedDrag = 0;
            }
            
            if (Event.current.type == EventType.MouseUp)
            {
                if (accumulatedDrag > 0)
                    Event.current.Use();
            }
            
            // alt + right click resets the view immediately
            if (Event.current.type == EventType.MouseDown && Event.current.button == 1)
            {
                Event.current.Use();

                if(Event.current.alt)
                {
                    fromTimeValue = 0;
                    toTimeValue = 1;
                    Repaint();
                }
            }
            
            // need to do this before the entries, otherwise the entry buttons catch drag events.
            if (Event.current.type == EventType.MouseDrag)
            {
                var pixelShift = Event.current.delta.x;
                var totalScaledPixelWidth = Remap(fromTimeValue * Screen.width, toTimeValue * Screen.width, 0, Screen.width, Screen.width);
                var percentageShift = pixelShift / totalScaledPixelWidth * 0.5f;
                
                // clamp shift
                if(fromTimeValue - percentageShift >= 0 && fromTimeValue - percentageShift <= 1 && toTimeValue - percentageShift >= 0 && toTimeValue - percentageShift <= 1)
                {
                    fromTimeValue -= percentageShift;
                    toTimeValue -= percentageShift;
                }

                var verticalShift = Event.current.delta.y;
                selectedScrollPosition.y -= verticalShift;
                scrollPosition.y -= verticalShift;

                accumulatedDrag += Event.current.delta.sqrMagnitude;
                
                Repaint();
            }
            
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
                    var freeSlots = graphSlots.Where(slot => slot.Value + 0 < x).ToList();
                    int freeSlot;
                    if (freeSlots.Any()) {
                        freeSlot = freeSlots.OrderByDescending(slot => x - slot.Value).First().Key;
                    }
                    else {
                        if (graphSlots.Any())
                            freeSlot = graphSlots.Last().Key + 1;
                        else
                            freeSlot = 0;
                        graphSlots.Add(freeSlot, x + w);
                    }

                    graphSlots[freeSlot] = x + w;

                    // remap x and w
                    var xEnd = x + w;
                    x = Remap(fromWidth, toWidth, 0, viewRect.width, x);
                    w = Remap(fromWidth, toWidth, 0, viewRect.width, xEnd) - x;
                    
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
                
                // remap x and w
                var xEnd2 = x2 + w2;
                x2 = Remap(fromWidth, toWidth, 0, viewRect.width, x2);
                w2 = Remap(fromWidth, toWidth, 0, viewRect.width, xEnd2) - x2;
                
                DrawReloadIndicator(viewRect, ShowAssemblyReloads, x2, w2, Mathf.Max(0, (float) (iterationData.AfterAssemblyReload - iterationData.CompilationFinished).TotalSeconds));
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

            if (Event.current.type == EventType.ScrollWheel && Event.current.alt)
            {
                // we want to zoom around the current X position:
                var mPosX = Event.current.mousePosition.x;
                var scrollDelta = Event.current.delta.y;
                var scrollAmount = 1f + scrollDelta / 25f;
                var percentageOnPage = Mathf.InverseLerp(0, Screen.width, mPosX);
                var valueOnPage = Remap(0, 1, fromTimeValue, toTimeValue, percentageOnPage);
                
                // scale current from and to values around that valueOnPage
                fromTimeValue = valueOnPage - (valueOnPage - fromTimeValue) * scrollAmount;
                toTimeValue = valueOnPage + (toTimeValue - valueOnPage) * scrollAmount;
                fromTimeValue = Mathf.Clamp01(fromTimeValue);
                toTimeValue = Mathf.Clamp01(toTimeValue);
                
                Event.current.Use();
                
                Repaint();
            }

            GUI.EndScrollView();
        }

        private static float Remap(float srcFrom, float srcTo, float dstFrom, float dstTo, float val)
        {
            return (val - srcFrom) / (srcTo - srcFrom) * (dstTo - dstFrom) + dstFrom;
        }

        #if UNITY_2019_1_OR_NEWER
        [Shortcut("needle-compilation-visualizer-" + nameof(CompilePlayerScripts))]
        #endif
        void CompilePlayerScripts()
        {
            if(AllowRefresh)
                Clear();
            
            var settings = new ScriptCompilationSettings
            {
                group = BuildPipeline.GetBuildTargetGroup(buildTarget),
                target = buildTarget,
                options = ScriptCompilationOptions.None
            };
                
            // Debug.Log("Compiling Player Scripts for " + settings.group + "/" + settings.target);

            const string tempDir = "Temp/PlayerScriptCompilation/";
            EditorUtility.DisplayProgressBar("Compiling Player Scripts", "Build Target: " + settings.target + " (" + settings.group + ")", 0.1f);

            #if BEE_COMPILATION_PIPELINE
            try
            {
                // TODO figure out the right way to clear the compilation cache
                // if (Directory.Exists("Library/Bee")) Directory.Delete("Library/Bee");
                // if (Directory.Exists("Library/PramData")) Directory.Delete("Library/PramData");
                // if (Directory.Exists("Library/BuildPlayerData")) Directory.Delete("Library/BuildPlayerData");
                if (Directory.Exists("Library/Bee/artifacts")) Directory.Delete("Library/Bee/artifacts", true);
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir);
            }
            catch (Exception)
            {
                // ignore
            }
            #endif
            
            // RequestScriptCompilationOptions.CleanBuildCache?
            var results = PlayerBuildInterface.CompilePlayerScripts(settings, tempDir);
            // Debug.Log(string.Join("\n", results.assemblies));
            EditorUtility.ClearProgressBar();
        }
        
        void RecompileEverything()
        {
#if UNITY_2021_1_OR_NEWER
            CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache);         
#elif UNITY_2019_3_OR_NEWER
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
            var amount = totalSeconds * 1000f / viewRect.width;
            
            var multiplier = 1f;

            if (amount < 5)
                multiplier = 4f;
            if (amount < 2f)
                multiplier = 10f;
            if (amount < 0.5f)
                multiplier = 25f;
            if (amount > 50)
                multiplier = 0.5f;
            if (amount > 100)
                multiplier = 0.2f;
            if (amount > 500)
                multiplier = 0.05f;
            
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

            var c0 = GUI.color;
            var c = styles.verticalLineColor;
            c.a *= GUI.color.a;
            GUI.color = c;
            for (int i = 0; i < lineCount; i++) {
                GUI.DrawTexture(new Rect(i * lineDistance + viewRect.xMin, viewRect.yMin, 1, viewRect.height), Texture2D.whiteTexture);
            }

            GUI.color = c0;
        }

        Color ColorFromValue(float value, float min = 0.5f, float max = 5f, float hueRange = 0.25f) {
            return Color.HSVToRGB(Mathf.InverseLerp(max, min, value) * hueRange, UnityEditor.EditorGUIUtility.isProSkin ? 0.3f : 0.25f, UnityEditor.EditorGUIUtility.isProSkin ? 0.3f : 0.8f);
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

                        if (asm == null)
                        {
                            Debug.LogError("Assembly is null for " + path + " from " + c.assembly);
                            return;
                        }

                        var asmDefAsset = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(path);

                        var pi = string.IsNullOrEmpty(path)
                            ? null
                            :
#if UNITY_2019_2_OR_NEWER
                            PackageInfo.FindForAssetPath(path);
#else
                            default(PackageInfo);
#endif

                        var editorPath = Path.GetDirectoryName(EditorApplication.applicationPath) +
                                         Path.DirectorySeparatorChar + "Data" + Path.DirectorySeparatorChar;
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
                                                Path.GetFileName(x) + "   <color=#ffffff" + "55>" +
                                                Path.GetDirectoryName(x)?.Replace(editorPath, "") + "</color>")
                                        );
                        // Workaround for console log length limitations
                        const int MaxLogLength = 15000;
                        if (logString.Length > MaxLogLength)
                        {
                            var colorMarker = "</color>";
                            logString = logString.Substring(0, MaxLogLength);
                            int substringLength = logString.LastIndexOf(colorMarker, StringComparison.Ordinal) +
                                                  colorMarker.Length;
                            if (substringLength <= logString.Length)
                                logString = logString.Substring(0, substringLength) + "\n\n<b>(truncated)</b>";
                        }

                        Debug.Log(logString, asmDefAsset);
                    }
                    catch {
                        // ignored
                    }
                }
                // EditorGUIUtility.PingObject(asmDefAsset);
            }
        }

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
                    DrawConnector(i, assembly.assemblyReferences, false, styles.connectorColor);
                }
            }

            if (AssemblyDependantDict.TryGetValue(c.assembly, out var dependantList)) {
                var adjustedRect = originalRect;
                adjustedRect.x = adjustedRect.xMin + 1;
                adjustedRect.width = adjustedRect.height = 1;
                for (int i = 0; i < dependantList.Count; i++) {
                    DrawConnector(i, dependantList, true, styles.connectorColor2);
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
            #if UNITY_2021_1_OR_NEWER
            menu.AddItem(new GUIContent("Fetch Bee Trace"), false, () =>
            {
                data = CompilationData.GetAll();
            });
            menu.AddItem(new GUIContent("Open Chrome and Trace File"), false, () =>
            {
                #if UNITY_2021_2_OR_NEWER
                EditorUtility.RevealInFinder("Library/Bee/fullprofile.json");
                #endif
                // Application.OpenURL("chrome://trace"); // Chrome's built-in viewer; can't open that as URL directly
                Application.OpenURL("https://ui.perfetto.dev/v15.0.5/assets/catapult_trace_viewer.html"); // same viewer but as URL
            });
            #endif
        }
        
        protected void ShowButton(Rect r) {
#if !UNITY_2021_1_OR_NEWER // TODO need to figure out how to lock results on 2021+
            windowLockState.ShowButton(r, Styles.lockButton);
#endif
        }

        internal class PlayerScriptsSettingsWindow : EditorWindow
        {
            internal CompilationTimelineWindow parentWindow;
            internal static PlayerScriptsSettingsWindow window;
            
            internal static bool ShowAtPosition(Rect buttonRect, CompilationTimelineWindow parentWindow)
            {
                Event.current.Use();
                if (!window)
                {
                    window = CreateInstance<PlayerScriptsSettingsWindow>();
                    window.Init(buttonRect, parentWindow);
                    return true;
                }
                window.Cancel();
                return false;
            }

            private GUIStyle background;
            private void Init(Rect buttonRect, CompilationTimelineWindow parentWnd)
            {
                background = "grey_border";
                #if UNITY_2019_1_OR_NEWER
                buttonRect = GUIUtility.GUIToScreenRect(buttonRect);
                #endif
                parentWindow = parentWnd;
                var windowSize = new Vector2(320f, UnityEditor.EditorGUIUtility.singleLineHeight + 2 * 4);
                ShowAsDropDown(buttonRect, windowSize);
            }
            
            private void Cancel()
            {
                Close();
                GUI.changed = true;
                GUIUtility.ExitGUI();
            }

            private void OnGUI()
            {
                if (background == null) background = "grey_border";
                var wnd = new Rect(0, 0, position.width, position.height);

                var innerWnd = wnd;
                const int padding = 4;
                innerWnd.xMin += padding;
                innerWnd.xMax -= padding;
                innerWnd.yMin += padding;
                innerWnd.yMax -= padding;
                
                var width = UnityEditor.EditorGUIUtility.labelWidth; 
                UnityEditor.EditorGUIUtility.labelWidth = 90;
                parentWindow.buildTarget = (BuildTarget) EditorGUI.EnumPopup(innerWnd, "Build Target", parentWindow.buildTarget);
                UnityEditor.EditorGUIUtility.labelWidth = width;
                
                if (Event.current.type == EventType.Repaint)
                    background.Draw(wnd, GUIContent.none, false, false, false, false);
                
                if (Event.current.type != EventType.KeyDown || Event.current.keyCode != KeyCode.Escape)
                    return;
                
                Cancel();
            }
        }
    }
}