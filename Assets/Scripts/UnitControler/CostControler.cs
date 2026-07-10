// ElixirManager.cs
using UnityEngine;
using UnityEngine.UI;
using Mirror;


public class CostControler : NetworkBehaviour
{
    [Header("圣水设置")]
    public float maxElixir = 10f;           // 最大圣水量
    public float currentElixir = 2f;        // 当前圣水量（开局5点）
    public float elixirRegenRate = 1f;    // 每秒恢复量（1点/秒）
    public float doubleElixirTime = 120f;   // 双倍圣水开始时间（秒）

    [Header("UI引用")]
    public Image elixirBar;                // 圣水条Slider
    public Text elixirText;                 // 圣水数值Text
    public Image elixirBarFill;             // 圣水条填充Image
    public GameObject elixirParticle;       // 圣水恢复粒子效果
    public Text UIgameTime;                 // 游戏时间Text
    public GameObject notEnoughElixirPanel; // 圣水不足提示面板
    public Text notEnoughElixirText;        // 圣水不足提示文本

    [Header("颜色设置")]
    public Color normalColor = Color.magenta;   // 普通圣水颜色
    public Color doubleColor = Color.cyan;      // 双倍圣水颜色

    [Header("状态")]
    private bool isDoubleElixir = false;    // 是否双倍圣水
    public float gameTime = 0f;            // 游戏进行时间
    private float lastElixirUpdate = 0f;    // 上次圣水更新时间
    private bool isGameEnded = false;       // 游戏是否已结束

    [Header("数据")]
    private float originalsize;

    public AudioManager audiow;

    public override void OnStartClient()
    {
        if (!isLocalPlayer) return;
        // 重置游戏状态
        gameTime = 0;
        currentElixir = 2f;
        isGameEnded = false;
        isDoubleElixir = false;
        isFirstMusicPlayed = false;
        isSecondMusicPlayed = false;
        lastElixirUpdate = 0f;

        // 尽早获取 AudioManager，否则 Start 中的 PlayFirstMusic 会因 audiow 为 null 而无法播放
        audiow = GetComponent<AudioManager>();
        if (audiow == null) audiow = FindObjectOfType<AudioManager>();
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        Debug.Log($"本地costcontroler初始化: netId={netId}, isLocalPlayer={isLocalPlayer}");

        // 在这里注册本地玩家的引用，便于其他地方获取
    }

    // 绑定UI组件
    public void BindUIComponents()
    {
        // 查找Canvas根对象
        GameObject canvas = GameObject.FindWithTag("Canva");
        if (canvas != null)
        {
            // 查找圣水条相关UI
            Transform panelCost = canvas.transform.Find("Panel_Cost");
            if (panelCost != null)
            {
                Transform costBarMask = panelCost.Find("CostBar");
                if (costBarMask != null)
                {
                    Transform costBarTransform = costBarMask.Find("CostMask");
                    if (costBarTransform != null)
                    {
                        elixirBar = costBarTransform.GetComponent<Image>();
                    }
                    
                    Transform costFillTransform = costBarTransform.Find("Cost");
                    if (costFillTransform != null)
                    {
                        elixirBarFill = costFillTransform.GetComponent<Image>();
                    }
                }
                
                elixirText = panelCost.GetComponentInChildren<Text>();
            }
            
            // 查找圣水不足提示面板
            notEnoughElixirPanel = canvas.transform.Find("Panel_CostNotEnough")?.gameObject;
            if (notEnoughElixirPanel != null)
            {
                notEnoughElixirText = notEnoughElixirPanel.GetComponentInChildren<Text>();
            }
            
            // 查找游戏时间UI
            Transform panelTime = canvas.transform.Find("Panel_Time");
            if (panelTime != null)
            {
                Transform timeTransform = panelTime.Find("Time");
                if (timeTransform != null)
                {
                    UIgameTime = timeTransform.GetComponentInChildren<Text>();
                }
            }
        }
        audiow = GetComponent<AudioManager>();
        originalsize = elixirBar.rectTransform.rect.width;
        // 检查是否所有必要的UI都已找到
        if (elixirBar == null)
            Debug.LogWarning("CostControler: 未找到CostBar");
        if (elixirText == null)
            Debug.LogWarning("CostControler: 未找到圣水数值文本");
        if (elixirBarFill == null)
            Debug.LogWarning("CostControler: 未找到Cost填充");
        if (UIgameTime == null)
            Debug.LogWarning("CostControler: 未找到游戏时间文本");
        if (audiow == null)
            Debug.LogWarning("CostControler: 未找到音频播放器");

    }
    


    [Header("音乐播放状态")]
    private bool isFirstMusicPlayed = false;
    private bool isSecondMusicPlayed = false;
    
    void Start()
    {
        if (!isLocalPlayer) return;
        UpdateElixirUI();
        // 第一首音乐由 AudioManager 创建后自动播放，此处不再调用
    }
    
    /// <summary>
    /// 进入倒计时时播放第一首音乐
    /// </summary>
    private void PlayFirstMusic()
    {
        if (!isFirstMusicPlayed && audiow != null)
        {
            audiow.PlayCountdownMusic();
            isFirstMusicPlayed = true;
            Debug.Log("进入倒计时，播放第一首音乐");
        }
    }
    
    /// <summary>
    /// 游戏开始时播放第二首音乐
    /// </summary>
    public void PlaySecondMusic()
    {
        if (!isSecondMusicPlayed && audiow != null)
        {
            audiow.PlayGameStartMusic();
            isSecondMusicPlayed = true;
            Debug.Log("游戏开始，播放第二首音乐");
        }
    }

    void Update()
    {
        // ★ 非本地玩家的 CostControler 不执行任何逻辑
        if (!isLocalPlayer) return;
        
        // 从本地玩家的 PlayerElixir 读圣水值（优先使用静态实例，备用 NetworkClient.localPlayer）
        PlayerElixir localPlayerElixir = PlayerElixir.LocalInstance;
        if (localPlayerElixir == null && NetworkClient.localPlayer != null)
            localPlayerElixir = NetworkClient.localPlayer.GetComponent<PlayerElixir>();
        
        if (localPlayerElixir != null)
        {
            currentElixir = localPlayerElixir.currentElixir;
        }
        
        // 从 NetworkGameState 读全局状态
        if (NetworkGameState.Instance != null)
        {
            gameTime = NetworkGameState.Instance.gameTime;
            isGameEnded = NetworkGameState.Instance.isGameEnded;
            isDoubleElixir = NetworkGameState.Instance.isDoubleElixir;
        }
        
        if (!isGameEnded)
        {
            if (UIgameTime != null)
            {
                int totalSeconds = (int)Mathf.Max(0, Mathf.CeilToInt(240 - gameTime));
                int minutes = totalSeconds / 60;
                int seconds = totalSeconds % 60;
                UIgameTime.text = string.Format("{0:00}:{1:00}", minutes, seconds);
            }
        }
        UpdateElixirUI();
    }

    /// <summary>从网络同步圣水值（由 NetworkGameState 的 SyncVar hook 调用）</summary>
    public void SyncElixirFromNetwork(float value) { currentElixir = value; }
    /// <summary>从网络同步游戏时间</summary>
    public void SyncGameTimeFromNetwork(float value) { gameTime = value; }

    // 游戏结束时调用，暂停计时并保留对局时长
    public void OnGameEnd()
    {
        isGameEnded = true;
        if (UIgameTime != null)
        {
            // 计算实际对局时长（转换为分:秒格式）
            int totalGameSeconds = (int)Mathf.Ceil(240 - gameTime);
            int gameMinutes = totalGameSeconds / 60;
            int gameSeconds = totalGameSeconds % 60;
            // 格式化输出为 MM:SS（对局时长）
            UIgameTime.text = string.Format("{0:00}:{1:00}", gameMinutes, gameSeconds);
        }
        Debug.Log("游戏结束，暂停计时，对局时长: " + string.Format("{0:00}:{1:00}", (int)Mathf.Ceil(gameTime)/60, (int)Mathf.Ceil(gameTime)%60));
    }

    // 圣水恢复
    void RegenElixir()
    {
        // 网络模式下仅服务器更新资源，客户端从 SyncVar 同步
        if (NetworkGameState.Instance != null && !NetworkServer.active)
            return;
            
        float regenAmount = isDoubleElixir ? elixirRegenRate * 0.2f : elixirRegenRate * 0.1f;
        
        if (NetworkGameState.Instance != null && NetworkServer.active)
        {
            NetworkGameState.Instance.AddElixir(regenAmount);
            currentElixir = NetworkGameState.Instance.currentElixir;
        }
        else
        {
            currentElixir += regenAmount;
            currentElixir = Mathf.Clamp(currentElixir, 0, maxElixir);
        }

        UpdateElixirUI();
    }

    // 激活双倍圣水
    void ActivateDoubleElixir()
    {
        isDoubleElixir = true;
        if (NetworkGameState.Instance != null && NetworkServer.active)
            NetworkGameState.Instance.SetDoubleElixir(true);

        if (elixirBarFill != null)
            elixirBarFill.color = doubleColor;
        Debug.Log("双倍圣水已激活！");
    }

    // 消耗圣水
    public bool TrySpendElixir(float amount)
    {
        if (currentElixir >= amount)
        {
            currentElixir -= amount;
            UpdateElixirUI();

            // 播放消耗特效
            PlaySpendEffect();

            return true;
        }
        else
        {
            // 圣水不足提示
            ShowNotEnoughElixir();
            return false;
        }
    }

    // 更新UI：1v1 只显示本地玩家的圣水
    void UpdateElixirUI()
    {
        // 统一获取本地玩家的 PlayerElixir
        PlayerElixir localPlayerElixir = PlayerElixir.LocalInstance;
        if (localPlayerElixir == null && NetworkClient.localPlayer != null)
            localPlayerElixir = NetworkClient.localPlayer.GetComponent<PlayerElixir>();
        
        float displayElixir;
        float displayMax;
        if (localPlayerElixir != null)
        {
            displayElixir = localPlayerElixir.currentElixir;
            displayMax = localPlayerElixir.maxElixir;
        }
        else if (NetworkGameState.Instance != null)
        {
            displayElixir = NetworkGameState.Instance.currentElixir;
            displayMax = NetworkGameState.Instance.maxElixir;
        }
        else
        {
            displayElixir = currentElixir;
            displayMax = maxElixir;
        }
        
        // 防止 originalsize 为 0（布局尚未完成时的安全处理）
        if (originalsize <= 0f && elixirBar != null)
            originalsize = elixirBar.rectTransform.rect.width;
        
        if (elixirBar != null && originalsize > 0f && displayMax > 0f)
            elixirBar.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, displayElixir / displayMax * originalsize);
        if (elixirText != null)
            elixirText.text = $"{displayElixir.ToString("F1")}";
    }

    // 播放消耗特效
    void PlaySpendEffect()
    {
        // TODO: 添加圣水消耗动画/音效
    }

    // 圣水不足提示
    void ShowNotEnoughElixir()
    {
        // 可以添加UI抖动或颜色闪烁
        if (elixirBarFill != null)
        {
            StartCoroutine(FlashElixirBar());
        }

        Debug.Log("圣水不足！");
    }

    // 圣水条闪烁（协程）
    System.Collections.IEnumerator FlashElixirBar()
    {
        Color originalColor = elixirBarFill.color;
        elixirBarFill.color = Color.red;

        yield return new WaitForSeconds(0.2f);

        elixirBarFill.color = originalColor;
    }

    // 显示圣水不足提示
    public void ShowNotEnoughElixir(float requiredCost)
    {
        if (notEnoughElixirPanel != null)
        {
            notEnoughElixirPanel.SetActive(true);

            if (notEnoughElixirText != null)
            {
                float current = GetCurrentElixir();
                notEnoughElixirText.text = $"需要 {requiredCost} 源石\n当前只有 {current:F1} 源石";
            }

            // 0.5秒后自动关闭
            Invoke("HideNotEnoughElixir", 0.5f);
        }
    }

    // 隐藏圣水不足提示
    public void HideNotEnoughElixir()
    {
        if (notEnoughElixirPanel != null)
        {
            notEnoughElixirPanel.SetActive(false);
        }
    }

    // 获取当前圣水：1v1 只读本地玩家的圣水
    public float GetCurrentElixir()
    {
        var localPlayerElixir = PlayerElixir.LocalInstance;
        if (localPlayerElixir == null && NetworkClient.localPlayer != null)
            localPlayerElixir = NetworkClient.localPlayer.GetComponent<PlayerElixir>();
        if (localPlayerElixir != null) return localPlayerElixir.currentElixir;
        if (NetworkGameState.Instance != null) return NetworkGameState.Instance.currentElixir;
        return currentElixir;
    }
}