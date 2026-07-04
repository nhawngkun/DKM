using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NabaGame.Core.Editor.Pool
{
    public static class CoreEditorMenuItem
    {
        [MenuItem("GameObject/BMH Game/FastPoolManager", false, 0)]
        public static void AddFastPoolManager()
        {
            Object prefab =
                AssetDatabase.LoadAssetAtPath<Object>(
                    "Packages/com.nabagame.core/Runtime/FastPool/Prefabs/FastPoolManager.prefab");
            if (prefab != null)
            {
                PrefabUtility.InstantiatePrefab(prefab);
            }
            else
            {
                Debug.LogError("Cannot find FastPoolManager prefab");
            }
        }

        [MenuItem("GameObject/BMH Game/Disable RaycastTargets", false, 49)]
        private static void DisableRaycastTarget()
        {
            foreach (GameObject go in Selection.gameObjects)
            {
                TextMeshProUGUI[] texts = go.GetComponentsInChildren<TextMeshProUGUI>();
                for (int i = 0; i < texts.Length; i++)
                {
                    texts[i].raycastTarget = false;
                }

                Image[] images = go.GetComponentsInChildren<Image>();
                for (int i = 0; i < images.Length; i++)
                {
                    images[i].raycastTarget = false;
                }

                Button[] buttons = go.GetComponentsInChildren<Button>();
                if (buttons.Length > 0)
                {
                    for (int i = 0; i < buttons.Length; i++)
                    {
                        buttons[i].targetGraphic.raycastTarget = true;
                    }

                    buttons[0].transition = Selectable.Transition.None;
                }

                Toggle[] toggle = go.GetComponentsInChildren<Toggle>();
                for (int i = 0; i < toggle.Length; i++)
                {
                    toggle[i].targetGraphic.raycastTarget = true;
                }

                ScrollRect[] scrolls = go.GetComponentsInChildren<ScrollRect>();
                for (int i = 0; i < scrolls.Length; i++)
                {
                    scrolls[i].viewport.GetComponent<Image>().raycastTarget = true;
                }
            }
        }

        [MenuItem("GameObject/BMH Game/Disable RaycastTargets", true, 49)]
        private static bool ValidateDisableRayCastTargets()
        {
            if (Selection.gameObjects.Length <= 0)
            {
                return false;
            }

            for (int i = 0; i < Selection.gameObjects.Length; i++)
            {
                if (Selection.gameObjects[i].GetComponent<RectTransform>() == null)
                {
                    return false;
                }
            }

            return true;
        }
    }
}