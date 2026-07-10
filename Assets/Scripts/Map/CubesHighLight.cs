﻿﻿﻿﻿using UnityEngine;

public class CubesHighLight : MonoBehaviour
{
    [Header("地砖信息")]
    public Vector2Int gridPosition;    // 网格坐标
    public TileType tileType = TileType.Normal;
    public bool isPlaceable = true;    // 是否可以放置
    public bool isWalkable = true;     // 是否可以行走
    public bool isTower = false;    // 是否为防御塔
    public bool isOccupied = false;    // 是否被占用

    [Header("高亮效果")]
    public Material normalMaterial;
    public Material hoverMaterial;
    public Material blockedMaterial;

    [Header("状态")]
    public GameObject occupiedUnit;    // 当前占用的单位
    private Renderer tileRenderer;
    private Material originalMaterial;

    public enum TileType
    {
        Normal,     
        Path,      
        River,      
        Bridge,
        Spawn     
    }

    void Awake()
    {
        tileRenderer = GetComponent<Renderer>();
        if (tileRenderer != null)
        {
            originalMaterial = tileRenderer.material;
        }

        // 根据标签初始化
        InitializeTile();
    }

    void InitializeTile()
    {
        // 优先检查是否为高台（双方高台都需要识别）
        if (gameObject.CompareTag("MyTower") || gameObject.CompareTag("EnemyTower"))
        {
            tileType = TileType.Normal;
            isPlaceable = true;
            isWalkable = false;
            isTower = true;
            return;
        }

        if (gameObject.CompareTag("bridge"))
        {
            tileType = TileType.Bridge;
        }
        if(gameObject.CompareTag("cantplace"))
        {
            tileType = TileType.Path;
        }
        if (gameObject.CompareTag("River") )
        {
            tileType = TileType.River;
        }
        switch (tileType)
        {
            case TileType.Normal:
                isPlaceable = true;
                isWalkable = true;
                break;

            case TileType.Bridge:
                isPlaceable = false;
                isWalkable = true;
                break;

            case TileType.Path:
                isPlaceable = false;
                isWalkable = false;
                isTower = false;
                break;

            case TileType.River:
                isPlaceable = false;
                isWalkable = false;
                break;

            case TileType.Spawn:
                isPlaceable = false;  
                isWalkable = true;
                break;

        }
    }

    public void Occupy(GameObject unit)
    {
        isOccupied = true;
        occupiedUnit = unit;

        // �Ӿ�����
        if (tileRenderer != null && blockedMaterial != null)
        {
            tileRenderer.material = blockedMaterial;
        }
    }

    // �ͷ���Ƭ
    public void Release()
    {
        isOccupied = false;
        occupiedUnit = null;

        // �ָ�����
        if (tileRenderer != null)
        {
            tileRenderer.material = originalMaterial;
        }
    }

    // ��ȡ��Ƭ���ĵ㣨���ǵ�λվ���߶ȣ�
    public Vector3 GetSpawnPosition()
    {
        return transform.position + Vector3.up * 0.5f; // �ӵ�������0.5��λ
    }
}