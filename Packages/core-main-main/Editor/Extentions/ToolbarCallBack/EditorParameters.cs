using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[CreateAssetMenu(fileName = "EditorParameters", menuName = "Data/Editor/EditorParameters", order = 0)]

public class EditorParameters : ScriptableObject
{
    public static EditorParameters Instance => GetSingleAtPath<EditorParameters>("Editor","EditorParameters.asset");
    [Sirenix.OdinInspector.FolderPath(ParentFolder = "Assets", AbsolutePath = true)]
    public string curEditScene;
    public string startScene;
    public static T GetSingleAtPath<T>(string filepath, string filename, bool isRecusive = true) where T : UnityEngine.Object
    {
        ArrayList al = new ArrayList();
        string absolutePath = Application.dataPath + "/" + filepath;
        SearchOption searchOption = isRecusive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        if (!Directory.Exists(absolutePath)) return default(T);
        string[] fileEntries = Directory.GetFiles(absolutePath, "*", searchOption);

        int j = 0;
        foreach (string fileAbsolutePath in fileEntries)
        {
            j++;
            string fileAbsolute = fileAbsolutePath.Replace(@"\","/");
            int assetPathIndex = fileAbsolute.IndexOf("Assets");
            string localPath = fileAbsolute.Substring(assetPathIndex);
            string name = fileAbsolute.Substring(fileAbsolute.LastIndexOf("/") + 1);
            if (filename == name)
            {
                UnityEngine.Object t = AssetDatabase.LoadAssetAtPath(localPath, typeof(T));

                if (t != null)
                {
                    return (T) t;
                }
            }
        }

        return default(T);
    }
}