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
        static void InitCompilationEvents() {
#if UNITY_2019_1_OR_NEWER
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
#endif
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
        }

        private static void OnCompilationStarted(object obj) => instance.CompilationStarted = DateTime.Now;
        private static void OnCompilationFinished(object obj) => instance.CompilationFinished = DateTime.Now;
        private static void OnBeforeAssemblyReload() => instance.BeforeAssemblyReload = DateTime.Now;
        private static void OnAfterAssemblyReload() => instance.AfterAssemblyReload = DateTime.Now;
        // {       
            // Debug.Log("Reload time: " + (instance.AfterAssemblyReload - instance.BeforeAssemblyReload).TotalSeconds + ", " + "since last comp: " + (instance.BeforeAssemblyReload - instance.CompilationFinished).TotalSeconds);
            // Debug.Log(nameof(OnAfterAssemblyReload) + " " + DateTime.Now);

            // string Log(DateTime a, DateTime b) => (b - a).TotalSeconds + "s ";
            //
            // Debug.Log("from start to finish: " + Log(instance.CompilationStarted, instance.AfterAssemblyReload) +
            //           "\ncompilation: " + Log(instance.CompilationStarted, instance.CompilationFinished) + 
            //           "\nend of comp to begin reload: " + Log(instance.CompilationFinished, instance.BeforeAssemblyReload) + 
            //           "\nreload: " + Log(instance.BeforeAssemblyReload, instance.AfterAssemblyReload));
        // }
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

        private static CompilationData ConvertBeeDataToCompilationData()
        {
            #if UNITY_2021_2_OR_NEWER
            const string ProfilerJson = "Library/Bee/fullprofile.json";
            #else
            const string ProfilerJson = "Library/Bee/profiler.json";
            #endif
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
                                && x.pid == "bee_backend"
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
                        .ToList()
                };

                // var beeCompilationStarted = new DateTime((beeData.traceEvents.First().ts) * conv);
                // var beeCompilationFinished = new DateTime((beeData.traceEvents.Last().ts) * conv);
                // Debug.Log(beeCompilationStarted + " - " + cc.CompilationStarted + ", " + beeCompilationFinished + " - " + cc.CompilationFinished);
                
                // foreach (var asm in cc.compilationData)
                //     Debug.Log(asm);

                return cc;
            }
            catch (Exception e)
            {
                Debug.LogError("Couldn't fetch compilation data: Please report a bug to hi@needle.tools.\n" + e);
                return null;
            }
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
