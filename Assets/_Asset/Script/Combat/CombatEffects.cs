using System.Collections.Generic;
using UnityEngine;

public enum SwingKind
{
    Slash,
    Heavy,
    Thrust,
}

/// <summary>
/// Hiệu ứng + âm thanh của đòn đánh, pool sẵn lúc vào game. Không quyết định khi nào phát:
/// MeleeHitDetector gọi đúng frame kiếm vung / chạm.
/// </summary>
public class CombatEffects : MonoBehaviour
{
    private class Pool
    {
        private readonly List<ParticleSystem> _items = new List<ParticleSystem>();
        private int _cursor;

        public void Prewarm(ParticleSystem prefab, int count)
        {
            if (prefab == null) return;
            for (int i = 0; i < count; i++)
            {
                // không làm con của nhân vật: hiệu ứng phải đứng yên tại chỗ chém
                ParticleSystem instance = Instantiate(prefab);
                instance.gameObject.SetActive(true);
                _items.Add(instance);
            }
        }

        public void Play(Vector3 position, Quaternion rotation, float scale)
        {
            if (_items.Count == 0) return;
            ParticleSystem effect = _items[_cursor];
            _cursor = (_cursor + 1) % _items.Count;

            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Transform t = effect.transform;
            t.SetPositionAndRotation(position, rotation);
            t.localScale = Vector3.one * scale;
            effect.Play(true);
        }
    }

    [Header("VFX chém (prefab: Scaling Mode = Hierarchy, Stop Action = None)")]
    public ParticleSystem SlashPrefab;
    public ParticleSystem HeavySlashPrefab;
    public ParticleSystem ThrustPrefab;
    public int SlashPoolSize = 4;

    [Header("VFX va chạm")]
    public ParticleSystem HitSparkPrefab;
    public ParticleSystem SlamPrefab;
    public int HitPoolSize = 6;
    public int SlamPoolSize = 2;
    [Tooltip("Scale tia lửa ở đòn yếu nhất")]
    public float MinHitScale = 0.75f;
    [Tooltip("Scale tia lửa ở đòn mạnh nhất")]
    public float MaxHitScale = 1.5f;

    [Header("Âm thanh")]
    public AudioSource Source;
    public AudioClip[] SwingClips;
    public AudioClip[] HeavySwingClips;
    public AudioClip[] ThrustClips;
    public AudioClip[] HitClips;
    public AudioClip[] HeavyHitClips;
    public AudioClip[] SlamClips;
    public AudioClip[] KickSwingClips;
    public AudioClip[] KickHitClips;
    [Range(0f, 1f)] public float SwingVolume = 0.6f;
    [Range(0f, 1f)] public float HitVolume = 0.9f;
    [Tooltip("Lệch cao độ ngẫu nhiên để các nhát không giống hệt nhau")]
    public float PitchVariation = 0.08f;
    [Range(0f, 1f), Tooltip("Từ độ mạnh này trở lên dùng hiệu ứng và tiếng của đòn nặng")]
    public float HeavyThreshold = 0.6f;

    private readonly Pool _slash = new Pool();
    private readonly Pool _heavySlash = new Pool();
    private readonly Pool _thrust = new Pool();
    private readonly Pool _hit = new Pool();
    private readonly Pool _slam = new Pool();

    private void Start()
    {
        _slash.Prewarm(SlashPrefab, SlashPoolSize);
        _heavySlash.Prewarm(HeavySlashPrefab, SlashPoolSize);
        _thrust.Prewarm(ThrustPrefab, SlashPoolSize);
        _hit.Prewarm(HitSparkPrefab, HitPoolSize);
        _slam.Prewarm(SlamPrefab, SlamPoolSize);
    }

    public SwingKind KindFor(float strength, bool thrust)
    {
        if (thrust) return SwingKind.Thrust;
        return strength >= HeavyThreshold ? SwingKind.Heavy : SwingKind.Slash;
    }

    public void PlaySwingSound(SwingKind kind)
    {
        switch (kind)
        {
            case SwingKind.Slash: PlayClip(SwingClips, SwingVolume); break;
            case SwingKind.Heavy: PlayClip(HeavySwingClips, SwingVolume); break;
            case SwingKind.Thrust: PlayClip(ThrustClips, SwingVolume); break;
            default: throw new System.ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    public void PlaySwingEffect(SwingKind kind, Vector3 position, Quaternion rotation, float scale)
    {
        switch (kind)
        {
            case SwingKind.Slash: _slash.Play(position, rotation, scale); break;
            case SwingKind.Heavy: _heavySlash.Play(position, rotation, scale); break;
            case SwingKind.Thrust: _thrust.Play(position, rotation, scale); break;
            default: throw new System.ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    public void PlayHit(Vector3 point, Vector3 normal, float strength)
    {
        PlayHitEffect(point, normal, strength);
        PlayClip(strength >= HeavyThreshold ? HeavyHitClips : HitClips, HitVolume);
    }

    public void PlayKickSwing()
    {
        PlayClip(KickSwingClips, SwingVolume);
    }

    public void PlayKickHit(Vector3 point, Vector3 normal, float strength)
    {
        PlayHitEffect(point, normal, strength);
        PlayClip(KickHitClips, HitVolume);
    }

    private void PlayHitEffect(Vector3 point, Vector3 normal, float strength)
    {
        Quaternion rotation = Quaternion.LookRotation(normal.sqrMagnitude > 0.0001f ? normal : Vector3.up);
        _hit.Play(point, rotation, Mathf.Lerp(MinHitScale, MaxHitScale, strength));
    }

    public void PlaySlam(Vector3 groundPoint)
    {
        _slam.Play(groundPoint, Quaternion.LookRotation(Vector3.up), 1f);
        PlayClip(SlamClips, 1f);
    }

    private void PlayClip(AudioClip[] clips, float volume)
    {
        if (Source == null || clips == null || clips.Length == 0) return;
        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null) return;
        Source.pitch = 1f + Random.Range(-PitchVariation, PitchVariation);
        Source.PlayOneShot(clip, volume);
    }
}
