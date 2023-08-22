#if UNITY_2021_1_OR_NEWER

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Needle.CompilationVisualizer
{
    internal class TraceData : ScriptableSingleton<TraceData>, ISerializationCallbackReceiver
    {
        public SerializableDateTime
            compilationStarted,
            compilationFinished,
            beforeAssemblyReload,
            afterAssemblyReload;
        
        public DateTime CompilationFinished;
        public DateTime CompilationStarted;
        public DateTime AfterAssemblyReload;
        public DateTime BeforeAssemblyReload;    
        
        public void OnBeforeSerialize()
        {
            compilationStarted = CompilationStarted;
            compilationFinished = CompilationFinished;
            afterAssemblyReload = AfterAssemblyReload;
            beforeAssemblyReload = BeforeAssemblyReload;
        }

        public void OnAfterDeserialize()
        {
            CompilationStarted = compilationStarted;
            CompilationFinished = compilationFinished;
            AfterAssemblyReload = afterAssemblyReload;
            BeforeAssemblyReload = beforeAssemblyReload;
        }
        
        [InitializeOnLoadMethod]
        static void InitCompilationEvents()
        {
#if UNITY_2019_1_OR_NEWER
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
#endif
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
        }

        private static void OnCompilationStarted(object obj) => instance.CompilationStarted = DateTime.Now;
        private static void OnCompilationFinished(object obj) => instance.CompilationFinished = DateTime.Now;
        private static void OnBeforeAssemblyReload() { if(!EditorApplication.isPlayingOrWillChangePlaymode) instance.BeforeAssemblyReload = DateTime.Now; }
        private static void OnAfterAssemblyReload() { if(!EditorApplication.isPlayingOrWillChangePlaymode) instance.AfterAssemblyReload = DateTime.Now; }
    }
    
    internal class CompilationData
    {
        internal class IterativeCompilationData
        {
            public List<CompilationData> iterations;
        }
        
        public DateTime CompilationFinished;
        public DateTime CompilationStarted;
        public DateTime AfterAssemblyReload;
        public DateTime BeforeAssemblyReload;
        
        public List<AssemblyCompilationData> compilationData;

        public static IterativeCompilationData GetAll()
        {
            var data = ConvertBeeDataToCompilationData();
            if (data == null) return null;
            
            return new IterativeCompilationData()
            {
                iterations = new List<CompilationData>()
                {
                    data
                }
            };
        }

#if UNITY_2021_2_OR_NEWER
        const string ProfilerJson = "Library/Bee/fullprofile.json";
#else
        const string ProfilerJson = "Library/Bee/profiler.json";
#endif
        
        private static CompilationData ConvertBeeDataToCompilationData()
        {
            if (!File.Exists(ProfilerJson)) return null;
            
            try
            {
                var beeData =
                    JsonUtility.FromJson<BeeProfilerData>(
                        File.ReadAllText(ProfilerJson));
                if (beeData.traceEvents == null || !beeData.traceEvents.Any()) return null;

                beeData.traceEvents = beeData.traceEvents
                    .Where(x => x.ts > 0
                                #if UNITY_2021_2_OR_NEWER
                                && x.pid != "0"
                                #endif
                                )
                    .OrderBy(x => x.ts)
                    .ToList();
                var ticksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;
                
                var firstTs = beeData.traceEvents.First().ts;
                var lastTs = beeData.traceEvents.Last().ts;
                var beeCompilationSpan = lastTs - firstTs;
                var unityCompilationSpan = (TraceData.instance.CompilationFinished - TraceData.instance.CompilationStarted).Ticks / ticksPerMicrosecond;
                var compilationSpanOffset = Math.Max(0, unityCompilationSpan - beeCompilationSpan);
                var offsetToFirstTs = TraceData.instance.CompilationStarted.Ticks / ticksPerMicrosecond - firstTs + compilationSpanOffset;

                var cc = new CompilationData()
                {
                    CompilationStarted = TraceData.instance.CompilationStarted,
                    CompilationFinished = TraceData.instance.CompilationFinished,
                    AfterAssemblyReload = TraceData.instance.AfterAssemblyReload,
                    BeforeAssemblyReload = TraceData.instance.BeforeAssemblyReload,
                    compilationData = beeData.traceEvents
                        .Where(x => x.name.Equals("Csc", StringComparison.Ordinal) && x.args.detail != null)
                        .Select(x => new AssemblyCompilationData()
                        {
                            assembly = "Library/ScriptAssemblies/" +
                                       Path.GetFileName(x.args.detail.Split(' ').FirstOrDefault()),
                            StartTime = new DateTime((x.ts + offsetToFirstTs) * ticksPerMicrosecond),
                            EndTime = new DateTime((x.ts + offsetToFirstTs + x.dur) * ticksPerMicrosecond),
                        })
                        // hack to remove double Csc entries in trace file
                        .ToLookup(x => x.assembly, x => x)
                        .Select(x => x.First())
                        // end hack
                        .ToList()
                };

                // fix up incorrect reported compilation times
                if (cc.compilationData != null && cc.compilationData.Any())
                {
                    // fix reported start/end times for compilation
                    var minStart = cc.compilationData.Min(x => x.StartTime);
                    var maxEnd = cc.compilationData.Max(x => x.StartTime);
                    if (minStart < cc.CompilationStarted) cc.CompilationStarted = minStart;
                    if (maxEnd > cc.CompilationFinished) cc.CompilationFinished = maxEnd;
                }
                
                // var beeCompilationStarted = new DateTime((beeData.traceEvents.First().ts) * ticksPerMicrosecond);
                // var beeCompilationFinished = new DateTime((beeData.traceEvents.Last().ts) * ticksPerMicrosecond);
                // Debug.Log(beeCompilationStarted + " - " + cc.CompilationStarted + ", " + beeCompilationFinished + " - " + cc.CompilationFinished);
                
                // foreach (var asm in cc.compilationData)
                //     Debug.Log(asm);

                return cc;
            }
            catch (IOException e)
            {
                Debug.LogWarning($"IOException when trying to fetch compilation data: {e}. Please try again. If the issue persists: {bugReportString}");
                return null;
            }
            catch (Exception e)
            {
                Debug.LogError($"Couldn't fetch compilation data; the format has probably changed. {bugReportString}\n{e}");
                return null;
            }
        }

        private static readonly string bugReportString = $"Please report a bug at <a href=\"{newIssueUrl}\">{newIssueUrl}</a> and include the package + Unity version.";
        const string newIssueUrl = "https://github.com/needle-tools/compilation-visualizer/issues/new";

        public static void Clear()
        {
            if (File.Exists(ProfilerJson))
                File.Delete(ProfilerJson);
        }
    }

    [Serializable]
    internal class Args {
        public string name; 
        public int durationMS; 
        public string detail; 
    }

    [Serializable]
    internal class TraceEvent {
        public string cat; 
        public string pid; 
        public int tid; 
        public long ts; 
        public string ph; 
        public string name; 
        public Args args; 
        public int dur; 
        public string cname; 
    }

    [Serializable]
    internal class BeeProfilerData {
        public List<TraceEvent> traceEvents; 
    }
}

#endif
