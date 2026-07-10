using Mirror;
using UnityEditor;
using UnityEngine;
using static PlayerFaction;

/// <summary>
/// 1v1 每位玩家独立的圣水，挂在 Player 预制体上。
/// 服务器权威：恢复和扣除都在服务器执行，SyncVar 自动同步到客户端。
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
public class PlayerElixir : NetworkBehaviour
{
    [Header("阵营")]
    [SyncVar(hook = nameof(OnPlayerIndexChanged))]
    public int playerIndex = -1; // 0=玩家1(主机), 1=玩家2(客户端)

    public bool IsPlayer2 => playerIndex == 1;

    [Header("圣水属性")]
    [SyncVar(hook = nameof(OnElixirChanged))]
    public float currentElixir = 2f;

    [SyncVar]
    public float maxElixir = 10f;

    [SyncVar]
    public float elixirRegenRate = 1f;

    private float _regenTimer;

    /// <summary>本地玩家快捷访问。只有 isLocalPlayer 的那个实例会设置。</summary>
    public static PlayerElixir LocalInstance { get; private set; }

    // ───────── 生命周期 ─────────

    public override void OnStartClient()
    {

        base.OnStartClient();
        Debug.Log($"[PlayerElixir] OnStartClient: netId={netId}, isLocalPlayer={isLocalPlayer}, playerIndex={playerIndex}");
    }

    public override void OnStartLocalPlayer()
    {
        LocalInstance = this;
        Debug.Log($"[PlayerElixir] ★ 本地玩家启动, netId={netId}, playerIndex={playerIndex}");

        // playerIndex 可能已经通过初始状态同步到了，也可能还是 -1（等 SyncVar hook 触发）
        if (IsPlayer2)
        {
            SetupPlayer2View();
        }
    }

    /// <summary>SyncVar hook: playerIndex 从服务器同步到客户端后触发</summary>
    private void OnPlayerIndexChanged(int oldVal, int newVal)
    {
        Debug.Log($"[PlayerElixir] ★ playerIndex 同步: {oldVal} → {newVal}, isLocalPlayer={isLocalPlayer}");
        if (isLocalPlayer && newVal == 1)
        {
            SetupPlayer2View();
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        currentElixir = 2f;
        _regenTimer = 0f;

        // Host（服务器端玩家）永远是玩家1，远程客户端是玩家2
        playerIndex = (connectionToClient == NetworkServer.localConnection) ? 0 : 1;
        Debug.Log($"[PlayerElixir] OnStartServer: netId={netId}, playerIndex={playerIndex}, connId={connectionToClient?.connectionId}");
    }

    void OnDestroy()
    {
        if (LocalInstance == this)
            LocalInstance = null;
    }

    // ───────── 玩家2视角设置 ─────────

    private void SetupPlayer2View()
    {
        // 摄像头：position.z=30, rotation.y=180
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 pos = cam.transform.position;
            pos.z = 30f;
            cam.transform.position = pos;

            Vector3 rot = cam.transform.eulerAngles;
            rot.y = 180f;
            cam.transform.eulerAngles = rot;
            Debug.Log($"[PlayerElixir] 玩家2摄像头已设置: pos.z=30, rot.y=180");
        }

        // ray扫描层：在原有图层（含高台）基础上追加 enemycubes，确保高台也能被射线命中
        NetworkUnitPlacer placer = GetComponent<NetworkUnitPlacer>();
        if (placer != null)
        {
            placer.groundLayer = LayerMask.GetMask("enemycubes");
            Debug.Log($"[PlayerElixir] 玩家2: groundLayer改为enemycubes, 最终mask={placer.groundLayer.value}");
        }
    }

    // ───────── 圣水恢复（仅服务器） ─────────

    [ServerCallback]
    void Update()
    {
        if (NetworkGameState.Instance == null || !NetworkGameState.Instance.isGameStarted) return;
        if (currentElixir >= maxElixir) return;

        float rate = elixirRegenRate * 0.8f;
        if (NetworkGameState.Instance != null && NetworkGameState.Instance.isDoubleElixir)
            rate = elixirRegenRate * 1.6f;

        _regenTimer += rate * Time.deltaTime;
        if (_regenTimer >= 0.1f)
        {
            currentElixir = Mathf.Min(currentElixir + _regenTimer, maxElixir);
            _regenTimer = 0f;
        }
    }

    // ───────── 扣费：服务器直接调用 ─────────

    /// <summary>
    /// 服务器端直接扣除圣水。在 Command 处理函数内调用。
    /// </summary>
    [Server]
    public bool ServerTrySpend(float amount)
    {
        if (currentElixir >= amount)
        {
            currentElixir -= amount;
            Debug.Log($"[Server] 玩家 netId={netId} 消耗 {amount} 圣水, 剩余 {currentElixir:F1}");
            return true;
        }
        Debug.Log($"[Server] 玩家 netId={netId} 圣水不足, 需要 {amount} 当前 {currentElixir:F1}");
        return false;
    }

    // ───────── 扣费：客户端发起 ─────────

    /// <summary>本地玩家调用，先本地检查再发 Command。</summary>
    public bool SpendElixir(float amount)
    {
        if (!isLocalPlayer) return false;
        if (currentElixir < amount) return false;
        CmdSpendElixir(amount);
        return true;
    }

    [Command]
    private void CmdSpendElixir(float amount)
    {
        ServerTrySpend(amount);
    }

    // ───────── SyncVar Hook ─────────

    private void OnElixirChanged(float oldVal, float newVal)
    {
        //Debug.Log($"[PlayerElixir] OnElixirChanged: netId={netId}, isLocalPlayer={isLocalPlayer}, {oldVal:F1} → {newVal:F1}");
        
        // 只更新本地玩家的 UI
        if (!isLocalPlayer) return;

        // 通知 CostControler 刷新显示
        CostControler costCtrl = GetComponent<CostControler>();
        if (costCtrl != null)
            costCtrl.currentElixir = newVal;
    }
}
