using Mirror;
using System.Collections;
using UnityEngine;

public class RoomToGame : MonoBehaviour
{
    void Start()
    {
        // 注册连接成功回调
        NetworkClient.OnConnectedEvent += OnClientConnected;
    }

    void OnDestroy()
    {
        // 取消注册
        NetworkClient.OnConnectedEvent -= OnClientConnected;
    }

    private void OnClientConnected()
    {
        // 连接成功后自动准备并添加玩家
        if (!NetworkClient.ready)
        {
            NetworkClient.Ready();
            NetworkClient.AddPlayer();
        }
    }

}
