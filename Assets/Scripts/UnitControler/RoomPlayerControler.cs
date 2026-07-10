using UnityEngine;
using Mirror;

/// <summary>
/// RoomPlayer 移动控制，仅在有权威的客户端执行。通过轮盘/键盘等输入移动，NetworkTransform 同步位置。
/// </summary>
public class RoomPlayerControler : NetworkBehaviour
{
    [SerializeField] float moveSpeed = 5f;

    [Header("XZ 移动范围（留空则不限制）")]
    [Tooltip("X 轴最小值，不限制则设为 float.MinValue")]
    [SerializeField] float boundsMinX = -20f;
    [Tooltip("X 轴最大值")]
    [SerializeField] float boundsMaxX = 20f;
    [Tooltip("Z 轴最小值")]
    [SerializeField] float boundsMinZ = -20f;
    [Tooltip("Z 轴最大值")]
    [SerializeField] float boundsMaxZ = 20f;

    [SerializeField] bool useBounds = true;

    [Header("输入源（不填则自动查找，仅本地玩家使用）")]
    [Tooltip("摇杆/键盘输入，每个玩家独立引用，避免相互干扰")]
    [SerializeField] MobileInput mobileInput;

    /// <summary>
    /// 移动状态 1 ：idle
    /// 2 ： walk
    /// </summary>
    [SerializeField] int state = 1;
    public Role role;

    private void Start()
    {
        role = GetComponentInChildren<Role>();
        if (mobileInput == null) mobileInput = FindObjectOfType<MobileInput>();
    }

    void Update()
    {
        var ni = GetComponent<NetworkIdentity>();
        if (ni == null || !ni.isOwned) return;

        // 仅本地玩家处理输入，确保两个玩家的摇杆不会相互干扰
        float h = mobileInput != null ? mobileInput.GetHorizontal() : Input.GetAxis("Horizontal");
        float v = mobileInput != null ? mobileInput.GetVertical() : Input.GetAxis("Vertical");
        Vector3 move = new Vector3(h, 0, v).normalized * moveSpeed * Time.deltaTime;
        if (move.sqrMagnitude > 0.0001f)
        {
            Vector3 newPos = transform.position + move;
            if (useBounds)
            {
                newPos.x = Mathf.Clamp(newPos.x, boundsMinX, boundsMaxX);
                newPos.z = Mathf.Clamp(newPos.z, boundsMinZ, boundsMaxZ);
            }
            transform.position = newPos;
            if (state != 2)
            {
                state = 2;
                CmdSetState(2);
            }
        }
        else
        {
            if (state != 1)
            {
                state = 1;
                CmdSetState(1);
            }
        }
        if (Input.GetKeyDown(KeyCode.J))
        {
            CmdAttack();
        }
    }

    [Command]
    private void CmdSetState(int newState)
    {
        state = newState;
        RpcSetState(newState);
    }

    [Command]
    private void CmdAttack()
    {
        RpcAttackMessage();
    }

    [ClientRpc]
    private void RpcSetState(int newState)
    {
        state = newState;
        if (role != null)
        {
            if (newState == 2) role.SendMessage("onMove", SendMessageOptions.DontRequireReceiver);
            else role.SendMessage("onIdle", SendMessageOptions.DontRequireReceiver);
        }
    }

    [ClientRpc]
    private void RpcAttackMessage()
    {
        if (role != null)
            role.SendMessage("onAttackF", SendMessageOptions.DontRequireReceiver);
    }
}
