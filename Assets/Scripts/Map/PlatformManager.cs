﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿using UnityEngine;
using Mirror;

public class PlatformManager : NetworkBehaviour
{
    [Header("高台配置")]
    public static float platformHeight = 1.5f;
    public static float attackBonus = 1.5f; // 高台上的攻击力加成倍数

    private void Awake()
    {
        // 非单例模式，不需要设置静态实例
    }
    
    // 检查地块是否为高台
    public static bool IsPlatform(CubesHighLight tile)
    {
        return tile != null && (tile.gameObject.CompareTag("MyTower")|| tile.gameObject.CompareTag("EnemyTower"));
    }
    
    public static float GetPlatformHeight()
    {
        return platformHeight;
    }
    
    // 在高台上放置干员（服务器端，由 CmdTryPlaceAtPosition 直接调用）
    [Server]
    public static void ServerPlaceOnPlatform(GameObject unit, GameObject platform)
    {
        CubesHighLight platformTile = platform.GetComponent<CubesHighLight>();
        if (!IsPlatform(platformTile)) return;
        
        platformTile.Occupy(unit);
        
        SoldierController unitController = unit.GetComponent<SoldierController>();
        if (unitController != null)
        {
            unitController.canMove = false;
            unitController.attackDamage *= attackBonus;
            unitController.platformTile = platformTile;
            unitController.attackRange += 1.5f;

            // 高台逻辑：干员血量和防御等量增加给防御塔
            SoldierController tower = platform.GetComponent<SoldierController>();
            if (tower != null && tower.isTower)
            {
                float addHealth = unitController.maxHealth;
                float addDefense = unitController.defense;
                tower.maxHealth += addHealth;
                tower.currentHealth += addHealth;
                tower.defense += addDefense;
                unitController.platformBonusHealth = addHealth;
                unitController.platformBonusDefense = addDefense;
            }
            
            Collider collider = unit.GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }
            
            Collider[] childColliders = unit.GetComponentsInChildren<Collider>();
            foreach (Collider childCollider in childColliders)
            {
                childCollider.isTrigger = true;
            }
            
            PlatformManager instance = FindObjectOfType<PlatformManager>();
            if (instance != null)
            {
                instance.RpcSyncPlatformOccupied(platformTile.gameObject, unit);
            }
        }
    }
    
    // 从高台上释放干员（服务器端，静态方法）
    [Server]
    public static void ServerReleaseFromPlatform(SoldierController unitController)
    {
        if (unitController.platformTile != null)
        {
            // 高台逻辑：从防御塔回退干员贡献的血量和防御
            SoldierController tower = unitController.platformTile.GetComponent<SoldierController>();
            if (tower != null && tower.isTower && (unitController.platformBonusHealth > 0 || unitController.platformBonusDefense > 0))
            {
                tower.maxHealth -= unitController.platformBonusHealth;
                tower.currentHealth = Mathf.Clamp(tower.currentHealth - unitController.platformBonusHealth, 0f, tower.maxHealth);
                tower.defense -= unitController.platformBonusDefense;
                unitController.platformBonusHealth = 0;
                unitController.platformBonusDefense = 0;
            }

            // 释放高台占用
            unitController.platformTile.Release();
            
            // 向客户端同步高台释放情况
            // 由于是静态方法，需要找到一个PlatformManager实例来调用ClientRpc
            PlatformManager instance = FindObjectOfType<PlatformManager>();
            if (instance != null)
            {
                instance.RpcSyncPlatformReleased(unitController.platformTile.gameObject);
            }
            
            unitController.platformTile = null;
        }
    }
    
    // 客户端同步高台占用情况
    [ClientRpc]
    private void RpcSyncPlatformOccupied(GameObject platformTileObj, GameObject unitObj)
    {
        // 查找对应的CubesHighLight组件
        CubesHighLight platformTile = platformTileObj.GetComponent<CubesHighLight>();
        if (platformTile != null)
        {
            platformTile.Occupy(unitObj);
        }
    }
    
    // 客户端同步高台释放情况
    [ClientRpc]
    private void RpcSyncPlatformReleased(GameObject platformTileObj)
    {
        // 查找对应的CubesHighLight组件
        CubesHighLight platformTile = platformTileObj.GetComponent<CubesHighLight>();
        if (platformTile != null)
        {
            platformTile.Release();
        }
    }
}