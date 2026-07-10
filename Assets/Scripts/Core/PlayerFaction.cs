using Mirror;
using UnityEngine;

public class PlayerFaction : NetworkBehaviour
{
    // 阵营枚举
    public enum Faction
    {
        None = -1,
        TeamA = 0,  // 红方/蓝方
        TeamB = 1   // 蓝方/红方
    }

    [SyncVar(hook = nameof(OnFactionChanged))]
    public Faction currentFaction = Faction.None;

    // 阵营改变时的回调
    void OnFactionChanged(Faction oldFaction, Faction newFaction)
    {
        Debug.Log($"玩家 {netId} 阵营: {newFaction}");

        // 根据阵营更新外观（如颜色、模型）
        UpdateAppearance(newFaction);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        // 服务器端分配阵营
        AssignFaction();
    }

    [Server]
    void AssignFaction()
    {
        // 获取所有已连接的玩家
        var allPlayers = FindObjectsOfType<PlayerFaction>();

        // 第一个玩家分配 TeamA，第二个分配 TeamB
        if (allPlayers.Length == 1)
        {
            currentFaction = Faction.TeamA;
        }
        else if (allPlayers.Length == 2)
        {
            currentFaction = Faction.TeamB;
        }

        Debug.Log($"服务器分配阵营: 玩家 {netId} = {currentFaction}");
    }

    void UpdateAppearance(Faction faction)
    {
        // 根据阵营更新外观（颜色、模型等）
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = faction == Faction.TeamA ? Color.red : Color.blue;
        }
    }

    // 其他脚本可以通过这个方法查询
    public static Faction GetLocalPlayerFaction()
    {
        var localPlayer = NetworkClient.localPlayer;
        if (localPlayer != null)
        {
            var factionComp = localPlayer.GetComponent<PlayerFaction>();
            return factionComp != null ? factionComp.currentFaction : Faction.None;
        }
        return Faction.None;
    }
}