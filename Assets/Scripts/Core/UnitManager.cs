using System.Collections.Generic;
using UnityEngine;

public class UnitManager : Singleton<UnitManager>
{
    public class UnitInfo
    {
        public GameObject unitObject;
        public int teamIndex;
        public SoldierController unitController;
        public Vector3 position;

        public UnitInfo(GameObject obj, int team, SoldierController controller)
        {
            unitObject = obj;
            teamIndex = team;
            unitController = controller;
            position = obj.transform.position;
        }

        public void UpdatePosition()
        {
            if (unitObject != null)
                position = unitObject.transform.position;
        }
    }

    private List<UnitInfo> allUnits = new List<UnitInfo>();
    private Dictionary<int, List<UnitInfo>> unitsByTeam = new Dictionary<int, List<UnitInfo>>();

    protected override void Awake()
    {
        base.Awake();
    }

    private List<UnitInfo> GetOrCreateTeamList(int team)
    {
        if (!unitsByTeam.ContainsKey(team))
            unitsByTeam[team] = new List<UnitInfo>();
        return unitsByTeam[team];
    }

    public void RegisterUnit(GameObject unitObj, int teamIndex)
    {
        SoldierController controller = unitObj.GetComponent<SoldierController>();
        if (controller == null) return;
        if (allUnits.Exists(u => u.unitObject == unitObj)) return;

        UnitInfo info = new UnitInfo(unitObj, teamIndex, controller);
        allUnits.Add(info);
        GetOrCreateTeamList(teamIndex).Add(info);
    }

    public void UnregisterUnit(GameObject unitObj)
    {
        UnitInfo info = allUnits.Find(u => u.unitObject == unitObj);
        if (info == null) return;

        allUnits.Remove(info);
        if (unitsByTeam.ContainsKey(info.teamIndex))
            unitsByTeam[info.teamIndex].Remove(info);
    }

    /// <summary>
    /// 找到距离最近的敌方单位（teamIndex 不同的所有存活单位）
    /// </summary>
    public UnitInfo FindNearestEnemy(Vector3 position, int myTeamIndex)
    {
        UnitInfo nearest = null;
        float shortest = Mathf.Infinity;

        Pathfinding pathfinding = Pathfinding.Instance;

        foreach (UnitInfo unit in allUnits)
        {
            if (unit.unitObject == null) continue;
            if (unit.teamIndex == myTeamIndex) continue;
            if (unit.unitController == null || !unit.unitController.isAlive) continue;

            unit.UpdatePosition();

            float dist = (pathfinding != null)
                ? pathfinding.CalculateWalkableDistance(position, unit.position)
                : Vector3.Distance(position, unit.position);

            if (dist < shortest)
            {
                shortest = dist;
                nearest = unit;
            }
        }

        return nearest;
    }

    public List<UnitInfo> GetAllUnits()
    {
        return new List<UnitInfo>(allUnits);
    }

    public List<UnitInfo> GetTeamUnits(int teamIndex)
    {
        return new List<UnitInfo>(GetOrCreateTeamList(teamIndex));
    }

    public void UpdateAllUnitPositions()
    {
        foreach (UnitInfo unit in allUnits)
        {
            unit.UpdatePosition();
        }
    }

    /// <summary>清理已销毁的单位引用</summary>
    public void CleanupNulls()
    {
        allUnits.RemoveAll(u => u.unitObject == null);
        foreach (var list in unitsByTeam.Values)
            list.RemoveAll(u => u.unitObject == null);
    }
}
