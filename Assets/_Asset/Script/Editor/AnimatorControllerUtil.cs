using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Dùng chung cho LocomotionAnimatorBuilder và WeaponComboBuilder khi dựng lại layer trong controller riêng của từng nhân vật
public static class AnimatorControllerUtil
{
    public const string IKLayerName = "IK";
    public const string Folder = "Assets/_Asset/Animation/Player";

    // Controller mới cho bộ chưa có: Animation/Player/<Tên>.controller, bỏ Base Layer rỗng mặc định
    public static AnimatorController Create(string name)
    {
        string path = AssetDatabase.GenerateUniqueAssetPath($"{Folder}/{name}.controller");
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        foreach (AnimatorControllerLayer layer in controller.layers) Object.DestroyImmediate(layer.stateMachine, true);
        controller.layers = new AnimatorControllerLayer[0];
        Debug.Log("[AnimatorControllerUtil] Tạo controller " + path);
        return controller;
    }

    // Layer IK: 1 state rỗng, PlayerIK tìm layer theo tên này
    public static void EnsureIKLayer(AnimatorController controller)
    {
        if (controller.layers.Any(layer => layer.name == IKLayerName)) return;
        AnimatorStateMachine machine = new AnimatorStateMachine
        {
            name = IKLayerName,
            hideFlags = HideFlags.HideInHierarchy,
        };
        AssetDatabase.AddObjectToAsset(machine, controller);
        machine.defaultState = machine.AddState("Empty", new Vector3(240f, 260f, 0f));

        List<AnimatorControllerLayer> layers = controller.layers.ToList();
        layers.Add(new AnimatorControllerLayer
        {
            name = IKLayerName,
            stateMachine = machine,
            defaultWeight = 1f,
            blendingMode = AnimatorLayerBlendingMode.Override,
        });
        controller.layers = layers.ToArray();
    }

    // Xoá state / blend tree / transition không còn thuộc layer nào: rác của các lần build trước,
    // hoặc của controller nhân bản từ nhân vật khác
    public static int RemoveOrphans(AnimatorController controller)
    {
        HashSet<Object> used = new HashSet<Object>();
        foreach (AnimatorControllerLayer layer in controller.layers) Collect(layer.stateMachine, used);

        int removed = 0;
        foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(controller)))
        {
            if (sub == null || sub == controller || used.Contains(sub)) continue;
            if (!(sub is AnimatorState || sub is AnimatorStateMachine || sub is BlendTree || sub is AnimatorTransitionBase || sub is StateMachineBehaviour)) continue;
            Object.DestroyImmediate(sub, true);
            removed++;
        }
        if (removed > 0) EditorUtility.SetDirty(controller);
        return removed;
    }

    private static void Collect(AnimatorStateMachine machine, HashSet<Object> used)
    {
        if (machine == null || !used.Add(machine)) return;
        foreach (StateMachineBehaviour behaviour in machine.behaviours) used.Add(behaviour);
        foreach (AnimatorStateTransition transition in machine.anyStateTransitions) used.Add(transition);
        foreach (AnimatorTransition transition in machine.entryTransitions) used.Add(transition);
        foreach (ChildAnimatorStateMachine child in machine.stateMachines)
        {
            Collect(child.stateMachine, used);
            foreach (AnimatorTransition transition in machine.GetStateMachineTransitions(child.stateMachine)) used.Add(transition);
        }
        foreach (ChildAnimatorState child in machine.states)
        {
            AnimatorState state = child.state;
            if (state == null) continue;
            used.Add(state);
            foreach (StateMachineBehaviour behaviour in state.behaviours) used.Add(behaviour);
            foreach (AnimatorStateTransition transition in state.transitions) used.Add(transition);
            CollectMotion(state.motion, used);
        }
    }

    private static void CollectMotion(Motion motion, HashSet<Object> used)
    {
        if (!(motion is BlendTree tree) || !used.Add(tree)) return;
        foreach (ChildMotion child in tree.children) CollectMotion(child.motion, used);
    }
}
