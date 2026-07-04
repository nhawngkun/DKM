using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class SceneEditor : EditorWindow
{
    private Vector2 scrollPosition;

    public static void OpenWindown()
    {
        var window = GetWindow<SceneEditor>("Open Scenes");
        window.minSize = new Vector2(300, 400);
        window.maxSize = new Vector2(300, 1000);
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Width(position.width),
            GUILayout.Height(position.height - 20));
        int sceneCount = EditorSceneManager.sceneCountInBuildSettings;
        GUILayout.BeginVertical();

        foreach (var scene in EditorBuildSettings.scenes)
        {
            // GUILayout.FlexibleSpace();
            if (scene.enabled)
            {
                string[] strings = scene.path.Split('/');
                string _name = strings.LastOrDefault().Replace(".unity", "");
                if (GUILayout.Button(new GUIContent(_name, "Start From LoadScene")))
                {
                    EditorSceneManager.OpenScene(scene.path);
                }
            }
        }

        GUILayout.EndVertical();
        GUILayout.EndScrollView();
    }
}