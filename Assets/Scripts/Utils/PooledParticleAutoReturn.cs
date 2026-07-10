using UnityEngine;
using Mirror;

/// <summary>
/// 挂载在池化粒子特效上，播放结束后自动回收到 EffectPoolManager。
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
[RequireComponent(typeof(PooledEffectOrigin))]
public class PooledParticleAutoReturn : MonoBehaviour
{
    private ParticleSystem _ps;
    private float _returnDelay;

    void Awake()
    {
        _ps = GetComponent<ParticleSystem>();
    }

    void OnEnable()
    {
        if (_ps == null) _ps = GetComponent<ParticleSystem>();
        if (_ps == null) return;

        var main = _ps.main;
        float duration = main.duration;
        float lifetime = main.startLifetime.constantMax;
        _returnDelay = Mathf.Max(0.1f, duration + lifetime + 0.5f);

        if (NetworkServer.active && EffectPoolManager.Instance != null)
            Invoke(nameof(ReturnToPool), _returnDelay);
    }

    void OnDisable()
    {
        CancelInvoke(nameof(ReturnToPool));
    }

    private void ReturnToPool()
    {
        if (!NetworkServer.active || EffectPoolManager.Instance == null) return;
        EffectPoolManager.Instance.ReturnNetworkEffect(gameObject, null);
    }
}
