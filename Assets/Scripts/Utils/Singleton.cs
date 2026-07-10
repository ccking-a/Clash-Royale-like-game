using UnityEngine;

/// <summary>
/// 单例模式基类，用于创建全局唯一实例的 MonoBehaviour 类
/// </summary>
/// <typeparam name="T">单例类型</typeparam>
public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    // 单例实例
    private static T _instance;
    
    // 锁对象，用于线程安全
    private static readonly object _lock = new object();
    
    // 表示是否已被销毁的标志
    private static bool _applicationIsQuitting = false;
    
    /// <summary>
    /// 获取单例实例
    /// </summary>
    public static T Instance
    {
        get
        {
            // 线程安全的单例实现
            lock (_lock)
            {
                if (_instance == null)
                {
                    // 重置应用程序退出标志，确保场景重新加载时单例能正确创建
                    _applicationIsQuitting = false;
                    
                    // 查找场景中是否已存在该类型的实例
                    _instance = FindObjectOfType<T>();
                    
                    // 如果场景中不存在该实例，创建一个新的游戏对象并添加组件
                    if (_instance == null)
                    {
                        GameObject singletonObject = new GameObject(typeof(T).Name);
                        _instance = singletonObject.AddComponent<T>();
                    }
                }
                
                return _instance;
            }
        }
    }
    
    /// <summary>
    /// 初始化单例实例
    /// </summary>
    protected virtual void Awake()
    {
        // 重置应用程序退出标志，确保场景重新加载时单例能正确创建
        _applicationIsQuitting = false;
        
        // 防止重复创建实例
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        // 如果实例不存在，设置为当前实例
        if (_instance == null)
        {
            _instance = this as T;
        }
    }
    
    /// <summary>
    /// 应用程序退出时调用
    /// </summary>
    protected virtual void OnApplicationQuit()
    {
        _applicationIsQuitting = true;
    }
    
    /// <summary>
    /// 对象销毁时调用
    /// </summary>
    protected virtual void OnDestroy()
    {
        // 只有当销毁的是当前实例时，才重置_instance
        if (_instance == this)
        {
            _instance = null;
            _applicationIsQuitting = false;
        }
    }
}