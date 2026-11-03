using UnityEngine;

// Rig đèn riêng cho nhân vật (key + rim), xoay theo hướng camera để rim luôn ở sau lưng nhân vật
[ExecuteAlways]
public class CharacterLightRig : MonoBehaviour
{
    [SerializeField] private Transform cameraTransform;

    private void LateUpdate()
    {
        if (cameraTransform == null)
        {
            if (Camera.main == null) return;
            cameraTransform = Camera.main.transform;
        }

        transform.rotation = Quaternion.Euler(0f, cameraTransform.eulerAngles.y, 0f);
    }
}
