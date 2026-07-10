using UnityEngine;

/// <summary>
/// 移动端摇杆/键盘输入，取消单例。由 RoomPlayerControler 通过引用获取，确保每个玩家只使用自己的输入源。
/// </summary>
public class MobileInput : MonoBehaviour
{
    public VirtualJoystick movementJoystick; // 拖拽摇杆

    void Awake()
    {
        // 自动检测是否在移动平台
#if UNITY_ANDROID || UNITY_IOS
        // 显示摇杆
        movementJoystick.gameObject.SetActive(true);
#else
        // 隐藏摇杆（使用键盘）
        movementJoystick.gameObject.SetActive(false);
#endif
    }

    // 获取水平移动值
    public float GetHorizontal()
    {
#if UNITY_ANDROID || UNITY_IOS
        return movementJoystick.Horizontal();
#else
        return Input.GetAxis("Horizontal");
#endif
    }

    // 获取垂直移动值
    public float GetVertical()
    {
#if UNITY_ANDROID || UNITY_IOS
        return movementJoystick.Vertical();
#else
        return Input.GetAxis("Vertical");
#endif
    }

    // 获取移动方向向量
    public Vector2 GetMovementVector()
    {
        return new Vector2(GetHorizontal(), GetVertical()).normalized;
    }
}