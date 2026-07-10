using System.Collections.Generic;
using UnityEngine;
using Mirror;

/// <summary>
/// 干员/单位对象池管理器，用于放置干员和敌兵生成
/// 支持：1) 客户端放置预览（无 NetworkIdentity） 2) 服务器网络单位（需 UnSpawn 后回收）
/// </summary>
public class UnitPoolManager : MonoBehaviour
{
    public static UnitPoolManager Instance { get; private set; }

    [Header("池配置")]
    public int initialPoolSizePerPrefab = 3;
    public int maxPoolSizePerPrefab = 30;

    /// <summary>预制体 -> 池（非网络对象，如放置预览）</summary>
    private Dictionary<GameObject, Queue<GameObject>> _previewPools = new Dictionary<GameObject, Queue<GameObject>>();

    /// <summary>预制体 -> 池（网络对象，仅服务器）</summary>
    private Dictionary<GameObject, Queue<GameObject>> _networkPools = new Dictionary<GameObject, Queue<GameObject>>();

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

    /// <summary>从池中获取放置预览对象（客户端，无 NetworkIdentity）</summary>
    public GameObject GetPreview(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;
        var pool = GetOrCreatePreviewPool(prefab);
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
        obj.SetActive(true);
        return obj;
    }

    /// <summary>回收放置预览对象</summary>
    public void ReturnPreview(GameObject obj, GameObject prefab)
    {
        if (obj == null || prefab == null) return;
        obj.SetActive(false);
        obj.transform.SetParent(_poolRoot);
        var pool = GetOrCreatePreviewPool(prefab);
        if (pool.Count < maxPoolSizePerPrefab)
            pool.Enqueue(obj);
        else
            Destroy(obj);
    }

    /// <summary>从池中获取网络单位（仅服务器，需 NetworkServer.Spawn）</summary>
    [Server]
    public GameObject GetNetworkUnit(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;
        if (!NetworkServer.active) return Instantiate(prefab, position, rotation);

        var pool = GetOrCreateNetworkPool(prefab);
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
        var origin = obj.GetComponent<PooledUnitOrigin>();
        if (origin == null) origin = obj.AddComponent<PooledUnitOrigin>();
        origin.sourcePrefab = prefab;
        obj.SetActive(true);  // 先激活再 ResetUnitState，否则 NavMeshAgent 会报 "Resume/ResetPath can only be called on an active agent"
        ResetUnitState(obj);
        return obj;
    }

    /// <summary>回收网络单位（仅服务器，先 UnSpawn 再入池）。prefab 可传 null，会从 PooledUnitOrigin 读取</summary>
    [Server]
    public void ReturnNetworkUnit(GameObject obj, GameObject prefab = null)
    {
        if (obj == null) return;
        if (prefab == null)
        {
            var origin = obj.GetComponent<PooledUnitOrigin>();
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
        {
            NetworkServer.UnSpawn(obj);
        }

        obj.SetActive(false);
        obj.transform.SetParent(_poolRoot);

        var pool = GetOrCreateNetworkPool(prefab);
        if (pool.Count < maxPoolSizePerPrefab)
            pool.Enqueue(obj);
        else
            Destroy(obj);
    }

    private Queue<GameObject> GetOrCreatePreviewPool(GameObject prefab)
    {
        if (!_previewPools.TryGetValue(prefab, out var pool))
        {
            pool = new Queue<GameObject>();
            _previewPools[prefab] = pool;
            for (int i = 0; i < initialPoolSizePerPrefab; i++)
            {
                var go = Instantiate(prefab, _poolRoot);
                go.SetActive(false);
                pool.Enqueue(go);
            }
        }
        return pool;
    }

    private Queue<GameObject> GetOrCreateNetworkPool(GameObject prefab)
    {
        if (!_networkPools.TryGetValue(prefab, out var pool))
        {
            pool = new Queue<GameObject>();
            _networkPools[prefab] = pool;
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

    private void ResetUnitState(GameObject obj)
    {
        var sc = obj.GetComponent<SoldierController>();
        if (sc == null) return;
        sc.isAlive = true;
        sc.currentHealth = sc.maxHealth;
        sc.canMove = true;
        sc.canAttack = true;
        sc.isInCombat = false;
        sc.isInSkillState = false;  // 对象池复用：恢复技能状态
        sc.isMoving = false;
        sc.isFollowingPath = false;
        sc.currentMaxTargetCount = sc.baseMaxTargetCount;  // 对象池复用：恢复攻击目标数
        if (sc.currentTargets != null) sc.currentTargets.Clear();
        var rb = obj.GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = false; rb.velocity = Vector3.zero; }
        var nav = obj.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (nav != null && nav.gameObject.activeInHierarchy && nav.isOnNavMesh)
        {
            nav.enabled = true;
            nav.isStopped = false;
            nav.ResetPath();
        }
        else if (nav != null)
        {
            nav.enabled = true;
        }
        var col = obj.GetComponent<Collider>();
        if (col != null) col.enabled = true;
    }

    /// <summary>预 warm 网络池（服务器启动后调用）</summary>
    [Server]
    public void WarmNetworkPools(GameObject[] prefabs)
    {
        if (prefabs == null) return;
        foreach (var p in prefabs)
        {
            if (p == null) continue;
            GetOrCreateNetworkPool(p);
        }
    }
}
