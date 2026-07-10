using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class VirtualJoystick : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("UI组件")]
    public RectTransform background;  // 摇杆背景
    public RectTransform handle;      // 摇杆手柄

    [Header("摇杆参数")]
    public float handleRange = 1f;    // 手柄移动范围（相对于背景半径）
    public float deadZone = 0.1f;     // 死区，避免微小抖动

    [Header("输出值")]
    public Vector2 inputVector = Vector2.zero;  // -1 到 1 的向量

    private Vector2 joystickCenter;   // 摇杆中心点
    private float bgRadius;           // 背景半径

    void Start()
    {
        // 初始化摇杆中心
        joystickCenter = background.position;
        // 计算背景半径（假设背景是圆形）
        bgRadius = background.rect.width * 0.5f;
    }

    // 当手指按下时
    public void OnPointerDown(PointerEventData eventData)
    {
        OnDrag(eventData);
    }

    // 当手指拖动时
    public void OnDrag(PointerEventData eventData)
    {
        // 计算手指相对于摇杆中心的位置
        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            background, eventData.position, eventData.pressEventCamera, out localPos);

        // 转换为方向向量（归一化）
        Vector2 direction = localPos.normalized;

        // 计算距离（限制在背景半径内）
        float distance = Mathf.Clamp(localPos.magnitude, 0, bgRadius);

        // 计算摇杆向量
        inputVector = direction * (distance / bgRadius);

        // 应用死区（小于死区时归零）
        if (inputVector.magnitude < deadZone)
        {
            inputVector = Vector2.zero;
        }

        // 移动手柄
        handle.anchoredPosition = inputVector * bgRadius * handleRange;
    }

    // 当手指抬起时
    public void OnPointerUp(PointerEventData eventData)
    {
        // 重置摇杆
        inputVector = Vector2.zero;
        handle.anchoredPosition = Vector2.zero;
    }

    // 获取水平轴的值（-1 到 1）
    public float Horizontal()
    {
        return inputVector.x;
    }

    // 获取垂直轴的值（-1 到 1）
    public float Vertical()
    {
        return inputVector.y;
    }
}