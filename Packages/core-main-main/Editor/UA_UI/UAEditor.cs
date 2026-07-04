using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class UAEditor : EditorWindow
{
    private bool ShowUI = true;
    private bool isPlaying;
    private CanvasGroup _canvasGroup;

    public static void OpenWindow()
    {
        var window = GetWindow<UAEditor>("Marketing Panel");
        window.minSize = new Vector2(300, 400);
        window.maxSize = new Vector2(300, 1000);
    }

    private void OnGUI()
    {
        GUILayout.Label("Marketing Panel", EditorStyles.boldLabel);
        ShowUI = EditorGUILayout.Toggle("Show UI", ShowUI);
        if (Application.isPlaying && !isPlaying)
        {
            isPlaying = true;
            var canvasAll = FindObjectsOfType<Canvas>();
            for (int i = 0; i < canvasAll.Length; i++)
            {
                if (canvasAll[i].renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    var canvasGroup = canvasAll[i].GetComponent<CanvasGroup>();
                    if (canvasGroup == null)
                    {
                        canvasAll[i].gameObject.AddComponent<CanvasGroup>();
                    }

                    _canvasGroup = canvasAll[i].GetComponent<CanvasGroup>();
                    _canvasGroup.alpha = ShowUI ? 1 : 0;
                }
            }
        }
        else
        {
            isPlaying = false;
            if (_canvasGroup != null)
                _canvasGroup.alpha = 1;
        }
    }
}