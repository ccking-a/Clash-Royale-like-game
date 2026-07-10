using UnityEngine;
using Mirror;

/// <summary>
/// 挂在最终守护目标（国王塔）上，用于检测游戏结束。
/// 子物体中有 RedDoor 和 BlueDoor，客户端根据阵营切换显示：敌方塔显示 RedDoor，己方塔显示 BlueDoor。
/// 仅客户端本地切换，不参与网络同步。
/// </summary>
public class GuowangDIe : MonoBehaviour
{
    void Start()
    {
        ApplyDoorVisibility();
        // 阵营可能尚未同步，延迟重试一次
        Invoke(nameof(ApplyDoorVisibility), 1f);
    }

    /// <summary>
    /// 根据阵营（faction/teamIndex）判断敌我，切换 RedDoor/BlueDoor 显示。
    /// 塔的 teamIndex 与本地玩家阵营不同 -> 敌方 -> RedDoor；相同 -> 己方 -> BlueDoor。
    /// </summary>
    void ApplyDoorVisibility()
    {
        Transform redDoor = transform.Find("RedDoor");
        Transform blueDoor = transform.Find("BlueDoor");
        if (redDoor == null || blueDoor == null) return;

        var sc = GetComponent<SoldierController>();
        if (sc == null) return;

        var localFaction = PlayerFaction.GetLocalPlayerFaction();
        if (localFaction == PlayerFaction.Faction.None) return;

        int localTeam = (int)localFaction;
        bool isEnemy = sc.teamIndex != localTeam;

        if (isEnemy)
        {
            redDoor.gameObject.SetActive(true);
            blueDoor.gameObject.SetActive(false);
        }
        else
        {
            redDoor.gameObject.SetActive(false);
            blueDoor.gameObject.SetActive(true);
        }
    }
}
