using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Player;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.TestTools;

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
        
#if UNITY_2020_2_OR_NEWER
        /*
         * https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-options/language#langversion
         * 2022.2: 
         * 2022.1: 
         * 2021.3: 
         * 2021.2: 	C# 9.0
         * 2021.1: 
         * 2020.3: 
         * 2020.2: .NET 4.6: C# 8
         * 2020.1: 
         * 2019.4: 
         * 2019.3: 
         * 2019.2: 
         * 2019.1: 
         * 2018.4: .NET 3.5: C# 4, .NET 4.6: C# 7.3
         */

        public class FeatureData
        {
            public string featureArg;
            public string unityHint;
            public override string ToString() => $"C# {featureArg}, used in {unityHint}";
        }
        
        private static IEnumerable<FeatureData> GetFeatures()
        {
            yield return new FeatureData() { featureArg = "-langversion:\"4\"", unityHint = "2018.4 — 2020.1, .NET 3.5" };
            yield return new FeatureData() { featureArg = "-langversion:\"7.3\"", unityHint = "2018.4 — 2020.1, .NET 4.6" };
            yield return new FeatureData() { featureArg = "-langversion:\"8\"", unityHint = "2020.2 — 2021.1" };
            yield return new FeatureData() { featureArg = "-langversion:\"9\"", unityHint = "2021.2+" };
        }
        
#pragma warning disable 618
        [UnityTest] [Explicit]
        public IEnumerator LanguageFeatureSupport([ValueSource(nameof(GetFeatures))] FeatureData feature)
        {
            var buildTarget = BuildTarget.StandaloneWindows64;
            var group = BuildPipeline.GetBuildTargetGroup(buildTarget);
            
            var additionalCompilerArgumentsForGroup = PlayerSettings.GetAdditionalCompilerArgumentsForGroup(group);
            const string key = nameof(CompilationVisualizer) + "_" + nameof(LanguageFeatureSupport) + "_" + nameof(additionalCompilerArgumentsForGroup);
            const string targetKey = nameof(CompilationVisualizer) + "_" + nameof(LanguageFeatureSupport) + "_" + nameof(buildTarget);
            
            SessionState.SetString(key, string.Join(",", additionalCompilerArgumentsForGroup) );
            SessionState.SetString(targetKey, buildTarget.ToString());
            
            var argList = additionalCompilerArgumentsForGroup.ToList();
            argList.Add(feature.featureArg);
            PlayerSettings.SetAdditionalCompilerArgumentsForGroup(group, argList.ToArray());
            yield return new WaitForDomainReload();

            // restore variables - they're reset after Domain Reload
            buildTarget = (BuildTarget)Enum.Parse(typeof(BuildTarget), SessionState.GetString(targetKey, BuildTarget.StandaloneWindows64.ToString()));
            group = BuildPipeline.GetBuildTargetGroup(buildTarget);

            var oldArgsList = SessionState.GetString(key, "");
            var oldArgs = oldArgsList.Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries);
            PlayerSettings.SetAdditionalCompilerArgumentsForGroup(group, oldArgs);
            yield return new WaitForDomainReload();
        }
#pragma warning restore
#endif
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