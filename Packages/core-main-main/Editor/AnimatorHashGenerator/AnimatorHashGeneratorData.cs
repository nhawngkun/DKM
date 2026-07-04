using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NabaGame.Core.Runtime.Extensions;
using NabaGame.Core.Editor.Utils;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

namespace NabaGame.Core.Editor
{
    public class AnimatorHashGeneratorData : ScriptableObject
    {
        private static string defaultAssetFolder = "BBPackages/EditorTool";
        private const string PROGRESS_TITLE = "Generating Animator Hash File...";
        private const string QUOTE = "\"";

        public static AnimatorHashGeneratorData Instance
        {
            get => GetInstance();
        }

        private static AnimatorHashGeneratorData GetInstance()
        {
            AnimatorHashGeneratorData value =
                AssetDatabase.LoadAssetAtPath<AnimatorHashGeneratorData>(
                    $"Assets/{defaultAssetFolder}/AnimatorHashGeneratorData.asset");
            if (value == null)
            {
                value = CreateInstance<AnimatorHashGeneratorData>();
                if (!Directory.Exists($"{Application.dataPath}/{defaultAssetFolder}"))
                {
                    Directory.CreateDirectory($"{Application.dataPath}/{defaultAssetFolder}");
                }

                AssetDatabase.CreateAsset(value, $"Assets/{defaultAssetFolder}/AnimatorHashGeneratorData.asset");
            }

            return value;
        }
#if ODIN_INSPECTOR
        [PropertySpace, TableList]
#endif
        public List<FolderPath> AnimatorsPaths;
#if ODIN_INSPECTOR
        [Button(ButtonSizes.Large), PropertySpace]
#endif
        public void GetAnimators()
        {
            animators.Clear();
            try
            {
                foreach (var path in AnimatorsPaths)
                {
                    animators.AddRange(AssetUtils.GetAtPath<AnimatorController>(path.path));
                }
            }
            catch (Exception exception)
            {
                animators = null;
            }
        }

#if ODIN_INSPECTOR
        [PropertySpace]
#endif
        public List<AnimatorController> animators = new List<AnimatorController>();
#if ODIN_INSPECTOR
        [Button(ButtonSizes.Gigantic), PropertySpace]
#endif
        public void GenerateHash()
        {
            // GetAnimators();
            if (animators.IsNullOrEmpty())
            {
                EditorUtility.DisplayDialog("Error", "Do not found any Animator Controller", "close");
                return;
            }

            // Try to find an existing file in the project called "AnimatorParameters.cs"
            string filePath = string.Empty;
            foreach (var file in Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories))
            {
                if (Path.GetFileNameWithoutExtension(file) == "AnimatorParameters")
                {
                    filePath = file;
                    break;
                }
            }

            // If no such file exists already, use the save panel to get a folder in which the file will be placed.
            if (string.IsNullOrEmpty(filePath))
            {
                string directory = EditorUtility.OpenFolderPanel("Choose location for AnimatorParameters.cs",
                    Application.dataPath, "");

                // Canceled choose? Do nothing.
                if (string.IsNullOrEmpty(directory))
                {
                    return;
                }

                filePath = Path.Combine(directory, "AnimatorParameters.cs");
            }

            List<string> parameterNames = new List<string>();

            using (var writer = new StreamWriter(filePath))
            {
                writer.WriteLine("using UnityEngine;\n");
                writer.WriteLine("public static class AnimatorParameters");
                writer.WriteLine("{");
                Dictionary<string, int> variableDict = new Dictionary<string, int>();
                for (int i = 0; i < animators.Count; i++)
                {
                    AnimatorController a = animators[i];
                    EditorUtility.DisplayProgressBar(PROGRESS_TITLE,
                        string.Format("Generating property hashes for {0}", a.name), (float)i / animators.Count);
                    writer.WriteLine("\t//" + a.name);

                    for (int j = 0; j < a.parameters.Length; j++)
                    {
                        var p = a.parameters[j];

                        if (parameterNames.Contains(p.name))
                            continue;

                        parameterNames.Add(p.name);
                        writer.WriteLine(ConstructVariableLine(p, ref variableDict));
                    }

                    writer.Write("\n");
                }

                writer.WriteLine("}");

                writer.Close();
                AssetDatabase.Refresh();
                EditorUtility.ClearProgressBar();
            }
        }

        private string ConstructVariableLine(AnimatorControllerParameter p, ref Dictionary<string,int> variableDict)
        {
            string variableName = ConstructVariable(p.name, p.type.ToString());
            if (variableDict.TryGetValue(variableName, out int count))
            {
                count++;
                variableDict[variableName] = count;
                variableName += $"_{count}";
            }
            else
            {
                variableDict.Add(variableName,0);
            }
            string finalString;
            string tabs = "\t";
            finalString = tabs + string.Format("public static readonly int {0} = Animator.StringToHash ({1}{2}{1});",
                variableName, QUOTE, p.name);
            return finalString;
        }

        private string ConstructVariable(string name, string type)
        {
            string finalName = name;
            finalName = SplitCamelCase(finalName);
            finalName = finalName.Replace(' ', '_');
            finalName = finalName.ToUpper();
            return finalName;
        }

        string ConstructVariable(AnimatorControllerParameter p)
        {
            return ConstructVariable(p.name, p.type.ToString());
        }

        private static string SplitCamelCase(string str)
        {
            return Regex.Replace(Regex.Replace(str, @"(\P{Ll})(\P{Ll}\p{Ll})", "$1 $2"), @"(\p{Ll})(\P{Ll})", "$1 $2");
        }
    }

    [Serializable]
    public class FolderPath
    {
#if ODIN_INSPECTOR
        [FolderPath(AbsolutePath = false, ParentFolder = "Assets", RequireExistingPath = true)]
#endif
        public string path;
    }
}