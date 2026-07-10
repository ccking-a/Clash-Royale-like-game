using Mirror;
using UnityEngine;
using System;

public class GameStart : NetworkBehaviour
{
    // 游戏开始事件
    public static event Action OnGameStarted;

    public override void OnStartClient()
    {
        base.OnStartClient();
        OnGameStarted?.Invoke();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        OnGameStarted?.Invoke();
    }

}