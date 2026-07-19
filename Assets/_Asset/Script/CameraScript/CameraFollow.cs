using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Module ĐỘC LẬP: chỉ tính toán VỊ TRÍ camera (follow lag, zoom, obstruction, safe zone).
/// KHÔNG tham chiếu tới CameraFocusComponent hay CameraEffectsComponent.
/// Nhận rotation + airBlend làm THAM SỐ từ CameraController (đã lấy từ Focus ở frame này).
/// Tự ghi Transform.position của chính GameObject nó gắn vào.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("References")]
    public Camera Camera;

    [Header("Framing")]
    public Vector2 FollowPointFraming = new Vector2(0f, 0f);
    public float FollowingSharpnessPlanar = 8f;
    public float FollowingSharpnessVertical = 15f;
    public float AirFollowingSharpnessVertical = 30f;

    [Header("Distance / Zoom")]
    public float DefaultDistance = 6f;
    public float MinDistance = 0f;
    public float MaxDistance = 10f;
    public float DistanceMovementSpeed = 5f;
    public float DistanceMovementSharpness = 10f;
    public float CombatZoomOutMultiplier = 1.4f;
    [Range(0f, 1f)] public float CombatBlend = 0f;
    public float AirZoomOutMultiplier = 1.2f;

    [Header("Obstruction")]
    public float ObstructionCheckRadius = 0.2f;
    public LayerMask ObstructionLayers = -1;
    public float ObstructionSharpness = 10000f;
    public List<Collider> IgnoredColliders = new List<Collider>();

    [Header("Safe Zone")]
    public bool EnableSafeZone = true;
    [Range(0f, 0.5f)] public float SafeZoneMarginTop = 0.22f;
    [Range(0f, 0.5f)] public float SafeZoneMarginBottom = 0.18f;
    [Range(0f, 0.5f)] public float SafeZoneMarginLeft = 0.22f;
    [Range(0f, 0.5f)] public float SafeZoneMarginRight = 0.22f;
    public float SafeZoneCorrectionGain = 4f;
    public float SafeZoneMaxOffset = 3f;
    public float SafeZoneCorrectionSharpness = 8f;

    [Header("Debug - Safe Zone Overlay")]
    public bool DrawSafeZoneOnGameView = true;
    public Color SafeZoneColor = new Color(0.06f, 0.8f, 0.55f, 1f);
    public Color OutOfZoneColor = new Color(0.9f, 0.35f, 0.2f, 1f);

    public float TargetDistance { get; set; }

    private bool _distanceIsObstructed;
    private float _currentDistance;
    private int _obstructionCount;
    private RaycastHit[] _obstructions = new RaycastHit[MaxObstructions];
    private Vector3 _currentFollowPosition;
    private float _currentSafeZoneOffsetX;
    private float _currentSafeZoneOffsetY;


    private const int MaxObstructions = 32;

    void Awake()
    {
        _currentDistance = DefaultDistance;
        TargetDistance = _currentDistance;
        if (Camera == null) Camera = GetComponentInChildren<Camera>();
    }

    void OnValidate()
    {
        DefaultDistance = Mathf.Clamp(DefaultDistance, MinDistance, MaxDistance);
    }

    /// <summary>Gọi 1 lần khi bắt đầu follow 1 nhân vật mới, tránh camera bị "bay" từ vị trí cũ tới.</summary>
    public void SnapTo(Vector3 followWorldPosition)
    {
        _currentFollowPosition = followWorldPosition;
    }

    /// <summary>
    /// Tính vị trí camera mới. Không đọc bất kỳ component nào khác — mọi input đều qua tham số.
    /// </summary>
    /// <param name="rotation">Rotation hiện tại, lấy từ CameraFocusComponent.CurrentRotation qua CameraController</param>
    /// <param name="airBlend">0..1, lấy từ CameraFocusComponent.AirBlend qua CameraController</param>
    /// <param name="hasLockOn">Có đang lock-on hay không (dùng cho combat zoom-out)</param>
    /// <param name="followWorldPosition">Vị trí thế giới của nhân vật đang follow</param>
    public void UpdateFollow(float deltaTime, float zoomInput, Quaternion rotation, float airBlend,
        bool hasLockOn, Vector3 followWorldPosition, Vector3 followUp)
    {


        // ---------------- DISTANCE / ZOOM ----------------
        if (_distanceIsObstructed && Mathf.Abs(zoomInput) > 0f)
        {
            TargetDistance = _currentDistance;
        }
        if (Mathf.Abs(zoomInput) > 0f)
        {
            TargetDistance += zoomInput * DistanceMovementSpeed * deltaTime;
            TargetDistance = Mathf.Clamp(TargetDistance, MinDistance, MaxDistance);
        }

        float effectiveCombatBlend = hasLockOn ? Mathf.Max(CombatBlend, 0.5f) : CombatBlend;
        float combatZoomMultiplier = Mathf.Lerp(1f, CombatZoomOutMultiplier, effectiveCombatBlend);
        float airZoomMultiplier = Mathf.Lerp(1f, AirZoomOutMultiplier, airBlend);

        float dynamicTargetDistance = TargetDistance * combatZoomMultiplier * airZoomMultiplier;
        dynamicTargetDistance = Mathf.Clamp(dynamicTargetDistance, MinDistance, MaxDistance);

        // ---------------- FOLLOW POSITION ----------------
        Vector3 toTarget = followWorldPosition - _currentFollowPosition;
        Vector3 planarDelta = Vector3.ProjectOnPlane(toTarget, followUp);
        Vector3 vertDelta = toTarget - planarDelta;

        float effectiveVerticalSharpness = Mathf.Lerp(FollowingSharpnessVertical, AirFollowingSharpnessVertical, airBlend);
        float planarLerp = 1f - Mathf.Exp(-FollowingSharpnessPlanar * deltaTime);
        float vertLerp = 1f - Mathf.Exp(-effectiveVerticalSharpness * deltaTime);

        _currentFollowPosition += planarDelta * planarLerp + vertDelta * vertLerp;

        // ---------------- OBSTRUCTION ----------------
        RaycastHit closestHit = new RaycastHit();
        closestHit.distance = Mathf.Infinity;
        _obstructionCount = Physics.SphereCastNonAlloc(
            _currentFollowPosition, ObstructionCheckRadius, -(rotation * Vector3.forward),
            _obstructions, dynamicTargetDistance, ObstructionLayers, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < _obstructionCount; i++)
        {
            bool isIgnored = false;
            for (int j = 0; j < IgnoredColliders.Count; j++)
            {
                if (IgnoredColliders[j] == _obstructions[i].collider) { isIgnored = true; break; }
            }
            if (!isIgnored && _obstructions[i].distance < closestHit.distance && _obstructions[i].distance > 0)
            {
                closestHit = _obstructions[i];
            }
        }

        if (closestHit.distance < Mathf.Infinity)
        {
            _distanceIsObstructed = true;
            _currentDistance = Mathf.Lerp(_currentDistance, closestHit.distance, 1f - Mathf.Exp(-ObstructionSharpness * deltaTime));
        }
        else
        {
            _distanceIsObstructed = false;
            _currentDistance = Mathf.Lerp(_currentDistance, dynamicTargetDistance, 1f - Mathf.Exp(-DistanceMovementSharpness * deltaTime));
        }

        // ---------------- FINAL POSITION + FRAMING + SAFE ZONE ----------------
        Vector3 finalPosition = _currentFollowPosition - (rotation * Vector3.forward * _currentDistance);
        finalPosition += (rotation * Vector3.right) * (FollowPointFraming.x + _currentSafeZoneOffsetX);
        finalPosition += (rotation * Vector3.up) * (FollowPointFraming.y + _currentSafeZoneOffsetY);

        transform.SetPositionAndRotation(finalPosition, rotation);

        // ---------------- SAFE ZONE CHECK (offset cho frame kế tiếp) ----------------
        if (EnableSafeZone && Camera != null)
        {
            Vector3 viewportPos = Camera.WorldToViewportPoint(followWorldPosition);
            float targetOffsetX = _currentSafeZoneOffsetX;
            float targetOffsetY = _currentSafeZoneOffsetY;

            if (viewportPos.z > 0.01f)
            {
                float topBound = 1f - SafeZoneMarginTop;
                float bottomBound = SafeZoneMarginBottom;
                float rightBound = 1f - SafeZoneMarginRight;
                float leftBound = SafeZoneMarginLeft;

                float errorY = 0f;
                if (viewportPos.y > topBound) errorY = viewportPos.y - topBound;
                else if (viewportPos.y < bottomBound) errorY = viewportPos.y - bottomBound;

                float errorX = 0f;
                if (viewportPos.x > rightBound) errorX = viewportPos.x - rightBound;
                else if (viewportPos.x < leftBound) errorX = viewportPos.x - leftBound;

                targetOffsetY = Mathf.Clamp(_currentSafeZoneOffsetY + errorY * SafeZoneCorrectionGain, -SafeZoneMaxOffset, SafeZoneMaxOffset);
                targetOffsetX = Mathf.Clamp(_currentSafeZoneOffsetX + errorX * SafeZoneCorrectionGain, -SafeZoneMaxOffset, SafeZoneMaxOffset);
            }
            else
            {
                targetOffsetX = 0f;
                targetOffsetY = 0f;
            }

            _currentSafeZoneOffsetX = Mathf.Lerp(_currentSafeZoneOffsetX, targetOffsetX, 1f - Mathf.Exp(-SafeZoneCorrectionSharpness * deltaTime));
            _currentSafeZoneOffsetY = Mathf.Lerp(_currentSafeZoneOffsetY, targetOffsetY, 1f - Mathf.Exp(-SafeZoneCorrectionSharpness * deltaTime));
        }
    }


}