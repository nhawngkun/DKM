using NabaGame.Core.Runtime.TickManager;
using UnityEngine;

public class CameraForward : TickableBehaviour
{
    private Transform _camTransform;
    private Quaternion _lastRot;

    public void Inject(Camera cam)
    {
        _camTransform = cam != null ? cam.transform : null;
    }

    private void Start()
    {
        if (_camTransform == null)
        {
            var cam = Camera.main; // fallback 1 lần duy nhất
            if (cam != null)
                _camTransform = cam.transform;
        }

        if (_camTransform != null)
            _lastRot = _camTransform.rotation;
    }

    private Vector3 _lastCamPos;

    public override void OnTickableUpdated(float dt)
    {
        if (_camTransform == null) return;

        if (_camTransform.rotation == _lastRot &&
            _camTransform.position == _lastCamPos)
            return;

        _lastRot = _camTransform.rotation;
        _lastCamPos = _camTransform.position;

        Vector3 dir = transform.position - _camTransform.position;
        transform.forward = dir.normalized;
    }
}