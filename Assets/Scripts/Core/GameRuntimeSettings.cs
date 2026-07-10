using UnityEngine;

/// <summary>
/// 游戏运行时设置，在最早时机执行。
/// 解决：编辑器中 Play Focused 小窗口、Android 打包后，游戏失去焦点时暂停导致干员几乎不移动的问题。
/// </summary>
public static class GameRuntimeSettings
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void OnBeforeSceneLoad()
    {
        Application.runInBackground = true;
        Application.targetFrameRate = 60;
        var go = new GameObject("GameRuntimeSettings");
        go.AddComponent<FrameRateKeeper>();
        Object.DontDestroyOnLoad(go);
    }
}

/// <summary>
/// 每帧强制保持 60 帧，防止被其他逻辑覆盖。
/// </summary>
public class FrameRateKeeper : MonoBehaviour
{
    void LateUpdate()
    {
        Application.targetFrameRate = 60;
    }
}
