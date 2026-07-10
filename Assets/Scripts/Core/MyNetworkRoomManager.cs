using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class MyNetworkRoomManager : NetworkRoomManager
{
    [Header("自定义设置")]
    [Tooltip("NetworkGameState 预制体（需有NetworkIdentity），进入游戏场景时生成")]
    public GameObject networkGameStatePrefab;
    private static MyNetworkRoomManager _instance;
    //public override void Awake()
    //{
    //    base.Awake();
    //    if (networkGameStatePrefab != null
    //        && networkGameStatePrefab.GetComponent<NetworkIdentity>() != null
    //        && !spawnPrefabs.Contains(networkGameStatePrefab))
    //    {
    //        spawnPrefabs.Add(networkGameStatePrefab);
    //    }

    //    // Awake 是最早执行的，确保在场景加载前就处理单例
    //        if (_instance != null && _instance != this)
    //        {
    //            // 如果已经存在一个实例，销毁新创建的
    //            Debug.LogWarning("检测到重复的 NetworkManager，正在销毁新实例。");
    //            Destroy(gameObject);
    //            return;
    //        }

    //        _instance = this;
    //        // 设置为不销毁，这样在场景切换时它就能一直存在
    //        DontDestroyOnLoad(gameObject);
        
    //}

    public override void OnServerSceneChanged(string sceneName)
    {
        if (sceneName == GameplayScene && pendingPlayers.Count == 0)
        {
            foreach (NetworkRoomPlayer rp in roomSlots)
            {
                if (rp == null) continue;
                var identity = rp.GetComponent<NetworkIdentity>();
                if (identity != null && identity.connectionToClient != null)
                {
                    pendingPlayers.Add(new PendingPlayer { conn = identity.connectionToClient, roomPlayer = rp.gameObject });
                }
            }
        }
        base.OnServerSceneChanged(sceneName);
    }

    public override void OnRoomServerSceneChanged(string sceneName)
    {
        base.OnRoomServerSceneChanged(sceneName);

        if (sceneName == GameplayScene)
        {
            Debug.Log("[MyNetworkRoomManager] ★ 进入游戏场景");

            if (networkGameStatePrefab != null && NetworkGameState.Instance == null)
            {
                var go = Instantiate(networkGameStatePrefab);
                NetworkServer.Spawn(go);
                Debug.Log("[MyNetworkRoomManager] NetworkGameState 已生成");
            }

            StartCoroutine(CleanupRoomPlayersRepeated());
        }
    }

    private System.Collections.IEnumerator CleanupRoomPlayersRepeated()
    {
        // 立即执行一次，然后每隔 0.5s 再执行几次（应对 observers 恢复的时序）
        CleanupRoomPlayersNow();
        for (int i = 0; i < 5; i++)
        {
            yield return new WaitForSeconds(0.5f);
            CleanupRoomPlayersNow();
        }
    }

    private void CleanupRoomPlayersNow()
    {
        // Mirror 的 ReplacePlayerForConnection(KeepAuthority) 不会销毁旧 RoomPlayer，
        // 必须显式清理，否则客户端 DontDestroyOnLoad 中会残留 RoomPlayer
        var toDestroy = new HashSet<GameObject>();

        // 方式1：从 roomSlots 直接获取并销毁（最可靠）
        foreach (NetworkRoomPlayer rp in roomSlots)
        {
            if (rp == null) continue;
            var go = rp.gameObject;
            if (go == null) continue;
            var ni = go.GetComponent<NetworkIdentity>();
            if (ni != null && NetworkServer.spawned.ContainsKey(ni.netId))
            {
                toDestroy.Add(go);
            }
        }

        // 方式2：兜底，遍历 spawned 中所有带 NetworkRoomPlayer 且无 PlayerElixir 的对象
        foreach (var kv in NetworkServer.spawned)
        {
            var rp = kv.Value.GetComponent<NetworkRoomPlayer>();
            if (rp != null && kv.Value.GetComponent<PlayerElixir>() == null)
            {
                toDestroy.Add(kv.Value.gameObject);
            }
        }

        foreach (var go in toDestroy)
        {
            var ni = go.GetComponent<NetworkIdentity>();
            if (ni != null)
            {
                // 场景切换时 SetAllClientsNotReady 会清空 observers，导致 NetworkServer.Destroy 的
                // SendToObservers 不发送（observers.Count==0），客户端收不到销毁消息。
                // 先手动向所有连接发送 ObjectDestroyMessage，确保客户端能销毁。
                var msg = new ObjectDestroyMessage { netId = ni.netId };
                foreach (var conn in NetworkServer.connections.Values)
                {
                    conn.Send(msg);
                }
            }
            NetworkServer.Destroy(go);
            Debug.Log($"[MyNetworkRoomManager] 清理 RoomPlayer: {go.name}");
        }
    }

    // playerIndex 不再由 RoomManager 分配，
    // 改为 PlayerElixir.OnStartServer 中直接判断 connectionToClient == NetworkServer.localConnection

    // ───────── RoomPlayer 进入房间（客户端权威） ─────────
    // RoomPlayer 由客户端权威，客户端可通过轮盘等方式移动，与 Player 预制体无关联
    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        if (!Utils.IsSceneActive(RoomScene))
        {
            Debug.Log($"[MyNetworkRoomManager] 非房间场景，断开迟到玩家 {conn.connectionId}");
            conn.Disconnect();
            return;
        }

        clientIndex++;
        allPlayersReady = false;

        GameObject newRoomGameObject = OnRoomServerCreateRoomPlayer(conn);
        if (newRoomGameObject == null && roomPlayerPrefab != null)
            newRoomGameObject = Instantiate(roomPlayerPrefab.gameObject, Vector3.zero, Quaternion.identity);

        if (newRoomGameObject != null)
        {
            // AddPlayerForConnection 将 RoomPlayer 设为该连接的玩家对象，并赋予客户端权威
            NetworkServer.AddPlayerForConnection(conn, newRoomGameObject);
            Debug.Log($"[MyNetworkRoomManager] 客户端 {conn.connectionId} 加入房间，RoomPlayer 已赋予客户端权威");
        }
    }

    // ───────── 连接/断开日志 ─────────

    public override void OnRoomServerConnect(NetworkConnectionToClient conn)
    {
        base.OnRoomServerConnect(conn);
        Debug.Log($"[MyNetworkRoomManager] 客户端 {conn.connectionId} 连接到服务器");
    }

    public override void OnRoomServerDisconnect(NetworkConnectionToClient conn)
    {
        base.OnRoomServerDisconnect(conn);
        Debug.Log($"[MyNetworkRoomManager] 客户端 {conn.connectionId} 断开连接");
    }

    // ───────── 游戏结束 ─────────

    /// <summary>服务器通知所有客户端游戏结束</summary>
    public void NotifyGameEnd()
    {
        if (NetworkServer.active && NetworkGameState.Instance != null)
            NetworkGameState.Instance.RpcGameEnd();
    }
}
