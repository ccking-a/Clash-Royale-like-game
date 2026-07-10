using UnityEngine;

using Mirror;

/// <summary>
/// 敌兵自动生成器，按照指定位置和时间间隔自动放置敌方单位
/// 网络模式下仅在服务器端生成，使用 NetworkServer.Spawn 同步到客户端
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("生成设置")]
    public GameObject enemyPrefab;          // 敌方单位预制体
    public float spawnInterval = 2f;        // 生成时间间隔（秒）
    public bool startSpawning = true;       // 是否自动开始生成

    [Header("开局倒计时")]
    public float countdownDuration = 3f;    // 开局倒计时时长（秒）
    private bool isCountdownFinished = false; // 倒计时是否结束
    private float countdownTimer = 0f;       // 倒计时计时器

    [Header("生成位置")]
    public Transform[] spawnPositions;      // 生成位置数组

    [Header("生成状态")]
    private float spawnTimer = 0f;          // 生成计时器
    private int currentSpawnIndex = 0;      // 当前生成位置索引

    // 使用单例引用，不再需要手动设置
    // public GameManager gameManager;
    // public MapManager mapManager;

    private void Awake()
    {
        if (UnitPoolManager.Instance == null && enemyPrefab != null)
        {
            var go = new GameObject("UnitPoolManager");
            go.AddComponent<UnitPoolManager>();
            if (NetworkServer.active)
                UnitPoolManager.Instance.WarmNetworkPools(new[] { enemyPrefab });
        }
    }

    private void Update()
    {
        // 网络模式下仅服务器生成敌兵，客户端不执行
        if (NetworkClient.active && !NetworkServer.active) return;

        if (startSpawning && spawnPositions.Length > 0)
        {
            // 先处理开局倒计时
            if (!isCountdownFinished)
            {
                countdownTimer += Time.deltaTime;

                // 检查倒计时是否结束
                if (countdownTimer >= countdownDuration)
                {
                    isCountdownFinished = true;
                    Debug.Log($"开局倒计时结束（{countdownDuration}秒），开始生成敌兵");
                }
            }
            // 倒计时结束后，才开始生成敌兵
            else
            {
                spawnTimer += Time.deltaTime;

                // 到达生成时间间隔
                if (spawnTimer >= spawnInterval)
                {
                    SpawnEnemy();
                    spawnTimer = 0f;
                }
            }
        }
    }

    /// <summary>
    /// 生成一个敌兵
    /// </summary>
    private void SpawnEnemy()
    {
        // 检查预制体是否有效
        if (enemyPrefab == null)
        {
            Debug.LogWarning("敌兵预制体未设置，无法生成");
            return;
        }

        // 获取当前生成位置
        Transform spawnPos = spawnPositions[currentSpawnIndex];

        // 找到最近的可放置地砖
        CubesHighLight nearestTile = FindNearestPlaceableTile(spawnPos.position);

        if (nearestTile != null)
        {
            if (nearestTile.isPlaceable && !nearestTile.isOccupied)
            {
                Vector3 spawnWorldPos = nearestTile.transform.position + Vector3.up * 0.5f;
                GameObject newEnemy = UnitPoolManager.Instance != null
                    ? UnitPoolManager.Instance.GetNetworkUnit(enemyPrefab, spawnWorldPos, Quaternion.identity)
                    : Instantiate(enemyPrefab, spawnWorldPos, Quaternion.identity);

                SoldierController sc = newEnemy.GetComponent<SoldierController>();
                if (sc != null)
                {
                    sc.teamIndex = 1;
                    if (UnitManager.Instance != null)
                        UnitManager.Instance.RegisterUnit(newEnemy, 1);
                }

                if (NetworkServer.active && newEnemy.GetComponent<NetworkIdentity>() != null)
                    NetworkServer.Spawn(newEnemy);

                Debug.Log($"在位置 {spawnWorldPos} 生成敌兵，使用地砖 {nearestTile.gridPosition}");
            }
            else
            {
                Debug.LogWarning($"位置 {spawnPos.position} 附近没有可放置的地砖");
            }
        }
        else
        {
            Debug.LogWarning($"在位置 {spawnPos.position} 附近没有找到地砖");
        }
    }

    /// <summary>
    /// 找到指定位置附近最近的可放置地砖
    /// </summary>
    /// <param name="position">指定位置</param>
    /// <returns>最近的可放置地砖</returns>
    private CubesHighLight FindNearestPlaceableTile(Vector3 position)
    {
        // 调用MapManager单例的方法，通过世界坐标获取地砖
        if (MapManager.Instance == null)
        {
            Debug.LogWarning("MapManager实例不存在，无法获取地砖");
            return null;
        }
        return MapManager.Instance.GetTileAtWorldPosition(position);
    }

    /// <summary>
    /// 开始生成敌兵
    /// </summary>
    public void StartSpawning()
    {
        startSpawning = true;
        spawnTimer = 0f;
        Debug.Log("开始生成敌兵");
    }

    /// <summary>
    /// 停止生成敌兵
    /// </summary>
    public void StopSpawning()
    {
        startSpawning = false;
        Debug.Log("停止生成敌兵");
    }

    /// <summary>
    /// 设置生成时间间隔
    /// </summary>
    /// <param name="interval">时间间隔（秒）</param>
    public void SetSpawnInterval(float interval)
    {
        spawnInterval = Mathf.Max(0.1f, interval);
        Debug.Log($"生成时间间隔设置为：{spawnInterval}秒");
    }

    /// <summary>
    /// 设置敌兵预制体
    /// </summary>
    /// <param name="prefab">敌兵预制体</param>
    public void SetEnemyPrefab(GameObject prefab)
    {
        enemyPrefab = prefab;
        Debug.Log("敌兵预制体已更新");
    }
}