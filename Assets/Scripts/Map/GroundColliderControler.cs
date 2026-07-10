// GroundColliderManager.cs
using UnityEngine;

public class GroundColliderManager : MonoBehaviour
{
    void Start()
    {
        // 创建一个大碰撞器覆盖所有地面
        BoxCollider mainCollider = gameObject.AddComponent<BoxCollider>();

        // 计算所有子物体的总包围盒
        Renderer[] childRenderers = GetComponentsInChildren<Renderer>();
        Bounds totalBounds = childRenderers[0].bounds;
        foreach (Renderer r in childRenderers)
        {
            totalBounds.Encapsulate(r.bounds);
        }

        // 设置大碰撞器
        mainCollider.center = totalBounds.center - transform.position;
        mainCollider.size = totalBounds.size;

        // 禁用所有子物体的碰撞器
        foreach (Collider col in GetComponentsInChildren<Collider>())
        {
            if (col.gameObject != this.gameObject)
                col.enabled = false;
        }
    }
}