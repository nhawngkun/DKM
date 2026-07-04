using UnityEditor;
using UnityEngine;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;
#endif

namespace NabaGame.Core.Editor
{
#if ODIN_INSPECTOR

    public class AnimatorHashGeneratorWindow : OdinMenuEditorWindow
    {
        [MenuItem("BMH Game/Editor Tools/Animator Hash Generator", false, 100)]
        private static void OpenWindow()
        {
            var window = GetWindow<AnimatorHashGeneratorWindow>();
            window.position = GUIHelper.GetEditorWindowRect().AlignCenter(800, 600);
            window.titleContent = new GUIContent("Animator Hash Generator");
        }

        protected override OdinMenuTree BuildMenuTree()
        {
            var tree = new OdinMenuTree(true);
            var customMenuStyle = new OdinMenuStyle
            {
                BorderPadding = 0f,
                AlignTriangleLeft = true,
                TriangleSize = 16f,
                TrianglePadding = 0f,
                Offset = 20f,
                Height = 23,
                IconPadding = 0f,
                BorderAlpha = 0.323f
            };
            tree.DefaultMenuStyle = customMenuStyle;
            tree.Config.DrawSearchToolbar = true;
            tree.AddObjectAtPath("Data Config", AnimatorHashGeneratorData.Instance);
            return tree;
        }
    }
#else
    public class AnimatorHashGeneratorWindow 
    {
        [MenuItem("BMH Game/Editor Tools/Animator Hash Generator", false, 100)]
        static void OpenWarningPanel()
        {
            EditorUtility.DisplayDialog("Install Odin", "Please Install Odin Package to use Animator Hash", "OK");
        }
    }
#endif
}