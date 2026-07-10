using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class MapManager : MonoBehaviour
{ 
    [Header("地图设置")]
    public List<CubesHighLight> allTiles = new List<CubesHighLight>();
    public int mapWidth =20;
    public int mapHeight  =30;
    
    // 地图网格数据，用于寻路算法
    private int[,] mapGrid;
    
    // 桥梁位置列表
    public List<Vector2Int> bridgePositions = new List<Vector2Int>();

    public static MapManager Instance { get; private set; }

    private bool _initialized = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        StartCoroutine(DelayedInitialize());
    }

    private IEnumerator DelayedInitialize()
    {
        yield return null;
        InitializeMap();
    }

    public void EnsureInitialized()
    {
        if (!_initialized) InitializeMap();
    }

    private void InitializeMap()
    {
        if (_initialized) return;
        _initialized = true;
        InitializeMapGrid();
        CollectAllTiles();
        InitializeAllTileGridPositions();
        IdentifyBridges();
        int towerCount = 0;
        foreach (var t in allTiles) { if (t.isTower) towerCount++; }
        Debug.Log($"[MapManager] 初始化完成, 共 {allTiles.Count} 个地砖, 其中 {towerCount} 个高台");
    }

    // 初始化所有瓦片的网格坐标
    private void InitializeAllTileGridPositions()
    {
        if (allTiles.Count == 0)
        {
            Debug.LogWarning("没有收集到任何瓦片，无法初始化网格坐标");
            return;
        }

        // 1. 找到所有瓦片的最小世界坐标（作为网格的原点）
        Vector3 minPosition = allTiles[0].transform.position;
        foreach (CubesHighLight tile in allTiles)
        {
            Vector3 pos = tile.transform.position;
            if (pos.x < minPosition.x) minPosition.x = pos.x;
            if (pos.z < minPosition.z) minPosition.z = pos.z;
        }

        // 2. 假设瓦片大小为1（可以根据实际情况调整）
        float tileSize = 1.0f;

        // 3. 为每个瓦片计算网格坐标
        foreach (CubesHighLight tile in allTiles)
        {
            Vector3 pos = tile.transform.position;
            
            // 计算相对于最小位置的偏移
            float offsetX = pos.x - minPosition.x;
            float offsetZ = pos.z - minPosition.z;
            
            // 转换为网格坐标（四舍五入确保整数）
            int gridX = Mathf.RoundToInt(offsetX / tileSize);
            int gridY = Mathf.RoundToInt(offsetZ / tileSize);
            
            // 设置网格坐标
            tile.gridPosition = new Vector2Int(gridX, gridY);
            
            
        }

        Debug.Log("所有瓦片的网格坐标初始化完成");
    }
    // 收集场景中所有的地砖
    private void CollectAllTiles()
    {
        allTiles.Clear();
        CubesHighLight[] tiles = FindObjectsOfType<CubesHighLight>();
        allTiles.AddRange(tiles);
        
        Debug.Log("收集到 " + allTiles.Count + " 个地砖");
    }

    // 初始化地图网格数据
    private void InitializeMapGrid()
    {
        mapGrid = new int[mapWidth, mapHeight];
        
        // 默认设置所有格子为可通行
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                mapGrid[x, y] = 0; // 0 = 可通行
            }
        }
    }

    // 识别桥梁位置
    private void IdentifyBridges()
    {
        bridgePositions.Clear();
        
        foreach (CubesHighLight tile in allTiles)
        {
            if (tile.tileType == CubesHighLight.TileType.Bridge)// && IsBridgeTile(tile)
            {
                bridgePositions.Add(tile.gridPosition);
            }
        }
        
        Debug.Log("识别到 " + bridgePositions.Count + " 个桥梁位置");
    }

    // 判断是否为桥梁地砖
    private bool IsBridgeTile(CubesHighLight tile)
    {
        // 桥梁通常连接河流两岸，所以检查周围是否有河流
        List<CubesHighLight> neighbors = GetNeighborTiles(tile.gridPosition);
        
        foreach (CubesHighLight neighbor in neighbors)
        {
            if (neighbor.tileType == CubesHighLight.TileType.River)
            {
                return true;
            }
        }
        
        return false;
    }

    public CubesHighLight GetTileAtPosition(Vector2Int gridPos)
    {
        EnsureInitialized();
        foreach (CubesHighLight tile in allTiles)
        {
            if (tile.gridPosition == gridPos)
            {
                return tile;
            }
        }
        return null;
    }

    public CubesHighLight GetTileAtWorldPosition(Vector3 worldPos)
    {
        EnsureInitialized();
        foreach (CubesHighLight tile in allTiles)
        {
            if (Vector3.Distance(tile.transform.position, worldPos) < 1f)
            {
                return tile;
            }
        }
        return null;
    }
    public CubesHighLight GetPlatformAtWorldPosition(Vector3 worldPos)
    {
        EnsureInitialized();
        foreach (CubesHighLight tile in allTiles)
        {
            if (Vector3.Distance(tile.transform.position, worldPos) < 1f && tile.isTower)
            {
                return tile;
            }
        }
        return null;
    }

    // 获取相邻地砖
    public List<CubesHighLight> GetNeighborTiles(Vector2Int gridPos)
    {
        List<CubesHighLight> neighbors = new List<CubesHighLight>();
        
        // 检查上、下、左、右四个方向
        Vector2Int[] directions = {
            new Vector2Int(0, 1),  // 上
            new Vector2Int(0, -1), // 下
            new Vector2Int(-1, 0), // 左
            new Vector2Int(1, 1),   // 右上
            new Vector2Int(1, -1),   // 右下
            new Vector2Int(-1, -1),   // 左下
            new Vector2Int(-1, 1)   // 左上
        };
        
        foreach (Vector2Int dir in directions)
        {
            Vector2Int neighborPos = gridPos + dir;
            
            // 检查边界
            if (neighborPos.x >= 0 && neighborPos.x < mapWidth && 
                neighborPos.y >= 0 && neighborPos.y < mapHeight)
            {
                CubesHighLight neighbor = GetTileAtPosition(neighborPos);
                if (neighbor != null)
                {
                    neighbors.Add(neighbor);
                }
            }
        }
        return neighbors;
    }

    // 检查位置是否可通行
    public bool IsWalkable(Vector2Int gridPos)
    {
        // 检查边界
        if (gridPos.x < 0 || gridPos.x >= mapWidth || 
            gridPos.y < 0 || gridPos.y >= mapHeight)
        {
            return false;
        }
        
        CubesHighLight tile = GetTileAtPosition(gridPos);
        if (tile != null)
        {
            // 只有 Normal、Path（包括桥梁）是可通行的
            return tile.isWalkable && 
                   (tile.tileType == CubesHighLight.TileType.Normal || 
                    tile.tileType == CubesHighLight.TileType.Path ||
                    tile.tileType == CubesHighLight.TileType.Bridge);
        }
        
        return false;
    }

    // 检查是否为河流
    public bool IsRiver(Vector2Int gridPos)
    {
        CubesHighLight tile = GetTileAtPosition(gridPos);
        return tile != null && tile.tileType == CubesHighLight.TileType.River;
    }

    // 检查是否为桥梁
    public bool IsBridge(Vector2Int gridPos)
    {
        return bridgePositions.Contains(gridPos);
    }
}

