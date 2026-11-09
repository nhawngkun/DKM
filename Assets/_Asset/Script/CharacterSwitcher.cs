using System.Collections.Generic;
using KinematicCharacterController.Examples;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Đổi nhân vật lúc chơi để test nhanh: phím 1..9 chọn thẳng, nút Select/View của tay cầm = nhân vật kế.
/// Gắn cạnh ExamplePlayer. Mỗi phần tử Characters là 1 prefab nhân vật đầy đủ (gốc có CharController,
/// vd Character.prefab / Vergil Variant.prefab). Đổi = sinh prefab mới đúng chỗ + hướng của nhân vật cũ,
/// nối lại input + camera rồi tắt nhân vật cũ (giữ lại để quay về không mất chỉnh sửa trong scene).
/// </summary>
public class CharacterSwitcher : MonoBehaviour
{
    public ExamplePlayer Player;
    public List<GameObject> Characters = new List<GameObject>();
    public bool ShowHint = true;

    private int _current = -1;
    private readonly Dictionary<int, CharController> _instances = new Dictionary<int, CharController>();
    private GUIStyle _hintStyle;

    private void Awake()
    {
        if (Player == null) Player = GetComponent<ExamplePlayer>();
    }

    // nhân vật đặt sẵn trong scene: đoán theo tên để phím của nó sáng sẵn và không sinh lại vô ích
    private void Start()
    {
        if (Player == null || Player.Character == null) return;
        string name = Player.Character.name;
        _current = Characters.FindIndex(prefab => prefab != null && name.StartsWith(prefab.name));
    }

    private void Update()
    {
        if (Player == null || Characters.Count == 0) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            for (int i = 0; i < Characters.Count && i < 9; i++)
            {
                if (keyboard[Key.Digit1 + i].wasPressedThisFrame) SwitchTo(i);
            }
        }

        Gamepad gamepad = Gamepad.current;
        if (gamepad != null && gamepad.selectButton.wasPressedThisFrame) SwitchTo((_current + 1) % Characters.Count);
    }

    public void SwitchTo(int index)
    {
        if (index < 0 || index >= Characters.Count || index == _current || Characters[index] == null) return;
        CharController old = Player.Character;
        if (old == null) return;

        // giữ hướng camera đang nhìn: SetFollowTransform reset hướng theo lưng nhân vật
        CameraController camera = Player.CharacterCamera;
        Vector3 cameraDirection = camera.Focus.PlanarDirection;
        Transform oldRoot = old.transform;
        Vector3 position = oldRoot.position;
        Quaternion rotation = oldRoot.rotation;

        // tắt chứ không xoá: nhân vật đặt sẵn trong scene giữ nguyên chỉnh sửa của scene (light layer, cloth...) khi quay lại
        if (_current >= 0) _instances[_current] = old;
        // bật lại thì animator về state mặc định của bộ đầu (Dante) -> giữ nguyên state đang phát
        foreach (Animator animator in old.GetComponentsInChildren<Animator>(true)) animator.keepAnimatorStateOnDisable = true;
        old.gameObject.SetActive(false);

        if (!_instances.TryGetValue(index, out CharController next) || next == null)
        {
            GameObject spawned = Instantiate(Characters[index], position, rotation, oldRoot.parent);
            spawned.name = Characters[index].name;
            next = spawned.GetComponent<CharController>();
            if (next == null)
            {
                Debug.LogError($"[CharacterSwitcher] {Characters[index].name} không có CharController ở gốc — phải là prefab nhân vật đầy đủ (variant của Character.prefab).");
                Destroy(spawned);
                old.gameObject.SetActive(true);
                return;
            }
            _instances[index] = next;
        }
        else
        {
            next.transform.SetPositionAndRotation(position, rotation);
            next.gameObject.SetActive(true);
        }
        next.Motor.SetPositionAndRotation(position, rotation);
        next.Motor.BaseVelocity = Vector3.zero;

        foreach (ComboCameraFeedback feedback in next.GetComponentsInChildren<ComboCameraFeedback>(true))
        {
            feedback.cameraController = camera;
        }

        Player.Character = next;
        camera.SetFollowTransform(next.CameraFollowPoint);
        camera.Focus.ResetPlanarDirection(cameraDirection);
        camera.ClearLockOnTarget();
        camera.Follow.IgnoredColliders.Clear();
        camera.Follow.IgnoredColliders.AddRange(next.GetComponentsInChildren<Collider>());

        // rig đèn key/rim gắn trong scene dưới nhân vật đang chơi -> đi theo nhân vật mới
        foreach (CharacterLightRig rig in old.GetComponentsInChildren<CharacterLightRig>(true))
        {
            rig.transform.SetParent(next.transform, false);
        }

        _current = index;
    }

    private void OnGUI()
    {
        if (!ShowHint || Characters.Count < 2) return;
        if (_hintStyle == null) _hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };

        string text = "<b>Đổi nhân vật</b>  ";
        for (int i = 0; i < Characters.Count && i < 9; i++)
        {
            if (Characters[i] == null) continue;
            string label = $"[{i + 1}] {Characters[i].name}";
            text += (i == _current ? $"<color=#4C9AFF><b>{label}</b></color>" : label) + "   ";
        }
        // script khác (FPS counter) có thể để lại GUI.color đỏ -> đặt lại trước khi vẽ
        Color previous = GUI.color;
        GUI.color = Color.white;
        _hintStyle.normal.textColor = Color.white;
        Vector2 size = _hintStyle.CalcSize(new GUIContent(text));
        Rect rect = new Rect(12f, Screen.height - 40f, size.x + 20f, 28f);
        GUI.Box(rect, GUIContent.none);
        GUI.Label(new Rect(rect.x + 10f, rect.y + 4f, size.x, 22f), text, _hintStyle);
        GUI.color = previous;
    }
}
