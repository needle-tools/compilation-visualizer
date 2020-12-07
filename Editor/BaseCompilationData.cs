using System;
using System.Collections.Generic;
using Needle.CompilationVisualizer;
using UnityEngine;

namespace Needle.CompilationVisualizer
{
    [Serializable]
    public class BaseCompilationData : ISerializationCallbackReceiver
    {
        public List<AssemblyCompilationData> compilationData = new List<AssemblyCompilationData>();
        
        public SerializableDateTime
            compilationStarted,
            compilationFinished,
            beforeAssemblyReload,
            afterAssemblyReload;
        
        public DateTime CompilationStarted { get; set; }
        public DateTime CompilationFinished { get; set; }
        public DateTime BeforeAssemblyReload { get; set; }
        public DateTime AfterAssemblyReload { get; set; }
        
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