using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QuickSetCubehighlight : MonoBehaviour
{
    private void Awake()
    {
        SetupAllGroundTiles();
    }
    [ContextMenu("快速设置地面")]
    void SetupAllGroundTiles()
    {
        // 查找所有地面Cube
        Transform[] allChildren = GetComponentsInChildren<Transform>();

        foreach (Transform child in allChildren)
        {
            //// 跳过非Cube物体
            //if (!child.name.Contains("Cube")) continue;

            //// 1. 确保有BoxCollider
            //BoxCollider collider = child.GetComponent<BoxCollider>();
            //if (collider == null)
            //{
            //    collider = child.gameObject.AddComponent<BoxCollider>();
            //}

            // 2. 添加GroundTile脚本
            CubesHighLight tileScript = child.GetComponent<CubesHighLight>();
            Renderer renderer = child.GetComponent<Renderer>();
            if (renderer != null )//&& !child.CompareTag("cantplace")
            {
                tileScript = child.gameObject.AddComponent<CubesHighLight>();
            }

            //// 3. 设置Layer为Ground
            //child.gameObject.layer = LayerMask.NameToLayer("Ground");

            //// 4. 设置标签
            //child.gameObject.tag = "GroundTile";
        }

        Debug.Log($"已设置 {allChildren.Length} 个地面瓦片");
    }
}
