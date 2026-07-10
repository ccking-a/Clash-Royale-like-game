using UnityEngine;
using UnityEditor;

public class DebugHierarchy : MonoBehaviour
{
    [ContextMenu("打印所有对象")]
    public void PrintAllObjects()
    {
        var allObjects = FindObjectsOfType<GameObject>(true);
        Debug.Log($"场景中共有 {allObjects.Length} 个对象");

        foreach (var obj in allObjects)
        {
            if (obj.name.Contains("NetWorkManager"))
            {
                Debug.LogError($"找到NetWorkManager");
            }
            Debug.Log($"找到" + obj.name);
        }
    }

    string GetFullPath(Transform trans)
    {
        string path = trans.name;
        while (trans.parent != null)
        {
            trans = trans.parent;
            path = trans.name + "/" + path;
        }
        return path;
    }
}