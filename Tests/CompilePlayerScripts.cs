using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEngine;

namespace Needle.CompilationVisualizer
{
    public class CompilePlayerScripts
    {
        [Test]
        public void BuildTargetCompiles([ValueSource(nameof(GetAllBuildTargets))] BuildTarget buildTarget)
        {
            var settings = new ScriptCompilationSettings
            {
                group = BuildPipeline.GetBuildTargetGroup(buildTarget),
                target = buildTarget,
                options = ScriptCompilationOptions.None
            };
            
            PlayerBuildInterface.CompilePlayerScripts(settings, TempDir + "_" + buildTarget);
        }
        
        private const string TempDir = "Temp/PlayerScriptCompilationTests";

        private static IEnumerable<BuildTarget> GetAllBuildTargets()
        {
            return Enum.GetValues(typeof(BuildTarget))
                .Cast<BuildTarget>()
                .Where(x => GetAttributeOfType<ObsoleteAttribute>(x) == null)
                .Except(new [] { BuildTarget.WSAPlayer, BuildTarget.NoTarget }); // excluded because they have errors even with just Unity packages.
        }
        
        private static T GetAttributeOfType<T>(Enum enumVal) where T:Attribute
        {
            var memInfo = enumVal.GetType().GetMember(enumVal.ToString());
            var attributes = memInfo[0].GetCustomAttributes(typeof(T), false);
            return attributes.Length > 0 ? (T) attributes[0] : null;
        }
    }
    
    /// <summary>
    /// Logs Assemblies and Types produced during <code>PlayerBuildInterface.CompilePlayerScripts</code>
    /// <example>
    /// <code>CompilationResultHelpers.LogAssembliesAndTypes(buildTarget, scriptCompilationResult);</code>
    /// </example>
    /// </summary>
    public static class CompilationResultHelpers
    {
        public static void LogAssembliesAndTypes(BuildTarget buildTarget, ScriptCompilationResult result)
        {
            var log = $"<b>[{buildTarget}] Build Assemblies:</b>\n• {string.Join("\n• ", result.assemblies)}";
#if UNITY_2019_1_OR_NEWER
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0}", log);
#else
            Debug.Log(log);
#endif
            var typeDbString = result.typeDB.GetType().GetMethod("SerializeToJson", (System.Reflection.BindingFlags)(-1))?.Invoke(result.typeDB, null) as string;
            if (typeDbString != null)
            {
                if(typeDbString.StartsWith("Types:"))
                    typeDbString = typeDbString.Substring("Types:".Length);
            
                Debug.Log(typeDbString);
                var dbData = new TypeDBData();
                EditorJsonUtility.FromJsonOverwrite(typeDbString, dbData);
                if (dbData.m_Classes != null)
                {
                    var log2 = "Types:\n• " + string.Join("\n• ", dbData.m_Classes.Select(x => x.first + "\n" + x.second));
#if UNITY_2019_1_OR_NEWER
                    Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0}", log);
#else
                    Debug.Log(log);
#endif
                }
            }
        }
        
        // TypeDB JSON serialization format
        [Serializable]
        internal class TypeDBData
        {
            public List<MClass> m_Classes;

            [Serializable]
            public class ClassInfo
            {
                public string className;
                public int assemblyIndex;

                public override string ToString() => $"{className} [in assembly {assemblyIndex}]";
            }

            [Serializable]
            public class MClass
            {
                public ClassInfo first;
                public FieldInfo second;
            }
                    
            [Serializable]
            public class FieldInfo
            {
                public List<Field> fields;

                public override string ToString() => $"·    − {string.Join("\n·    − ", fields)}";
            }

            [Serializable]
            public class Field
            {
                public string typeName;
                public string name;
                public int flags;
                public int fixedBufferLength;
                public string fixedBufferTypename;

                public override string ToString() => $"{name} ({typeName})";
            }
        }
    }
}