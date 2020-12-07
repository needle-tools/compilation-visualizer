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

        private static void OnCompilationStarted(object obj)
        {
            instance.CompilationStarted = DateTime.Now;
            // Debug.Log(nameof(OnCompilationStarted) + " " + DateTime.Now);
        }

        private static void OnCompilationFinished(object obj)
        {
            instance.CompilationFinished = DateTime.Now;
            // Debug.Log(nameof(OnCompilationFinished) + " " + DateTime.Now);
        }



        private static void OnBeforeAssemblyReload()
        {
            instance.BeforeAssemblyReload = DateTime.Now;
            // Debug.Log(nameof(OnBeforeAssemblyReload) + " " + DateTime.Now);
        }
        
        private static void OnAfterAssemblyReload()
        {
            instance.AfterAssemblyReload = DateTime.Now;
            Debug.Log("Reload time: " + (instance.AfterAssemblyReload - instance.BeforeAssemblyReload).TotalSeconds + ", " + "since last comp: " + (instance.BeforeAssemblyReload - instance.CompilationFinished).TotalSeconds);
            // Debug.Log(nameof(OnAfterAssemblyReload) + " " + DateTime.Now);
        }
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
            const string ProfilerJson = "Library/Bee/profiler.json";
            if (!File.Exists(ProfilerJson)) return null;
            
            try
            {
                var beeData =
                    JsonUtility.FromJson<BeeProfilerData>(
                        File.ReadAllText(ProfilerJson)); // "profiler.json")); // "Library/Bee/profiler.json"));
                if (beeData.traceEvents == null || !beeData.traceEvents.Any()) return null;

                beeData.traceEvents = beeData.traceEvents.Where(x => x.ts > 0).ToList();
                var firstTs = beeData.traceEvents.First().ts;
                var conv = TimeSpan.TicksPerMillisecond / 1000;
                var offsetToFirstTs = TraceData.instance.CompilationStarted.Ticks / conv - firstTs;
                

                var cc = new CompilationData()
                {
                    CompilationStarted = new DateTime((beeData.traceEvents.First().ts + offsetToFirstTs) * conv),
                    CompilationFinished = new DateTime((beeData.traceEvents.Last().ts + offsetToFirstTs) * conv),
                    compilationData = beeData.traceEvents
                        .Where(x => x.name.Equals("Csc", StringComparison.Ordinal) && x.args.detail != null)
                        .Select(x => new AssemblyCompilationData()
                        {
                            assembly = "Library/ScriptAssemblies/" +
                                       Path.GetFileName(x.args.detail.Split(' ').FirstOrDefault()),
                            StartTime = new DateTime((x.ts + offsetToFirstTs) * conv),
                            EndTime = new DateTime((x.ts + offsetToFirstTs + x.dur) * conv),
                        })
                        .OrderBy(x => x.StartTime)
                        .ToList()
                };

                cc.AfterAssemblyReload = TraceData.instance.AfterAssemblyReload;
                cc.BeforeAssemblyReload = TraceData.instance.BeforeAssemblyReload;

                // foreach (var asm in cc.compilationData)
                //     Debug.Log(asm);

                return cc;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
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
