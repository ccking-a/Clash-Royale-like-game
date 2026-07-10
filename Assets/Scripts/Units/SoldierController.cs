using Unity.VisualScripting;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using System;
using UnityEngine.UI;// 添加UI命名空间引用
using Mirror;
using UnityEngine.AI;

public class SoldierController : NetworkBehaviour
{
    [Header("单位属性")]
    public int unitID;
    public string unitName = "士兵";

    [SyncVar]
    public int teamIndex = 0; // 0=玩家1阵营, 1=玩家2阵营

    public bool isPlayerUnit => teamIndex == 0;

    public bool isTower = false;

    [SyncVar] public float moveSpeed = 3f;
    public float Cost;

    [Header("战斗属性")]
    [SyncVar] public float maxHealth = 100f;

    [SyncVar(hook = (nameof(OnHealthChanged)))]
    public float currentHealth = 100f;

    [SyncVar] public float attackDamage = 10f;   // 物理攻击力
    [SyncVar] public float magicDamage = 0f;     // 法术攻击力
    [SyncVar] public float defense = 0f;         // 防御力
    [SyncVar] public float attackRange = 3f;
    [SyncVar] public float attackCooldown = 1f;

    [Header("状态")]
    [SyncVar]
    public bool isAlive = true;

    [SyncVar]
    public bool canMove = true;
    [SyncVar]
    public bool canAttack = true;
    [SyncVar]
    public bool isInCombat = false;  // 是否在战斗状态

    [SyncVar]
    public bool isInSkillState = false;  // 是否处于技能状态

    [SyncVar(hook = nameof(OnIsMovingChanged))]
    public bool isMoving = false;

    [Header("群攻参数")]
    public bool isAOEAttack = false;  // 正常状态下是否是群攻干员
    public int baseMaxTargetCount = 1;  // 正常状态下的最大攻击目标数量
    public int skillMaxTargetCount = 3;  // 技能状态下的最大攻击目标数量
    [SyncVar]
    public int currentMaxTargetCount;  // 当前状态下的最大攻击目标数量

    [Header("组件引用")]
    public LayerMask enemyLayerMask;  // 在编辑器中设置的敌人层
    private Rigidbody rb;
    private Collider col;
    private SphereCollider trigger; // 战斗检测范围触发器
    public NavMeshAgent navMeshAgent; // NavMeshAgent组件引用
    public List<Transform> currentTargets = new List<Transform>();  // 当前目标数组
    private Vector3 moveTarget;
    private UIManager uiManager;
    public Component roleComponent; // 角色模型控制脚本引用，使用Component类型避免编译依赖
    public ParticleSystem particle;

    [Header("防御塔")]
    public CubesHighLight platformTile = null; // 所属高台
    /// <summary>放置到高台时贡献给防御塔的血量（用于释放时回退）</summary>
    [System.NonSerialized] public float platformBonusHealth;
    /// <summary>放置到高台时贡献给防御塔的防御（用于释放时回退）</summary>
    [System.NonSerialized] public float platformBonusDefense;

    // 寻路相关变量（NavMesh版）
    public bool isFollowingPath = false;

    [Header("血条")]
    public HealthBar healthBar;
    private GameObject healthBarCanvas;
    public float offsetX = 0f; // 血条在单位x轴方向的偏移量
    public float offsetY = 2f; // 血条在单位上方的偏移量
    public float offsetZ = 0f; // 血条 Z 轴偏移量（可调）
    public float healthBarWidth = 1.5f; // 血条宽度
    public float healthBarHeight = 0.4f; // 血条高度

    /// <summary>对象池复用时，血条已被销毁，OnEnable 时重新创建（Start 可能已执行过不再运行）</summary>
    void OnEnable()
    {
        if (healthBar == null && isClient)
            InitializeHealthBar();
    }

    void Start()
    {
        // ═══════════ 服务器专属初始化 ═══════════
        if (isServer)
        {
            // 场景中的塔：根据 tag 初始化阵营和塔标记（仅用于场景预置物）
            if (gameObject.CompareTag("MyTower"))
            {
                isTower = true;
                teamIndex = 0;
            }
            else if (gameObject.CompareTag("EnemyTower"))
            {
                isTower = true;
                teamIndex = 1;
            }

            // 获取组件
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();
            navMeshAgent = GetComponent<NavMeshAgent>();

            if (rb == null)
            {
                Debug.LogError($"{unitName} 缺少Rigidbody组件");
            }
            if (col == null)
            {
                Debug.LogError($"{unitName} 缺少Collider组件");
            }
            if (navMeshAgent == null)
            {
                Debug.LogError($"{unitName} 缺少NavMeshAgent组件");
            }
            else
            {
                // 设置NavMeshAgent参数
                navMeshAgent.speed = moveSpeed;
                navMeshAgent.angularSpeed = 0f; // 禁用旋转
                navMeshAgent.updateRotation = false; // 禁用自动旋转
                navMeshAgent.updateUpAxis = false;
            }

            // 初始化
            currentHealth = maxHealth;
            isAlive = true;
            isInCombat = false;
            isInSkillState = false;

            // 初始化当前最大攻击目标数为基础值
            UpdateMaxTargetCount();

            // 创建战斗检测范围（用于提前发现敌人）
            if (col != null)
            {
                // 使用触发器来检测进入战斗范围
                GameObject triggerObj = new GameObject("CombatTrigger");
                triggerObj.transform.SetParent(transform);
                triggerObj.transform.localPosition = Vector3.zero;
                trigger = triggerObj.AddComponent<SphereCollider>();
                trigger.radius = attackRange;
                trigger.isTrigger = true;
                triggerObj.AddComponent<CombatTrigger>().soldierController = this;
            }

            // 注册到单位管理器
            RegisterToUnitManager();

            if (isTower)
            {
                canMove = false;
            }
        }

        // ═══════════ 所有端（服务器+客户端）都需要的初始化 ═══════════

        // 获取UIManager引用
        if (uiManager == null)
        {
            // 客户端或备用：通过本地玩家获取
            if (uiManager == null && NetworkClient.localPlayer != null)
            {
                uiManager = NetworkClient.localPlayer.GetComponent<UIManager>();
            }

            // 最后备用
            if (uiManager == null)
            {
                uiManager = FindObjectOfType<UIManager>();
            }
        }

        // 获取子对象中的Role脚本引用（动画需要在所有客户端运行）
        roleComponent = null;
        FindRoleComponentRecursive(transform);
        if (roleComponent == null)
        {
            Debug.Log($"{unitName} 未找到Role脚本");
        }

        // 初始化血条（纯视觉，所有客户端都需要）
        InitializeHealthBar();

        if (isClient)
        {
            StartCoroutine(WaitAndMarkPlayer2View());
            if (roleComponent != null)
                StartCoroutine(SyncInitialAnimationState());
        }

    }

    private void OnIsMovingChanged(bool oldVal, bool newVal)
    {
        if (roleComponent == null) return;
        if (newVal && !isInCombat)
        {
            roleComponent.SendMessage("onMove", SendMessageOptions.DontRequireReceiver);
        }
        else if (!newVal && !isInCombat)
        {
            roleComponent.SendMessage("onIdle", SendMessageOptions.DontRequireReceiver);
        }
    }

    private IEnumerator SyncInitialAnimationState()
    {
        yield return null;
        yield return null;
        if (roleComponent == null) yield break;

        if (isInCombat)
        {
            if (isInSkillState)
                roleComponent.SendMessage("onSkillF", SendMessageOptions.DontRequireReceiver);
            else
                roleComponent.SendMessage("onAttackF", SendMessageOptions.DontRequireReceiver);
        }
        else if (isMoving)
        {
            roleComponent.SendMessage("onMove", SendMessageOptions.DontRequireReceiver);
        }
    }

    private bool _forcePlayer2Rotation = false;

    private IEnumerator WaitAndMarkPlayer2View()
    {
        while (PlayerElixir.LocalInstance == null)
            yield return null;

        _forcePlayer2Rotation = PlayerElixir.LocalInstance.IsPlayer2;
    }

    void LateUpdate()
    {
        if (_forcePlayer2Rotation)
        {
            transform.rotation = Quaternion.Euler(0, 180f, 0);
        }
    }

    //服务器初始化
    public override void OnStartServer()
    {
        base.OnStartServer();
        currentHealth = maxHealth;
        navMeshAgent = GetComponent<NavMeshAgent>();
        if (navMeshAgent != null) navMeshAgent.enabled = true;

        // 关闭旋转同步，旋转由客户端根据阵营视角自行处理
        NetworkTransformBase nt = GetComponent<NetworkTransformBase>();
        if (nt != null) nt.syncRotation = false;
    }

    //客户端初始化
    public override void OnStartClient()
    {
        base.OnStartClient();
        navMeshAgent = GetComponent<NavMeshAgent>();
        if (navMeshAgent != null && !isServer) navMeshAgent.enabled = false;

        // 客户端也关闭旋转同步，否则 NetworkTransform 每帧覆盖客户端的本地旋转
        NetworkTransformBase nt = GetComponent<NetworkTransformBase>();
        if (nt != null) nt.syncRotation = false;
    }

    private void RegisterToUnitManager()
    {
        if (UnitManager.Instance != null)
        {
            UnitManager.Instance.RegisterUnit(gameObject, teamIndex);
        }
    }

    void Update()
    {
        if (Time.frameCount % 3 != 0) return;  // 每三帧调用一次，减轻性能负担
        if (!isAlive) return;
        if (isServer)
        {
            // 状态判断
            ServerUpdateCombatState();
        }

        // 监听Role组件的isHurt字段，用于动画帧触发攻击
        if (roleComponent != null && currentTargets.Count > 0)
        {
            try
            {
                // 使用反射获取Role组件的isHurt字段（注意：isHurt是字段不是属性）
                bool isHurt = (bool)roleComponent.GetType().GetField("isHurt").GetValue(roleComponent);
                if (isHurt)
                {
                    // 执行攻击扣血
                    if (isServer) Attack();
                    // 播放技能特效（仅服务器触发，因干员为放置物无客户端权限，且需 NetworkServer.Spawn）
                    if (isServer && isInSkillState)
                        ServerPlaySkillEffect();
                    // 将isHurt设置为false
                    roleComponent.GetType().GetField("isHurt").SetValue(roleComponent, false);
                    Debug.Log($"{unitName} 通过动画帧触发攻击，isHurt重置为false");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"获取或设置isHurt字段失败: {e.Message}");
            }
        }
    }

    // 更新战斗状态
    [Server]
    void ServerUpdateCombatState()
    {
        if (!isAlive) return;

        // 如果有目标且目标还存活，保持战斗状态
        if (currentTargets.Count > 0)
        {
            // 检查目标是否还存活，移除已死亡的目标
            List<Transform> validTargets = new List<Transform>();
            foreach (Transform target in currentTargets)
            {
                if (target != null)
                {
                    SoldierController targetUnit = target.GetComponent<SoldierController>();
                    if (targetUnit != null && targetUnit.isAlive)
                    {
                        validTargets.Add(target);
                    }
                }
            }

            // 更新目标数组
            currentTargets = validTargets;

            // 如果还有有效目标，保持战斗状态
            if (currentTargets.Count > 0)
            {
                // 从非战斗状态切换到战斗状态，调用攻击模型
                if (!isInCombat)
                {
                    Debug.Log($"{unitName} 从非战斗状态切换到战斗状态");
                    isInCombat = true;
                    isMoving = false;
                    // 调用role脚本的攻击方法：技能状态调用onSkillF，否则调用onAttackF
                    if (isInSkillState)
                    {
                        Debug.Log($"{unitName} 在UpdateCombatState中调用技能模型: onSkillF");
                        RpcOnSkillF();
                    }
                    else
                    {
                        Debug.Log($"{unitName} 在UpdateCombatState中调用攻击模型: onAttackF");
                        RpcOnAttackF();
                    }
                }
                return;
            }
            else
            {
                // 目标不再有效（被击杀等），尝试在范围内重新锁定其他敌人
                if (!isAOEAttack)
                {
                    Transform newTarget = TryFindNewTargetInRange();
                    if (newTarget != null)
                    {
                        SetAttackTarget(newTarget);
                        return;
                    }
                }

                // 范围内无其他目标，退出战斗状态
                Debug.Log($"{unitName} 目标不再有效，范围内无其他敌人，清除目标");
                isInCombat = false;
                if (roleComponent != null)
                {
                    navMeshAgent.isStopped = false;
                    Debug.Log($"{unitName} 退出战斗状态，切换模型");
                    if (platformTile != null)
                    {
                        RpcOnIdle();
                    }
                    else
                    {
                        isMoving = true;
                        RpcOnMove();
                    }
                }
            }
        }

        // 无目标时的处理：修复「isInCombat=true 但 currentTargets=0」的不一致状态
        // 该状态可能由 TakeDamage 先于 OnTriggerEnter 触发导致（先受伤再进入范围，OnTriggerEnter 因 isInCombat 提前 return）
        if (currentTargets.Count == 0)
        {
            Transform targetInRange = TryFindNewTargetInRange();
            if (targetInRange != null)
            {
                SetAttackTarget(targetInRange);
                return;
            }
            // 范围内无敌人，清除可能由 TakeDamage 造成的错误战斗状态
            if (isInCombat)
            {
                isInCombat = false;
                if (navMeshAgent != null) navMeshAgent.isStopped = false;
            }
        }

        if (isTower) return;
        if (platformTile != null) return;
        // 如果没有目标，寻找最近的敌人（包括敌方单位和防御塔）
        GameObject nearestTarget = FindNearestEnemy();
        if (nearestTarget != null)
        {
            MoveToTargetWithPathfinding(nearestTarget.transform.position);
        }
        else
        {
            Debug.Log("找不到目标了");
            isInCombat = false;
        }
    }

    // 设置移动目标 (使用NavMesh寻路)
    [Server]
    public void MoveToTargetWithPathfinding(Vector3 targetPosition)
    {
        if (!canMove || !isAlive || navMeshAgent == null)
            return;

        // 使用NavMesh寻路
        navMeshAgent.SetDestination(targetPosition);
        isFollowingPath = true;
        if (!isMoving)
        {
            isMoving = true;
            RpcOnMove();
        }
    }

    // 设置攻击目标（通过碰撞检测或其他方式）
    [Server]
    public void SetAttackTarget(Transform target)
    {
        if (!isAlive) return;

        // 检查目标是否是敌对单位
        SoldierController targetUnit = target.GetComponent<SoldierController>();
        if (targetUnit != null)
        {
            if (teamIndex == targetUnit.teamIndex)
            {
                return;
            }
        }

        // 如果不是群攻干员，替换当前目标
        if (!isAOEAttack)
        {
            currentTargets.Clear();
            currentTargets.Add(target);
        }
        // 如果是群攻干员且目标数组未满，添加目标
        else if (!currentTargets.Contains(target) && currentTargets.Count < currentMaxTargetCount)
        {
            currentTargets.Add(target);
        }

        isInCombat = true;
        isFollowingPath = false;

        Debug.Log($"{unitName} 锁定目标: {target.name}");

        // 停止移动
        isFollowingPath = false;
        if (navMeshAgent != null)
        {
            navMeshAgent.isStopped = true;
            navMeshAgent.ResetPath();
        }
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
        }

        // 立即调用攻击方法切换模型：技能状态调用onSkillF，否则调用onAttackF
        if (isInSkillState)
        {
            Debug.Log($"{unitName} 调用技能模型: onSkillF");
            RpcOnSkillF();
        }
        else
        {
            Debug.Log($"{unitName} 调用攻击模型: onAttackF");
            RpcOnAttackF();
        }
    }

    // 执行攻击
    [Server]
    void Attack()
    {
        // 如果目标数组为空，返回
        if (currentTargets.Count == 0) return;

        // 遍历所有目标，造成伤害
        foreach (Transform target in currentTargets)
        {
            if (target != null)
            {
                // 造成伤害
                SoldierController targetUnit = target.GetComponent<SoldierController>();
                if (targetUnit != null && targetUnit.isAlive)
                {
                    // 计算物理伤害
                    float physicalDamage = Mathf.Max(0, attackDamage - targetUnit.defense);
                    // 计算法术伤害
                    float magicalDamage = magicDamage;
                    // 总伤害
                    float totalDamage = physicalDamage + magicalDamage;

                    targetUnit.TakeDamage(totalDamage);
                    Debug.Log($"{unitName} 攻击了 {targetUnit.unitName}, 造成 {totalDamage} 点伤害 (物理: {physicalDamage}, 法术: {magicalDamage})");
                }
            }
        }
    }

    /// <summary>
    /// 初始化血条系统，创建完整的血条UI层级。对象池复用时血条已被销毁，需重新创建。
    /// </summary>
    [Client]
    public void InitializeHealthBar()
    {
        if (healthBar != null) return;  // 已有血条则跳过（避免 Start 与 OnEnable 重复创建）
        // 创建血条根对象，挂到场景根节点而非单位下，避免玩家2视角下单位180°旋转导致血条位置错误
        GameObject healthBarObj = new GameObject("HealthBar");
        healthBarObj.transform.SetParent(null);
        healthBarObj.transform.localPosition = Vector3.zero;
        healthBarObj.transform.localRotation = Quaternion.identity;
        healthBarObj.transform.localScale = Vector3.one;

        // 添加Canvas组件
        Canvas canvas = healthBarObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100; // 设置较高的排序顺序，确保血条显示在其他UI元素上方

        // 添加CanvasScaler组件
        CanvasScaler canvasScaler = healthBarObj.AddComponent<CanvasScaler>();
        canvasScaler.dynamicPixelsPerUnit = 100f;
        canvasScaler.scaleFactor = 1f;

        // 添加GraphicRaycaster组件
        healthBarObj.AddComponent<GraphicRaycaster>();

        // 添加CanvasGroup组件确保血条可见
        CanvasGroup canvasGroup = healthBarObj.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // ********** 血条背景 **********
        GameObject backgroundObj = new GameObject("Background");
        backgroundObj.transform.SetParent(healthBarObj.transform);
        backgroundObj.AddComponent<RectTransform>(); // 确保添加RectTransform
        Image backgroundImage = backgroundObj.AddComponent<Image>();
        backgroundImage.color = new Color(0.2f, 0.2f, 0.2f, 1.0f); // 完全不透明的深灰色背景
        backgroundImage.rectTransform.sizeDelta = new Vector2(healthBarWidth, healthBarHeight); // 使用controler中的变量控制背景尺寸
        backgroundImage.rectTransform.localPosition = Vector3.zero;
        backgroundImage.rectTransform.localRotation = Quaternion.identity;
        backgroundImage.rectTransform.localScale = Vector3.one;
        backgroundObj.SetActive(true); // 确保对象是激活状态
        Debug.Log($"背景对象状态: active={backgroundObj.activeSelf}, color={backgroundImage.color}, size={backgroundImage.rectTransform.sizeDelta}");

        // ********** 血条边框 **********
        GameObject borderObj = new GameObject("Border");
        borderObj.transform.SetParent(healthBarObj.transform);
        borderObj.AddComponent<RectTransform>(); // 确保添加RectTransform
        Image borderImage = borderObj.AddComponent<Image>();
        borderImage.color = new Color(0, 0, 0, 1.0f); // 不透明的黑色边框
        borderImage.rectTransform.sizeDelta = new Vector2(healthBarWidth, healthBarHeight); // 使用controler中的变量控制边框尺寸
        borderImage.rectTransform.localPosition = Vector3.zero;
        borderImage.rectTransform.localRotation = Quaternion.identity;
        borderImage.rectTransform.localScale = Vector3.one;
        borderObj.SetActive(true); // 确保对象是激活状态
        Debug.Log($"边框对象状态: active={borderObj.activeSelf}, color={borderImage.color}, size={borderImage.rectTransform.sizeDelta}");

        // ********** 血条填充容器 **********
        GameObject fillContainerObj = new GameObject("FillContainer");
        fillContainerObj.transform.SetParent(healthBarObj.transform);
        fillContainerObj.transform.localPosition = Vector3.zero;
        fillContainerObj.transform.localRotation = Quaternion.identity;
        fillContainerObj.transform.localScale = Vector3.one;
        fillContainerObj.AddComponent<RectTransform>(); // 确保添加RectTransform

        RectTransform fillContainerRect = fillContainerObj.GetComponent<RectTransform>();
        // 填充区域略小于背景，保持适当的内边距
        float paddingX = 0.2f;
        float paddingY = 0.15f;
        fillContainerRect.sizeDelta = new Vector2(healthBarWidth - paddingX, healthBarHeight - paddingY); // 使用controler中的变量控制填充容器尺寸
        fillContainerRect.localPosition = Vector3.zero;
        fillContainerObj.SetActive(true); // 确保对象是激活状态
        Debug.Log($"填充容器状态: active={fillContainerObj.activeSelf}, size={fillContainerRect.sizeDelta}");

        // ********** 血条填充 **********
        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(fillContainerObj.transform);
        fillObj.AddComponent<RectTransform>(); // 确保添加RectTransform
        Image fillImage = fillObj.AddComponent<Image>();

        int localTeam = (PlayerElixir.LocalInstance != null && PlayerElixir.LocalInstance.IsPlayer2) ? 1 : 0;
        bool isMyUnit = teamIndex == localTeam;
        Color healthColor = isMyUnit ? new Color(0.4f, 0.7f, 1f) : Color.red;
        fillImage.color = healthColor;

        // 设置Image类型为Filled
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;

        // 动态创建一个简单的纹理作为默认图片
        Texture2D simpleTexture = new Texture2D(1, 1);
        simpleTexture.SetPixel(0, 0, healthColor);
        simpleTexture.Apply();
        Sprite fillSprite = Sprite.Create(simpleTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        fillImage.sprite = fillSprite;

        // 设置填充图片的锚点和大小
        fillImage.rectTransform.anchorMin = Vector2.zero;
        fillImage.rectTransform.anchorMax = Vector2.one;
        fillImage.rectTransform.offsetMin = Vector2.zero;
        fillImage.rectTransform.offsetMax = Vector2.zero;
        fillImage.rectTransform.localPosition = Vector3.zero;
        fillImage.rectTransform.localRotation = Quaternion.identity;
        fillImage.rectTransform.localScale = Vector3.one;
        fillObj.SetActive(true); // 确保对象是激活状态

        // 明确设置UI元素的层级顺序 - 背景在最底层，然后是边框，最后是填充容器
        backgroundObj.transform.SetSiblingIndex(0); // 背景在最底层
        borderObj.transform.SetSiblingIndex(1); // 边框在背景之上
        fillContainerObj.transform.SetSiblingIndex(2); // 填充容器在边框之上

        // 添加HealthBar脚本并初始化
        healthBar = healthBarObj.AddComponent<HealthBar>();
        healthBar.healthBarFill = fillImage;
        healthBar.Initialize(transform, maxHealth, offsetY, offsetX, offsetZ);

        // 确保所有血条组件都是激活状态
        healthBarObj.SetActive(true);

        // 输出完整的血条层级信息
        Debug.Log($"血条对象层级检查：");
        for (int i = 0; i < healthBarObj.transform.childCount; i++)
        {
            Transform child = healthBarObj.transform.GetChild(i);
            Image image = child.GetComponent<Image>();
            string imageInfo = (image != null) ? $"Image(color={image.color}, size={image.rectTransform.sizeDelta})" : "No Image Component";
            Debug.Log($"  子对象 {i}: {child.name} - {imageInfo} - Active: {child.gameObject.activeSelf} - SiblingIndex: {child.GetSiblingIndex()}");
        }

        // 添加调试信息
        Debug.Log($"血条组件引用情况: healthBar={healthBar}, healthBarFill={fillImage}");
        Debug.Log($"血条Canvas设置: renderMode={canvas.renderMode}, sortingOrder={canvas.sortingOrder}");
        Debug.Log($"血条尺寸: backgroundSize={backgroundImage.rectTransform.sizeDelta}, fillSize={fillImage.rectTransform.sizeDelta}");
        Debug.Log($"为 {unitName} 创建了血条，当前生命值: {currentHealth}/{maxHealth}");
    }

    // 受到伤害
    [Server]
    public void TakeDamage(float damage)
    {
        if (!isAlive) return;

        currentHealth -= damage;
        // Debug.Log($"{unitName} 受到 {damage} 点伤害, 剩余生命值: {currentHealth}");

        // 更新血条
        if (healthBar != null)
        {
            healthBar.UpdateHealthBar(currentHealth, maxHealth);
        }

        // 受到伤害时进入战斗状态（如果没有敌人）
        if (!isInCombat)
        {
            isInCombat = true;
        }

        if (currentHealth <= 0)
        {
            Die(fromGameEndCleanup: false);
        }
    }

    // 更新当前最大攻击目标数
    [Server]
    public void UpdateMaxTargetCount()
    {
        // 根据技能状态决定当前最大攻击目标数
        if (isInSkillState)
        {
            currentMaxTargetCount = skillMaxTargetCount;
        }
        else
        {
            currentMaxTargetCount = baseMaxTargetCount;
        }
        Debug.Log($"{unitName} 当前最大攻击目标数更新为: {currentMaxTargetCount}");
    }

    // 更新战斗检测范围半径
    [Server]
    public void UpdateAttackRange()
    {
        if (trigger != null)
        {
            trigger.radius = attackRange;
            Debug.Log($"{unitName} 战斗检测范围更新为: {attackRange}");
        }
    }

    // 切换技能状态
    [Server]
    public void ToggleSkillState()
    {
        isInSkillState = !isInSkillState;
        Debug.Log($"{unitName} 技能状态切换为: {isInSkillState}");

        // 更新当前最大攻击目标数
        UpdateMaxTargetCount();

        // 如果在战斗状态，根据技能状态切换模型
        if (isInCombat)
        {
            if (isInSkillState)
            {
                RpcOnSkillF();
            }
            else
            {
                RpcOnAttackF();
            }
        }
    }

    // 客户端请求激活技能（网络化调用）
    [Command]
    public void CmdActivateSkill(int effectTypeInt, float effectValue)
    {
        ServerActivateSkill(effectTypeInt, effectValue);
    }

    [Server]
    public void ServerActivateSkill(int effectTypeInt, float effectValue)
    {
        if (!isAlive) return;
        ApplySkillEffect((SkillEffectType)effectTypeInt, effectValue);
    }

    [Server]
    private void ApplySkillEffect(SkillEffectType effectType, float effectValue)
    {
        switch (effectType)
        {
            case SkillEffectType.IncreaseAttackDamage: attackDamage += effectValue; break;
            case SkillEffectType.IncreaseMagicDamage: magicDamage += effectValue; break;
            case SkillEffectType.IncreaseDefense: defense += effectValue; break;
            case SkillEffectType.IncreaseMoveSpeed: moveSpeed += effectValue; break;
            case SkillEffectType.HealHealth: currentHealth = Mathf.Min(currentHealth + effectValue, maxHealth); break;
            case SkillEffectType.DecreaseAttackCooldown: attackCooldown = Mathf.Max(0.1f, attackCooldown - effectValue); break;
            case SkillEffectType.IncreaseAttackRange: attackRange += effectValue; UpdateAttackRange(); break;
            case SkillEffectType.IncreaseMaxHealth: maxHealth += effectValue; currentHealth += effectValue; break;
            case SkillEffectType.IncreaseAllStats:
                attackDamage += effectValue * 0.5f; magicDamage += effectValue * 0.5f;
                defense += effectValue * 0.3f; moveSpeed += effectValue * 0.2f;
                maxHealth += effectValue * 2f; currentHealth += effectValue * 2f; break;
            default: attackDamage += effectValue; break;
        }
        SetSkillState(true);
    }

    // 设置技能状态（用于外部调用）
    [Server]
    public void SetSkillState(bool skillState)
    {
        isInSkillState = skillState;
        Debug.Log($"{unitName} 技能状态设置为: {isInSkillState}");

        // 更新当前最大攻击目标数
        UpdateMaxTargetCount();

        // 更新战斗检测范围
        UpdateAttackRange();

        // 如果在战斗状态，根据技能状态切换模型
        if (isInCombat)
        {
            if (isInSkillState)
            {
                RpcOnSkillF();
            }
            else
            {
                RpcOnAttackF();
            }
        }
    }

    // 死亡。fromGameEndCleanup=true 时由 GameEnd 流程调用，跳过国王塔触发逻辑
    [Server]
    public void Die(bool fromGameEndCleanup = false)
    {
        isAlive = false;
        canMove = false;
        canAttack = false;
        isInCombat = false;

        // 最先通知客户端销毁血条和技能按钮，避免后续逻辑卡住或 Rpc 丢失
        uint netId = GetComponent<NetworkIdentity>()?.netId ?? 0;
        RpcRemoveSkillButton(netId);
        RpcDestroyHealthBar();
        

        // 高台死亡时，将高台上的干员一并击杀
        if (isTower)
        {
            CubesHighLight platformTile = GetComponent<CubesHighLight>();
            if (platformTile != null && platformTile.occupiedUnit != null)
            {
                SoldierController unitOnPlatform = platformTile.occupiedUnit.GetComponent<SoldierController>();
                if (unitOnPlatform != null && unitOnPlatform.isAlive)
                {
                    unitOnPlatform.TakeDamage(float.MaxValue);
                }
            }
        }

        GuowangDIe guowang = GetComponent<GuowangDIe>();
        if (guowang != null && !fromGameEndCleanup)
        {
            Debug.Log("国王塔死亡，触发游戏结束流程");
            if (NetworkServer.active)
            {
                NetworkGameState.Instance.SetGameEnded(true);
                NetworkGameState.Instance.GameEnd();
                NetworkGameState.Instance.RpcGameEnd();
            }
            // 国王塔不立即回收，由 GameEnd 流程统一延迟处理
            return;
        }

        // 停止移动
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.velocity = Vector3.zero;
        }

        // 禁用NavMeshAgent
        if (navMeshAgent != null)
        {
            navMeshAgent.isStopped = true;
            navMeshAgent.enabled = false;
        }

        // 禁用碰撞
        if (col != null)
        {
            col.enabled = false;
        }

        Debug.Log($"{unitName} 死亡");

        // 调用role脚本的死亡方法
        RpcOnDie();

        // 从高台上释放（如果在高台上）
        if (platformTile != null)
        {
            // 调用服务器端方法释放高台
            PlatformManager.ServerReleaseFromPlatform(this);
        }

        // 注销单位
        if (UnitManager.Instance != null)
        {
            UnitManager.Instance.UnregisterUnit(gameObject);
        }
        // 干员回收改为一秒延迟协程，确保 Rpc 有足够时间送达客户端
        StartCoroutine(DelayedRecycleUnit());
    }

    private IEnumerator DelayedRecycleUnit()
    {
        yield return new WaitForSeconds(1f);
        if (UnitPoolManager.Instance != null && GetComponent<PooledUnitOrigin>() != null)
        {
            UnitPoolManager.Instance.ReturnNetworkUnit(gameObject, null);
        }
        else if (NetworkServer.active)
        {
            NetworkServer.Destroy(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }


    [Server]
    private GameObject FindNearestEnemy()
    {
        if (UnitManager.Instance == null)
        {
            Debug.LogError("UnitManager实例不存在");
            return null;
        }

        UnitManager.UnitInfo nearestTarget = UnitManager.Instance.FindNearestEnemy(transform.position, teamIndex);
        return nearestTarget?.unitObject;
    }

    /// <summary>
    /// 在攻击范围内寻找最近的存活敌人（用于单目标干员击杀当前目标后重新索敌）
    /// </summary>
    [Server]
    private Transform TryFindNewTargetInRange()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, attackRange);
        if (hits.Length == 0) return null;

        Transform closest = null;
        float closestDist = float.MaxValue;
        Vector3 pos = transform.position;

        foreach (Collider c in hits)
        {
            var sc = c.GetComponent<SoldierController>();
            if (sc == null || !sc.isAlive || sc.teamIndex == teamIndex) continue;

            float d = Vector3.SqrMagnitude(c.transform.position - pos);
            if (d < closestDist)
            {
                closestDist = d;
                closest = c.transform;
            }
        }
        return closest;
    }


    // 递归查找Role脚本
    private void FindRoleComponentRecursive(Transform parent)
    {
        // 首先检查当前父对象是否有Role脚本
        Component[] parentComponents = parent.GetComponents<MonoBehaviour>();
        foreach (Component comp in parentComponents)
        {
            if (comp.GetType().Name == "Role")
            {
                roleComponent = comp;
                Debug.Log($"为 {unitName} 找到了Role脚本（当前对象）");
                if (platformTile != null) RpcOnIdle();
                return;
            }
        }

        // 检查当前父对象的子对象
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            // 检查当前子对象是否有Role脚本
            Component[] components = child.GetComponents<MonoBehaviour>();
            foreach (Component comp in components)
            {
                if (comp.GetType().Name == "Role")
                {
                    roleComponent = comp;
                    Debug.Log($"为 {unitName} 找到了Role脚本（子对象）");
                    return;
                }
            }

            // 递归查找当前子对象的子对象
            FindRoleComponentRecursive(child);

            // 如果已经找到，提前返回
            if (roleComponent != null)
            {
                return;
            }
        }
    }
    void OnHealthChanged(float OldHealth, float NewHealth)
    {
        // 更新血条
        if (healthBar != null)
        {
            healthBar.UpdateHealthBar(currentHealth, maxHealth);
        }
    }

    // 服务器端播放技能特效（干员为放置物无客户端权限，仅服务器可 NetworkServer.Spawn 并同步到客户端）
    // 使用 EffectPoolManager 对象池复用粒子，减少 GC 和卡顿
    [Server]
    private void ServerPlaySkillEffect()
    {
        if (particle == null || currentTargets.Count == 0 || currentTargets[0] == null) return;

        GameObject prefab = particle.gameObject;
        GameObject obj = EffectPoolManager.Instance != null
            ? EffectPoolManager.Instance.GetNetworkEffect(prefab, transform.position, Quaternion.identity)
            : Instantiate(prefab, transform.position, Quaternion.identity);

        Vector3 direction = currentTargets[0].position - transform.position;
        direction.y = 0;
        if (direction.sqrMagnitude > 0.001f)
        {
            direction.Normalize();
            obj.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        var par = obj.GetComponentInChildren<ParticleSystem>();
        if (par != null) par.Play();
        NetworkServer.Spawn(obj);
    }

    // ClientRPC方法：切换到技能状态动画
    [ClientRpc]
    public void RpcOnSkillF()
    {
        if (roleComponent != null)
        {
            roleComponent.SendMessage("onSkillF", SendMessageOptions.DontRequireReceiver);
        }
    }

    // ClientRPC方法：切换到攻击状态动画
    [ClientRpc]
    public void RpcOnAttackF()
    {
        if (roleComponent != null)
        {
            roleComponent.SendMessage("onAttackF", SendMessageOptions.DontRequireReceiver);
        }
    }

    // ClientRPC方法：切换到 idle 状态动画
    [ClientRpc]
    public void RpcOnIdle()
    {
        if (roleComponent != null)
        {
            roleComponent.SendMessage("onIdle", SendMessageOptions.DontRequireReceiver);
        }
    }

    // ClientRPC方法：切换到移动状态动画
    [ClientRpc]
    public void RpcOnMove()
    {
        if (roleComponent != null)
        {
            roleComponent.SendMessage("onMove", SendMessageOptions.DontRequireReceiver);
        }
    }

    // ClientRPC方法：切换到死亡状态动画
    [ClientRpc]
    public void RpcOnDie()
    {
        if (roleComponent != null)
        {
            roleComponent.SendMessage("onDie", SendMessageOptions.DontRequireReceiver);
        }
    }

    // ClientRPC方法：通知客户端删除该干员的技能图标（在对象回收/销毁前调用）
    [ClientRpc]
    private void RpcRemoveSkillButton(uint netId)
    {
        // 按 netId 删除，解决 Host 对象池 UnSpawn 先于 Rpc 导致 gameObject 已销毁、技能图标未删除的问题
        foreach (var uiManager in FindObjectsOfType<UIManager>())
            uiManager.RemoveSkillButtonByNetId(netId);
    }

    [ClientRpc]
    private void RpcDestroyHealthBar()
    {
        if (healthBar != null)
        {
            Destroy(healthBar.gameObject);
            healthBar = null;
        }
    }
}

// 战斗触发器组件
public class CombatTrigger : MonoBehaviour
{
    public SoldierController soldierController;

    void OnTriggerEnter(Collider other)
    {
        if (soldierController == null || !soldierController.isAlive) return;
        // 已有目标时不再响应新进入的敌人；无目标时允许锁定（修复 TakeDamage 先于触发导致的 isInCombat=true 但 currentTargets=0 状态）
        if (soldierController.isInCombat && soldierController.currentTargets.Count > 0) return;

        // 检查是否是敌人
        SoldierController otherUnit = other.GetComponent<SoldierController>();
        if (otherUnit != null && otherUnit.isAlive)
        {
            bool isEnemy = soldierController.teamIndex != otherUnit.teamIndex;
            if (isEnemy)
            {
                soldierController.SetAttackTarget(other.transform);
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (soldierController == null || !soldierController.isAlive) return;

        // 如果是群攻干员
        if (soldierController.isAOEAttack)
        {
            // 从目标数组中移除离开的敌人
            if (soldierController.currentTargets.Contains(other.transform))
            {
                soldierController.currentTargets.Remove(other.transform);

                // 如果目标数组为空，退出战斗状态
                if (soldierController.currentTargets.Count == 0)
                {
                    soldierController.canMove = true;
                    soldierController.isInCombat = false;
                    Debug.Log("所有敌人离开范围，解除战斗状态");
                    // 退出战斗状态，根据是否在高台切换模型
                    Debug.Log($"{soldierController.unitName} 退出战斗状态，切换模型");
                    // 如果在高台上，调用onIdle，否则调用onMove
                    if (soldierController.platformTile != null)
                    {
                        soldierController.RpcOnIdle();
                    }
                    else
                    {
                        soldierController.isMoving = true;
                        soldierController.RpcOnMove();
                    }
                }
            }
        }
        // 如果不是群攻干员，保持原有单目标逻辑
        else
        {
            if (soldierController.currentTargets.Count > 0 && other.transform == soldierController.currentTargets[0])
            {
                soldierController.currentTargets.Clear();
                soldierController.canMove = true;
                soldierController.isInCombat = false;
                Debug.Log("敌人离开范围，解除锁定");
                // 退出战斗状态，根据是否在高台切换模型
                Debug.Log($"{soldierController.unitName} 退出战斗状态，切换模型");
                // 如果在高台上，调用onIdle，否则调用onMove
                if (soldierController.platformTile != null)
                {
                    soldierController.RpcOnIdle();
                }
                else
                {
                    soldierController.isMoving = true;
                    soldierController.RpcOnMove();
                }
            }
        }
    }
    void OnTriggerStay(Collider other)
    {
        if (soldierController == null || !soldierController.isAlive) return;

        // 检查是否是敌人
        SoldierController enemy = other.GetComponent<SoldierController>();
        if (enemy == null || !enemy.isAlive) return;

        bool isEnemy = soldierController.teamIndex != enemy.teamIndex;
        if (!isEnemy) return;

        // 如果不是群攻干员，保持原有单目标逻辑
        if (!soldierController.isAOEAttack)
        {
            //当前目标不再有效
            if (soldierController.currentTargets.Count == 0)
            {
                // 获取攻击范围内的所有敌人
                Collider[] enemiesInRange = Physics.OverlapSphere(transform.position,
                    soldierController.attackRange);

                if (enemiesInRange.Length == 0) return;

                Transform closestEnemy = null;
                float closestDistance = Mathf.Infinity;

                // 缓存当前位置，避免重复调用transform.position
                Vector3 currentPos = transform.position;

                foreach (Collider enemyCol in enemiesInRange)
                {
                    if (enemyCol.GetComponent<SoldierController>() != null)
                    {
                        float enemyDist = Vector3.Distance(currentPos, enemyCol.transform.position);
                        if (enemyDist < closestDistance)
                        {
                            closestDistance = enemyDist;
                            closestEnemy = enemyCol.transform;
                        }
                    }
                }

                if (closestEnemy != null)
                {
                    // 只有在找到最近敌人时才获取组件
                    SoldierController closestEnemyCtrl = closestEnemy.GetComponent<SoldierController>();
                    if (closestEnemyCtrl != null && closestEnemyCtrl.isAlive &&
                        soldierController.teamIndex != closestEnemyCtrl.teamIndex)
                    {
                        Debug.Log("在攻击范围内发现敌人");
                        soldierController.SetAttackTarget(closestEnemy);
                    }
                }
                return;
            }
        }
        else
        {
            // 群攻逻辑优化：减少不必要的计算
            // 只在目标数组需要更新时才执行
            if (soldierController.currentTargets.Count < soldierController.currentMaxTargetCount)
            {
                // 缓存当前位置，避免重复调用transform.position
                Vector3 currentPos = transform.position;

                // 获取攻击范围内的所有敌人
                Collider[] enemiesInRange = Physics.OverlapSphere(currentPos,
                    soldierController.attackRange);

                if (enemiesInRange.Length == 0) return;

                // 创建敌人列表并按距离排序
                List<Transform> enemies = new List<Transform>();
                foreach (Collider enemyCol in enemiesInRange)
                {
                    if (enemyCol.GetComponent<SoldierController>() != null)
                    {
                        // 优化：避免重复获取组件，只在必要时才获取
                        SoldierController enemyUnit = enemyCol.GetComponent<SoldierController>();
                        if (enemyUnit != null && enemyUnit.isAlive &&
                            soldierController.teamIndex != enemyUnit.teamIndex &&
                            !soldierController.currentTargets.Contains(enemyCol.transform))
                        {
                            enemies.Add(enemyCol.transform);
                        }
                    }

                }

                if (enemies.Count == 0) return;

                // 按距离排序
                enemies.Sort((a, b) => Vector3.Distance(currentPos, a.position)
                    .CompareTo(Vector3.Distance(currentPos, b.position)));

                // 只添加新的敌人，不清除现有目标
                int remainingSlots = soldierController.currentMaxTargetCount - soldierController.currentTargets.Count;
                for (int i = 0; i < enemies.Count && i < remainingSlots; i++)
                {
                    soldierController.currentTargets.Add(enemies[i]);
                }
            }

            //// 如果目标数组不为空，进入战斗状态
            //if (soliderControler.currentTargets.Count > 0)
            //{
            //    soliderControler.isInCombat = true;
            //    soliderControler.canMove = false;

            //    // 如果NavMeshAgent存在，停止移动
            //    if (soliderControler.navMeshAgent != null)
            //    {
            //        soliderControler.navMeshAgent.isStopped = true;
            //        soliderControler.navMeshAgent.ResetPath();
            //    }

            //    // 切换模型：技能状态调用onSkillF，否则调用onAttackF
            //    if (soliderControler.isInSkillState && soliderControler.roleComponent != null)
            //    {
            //        soliderControler.roleComponent.SendMessage("onSkillF", SendMessageOptions.DontRequireReceiver);
            //    }
            //    else if (soliderControler.roleComponent != null)
            //    {
            //        soliderControler.roleComponent.SendMessage("onAttackF", SendMessageOptions.DontRequireReceiver);
            //    }
            //}
        }
    }

}