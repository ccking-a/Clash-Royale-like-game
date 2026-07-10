using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using Mirror;
using UnityEngine.SceneManagement;

public class NetworkGameManager : NetworkManager
{
    [Header("自定义设置")]
    public GameObject gameManagerPrefab;
    [Tooltip("NetworkGameState 预制体（需有NetworkIdentity），服务器启动时生成")]
    public GameObject networkGameStatePrefab;

    private int _nextPlayerIndex = 0;

    public override void Awake()
    {
        base.Awake();
        if (networkGameStatePrefab != null && networkGameStatePrefab.GetComponent<NetworkIdentity>() != null && !spawnPrefabs.Contains(networkGameStatePrefab))
            spawnPrefabs.Add(networkGameStatePrefab);
    }

    // 服务器启动时
    public override void OnStartServer()    
    {
        base.OnStartServer();
        _nextPlayerIndex = 0;
        Debug.Log("NetworkGameManager.OnStartServer() 被调用");
        
        if (networkGameStatePrefab != null && NetworkGameState.Instance == null)
        {
            var go = Instantiate(networkGameStatePrefab);
            NetworkServer.Spawn(go);
        }
    }

    // 客户端连接时
    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        base.OnServerConnect(conn);
        Debug.Log($"客户端 {conn.connectionId} 连接到服务器");
    }

    // 客户端断开连接时
    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        base.OnServerDisconnect(conn);
        Debug.Log($"客户端 {conn.connectionId} 从服务器断开连接");
    }

    // 服务器停止时
    public override void OnStopServer()
    {
        base.OnStopServer();
        _nextPlayerIndex = 0;
        Debug.Log("NetworkGameManager.OnStopServer() 被调用");
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        Debug.LogError($"========== OnServerAddPlayer: 连接ID={conn.connectionId} ==========");

        Transform startPos = GetStartPosition();
        GameObject player = startPos != null
            ? Instantiate(playerPrefab, startPos.position, startPos.rotation)
            : Instantiate(playerPrefab);

        // ★ 必须先 Spawn，再设置 SyncVar，这样 Mirror 才能正确标记 dirty 并同步到客户端
        NetworkServer.AddPlayerForConnection(conn, player);

        PlayerElixir pe = player.GetComponent<PlayerElixir>();
        if (pe != null)
        {
            pe.playerIndex = _nextPlayerIndex;
            Debug.LogError($"[NetworkGameManager] ★ Spawn后分配 playerIndex={_nextPlayerIndex} 给连接 {conn.connectionId}, netId={player.GetComponent<NetworkIdentity>()?.netId}");
            _nextPlayerIndex++;
        }
        else
        {
            Debug.LogError("[NetworkGameManager] Player预制体上没有 PlayerElixir 组件！");
        }
    }

    /// <summary>服务器通知所有客户端游戏结束</summary>
    public void NotifyGameEnd()
    {
        if (NetworkServer.active && NetworkGameState.Instance != null)
            NetworkGameState.Instance.RpcGameEnd();
    }
}

