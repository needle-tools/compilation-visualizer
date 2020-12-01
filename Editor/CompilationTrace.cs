#if UNITY_2021_1_OR_NEWER

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Needle.CompilationVisualizer
{
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
            return new IterativeCompilationData()
            {
                iterations = new List<CompilationData>()
                {
                    ConvertBeeDataToCompilationData()
                }
            };
        }

        private static CompilationData ConvertBeeDataToCompilationData()
        {
            var beeData = JsonUtility.FromJson<Root>(File.ReadAllText("Library/Bee/profiler.json")); // "profiler.json")); // "Library/Bee/profiler.json"));
            beeData.traceEvents = beeData.traceEvents.Where(x => x.ts > 0).ToList();
            var firstTs = beeData.traceEvents.First().ts;
            var conv = TimeSpan.TicksPerMillisecond / 1000;
            
            var cc = new CompilationData()
            {
                CompilationStarted = new DateTime((beeData.traceEvents.First().ts - firstTs) * conv),
                CompilationFinished = new DateTime((beeData.traceEvents.Last().ts - firstTs) * conv),
                compilationData = beeData.traceEvents
                    .Where(x => x.name.Equals("Csc", StringComparison.Ordinal) && x.args.detail != null)
                    .Select(x => new AssemblyCompilationData()
                    {
                        assembly = "Library/ScriptAssemblies/" + Path.GetFileName(x.args.detail.Split(' ').FirstOrDefault()),
                        StartTime = new DateTime((x.ts - firstTs) * conv),
                        EndTime = new DateTime((x.ts - firstTs + x.dur) * conv),
                    })
                    .ToList()
            };
            
            cc.AfterAssemblyReload = cc.CompilationFinished;
            cc.BeforeAssemblyReload = cc.CompilationStarted;
            
            // foreach (var asm in cc.compilationData)
            //     Debug.Log(asm);
            
            return cc;
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
    internal class Root {
        public List<TraceEvent> traceEvents; 
    }
}

#endif