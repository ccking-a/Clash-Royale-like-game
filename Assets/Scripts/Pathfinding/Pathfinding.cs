﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Pathfinding : Singleton<Pathfinding>
{
    private MapManager mapManager;

    protected override void Awake()
    {
        base.Awake();
        // 在Awake中初始化引用，确保在任何其他方法调用前可用
        mapManager = MapManager.Instance;
        if (mapManager == null)
        {
            Debug.LogWarning("Pathfinding初始化时MapManager实例不存在");
        }
    }

    private void Start()
    {
        // 不再需要Start方法初始化mapManager
    }

    // A*节点类
    private class Node
    {
        public Vector2Int gridPosition;
        public Node parent;
        public float gCost; // 起点到当前节点的实际代价
        public float hCost; // 当前节点到目标节点的预估代价
        public float fCost { get { return gCost + hCost; } } // 总代价

        public Node(Vector2Int position)
        {
            gridPosition = position;
        }
    }

    // 寻找路径（使用NavMesh计算最短路径）
    public List<Vector3> FindPath(Vector3 startPos, Vector3 targetPos)
    {
        // 检查缓存
        if (TryGetCachedPath(startPos, targetPos, out List<Vector3> cachedPath))
        {
            return cachedPath;
        }
        
        // 使用NavMesh计算最短路径
        NavMeshPath navMeshPath = new NavMeshPath();
        bool pathFound = NavMesh.CalculatePath(startPos, targetPos, NavMesh.AllAreas, navMeshPath);
        
        List<Vector3> path = null;
        if (pathFound && navMeshPath.status == NavMeshPathStatus.PathComplete)
        {
            // 将NavMeshPath转换为List<Vector3>
            path = new List<Vector3>(navMeshPath.corners);
            
            // 添加到缓存
            if (path != null && path.Count > 0)
            {
                AddPathToCache(startPos, targetPos, path);
            }
        }
        else
        {
            Debug.LogWarning("NavMesh路径未找到: " + startPos + " -> " + targetPos + ", 状态: " + navMeshPath.status);
        }
        
        return path;
    }

    // 寻找路径（网格坐标版本，转换为世界坐标使用NavMesh）
    public List<Vector3> FindPath(Vector2Int startPos, Vector2Int targetPos)
    {
        // 将网格坐标转换为世界坐标
        CubesHighLight startTile = mapManager.GetTileAtPosition(startPos);
        CubesHighLight targetTile = mapManager.GetTileAtPosition(targetPos);

        if (startTile == null || targetTile == null)
        {
            Debug.LogWarning("起点或终点不在地图上");
            return null;
        }

        return FindPath(startTile.transform.position, targetTile.transform.position);
    }

    // 计算两个位置之间的实际可行走距离（使用NavMesh）
    public float CalculateWalkableDistance(Vector3 startPos, Vector3 targetPos)
    {
        // 使用NavMesh.SamplePosition确保起点和终点在NavMesh上
        NavMeshHit startHit;
        NavMeshHit targetHit;
        
        if (NavMesh.SamplePosition(startPos, out startHit, 2.0f, NavMesh.AllAreas) &&
            NavMesh.SamplePosition(targetPos, out targetHit, 2.0f, NavMesh.AllAreas))
        {
            // 使用NavMesh.CalculatePath获取最短路径
            NavMeshPath path = new NavMeshPath();
            if (NavMesh.CalculatePath(startHit.position, targetHit.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
            {
                // 计算路径总长度
                float totalDistance = 0f;
                Vector3[] corners = path.corners;
                for (int i = 0; i < corners.Length - 1; i++)
                {
                    totalDistance += Vector3.Distance(corners[i], corners[i + 1]);
                }
                return totalDistance;
            }
        }
        
        return Mathf.Infinity; // 不可达
    }
    
    // 性能优化：路径缓存
    private Dictionary<PathKey, List<Vector3>> pathCache = new Dictionary<PathKey, List<Vector3>>();
    private float cacheDuration = 5f;
    private Dictionary<PathKey, float> cacheTimestamps = new Dictionary<PathKey, float>();
    
    // 性能优化：计算频率限制
    private float pathFindingCooldown = 0.2f;
    private Dictionary<GameObject, float> lastPathFindingTimes = new Dictionary<GameObject, float>();
    
    // 路径键类，用于缓存
    private class PathKey
    {
        public Vector3 start;
        public Vector3 end;
        
        public PathKey(Vector3 s, Vector3 e)
        {
            start = s;
            end = e;
        }
        
        public override bool Equals(object obj)
        {
            if (obj is PathKey)
            {
                PathKey other = (PathKey)obj;
                return Vector3.Distance(start, other.start) < 0.1f && Vector3.Distance(end, other.end) < 0.1f;
            }
            return false;
        }
        
        public override int GetHashCode()
        {
            return (start.ToString() + end.ToString()).GetHashCode();
        }
    }
    
    // 检查路径是否在缓存中
    private bool TryGetCachedPath(Vector3 start, Vector3 end, out List<Vector3> path)
    {
        PathKey key = new PathKey(start, end);
        
        if (pathCache.TryGetValue(key, out path) && cacheTimestamps.TryGetValue(key, out float timestamp))
        {
            if (Time.time - timestamp < cacheDuration)
            {
                return true;
            }
            else
            {
                // 缓存过期
                pathCache.Remove(key);
                cacheTimestamps.Remove(key);
            }
        }
        
        path = null;
        return false;
    }
    
    // 添加路径到缓存
    private void AddPathToCache(Vector3 start, Vector3 end, List<Vector3> path)
    {
        if (path == null || path.Count == 0) return;
        
        PathKey key = new PathKey(start, end);
        pathCache[key] = path;
        cacheTimestamps[key] = Time.time;
        
        // 限制缓存大小
        if (pathCache.Count > 100)
        {
            // 删除最旧的缓存
            float oldestTime = float.MaxValue;
            PathKey oldestKey = null;
            
            foreach (var pair in cacheTimestamps)
            {
                if (pair.Value < oldestTime)
                {
                    oldestTime = pair.Value;
                    oldestKey = pair.Key;
                }
            }
            
            if (oldestKey != null)
            {
                pathCache.Remove(oldestKey);
                cacheTimestamps.Remove(oldestKey);
            }
        }
    }
    
    // 检查是否可以进行寻路计算
    public bool CanCalculatePath(GameObject unit)
    {
        if (!lastPathFindingTimes.TryGetValue(unit, out float lastTime))
        {
            return true;
        }
        
        return Time.time - lastTime >= pathFindingCooldown;
    }
    
    // 更新寻路时间
    public void UpdatePathFindingTime(GameObject unit)
    {
        lastPathFindingTimes[unit] = Time.time;
    }
}