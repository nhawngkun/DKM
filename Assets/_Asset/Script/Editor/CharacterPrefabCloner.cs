using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Nhân bản prefab nhân vật mẫu (KCC + combat đã chỉnh sẵn) rồi thay model: component trên model cũ
// được chép sang model mới, mọi tham chiếu (component, xương Humanoid) nối lại sang model mới.
// Prefab đã có thì giữ nguyên, không ghi đè chỉnh sửa tay.
public static class CharacterPrefabCloner
{
    private static readonly System.Type[] Skipped = { typeof(Transform), typeof(Animator), typeof(CoatPhysics), typeof(SpellCaster) };
    private const string MeleeSource = "Assets/_Asset/Prefab/Character.prefab";

    public static bool Ensure(string templatePath, string modelPath, string outPath, string name)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(outPath) != null) return true;

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model == null || AssetDatabase.LoadAssetAtPath<GameObject>(templatePath) == null)
        {
            Debug.LogError($"[CharacterPrefabCloner] thiếu {templatePath} hoặc {modelPath}");
            return false;
        }
        if (!AssetDatabase.CopyAsset(templatePath, outPath))
        {
            Debug.LogError($"[CharacterPrefabCloner] không chép được {templatePath} -> {outPath}");
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(outPath);
        try
        {
            Animator oldAnimator = root.GetComponentInChildren<Animator>(true);
            GameObject oldModel = oldAnimator.gameObject;
            Transform holder = oldModel.transform.parent;
            holder.name = name;

            var newModel = (GameObject)PrefabUtility.InstantiatePrefab(model, holder);
            newModel.name = name + "_Model";
            newModel.transform.SetSiblingIndex(oldModel.transform.GetSiblingIndex());
            newModel.transform.SetLocalPositionAndRotation(oldModel.transform.localPosition, oldModel.transform.localRotation);
            newModel.transform.localScale = oldModel.transform.localScale;

            Animator newAnimator = newModel.GetComponent<Animator>();
            if (newAnimator == null) newAnimator = newModel.AddComponent<Animator>();
            newAnimator.applyRootMotion = true;
            newAnimator.updateMode = oldAnimator.updateMode;
            newAnimator.cullingMode = oldAnimator.cullingMode;
            newAnimator.runtimeAnimatorController = oldAnimator.runtimeAnimatorController;

            var map = new Dictionary<Object, Object>
            {
                { oldModel, newModel },
                { oldModel.transform, newModel.transform },
                { oldAnimator, newAnimator },
            };
            foreach (Component component in oldModel.GetComponents<Component>())
            {
                if (component == null || Skipped.Contains(component.GetType())) continue;
                map[component] = Paste(component, newModel);
            }
            if (newModel.GetComponent<MeleeHitDetector>() == null) AddMelee(newModel);

            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                Transform from = oldAnimator.isHuman ? oldAnimator.GetBoneTransform((HumanBodyBones)i) : null;
                Transform to = newAnimator.isHuman ? newAnimator.GetBoneTransform((HumanBodyBones)i) : null;
                if (from != null && to != null) map[from] = to;
            }

            WalkLikeDante(root.GetComponent<CharController>());
            int rewired = Rewire(root, map, oldModel);
            Object.DestroyImmediate(oldModel);
            PrefabUtility.SaveAsPrefabAsset(root, outPath);
            Debug.Log($"[CharacterPrefabCloner] tạo {outPath} từ {templatePath}, model {modelPath}, nối lại {rewired} tham chiếu");
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // mẫu là nhân vật bay (Trish): nhân vật mới đi bộ bằng root motion như Dante
    private static void WalkLikeDante(CharController controller)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(MeleeSource);
        CharController walker = source != null ? source.GetComponent<CharController>() : null;
        if (controller == null || walker == null) return;
        controller.CanFly = false;
        controller.AlwaysFly = false;
        controller.InPlaceLocomotion = false;
        controller.FlyHeight = walker.FlyHeight;
        controller.WalkSpeed = walker.WalkSpeed;
        controller.JogSpeed = walker.JogSpeed;
        controller.RunSpeed = walker.RunSpeed;
        controller.SprintSpeed = walker.SprintSpeed;
    }

    private static Component Paste(Component source, GameObject target)
    {
        UnityEditorInternal.ComponentUtility.CopyComponent(source);
        UnityEditorInternal.ComponentUtility.PasteComponentAsNew(target);
        Component[] all = target.GetComponents(source.GetType());
        return all[all.Length - 1];
    }

    // chép thông số đã tune của MeleeHitDetector trên Dante, bỏ vệt chém (thuộc model Dante)
    private static void AddMelee(GameObject target)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(MeleeSource);
        MeleeHitDetector tuned = source != null ? source.GetComponentInChildren<MeleeHitDetector>(true) : null;
        MeleeHitDetector melee = tuned != null ? (MeleeHitDetector)Paste(tuned, target) : target.AddComponent<MeleeHitDetector>();
        melee.BladeTrails = new TrailRenderer[0];
        melee.characterCombo = target.GetComponent<CharacterCombo>();
        melee.weaponHolder = target.GetComponent<WeaponHolder>();
        melee.cameraFeedback = target.GetComponent<ComboCameraFeedback>();
        melee.effects = target.GetComponent<CombatEffects>();
    }

    private static int Rewire(GameObject root, Dictionary<Object, Object> map, GameObject oldModel)
    {
        int count = 0;
        var dangling = new List<string>();
        foreach (Component component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null || component.transform.IsChildOf(oldModel.transform)) continue;
            var so = new SerializedObject(component);
            SerializedProperty it = so.GetIterator();
            bool changed = false;
            while (it.Next(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
                Object value = it.objectReferenceValue;
                if (map.TryGetValue(value, out Object replacement))
                {
                    it.objectReferenceValue = replacement;
                    changed = true;
                    count++;
                }
                else if (InOldModel(value, oldModel))
                {
                    dangling.Add($"{component.GetType().Name}.{it.propertyPath}");
                }
            }
            if (changed) so.ApplyModifiedPropertiesWithoutUndo();
        }
        if (dangling.Count > 0)
            Debug.LogWarning("[CharacterPrefabCloner] tham chiếu vào model cũ không nối được (sẽ thành null): " + string.Join(", ", dangling.Distinct()));
        return count;
    }

    private static bool InOldModel(Object value, GameObject oldModel)
    {
        Transform t = value is GameObject go ? go.transform : value is Component c ? c.transform : null;
        return t != null && t.IsChildOf(oldModel.transform);
    }
}
