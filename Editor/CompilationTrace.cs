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
    [Serializable]
    internal class CompilationDataTrace
    {
//         [InitializeOnLoadMethod]
//         static void InitCompilationEvents() {
// #if UNITY_2019_1_OR_NEWER
//             CompilationPipeline.compilationStarted += OnCompilationStarted;
//             CompilationPipeline.compilationFinished += OnCompilationFinished;
// #endif
//             AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
//             AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
//
//             cachedCompilationData = new CompilationData();
//         }

        // private static CompilationData cachedCompilationData;

        // private static void OnCompilationStarted(object obj)
        // {
        //     cachedCompilationData.CompilationStarted = DateTime.Now;
        //     // Debug.Log(nameof(OnCompilationStarted) + " " + DateTime.Now);
        // }
        //
        // private static void OnCompilationFinished(object obj)
        // {
        //     cachedCompilationData.CompilationFinished = DateTime.Now;
        //     // Debug.Log(nameof(OnCompilationFinished) + " " + DateTime.Now);
        // }
        //
        // private static void OnBeforeAssemblyReload()
        // {
        //     cachedCompilationData.BeforeAssemblyReload = DateTime.Now;   
        //     // Debug.Log(nameof(OnBeforeAssemblyReload) + " " + DateTime.Now);
        // }
        //
        // private static void OnAfterAssemblyReload()
        // {
        //     cachedCompilationData.AfterAssemblyReload = DateTime.Now;
        //     // Debug.Log(nameof(OnAfterAssemblyReload) + " " + DateTime.Now);
        // }

        // internal class IterativeCompilationData
        // {
        //     public List<CompilationData> iterations;
        // }
        
        // public List<AssemblyCompilationData> compilationData;

        // public static IterativeCompilationData GetAll()
        // {
        //     var data = ConvertBeeDataToCompilationData();
        //     if (data == null) return null;
        //     
        //     return new IterativeCompilationData()
        //     {
        //         iterations = new List<CompilationData>()
        //         {
        //             data
        //         }
        //     };
        // }

        internal static CompilationData ConvertBeeDataToCompilationData(CompilationData source)
        {
            const string ProfilerJson = "Library/Bee/profiler.json";
            if (!File.Exists(ProfilerJson)) return null;
            if (source == null) return null;
            
            try
            {
                var cc = source;
                var beeData =
                    JsonUtility.FromJson<BeeProfilerData>(
                        File.ReadAllText(ProfilerJson)); // "profiler.json")); // "Library/Bee/profiler.json"));
                if (beeData.traceEvents == null || !beeData.traceEvents.Any()) return null;

                beeData.traceEvents = beeData.traceEvents.Where(x => x.ts > 0).ToList();
                var firstTs = beeData.traceEvents.First().ts;
                var offsetToFirstTs = cc.CompilationStarted.Ticks - firstTs;
                var conv = TimeSpan.TicksPerMillisecond / 1000;

                
                
                // var cc = new CompilationData()
                {
                    // CompilationStarted = new DateTime((beeData.traceEvents.First().ts + offsetToFirstTs) * conv),
                    // CompilationFinished = new DateTime((beeData.traceEvents.Last().ts + offsetToFirstTs) * conv),
                    cc.compilationData = beeData.traceEvents
                        .Where(x => x.name.Equals("Csc", StringComparison.Ordinal) && x.args.detail != null)
                        .Select(x => new AssemblyCompilationData()
                        {
                            assembly = "Library/ScriptAssemblies/" +
                                       Path.GetFileName(x.args.detail.Split(' ').FirstOrDefault()),
                            StartTime = new DateTime((x.ts + offsetToFirstTs) * conv),
                            EndTime = new DateTime((x.ts + offsetToFirstTs + x.dur) * conv),
                        })
                        .OrderBy(x => x.StartTime)
                        .ToList();
                };

                // cc.AfterAssemblyReload = cachedCompilationData.AfterAssemblyReload;
                // cc.BeforeAssemblyReload = cachedCompilationData.BeforeAssemblyReload;

                // foreach (var asm in cc.compilationData)
                //     Debug.Log(asm);

                return cc;
            }
            catch (Exception)
            {
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
        public int pid; 
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