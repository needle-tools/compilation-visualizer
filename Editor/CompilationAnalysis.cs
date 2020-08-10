using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Needle.CompilationVisualizer
{
    /// Extended by hybridherbst / NeedleTools
    /// Based on ideas by karjj1 and angularsen:
    /// https://gist.github.com/karljj1/9c6cce803096b5cd4511cf0819ff517b
    /// https://gist.github.com/angularsen/7a48f47beb0f8a65dd786ec38b02da57/revisions
    [InitializeOnLoad]
    internal class CompilationAnalysis
    {
        private const string EditorPrefStore = "Needle.CompilationVisualizer.CompilationData";

        private const string AllowLoggingPrefsKey = nameof(CompilationAnalysis) + "_" + nameof(AllowLogging);  
        public static bool AllowLogging {
            get => EditorPrefs.HasKey(AllowLoggingPrefsKey) && EditorPrefs.GetBool(AllowLoggingPrefsKey);
            set => EditorPrefs.SetBool(AllowLoggingPrefsKey, value);
        }

        static CompilationAnalysis() {
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            CompilationPipeline.assemblyCompilationStarted += OnAssemblyCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
        }

        private static void OnCompilationStarted(object o) {
            if(AllowLogging) Debug.Log("Compilation Started at " + DateTime.Now);
            var data = new CompilationData {
                CompilationStarted = DateTime.Now
            };
            CompilationData.Write(data);
        }

        private static void OnCompilationFinished(object o) {
            if(AllowLogging) Debug.Log("Compilation Finished at " + DateTime.Now);
            var data = CompilationData.Get();
            data.CompilationFinished = DateTime.Now;
            CompilationData.Write(data);
        }
        
        private static void OnAssemblyCompilationStarted(string assembly)
        {
            var data = CompilationData.Get();
            var compilationData = data.compilationData.FirstOrDefault(x => x.assembly == assembly);
            if(compilationData == null) {
                compilationData = new CompilationData.AssemblyCompilationData() {
                    assembly = assembly,
                    StartTime = DateTime.Now
                };
                data.compilationData.Add(compilationData);
            }
            
            compilationData.StartTime = DateTime.Now;
            CompilationData.Write(data);
        }

        private static void OnAssemblyCompilationFinished(string assembly, CompilerMessage[] arg2)
        {
            var data = CompilationData.Get();
            var compilationData = data.compilationData.FirstOrDefault(x => x.assembly == assembly);
            if(compilationData == null) {
                Debug.LogError("Compilation finished for " + assembly + ", but no startTime found!");
                return;
            }
            
            compilationData.EndTime = DateTime.Now;
            CompilationData.Write(data);
        }

        private static void OnBeforeAssemblyReload()
        {
            if(AllowLogging) Debug.Log("Before Assembly Reload at " + DateTime.Now);
            var data = CompilationData.Get();
            data.BeforeAssemblyReload = DateTime.Now;
            CompilationData.Write(data);
        }

        private static void OnAfterAssemblyReload() {
            var data = CompilationData.Get();
            data.AfterAssemblyReload = DateTime.Now;
            CompilationData.Write(data);

            if (!AllowLogging) return;
            
            Debug.Log("After Assembly Reload at " + DateTime.Now);
            
            var compilationSpan = data.CompilationFinished - data.CompilationStarted;
            Debug.Log("<b>Compilation Report</b> - Total Time: " + compilationSpan);
            foreach (var d in data.compilationData) {
                Debug.Log(d);
            }

            var span = data.AfterAssemblyReload - data.BeforeAssemblyReload;
            Debug.Log("<b>Assembly Reload</b> - Total Time: " + span);
        }
        
        [Serializable]
        public class CompilationData : ISerializationCallbackReceiver
        {
            public SerializableDateTime
                compilationStarted,
                compilationFinished,
                beforeAssemblyReload,
                afterAssemblyReload;

            public DateTime CompilationStarted { get; set; }
            public DateTime CompilationFinished { get; set; }
            public DateTime BeforeAssemblyReload { get; set; }
            public DateTime AfterAssemblyReload { get; set; }

            [Serializable]
            public struct SerializableDateTime
            {
                private static string format = "MM-dd-yyyy HH:mm:ss.fff";
                public string utc;
                
                public DateTime DateTime {
                    get
                    {
                        if (DateTime.TryParseExact(utc, format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime result))
                            return result;
                        
                        return DateTime.Now;
                    }
                    set => utc = value.ToString(format, CultureInfo.InvariantCulture);
                }

                public static implicit operator SerializableDateTime(DateTime dateTime) {
                    var sd = new SerializableDateTime { DateTime = dateTime };
                    return sd;
                }
                
                public static implicit operator DateTime(SerializableDateTime dateTime) {
                    return dateTime.DateTime;
                }
            }
            
            [Serializable]
            public class AssemblyCompilationData : ISerializationCallbackReceiver
            {
                private static string format = "HH:mm:ss.fff";
                public override string ToString() {
                    return assembly + ": " + (EndTime - StartTime) + " (from " + StartTime.ToString(format, CultureInfo.CurrentCulture) + " to " + EndTime.ToString(format, CultureInfo.CurrentCulture) + ")";
                }
                
                public string assembly;
                public SerializableDateTime startTime;
                public SerializableDateTime endTime;
                public DateTime StartTime { get; set; }
                public DateTime EndTime { get; set; }
                
                public void OnBeforeSerialize()
                {
                    startTime = StartTime;
                    endTime = EndTime;
                }

                public void OnAfterDeserialize()
                {
                    StartTime = startTime;
                    EndTime = endTime;
                }
            }
            
            public List<AssemblyCompilationData> compilationData = new List<AssemblyCompilationData>();

            private static CompilationData tempData = null;
            
            public static CompilationData Get() {
                if (tempData != null) return tempData;
                
                if (!EditorPrefs.HasKey(EditorPrefStore)) {
                    var sd = new CompilationData();
                    Write(sd);
                }
                var restoredData = JsonUtility.FromJson<CompilationData>(EditorPrefs.GetString(EditorPrefStore));
                tempData = restoredData;
                return tempData;
            }

            public static void Write(CompilationData data) {
                tempData = data;
                var json = JsonUtility.ToJson(data, true);
                EditorPrefs.SetString(EditorPrefStore, json);
            }

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
        }
        
    }
}