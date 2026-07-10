using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using static UnitManager;
using System;
using UnityEngine.SceneManagement;
/// <summary>
/// 网络游戏状态 - 同步资源、游戏时间等核心数据到所有客户端
/// 需挂载在带有 NetworkIdentity 的场景对象或预制体上
/// </summary>
public class NetworkGameState : NetworkBehaviour
{
    public static NetworkGameState Instance { get; private set; }

    public float currentElixir = 2f;
    [SyncVar]
    public float maxElixir = 10f;
    [SyncVar]
    public float elixirRegenRate = 1f;
    [SyncVar]
    public bool isDoubleElixir = false;

    [Header("游戏时间")]
    [SyncVar(hook = nameof(OnGameTimeChanged))]
    public float gameTime = 0f;

    [SyncVar(hook = nameof(OnCountdownChanged))]
    public float gameCountdown = -1f;

    [SyncVar(hook = nameof(OnGameStartedChanged))]
    public bool isGameStarted = false;

    private float DoubleTime = 120f;
    private float EndTime = 240f;
    private float _soloWaitTimer = 0f;

    [SyncVar]
    public bool isGameEnded = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    [ServerCallback]
    private void Update()
    {
        if (isGameEnded) return;

        if (!isGameStarted)
        {
            if (gameCountdown >= 0f)
            {
                gameCountdown -= Time.deltaTime;
                if (gameCountdown <= 0f)
                {
                    gameCountdown = 0f;
                    isGameStarted = true;
                }
            }
            else
            {
                int gamePlayerCount = 0;
                foreach (var kv in NetworkServer.spawned)
                {
                    if (kv.Value.GetComponent<PlayerElixir>() != null)
                        gamePlayerCount++;
                }
                bool shouldStart = gamePlayerCount >= 2;
                if (!shouldStart && gamePlayerCount >= 1)
                {
                    _soloWaitTimer += Time.deltaTime;
                    if (_soloWaitTimer >= 5f) shouldStart = true;
                }
                if (shouldStart)
                    gameCountdown = 3f;
            }
        }
        else
        {
            gameTime += Time.deltaTime;
            if (gameTime > DoubleTime && !isDoubleElixir)
                SetDoubleElixir(true);
            if (gameTime > EndTime)
            {
                SetGameEnded(true);
                GameEnd();
                RpcGameEnd();
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        currentElixir = 2f;
        gameTime = 0f;
        gameCountdown = -1f;
        isGameStarted = false;
        isGameEnded = false;
        isDoubleElixir = false;
    }

    [Server]
    public void AddElixir(float amount)
    {
        currentElixir = Mathf.Clamp(currentElixir + amount, 0, maxElixir);
    }

    [Server]
    public bool TrySpendElixir(float amount)
    {
        if (currentElixir >= amount)
        {
            currentElixir -= amount;
            return true;
        }
        return false;
    }

    [Server]
    public void SetGameEnded(bool ended)
    {
        isGameEnded = ended;
    }


    [Server]
    public void SetDoubleElixir(bool doubled)
    {
        isDoubleElixir = doubled;
    }


    [ClientRpc]
    public void RpcGameEnd()
    {
        GameEnd();
    }


    private void OnGameTimeChanged(float oldVal, float newVal)
    {
        foreach (var cost in FindObjectsOfType<CostControler>())
            cost.gameTime = gameTime;
    }

    private void OnCountdownChanged(float oldVal, float newVal) { }

    private void OnGameStartedChanged(bool oldVal, bool newVal) { }

    [Header("游戏结束")]
    [Tooltip("返回的场景名（如 RoomScene、StartScene）")]
    public string gameEndReturnScene = "RoomScene";
    [Tooltip("镜头缩放持续时间")]
    public float gameEndCameraZoomDuration = 3f;
    [Tooltip("镜头缩放后延迟调用单位 Die 的等待时间")]
    public float gameEndDieDelay = 0.5f;
    [Tooltip("单位 Die 后到切场景的等待时间")]
    public float gameEndSceneDelay = 1f;

    public void GameEnd()
    {
        List<UnitInfo> allunits = UnitManager.Instance != null ? UnitManager.Instance.GetAllUnits() : new List<UnitInfo>();
        foreach (UnitInfo unitInfo in allunits)
        {
            unitInfo.unitController.canMove = false;
            unitInfo.unitController.canAttack = false;
            unitInfo.unitController.isAlive = false;
            Rigidbody rb = unitInfo.unitObject.GetComponent<Rigidbody>();
            if (rb != null) rb.velocity = Vector3.zero;
        }
        Debug.Log("游戏结束");

        CostControler[] cost = FindObjectsOfType<CostControler>();
        foreach (CostControler costControler in cost)
            costControler.OnGameEnd();

        AudioManager[] audioManagers = FindObjectsOfType<AudioManager>();
        foreach (AudioManager audio in audioManagers)
            audio.StopAllMusic();

        // 镜头缩放
        FixedWidthCamera[] fixedWidthCameras = FindObjectsOfType<FixedWidthCamera>();
        foreach (FixedWidthCamera camera in fixedWidthCameras)
            camera.SetTargetWidth(22f, gameEndCameraZoomDuration);

        Canvas[] allCanvases = FindObjectsOfType<Canvas>();
        foreach (Canvas canvas in allCanvases)
            canvas.enabled = false;

        StartCoroutine(GameEndSequence());
    }

    private IEnumerator GameEndSequence()
    {
        // 1. 等待镜头缩放完成
        yield return new WaitForSeconds(gameEndCameraZoomDuration);

        // 2. 服务器延迟调用所有单位 Die（统一清理血条、技能按钮、回收等）
        if (NetworkServer.active && UnitManager.Instance != null)
        {
            yield return new WaitForSeconds(gameEndDieDelay);
            List<UnitInfo> allUnits = UnitManager.Instance.GetAllUnits();
            foreach (UnitInfo ui in allUnits)
            {
                var sc = ui.unitController as SoldierController;
                if (sc != null)
                    sc.Die(fromGameEndCleanup: true);
            }
        }

        // 3. 等待单位 Die 处理完成
        yield return new WaitForSeconds(gameEndSceneDelay);

        // 4. 切断网络连接并返回 RoomScene
        var nm = NetworkManager.singleton;
        if (nm != null)
        {
            if (NetworkServer.active && NetworkClient.active)
                nm.StopHost();
            else if (NetworkClient.active)
                nm.StopClient();
        }
        Debug.Log($"游戏结束，已断开连接，返回 {gameEndReturnScene}");
        SceneManager.LoadScene(string.IsNullOrEmpty(gameEndReturnScene) ? "RoomScene" : gameEndReturnScene);
    }
}


