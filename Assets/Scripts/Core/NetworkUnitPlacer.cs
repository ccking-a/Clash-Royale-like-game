using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using Mirror;
using System.Linq;

public class NetworkUnitPlacer : NetworkBehaviour
{
    [Header("设置")]
    public Camera gameCamera;
    public LayerMask groundLayer;

    [Header("放置状态")]
    public GameObject placementPreview;  // 本地预览对象，仅本地创建，不发给服务器
    // 以下为纯本地状态：点击/拖拽只改本地，不请求服务器；只有松开确认放置时才发 Command
    private bool _localIsPlacing = false;
    private int _localUnitIndex = -1;
    private int _localPositionIndex = -1;

    [Header("兵种数据")]
    public UnitData[] allUnits;          // 所有兵种数据

    [Header("资源提示")]
    public float requiredCost;  // 仅本地用于预览显示

    // 本地引用
    private UIManager uiManager;

    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("NetworkUnitPlacer.OnStartServer() 被调用");
        InitializeReferences();
        EnsureUnitPoolManager();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        Debug.Log("NetworkUnitPlacer.OnStartClient() 被调用1111111111111111111111");
        InitializeReferences();
        EnsureUnitPoolManager();
    }

    private void EnsureUnitPoolManager()
    {
        if (UnitPoolManager.Instance == null)
        {
            var go = new GameObject("UnitPoolManager");
            go.AddComponent<UnitPoolManager>();
        }
        if (NetworkServer.active && allUnits != null && allUnits.Length > 0)
        {
            var prefabs = new System.Collections.Generic.List<GameObject>();
            foreach (var u in allUnits)
                if (u?.unitPrefab != null) prefabs.Add(u.unitPrefab);
            if (prefabs.Count > 0)
                UnitPoolManager.Instance.WarmNetworkPools(prefabs.ToArray());
        }
    }

    private void RegisterUnitPrefabs()
    {
        if (allUnits == null) return;
        var nm = NetworkManager.singleton as NetworkManager;
        if (nm == null) return;
        foreach (var unitData in allUnits)
        {
            if (unitData?.unitPrefab == null) continue;
            if (unitData.unitPrefab.GetComponent<NetworkIdentity>() == null) continue;
            if (!nm.spawnPrefabs.Contains(unitData.unitPrefab))
                nm.spawnPrefabs.Add(unitData.unitPrefab);
        }
    }

    // 初始化引用
    private void InitializeReferences()
    {
        if (uiManager == null)
        {
            uiManager = FindObjectOfType<UIManager>();
        }
        if (gameCamera == null)
        {
            gameCamera = Camera.main;
        }
    }

    // Update方法只在客户端执行
    [Client]
    void Update()
    {
        if (!isLocalPlayer) return;
        if (!_localIsPlacing || placementPreview == null) return;

        // 安全检查：确保gameCamera始终有效
        if (gameCamera == null)
        {
            InitializeReferences();
            if (gameCamera == null)
            {
                Debug.LogError("NetworkUnitPlacer.Update()：无法初始化gameCamera");
                return;
            }
        }

        // 实时更新预览位置的准确位置
        Ray ray = gameCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 1000f, groundLayer))
        {
            // 显示预览对象
            placementPreview.SetActive(true);
            
            CubesHighLight[] allTiles = FindObjectsOfType<CubesHighLight>();
            if (allTiles.Length == 0) return;
            CubesHighLight nearestTile = null;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < allTiles.Length; i++)
            {
                if (!allTiles[i].isPlaceable) continue;
                float distance = Vector3.Distance(allTiles[i].transform.position, hit.point);
                if (distance < nearestDistance)
                {
                    nearestTile = allTiles[i];
                    nearestDistance = distance;
                }
            }

            if (nearestTile != null)
            {
                float yOffset = 0.5f;
                if (PlatformManager.IsPlatform(nearestTile))
                {
                    yOffset = PlatformManager.GetPlatformHeight();
                }
                placementPreview.transform.position = nearestTile.transform.position + Vector3.up * yOffset;

                bool isPlacementValid = nearestTile.isPlaceable && !nearestTile.isOccupied;
                placementPreview.GetComponent<Renderer>().material.color = isPlacementValid
                    ? new Color(0, 1, 0, 0.6f)
                    : new Color(1, 0, 0, 0.6f);
            }

        }
        else
        {
            // 鼠标未命中地面，隐藏预览对象
            placementPreview.SetActive(false);
        }

        // 左键放置（松开时都视为放置事件） - 在客户端获取鼠标位置并传递给服务器
        if (Input.GetMouseButtonUp(0))
        {
            Ray placeRay = gameCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit placeHit;
            if (Physics.Raycast(placeRay, out placeHit, 1000f, groundLayer))
            {
                CubesHighLight[] allTiles = FindObjectsOfType<CubesHighLight>();
                if (allTiles.Length == 0) return;
                CubesHighLight nearestTile = null;
                float nearestDistance = float.MaxValue;
                for (int i = 0; i < allTiles.Length; i++)
                {
                    if (!allTiles[i].isPlaceable || allTiles[i].isOccupied) continue;
                    float distance = Vector3.Distance(allTiles[i].transform.position, placeHit.point);
                    if (distance < nearestDistance)
                    {
                        nearestTile = allTiles[i];
                        nearestDistance = distance;
                    }
                }
                if (nearestTile != null)
                {
                    CmdTryPlaceAtPosition(nearestTile.transform.position, _localUnitIndex, _localPositionIndex);
                }
                else
                {
                    Debug.LogWarning("未找到可放置的格子");
                }
            }
        }

        // 右键取消（纯本地，不请求服务器）
        if (Input.GetMouseButtonDown(1))
        {
            CancelPlacementLocal();
        }
    }

    /// <summary>本地开始放置预览，不请求服务器；只有松开确认放置时才发 Command</summary>
    [Client]
    public void StartPlacingUnitLocal(int unitIndex, int positionIndex)
    {
        if (!isClient) return;
        if (_localIsPlacing) return;
        _localIsPlacing = true;
        _localUnitIndex = unitIndex;
        _localPositionIndex = positionIndex;

        if (uiManager != null && positionIndex != -1)
            uiManager.TranslateToggle(positionIndex, new Vector2(0, 60f));

        GameObject unitPrefab = GetUnitPrefabByIndex(unitIndex);
        if (unitPrefab == null)
        {
            Debug.LogError($"NetworkUnitPlacer: 无效的单位索引: {unitIndex}");
            return;
        }

        if (placementPreview != null)
        {
            if (UnitPoolManager.Instance != null)
                UnitPoolManager.Instance.ReturnPreview(placementPreview, unitPrefab);
            else
                Destroy(placementPreview);
        }

        Quaternion previewRot = Quaternion.identity;
        if (PlayerElixir.LocalInstance != null && PlayerElixir.LocalInstance.IsPlayer2)
            previewRot = Quaternion.Euler(0, 180f, 0);

        placementPreview = UnitPoolManager.Instance != null
            ? UnitPoolManager.Instance.GetPreview(unitPrefab, Vector3.zero, previewRot)
            : Instantiate(unitPrefab, Vector3.zero, previewRot);
        if (placementPreview != null)
            placementPreview.SetActive(false);
        SetRendererTransparent(placementPreview, 0.5f);
        Collider col = placementPreview.GetComponent<Collider>();
        if (col != null) col.enabled = false;
        SoldierController unitCtrl = placementPreview.GetComponent<SoldierController>();
        if (unitCtrl != null) unitCtrl.enabled = false;
    }

    /// <summary>本地取消放置，不请求服务器</summary>
    [Client]
    public void CancelPlacementLocal()
    {
        if (!isClient) return;
        if (placementPreview != null)
        {
            var prefab = _localUnitIndex >= 0 ? GetUnitPrefabByIndex(_localUnitIndex) : null;
            if (UnitPoolManager.Instance != null && prefab != null)
                UnitPoolManager.Instance.ReturnPreview(placementPreview, prefab);
            else
                Destroy(placementPreview);
            placementPreview = null;
        }
        _localIsPlacing = false;
        _localUnitIndex = -1;
        _localPositionIndex = -1;
    }

    // 根据单位索引获取预制体
    private GameObject GetUnitPrefabByIndex(int unitIndex)
    {
        if (unitIndex >= 0 && unitIndex < allUnits.Length)
        {
            return allUnits[unitIndex].unitPrefab;
        }
        Debug.LogError($"NetworkUnitPlacer: 无效的单位索引: {unitIndex}");
        return null;
    }

    // 松开确认放置时才请求服务器；服务器校验并只扣除该玩家的圣水
    [Command]
    public void CmdTryPlaceAtPosition(Vector3 placePosition, int unitIndex, int positionIndex)
    {
        GameObject unitPrefab = GetUnitPrefabByIndex(unitIndex);
        if (unitPrefab == null)
        {
            Debug.LogError($"NetworkUnitPlacer: 无效的单位索引: {unitIndex}");
            return;
        }

        SoldierController solider = unitPrefab.GetComponent<SoldierController>();
        float requiredCost = solider.Cost;

        // 1v1：只扣除发起放置的该玩家的圣水
        PlayerElixir playerElixir = connectionToClient?.identity?.GetComponent<PlayerElixir>();
        bool isPlayer2 = playerElixir != null && playerElixir.IsPlayer2;
        float availableElixir = playerElixir != null
            ? playerElixir.currentElixir
            : (NetworkGameState.Instance != null ? NetworkGameState.Instance.currentElixir : 0f);
        Debug.Log($"[放置] 玩家{(isPlayer2 ? 2 : 1)} 当前拥有{availableElixir}费用, 需要{requiredCost}");
        if (requiredCost <= availableElixir)
        {
            // 服务器端验证：通过位置查找对应地块
            CubesHighLight nearestTile = null;
            if (MapManager.Instance != null)
            {
                nearestTile = MapManager.Instance.GetPlatformAtWorldPosition(placePosition);
                if (nearestTile == null)
                {
                    nearestTile = MapManager.Instance.GetTileAtWorldPosition(placePosition);
                }
                Debug.Log($"[放置验证] MapManager存在, nearestTile={nearestTile?.name ?? "null"}, 请求位置={placePosition}");
            }
            else
            {
                Debug.LogError("[放置验证] MapManager.Instance 为 null！");
            }
            
            bool isPlatform = nearestTile != null && PlatformManager.IsPlatform(nearestTile);
            bool isValidPlacement = nearestTile != null && nearestTile.isPlaceable && !nearestTile.isOccupied;
            
            if (!isValidPlacement)
            {
                if (nearestTile == null)
                    Debug.LogError($"[放置验证] 失败: 未找到对应地块, 位置={placePosition}");
                else
                    Debug.LogError($"[放置验证] 失败: tile={nearestTile.name}, isPlaceable={nearestTile.isPlaceable}, isOccupied={nearestTile.isOccupied}");
            }

            if (isValidPlacement)
            {
                float yOffset = isPlatform ? PlatformManager.GetPlatformHeight() : 0.5f;
                Vector3 spawnPos = placePosition + Vector3.up * yOffset;
                
                // 对于非高台，使用NavMesh定位
                if (!isPlatform)
                {
                    NavMeshHit hitt;
                    if (NavMesh.SamplePosition(placePosition, out hitt, 1.0f, NavMesh.AllAreas))
                    {
                        spawnPos = hitt.position;
                    }
                }
                
                // 服务器统一用默认朝向生成，旋转由客户端根据阵营视角处理
                GameObject newUnit = UnitPoolManager.Instance != null
                    ? UnitPoolManager.Instance.GetNetworkUnit(unitPrefab, spawnPos, Quaternion.identity)
                    : Instantiate(unitPrefab, spawnPos, Quaternion.identity);
                
                SoldierController sc = newUnit.GetComponent<SoldierController>();
                if (sc != null)
                {
                    sc.teamIndex = isPlayer2 ? 1 : 0;
                    if (UnitManager.Instance != null)
                        UnitManager.Instance.RegisterUnit(newUnit, sc.teamIndex);
                }

                // ★ 位置同步：添加 NetworkTransformReliable，关闭旋转同步（旋转由客户端根据阵营视角处理）
                NetworkTransformBase existingNT = newUnit.GetComponent<NetworkTransformBase>();
                if (existingNT != null)
                {
                    existingNT.syncRotation = false;
                }
                else
                {
                    var nt = newUnit.AddComponent<NetworkTransformReliable>();
                    nt.syncRotation = false;
                }
                
                // 使用 Mirror 网络生成，同步到所有客户端
                if (newUnit.GetComponent<NetworkIdentity>() != null)
                {
                    NetworkServer.Spawn(newUnit);
                }
                else
                {
                    Debug.LogWarning($"单位预制体 {unitPrefab.name} 缺少 NetworkIdentity 组件，无法网络同步！");
                }

                // 服务器直接扣除该玩家的圣水
                if (playerElixir != null)
                    playerElixir.ServerTrySpend(requiredCost);
                else if (NetworkGameState.Instance != null)
                    NetworkGameState.Instance.TrySpendElixir(requiredCost);

                if (isPlatform)
                {
                    PlatformManager.ServerPlaceOnPlatform(newUnit, nearestTile.gameObject);
                }

                Debug.Log($"[放置] 玩家{(isPlayer2 ? 2 : 1)} 放置单位 {unitPrefab.name}, teamIndex={sc?.teamIndex}");
                OnPlacementComplete(connectionToClient, unitIndex, positionIndex, newUnit);
            }
        }
        else
        {
            TargetShowNotEnoughElixir(connectionToClient, requiredCost);
        }
    }

    [TargetRpc]
    private void TargetShowNotEnoughElixir(NetworkConnection target, float cost)
    {
        CostControler costControler = GetComponent<CostControler>();
        costControler?.ShowNotEnoughElixir(cost);
        //CancelPlacementLocal();  // 资源不足时清除本地预览
    }

    [Server]
    private void OnPlacementComplete(NetworkConnectionToClient conn, int unitIndex, int positionIndex, GameObject placedSoldier)
    {
        uint netId = placedSoldier != null && placedSoldier.TryGetComponent<NetworkIdentity>(out var ni) ? ni.netId : 0;
        TargetOnPlacementComplete(conn, unitIndex, positionIndex, placedSoldier, netId);
    }

    [TargetRpc]
    private void TargetOnPlacementComplete(NetworkConnection conn, int unitIndex, int positionIndex, GameObject placedSoldier, uint placedSoldierNetId)
    {
        _localIsPlacing = false;
        _localUnitIndex = -1;
        _localPositionIndex = -1;
        if (placementPreview != null)
        {
            var prefab = unitIndex >= 0 ? GetUnitPrefabByIndex(unitIndex) : null;
            if (UnitPoolManager.Instance != null && prefab != null)
                UnitPoolManager.Instance.ReturnPreview(placementPreview, prefab);
            else
                Destroy(placementPreview);
            placementPreview = null;
        }
        if (uiManager != null)
            uiManager.OnUnitPlaced(unitIndex, positionIndex, placedSoldier, placedSoldierNetId);
    }

    // 获取兵种索引通过预制体
    private int GetUnitIndexByPrefab(GameObject prefab)
    {
        for (int i = 0; i < allUnits.Length; i++)
        {
            if (allUnits[i].unitPrefab == prefab)
            {
                return i;
            }
        }
        return -1;
    }

    // 递归设置所有Renderer的透明度
    private void SetRendererTransparent(GameObject obj, float alpha)
    {
        //Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        //foreach (Renderer renderer in renderers)
        //{
        //    Color color = renderer.material.color;
        //    color.a = alpha;
        //    renderer.material.color = color;
        //}
    }

}
