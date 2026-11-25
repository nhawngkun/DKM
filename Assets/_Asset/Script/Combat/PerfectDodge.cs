using UnityEngine;

/// <summary>
/// Né chuẩn: EnemyAttack gọi Trigger khi đòn chạm đúng lúc người chơi đang trong i-frames.
/// Slow motion qua ComboCameraFeedback + thả bóng mờ (afterimage) bằng cách bake mesh skin theo thời gian thực.
/// </summary>
[RequireComponent(typeof(CharController))]
public class PerfectDodge : MonoBehaviour
{
    private class Ghost
    {
        public GameObject Root;
        public MeshFilter[] Filters;
        public MeshRenderer[] Renderers;
        public float Born = -1f;
    }

    public ComboCameraFeedback cameraFeedback;
    [Tooltip("Không kích hoạt lại trong bấy nhiêu giây thực")]
    public float Cooldown = 0.5f;
    public bool LogTriggers = true;

    [Header("Bóng mờ (afterimage)")]
    [Tooltip("Material trong suốt (HDRP Unlit Transparent, Additive). Trống = không có bóng")]
    public Material GhostMaterial;
    public string GhostColorProperty = "_UnlitColor";
    [ColorUsage(true, true)] public Color GhostColor = new Color(0.3f, 0.8f, 2.5f, 0.6f);
    [Tooltip("Thả bóng trong bấy nhiêu giây thực sau khi né chuẩn")]
    public float EmitDuration = 0.45f;
    [Tooltip("Giây thực giữa 2 bóng")]
    public float EmitInterval = 0.06f;
    [Tooltip("Bóng mờ dần hết trong bấy nhiêu giây thực")]
    public float GhostLifetime = 0.4f;
    public int MaxGhosts = 8;

    private SkinnedMeshRenderer[] _skins;
    private Ghost[] _ghosts;
    private int _cursor;
    private float _lastTrigger = -999f;
    private float _emitUntil = -1f;
    private float _nextEmit;
    private MaterialPropertyBlock _block;
    private int _colorId;

    private void Start()
    {
        if (cameraFeedback == null) cameraFeedback = GetComponentInChildren<ComboCameraFeedback>();
        if (cameraFeedback == null) Debug.LogWarning("[PerfectDodge] thiếu ComboCameraFeedback, né chuẩn sẽ không có slow motion", this);
        if (GhostMaterial == null) Debug.LogWarning("[PerfectDodge] thiếu GhostMaterial, né chuẩn sẽ không có bóng mờ", this);
        _block = new MaterialPropertyBlock();
        _colorId = Shader.PropertyToID(GhostColorProperty);
    }

    private void OnDestroy()
    {
        if (_ghosts == null) return;
        foreach (Ghost ghost in _ghosts)
        {
            foreach (MeshFilter filter in ghost.Filters) Destroy(filter.sharedMesh);
            Destroy(ghost.Root);
        }
    }

    public void Trigger()
    {
        if (Time.unscaledTime - _lastTrigger < Cooldown) return;
        _lastTrigger = Time.unscaledTime;
        if (cameraFeedback != null) cameraFeedback.PlayPerfectDodge();
        _emitUntil = Time.unscaledTime + EmitDuration;
        _nextEmit = Time.unscaledTime;
        if (LogTriggers) Debug.Log("[PerfectDodge] né chuẩn", this);
    }

    private void LateUpdate()
    {
        if (GhostMaterial == null) return;
        float now = Time.unscaledTime;
        if (now < _emitUntil && now >= _nextEmit)
        {
            _nextEmit = now + EmitInterval;
            Emit();
        }
        if (_ghosts == null) return;
        foreach (Ghost ghost in _ghosts) Fade(ghost, now);
    }

    private void Emit()
    {
        if (_ghosts == null) Build();
        Ghost ghost = _ghosts[_cursor];
        _cursor = (_cursor + 1) % _ghosts.Length;
        for (int i = 0; i < _skins.Length; i++)
        {
            SkinnedMeshRenderer skin = _skins[i];
            bool visible = skin != null && skin.enabled && skin.gameObject.activeInHierarchy;
            ghost.Renderers[i].enabled = visible;
            if (!visible) continue;
            skin.BakeMesh(ghost.Filters[i].sharedMesh, true);
            ghost.Filters[i].transform.SetPositionAndRotation(skin.transform.position, skin.transform.rotation);
        }
        ghost.Born = Time.unscaledTime;
        ghost.Root.SetActive(true);
    }

    private void Fade(Ghost ghost, float now)
    {
        if (ghost.Born < 0f) return;
        float life = 1f - (now - ghost.Born) / Mathf.Max(GhostLifetime, 0.01f);
        if (life <= 0f)
        {
            ghost.Born = -1f;
            ghost.Root.SetActive(false);
            return;
        }
        Color color = GhostColor;
        color.a *= life;
        _block.SetColor(_colorId, color);
        foreach (MeshRenderer renderer in ghost.Renderers) renderer.SetPropertyBlock(_block);
    }

    private void Build()
    {
        _skins = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        _ghosts = new Ghost[Mathf.Max(1, MaxGhosts)];
        for (int g = 0; g < _ghosts.Length; g++)
        {
            // không làm con của nhân vật: bóng phải đứng lại tại chỗ
            var ghost = new Ghost
            {
                Root = new GameObject($"{name}_Ghost{g}"),
                Filters = new MeshFilter[_skins.Length],
                Renderers = new MeshRenderer[_skins.Length],
            };
            for (int i = 0; i < _skins.Length; i++)
            {
                var part = new GameObject(_skins[i].name);
                part.transform.SetParent(ghost.Root.transform, false);
                ghost.Filters[i] = part.AddComponent<MeshFilter>();
                ghost.Filters[i].sharedMesh = new Mesh { name = part.name };
                ghost.Renderers[i] = part.AddComponent<MeshRenderer>();
                ghost.Renderers[i].sharedMaterial = GhostMaterial;
                ghost.Renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            ghost.Root.SetActive(false);
            _ghosts[g] = ghost;
        }
    }
}
