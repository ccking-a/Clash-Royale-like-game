using System.Collections.Generic;
using UnityEngine;
using Mirror;

/// <summary>
/// 技能特效（粒子）对象池管理器。
/// 复用 ParticleSystem 实例，避免频繁 Instantiate/Destroy 造成的 GC 压力和卡顿。
/// 仅服务器使用，特效通过 NetworkServer.Spawn 同步到客户端。
/// </summary>
public class EffectPoolManager : MonoBehaviour
{
    public static EffectPoolManager Instance { get; private set; }

    [Header("池配置")]
    public int initialPoolSizePerPrefab = 5;
    public int maxPoolSizePerPrefab = 30;

    private Dictionary<GameObject, Queue<GameObject>> _effectPools = new Dictionary<GameObject, Queue<GameObject>>();
    private Transform _poolRoot;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _poolRoot = transform;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// 从池中获取网络特效对象（仅服务器）。
    /// 若池为空则新建，否则复用池中对象。
    /// </summary>
    /// <param name="prefab">特效预制体（含 ParticleSystem 和 NetworkIdentity）</param>
    /// <param name="position">世界坐标</param>
    /// <param name="rotation">世界旋转</param>
    /// <returns>可用的特效 GameObject，需自行 Play 后 Spawn</returns>
    [Server]
    public GameObject GetNetworkEffect(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;
        if (!NetworkServer.active) return Instantiate(prefab, position, rotation);

        var pool = GetOrCreatePool(prefab);
        GameObject obj;
        if (pool.Count > 0)
        {
            obj = pool.Dequeue();
            obj.transform.SetParent(null);
            obj.transform.position = position;
            obj.transform.rotation = rotation;
        }
        else
        {
            obj = Instantiate(prefab, position, rotation);
        }

        var origin = obj.GetComponent<PooledEffectOrigin>();
        if (origin == null) origin = obj.AddComponent<PooledEffectOrigin>();
        origin.sourcePrefab = prefab;

        if (obj.GetComponent<PooledParticleAutoReturn>() == null)
            obj.AddComponent<PooledParticleAutoReturn>();

        obj.SetActive(true);
        return obj;
    }

    /// <summary>
    /// 回收网络特效到池（仅服务器）。会先 UnSpawn 再入池。
    /// </summary>
    /// <param name="obj">要回收的特效对象</param>
    /// <param name="prefab">预制体，可传 null 则从 PooledEffectOrigin 读取</param>
    [Server]
    public void ReturnNetworkEffect(GameObject obj, GameObject prefab = null)
    {
        if (obj == null) return;
        if (prefab == null)
        {
            var origin = obj.GetComponent<PooledEffectOrigin>();
            prefab = origin != null ? origin.sourcePrefab : null;
        }
        if (prefab == null)
        {
            Destroy(obj);
            return;
        }
        if (!NetworkServer.active)
        {
            Destroy(obj);
            return;
        }

        var ni = obj.GetComponent<NetworkIdentity>();
        if (ni != null && ni.netId != 0)
            NetworkServer.UnSpawn(obj);

        var ps = obj.GetComponent<ParticleSystem>();
        if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        obj.SetActive(false);
        obj.transform.SetParent(_poolRoot);

        var pool = GetOrCreatePool(prefab);
        if (pool.Count < maxPoolSizePerPrefab)
            pool.Enqueue(obj);
        else
            Destroy(obj);
    }

    private Queue<GameObject> GetOrCreatePool(GameObject prefab)
    {
        if (!_effectPools.TryGetValue(prefab, out var pool))
        {
            pool = new Queue<GameObject>();
            _effectPools[prefab] = pool;
            if (NetworkServer.active)
            {
                for (int i = 0; i < initialPoolSizePerPrefab; i++)
                {
                    var go = Instantiate(prefab, _poolRoot);
                    go.SetActive(false);
                    pool.Enqueue(go);
                }
            }
        }
        return pool;
    }
}
