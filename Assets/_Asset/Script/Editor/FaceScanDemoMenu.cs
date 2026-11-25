using UnityEditor;
using UnityEngine;

public static class FaceScanDemoMenu
{
    const string HeadPath = "Assets/_Asset/FaceScan/FaceHead.json";

    [MenuItem("Tools/FaceScan/Tạo đầu demo trong scene")]
    static void Create()
    {
        var json = AssetDatabase.LoadAssetAtPath<TextAsset>(HeadPath);
        if (json == null)
        {
            Debug.LogError($"FaceScan: thiếu {HeadPath} - chạy: python Tools/FaceScan/build_head.py <head.gltf>");
            return;
        }

        var go = new GameObject("FaceScanDemo");
        Undo.RegisterCreatedObjectUndo(go, "Create FaceScanDemo");
        var view = SceneView.lastActiveSceneView;
        if (view != null) go.transform.position = view.pivot;

        var demo = go.AddComponent<FaceScanDemo>();
        var so = new SerializedObject(demo);
        so.FindProperty("headJson").objectReferenceValue = json;
        so.ApplyModifiedPropertiesWithoutUndo();
        demo.Initialize();

        Selection.activeGameObject = go;
        SceneView.FrameLastActiveSceneView();
    }
}
