// UnitSelectionUI.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using System.Collections.Generic;
using Mirror;


// 技能按钮数据结构
[System.Serializable]
public class SkillButtonInfo
{
    public GameObject skillButton;        // 技能按钮游戏对象
    public Image skillIcon;              // 技能图标
    public Image cooldownMask;           // 技能冷却遮罩（圆饼进度条）
    public Button buttonComponent;       // 按钮组件
    public float cooldownTime = 3f;      // 冷却时间
    public float currentCooldown = 0f;   // 当前冷却进度
    public bool isCooldownComplete = true; // 冷却是否完成
    public GameObject soldier;           // 对应的士兵实体
    public uint soldierNetId;            // 士兵 netId，用于对象池销毁后仍能按 netId 删除技能按钮
}

// Toggle拖拽状态类，用于避免闭包陷阱
public class ToggleDragState
{
    public bool isDragging = false;
    public Vector2 dragStartPosition = Vector2.zero;
    public int toggleIndex = -1;
}

public class UIManager : NetworkBehaviour
{
    [Header("UI组件")]
    public Toggle[] unitToggles;          // 兵种选择Toggle数组
    public Image[] unitIcons;             // 兵种图标数组
    public Text[] unitCostTexts;          // 兵种消耗文本数组

    [Header("技能按钮设置")]
    public GameObject skillButtonPrefab;  // 技能按钮预制体
    public Transform skillButtonContainer; // 技能按钮容器（用于自动排列）
    public float buttonSpacing = 4f;     // 技能按钮间距（缩小一半）
    public float buttonSize = 160f;        // 技能按钮大小（增大尺寸）
    public List<SkillButtonInfo> skillButtons = new List<SkillButtonInfo>(); // 所有技能按钮
    public Dictionary<GameObject, SkillButtonInfo> soldierToButtonMap = new Dictionary<GameObject, SkillButtonInfo>(); // 士兵到技能按钮的映射

    [Header("系统设置")]
    public ToggleGroup toggleGroup;       // Toggle组，确保只能选择一个Toggle

    [Header("网络设置")]
    public NetworkUnitPlacer networkUnitPlacer; // 网络单位放置器

    [Header("兵种数据")]
    public UnitData[] allUnits;           // 所有可选兵种数据
    public int[] handCards;               // 当前手牌（4张）
    private Queue<int> cardQueue;         // 卡牌队列
    private int maxHandSize = 4;          // 最大手牌数量

    [Header("当前选择")]
    private int selectedUnitIndex = -1;   // 当前选中的兵种索引

    private void Awake()
    {
        // 初始化技能按钮列表和映射
        skillButtons = new List<SkillButtonInfo>();
        soldierToButtonMap = new Dictionary<GameObject, SkillButtonInfo>();
        // 初始化手牌数组和卡牌队列
        handCards = new int[maxHandSize];
        cardQueue = new Queue<int>();
        

    }
    public override void OnStartClient()
    {
        base.OnStartClient();

    }

    public void GameStart()
    {
        // ★ 关键：只有本地玩家才初始化 UI 绑定和卡牌系统
        // 对方玩家的 UIManager 实例什么都不做
        if (!isLocalPlayer) return;
        BindUIComponents();
        InitializeNetworkReferences();
        InitializeUnitSystem();
    }

    // 绑定UI组件
    private void BindUIComponents()
    {
        // 精确查找Toggle组件
        List<Toggle> toggles = new List<Toggle>();
        for (int i = 1; i <= 4; i++)
        {
            Toggle toggle = GameObject.Find($"Toggle{i}")?.GetComponent<Toggle>();
            if (toggle == null)
            {
                // 尝试在Card下查找
                toggle = GameObject.Find($"Card/Toggle{i}")?.GetComponent<Toggle>();
            }
            if (toggle == null)
            {
                // 尝试在Canvas下查找
                GameObject canvas = GameObject.FindWithTag("Canva");
                if (canvas != null)
                {
                    toggle = canvas.transform.Find($"Panel_Card/Card/Toggle{i}")?.GetComponent<Toggle>();
                }
            }
            if (toggle != null)
            {
                toggles.Add(toggle);
            }
        }
        unitToggles = toggles.ToArray();
        Debug.Log($"绑定了 {unitToggles.Length} 个Toggle组件");
        
        // 精确查找Image组件
        List<Image> images = new List<Image>();
        for (int i = 1; i <= 4; i++)
        {
            Image image = GameObject.Find($"Image{i}")?.GetComponent<Image>();
            if (image == null)
            {
                // 尝试在对应Toggle下查找
                image = GameObject.Find($"Toggle{i}/Image{i}")?.GetComponent<Image>();
            }
            if (image == null)
            {
                // 尝试在Canvas下查找
                GameObject canvas = GameObject.FindWithTag("Canva");
                if (canvas != null)
                {
                    image = canvas.transform.Find($"Panel_Card/Card/Toggle{i}/Image{i}")?.GetComponent<Image>();
                }
            }
            if (image != null)
            {
                images.Add(image);
            }
        }
        unitIcons = images.ToArray();
        Debug.Log($"绑定了 {unitIcons.Length} 个Image组件");
        
        // 精确查找Text组件
        List<Text> texts = new List<Text>();
        for (int i = 1; i <= 4; i++)
        {
            Text text = GameObject.Find($"Text{i}")?.GetComponent<Text>();
            if (text == null)
            {
                // 尝试在对应Toggle下查找
                text = GameObject.Find($"Toggle{i}/Text{i}")?.GetComponent<Text>();
            }
            if (text == null)
            {
                // 尝试在Canvas下查找
                GameObject canvas = GameObject.FindWithTag("Canva");
                if (canvas != null)
                {
                    text = canvas.transform.Find($"Panel_Card/Card/Toggle{i}/Text{i}")?.GetComponent<Text>();
                }
            }
            if (text != null)
            {
                texts.Add(text);
            }
        }
        unitCostTexts = texts.ToArray();
        Debug.Log($"绑定了 {unitCostTexts.Length} 个Text组件");
        
        // 查找ToggleGroup
        toggleGroup = FindObjectOfType<ToggleGroup>();
        if (toggleGroup != null)
        {
            Debug.Log("绑定了ToggleGroup组件");
        }
        else
        {
            Debug.LogWarning("未找到ToggleGroup组件");
        }
        
        // ★ 必须用 GetComponent 找本人 Player 上的 NetworkUnitPlacer，不能用 FindObjectOfType（会找到对方的）
        networkUnitPlacer = GetComponent<NetworkUnitPlacer>();
        if (networkUnitPlacer != null)
        {
            Debug.Log("绑定了本地玩家的 NetworkUnitPlacer 组件");
        }
        else
        {
            Debug.LogWarning("未在本地 Player 上找到 NetworkUnitPlacer 组件");
        }
        
        // 查找技能按钮容器
        skillButtonContainer = GameObject.Find("SkillPosition")?.transform;
        if (skillButtonContainer == null)
        {
            // 尝试查找Canvas下的技能按钮容器
            GameObject canvas = GameObject.FindWithTag("Canva"); 
            if (canvas != null)
            {
                skillButtonContainer = canvas.transform.Find("SkillPosition");
            }
        }
        if (skillButtonContainer != null)
        {
            Debug.Log("绑定了SkillPosition组件");
        }
        else
        {
            Debug.LogWarning("未找到SkillPosition组件");
        }
    }

    // 初始化网络引用（只用 GetComponent，确保是本人的）
    private void InitializeNetworkReferences()
    {
        if (networkUnitPlacer == null)
        {
            networkUnitPlacer = GetComponent<NetworkUnitPlacer>();
            if (networkUnitPlacer != null)
                Debug.Log("UIManager: 绑定了本地玩家的 NetworkUnitPlacer");
            else
                Debug.LogWarning("UIManager: Player 上没有 NetworkUnitPlacer 组件");
        }
    }

    // 移除拖拽相关接口实现，改为在InitializeUnitSystem中为每个Toggle单独配置

    void Update()
    {
        // ★ 非本地玩家的 UIManager 不执行任何逻辑
        if (!isLocalPlayer) return;
        
        if (networkUnitPlacer == null)
            networkUnitPlacer = GetComponent<NetworkUnitPlacer>();
        
        UpdateSkillCooldowns();
    }

    // 初始化兵种系统
    void InitializeUnitSystem()
    {
        // 确保有足够的兵种数据
        if (allUnits.Length == 0)
        {
            Debug.LogError("没有配置兵种数据！");
            return;
        }

        // 检查是否有足够的兵种
        if (allUnits.Length < 8)
        {
            Debug.LogWarning("兵种数量不足8个，将重复使用兵种数据");
        }

        // 清除所有现有技能按钮
        foreach (var buttonInfo in skillButtons)
        {
            if (buttonInfo.skillButton != null)
            {
                Destroy(buttonInfo.skillButton);
            }
        }
        skillButtons.Clear();
        soldierToButtonMap.Clear();

        // 初始化手牌数组
        handCards = new int[maxHandSize];
        // 初始化卡牌队列
        cardQueue = new Queue<int>();

        // 使用玩家在 StartScene 选择的干员；若没选过则回退到前 8 个
        List<int> deck;
        if (UnitSelectionData.HasValidSelection)
        {
            deck = UnitSelectionData.SelectedIndices;
            Debug.Log($"[UIManager] 使用玩家选择的 {deck.Count} 个干员");
        }
        else
        {
            deck = new List<int>();
            for (int i = 0; i < 8; i++)
                deck.Add(i % allUnits.Length);
            Debug.Log("[UIManager] 未检测到干员选择，使用默认前8个");
        }

        for (int i = 0; i < deck.Count; i++)
        {
            if (i < maxHandSize)
                handCards[i] = deck[i];
            else
                cardQueue.Enqueue(deck[i]);
        }

        // 为每个Toggle添加事件监听
        for (int i = 0; i < unitToggles.Length; i++)
        {
            // 确保ToggleGroup正确设置
            if (toggleGroup != null)
            {
                unitToggles[i].group = toggleGroup;
            }

            // 重置Toggle状态
            unitToggles[i].isOn = false;
            unitToggles[i].interactable = true;

            int toggleIndex = i;
            unitToggles[i].onValueChanged.RemoveAllListeners(); // 移除之前的监听器
            unitToggles[i].onValueChanged.AddListener((isOn) => OnUnitToggleChanged(isOn, toggleIndex));

            // 添加EventTrigger组件用于处理拖拽事件
            EventTrigger eventTrigger = unitToggles[i].GetComponent<EventTrigger>();
            if (eventTrigger == null)
            {
                eventTrigger = unitToggles[i].gameObject.AddComponent<EventTrigger>();
            }

            // 清除现有事件
            eventTrigger.triggers.Clear();

            // 创建Toggle的专属拖拽状态类，避免闭包陷阱
            ToggleDragState dragState = new ToggleDragState();
            dragState.toggleIndex = toggleIndex;

            // 添加PointerDown事件
            EventTrigger.Entry pointerDownEntry = new EventTrigger.Entry();
            pointerDownEntry.eventID = EventTriggerType.PointerDown;
            pointerDownEntry.callback.AddListener((data) =>
            {
                PointerEventData pointerData = (PointerEventData)data;
                dragState.isDragging = true;
                dragState.dragStartPosition = pointerData.position;
                Debug.Log($"UIManager: Toggle {dragState.toggleIndex} 开始拖拽");
            });
            eventTrigger.triggers.Add(pointerDownEntry);

            // 添加Drag事件
            EventTrigger.Entry dragEntry = new EventTrigger.Entry();
            dragEntry.eventID = EventTriggerType.Drag;
            dragEntry.callback.AddListener((data) =>
            {
                PointerEventData pointerData = (PointerEventData)data;
                if (dragState.isDragging)
                {
                    // 计算拖拽距离
                    float dragDistance = Vector2.Distance(dragState.dragStartPosition, pointerData.position);
                    Debug.Log($"UIManager: Toggle {dragState.toggleIndex} 拖拽距离: {dragDistance}");

                    // 如果拖拽距离超过阈值，开始放置模式
                    if (dragDistance > 100f)
                    {
                        Debug.Log($"UIManager: Toggle {dragState.toggleIndex} 拖拽距离超过阈值，开始放置模式");

                        // 检查Toggle是否对应数组中的有效位置
                        if (dragState.toggleIndex < 0 || dragState.toggleIndex >= maxHandSize)
                        {
                            Debug.Log("UIManager: Toggle索引超出手牌范围");
                            return;
                        }

                        // 确保handCards数组已初始化
                        if (handCards == null || handCards.Length == 0)
                        {
                            Debug.LogError("UIManager: handCards数组未初始化");
                            return;
                        }

                        // 获取数组中的兵种索引
                        int unitIndex = handCards[dragState.toggleIndex];
                        Debug.Log($"UIManager: Toggle {dragState.toggleIndex} 兵种索引: {unitIndex}");

                        // 检查兵种索引是否有效
                        if (unitIndex < 0 || unitIndex >= allUnits.Length)
                        {
                            Debug.Log("UIManager: 无效的兵种索引");
                            return;
                        }

                        // 记录当前选中的兵种和位置
                        selectedUnitIndex = unitIndex;

                        // 本地开始放置预览，不请求服务器
                        if (networkUnitPlacer != null)
                        {
                            networkUnitPlacer.StartPlacingUnitLocal(unitIndex, dragState.toggleIndex);
                            Debug.Log("开始放置");
                        }
                        //else
                        //{
                        //    // 备用：使用GameManager的放置逻辑
                        //    GameManager.Instance.StartPlacingUnit(allUnits[unitIndex].unitPrefab, dragState.toggleIndex);
                        //}
                        //TranslateToggle(dragState.toggleIndex, new Vector2(0, 60f));
                        Debug.Log($"UIManager: Toggle {dragState.toggleIndex} 调用了放置逻辑");

                        // 更新Toggle状态
                        unitToggles[dragState.toggleIndex].isOn = true;

                        // 结束拖拽状态
                        dragState.isDragging = false;
                    }
                }
            });
            eventTrigger.triggers.Add(dragEntry);

            // 添加PointerUp事件
            EventTrigger.Entry pointerUpEntry = new EventTrigger.Entry();
            pointerUpEntry.eventID = EventTriggerType.PointerUp;
            pointerUpEntry.callback.AddListener((data) =>
            {
                dragState.isDragging = false;
                Debug.Log($"UIManager: Toggle {dragState.toggleIndex} 结束拖拽");
            });
            eventTrigger.triggers.Add(pointerUpEntry);
        }

        // 更新UI显示当前队列中的兵种
        UpdateUnitQueueUI();

        // 重置选择状态
        selectedUnitIndex = -1;
    }

    // 兵种Toggle值改变事件
    void OnUnitToggleChanged(bool isOn, int toggleIndex)
    {
        Debug.Log($"Toggle事件: isOn={isOn}, toggleIndex={toggleIndex}, 当前Toggle.isOn={unitToggles[toggleIndex].isOn}, 选择的Toggle: {unitToggles[toggleIndex]}");

        // 只有当点击的Toggle被选中时才处理，避免UI更新时触发
        if (isOn && unitToggles[toggleIndex].isOn)
        {
            // 检查Toggle是否对应数组中的有效位置
            if (toggleIndex < 0 || toggleIndex >= maxHandSize)
            {
                Debug.Log("Toggle索引超出手牌范围");
                return;
            }

            // 确保数组已经初始化
            if (handCards == null || handCards.Length != maxHandSize)
            {
                Debug.LogError("手牌数组未正确初始化");
                return;
            }

            // 获取数组中的兵种索引
            int unitIndex = handCards[toggleIndex];

            // 检查兵种索引是否有效
            if (unitIndex < 0 || unitIndex >= allUnits.Length)
            {
                Debug.Log("无效的兵种索引");
                return;
            }

            // 记录当前选中的兵种和位置
            selectedUnitIndex = unitIndex;

            // 本地开始放置预览，不请求服务器；只有松开确认放置时才发请求
            if (networkUnitPlacer != null)
            {
                networkUnitPlacer.StartPlacingUnitLocal(unitIndex, toggleIndex);
            }
            //else
            //{
            //    // 备用：使用GameManager的放置逻辑
            //    GameManager.Instance.StartPlacingUnit(allUnits[unitIndex].unitPrefab, toggleIndex);
            //}

        }
        else
        {
             TranslateToggle(toggleIndex, new Vector2(0, -60f));
            CancelSelection();
        }
        // 避免UI更新时（isOn=false）触发CancelPlacement
        // 这样可以防止Toggle位置被错误还原
    }

    // 当兵种被放置完成后调用（从GameManager回调）
    // 将使用的卡牌放入队列，从队头取出新卡牌放入被使用位置
    // placedSoldierNetId：对象池复用或网络延迟时 soldier 可能为 null，用 netId 延迟查找
    public void OnUnitPlaced(int unitIndex, int positionIndex, GameObject soldier, uint placedSoldierNetId = 0)
    {
        Debug.Log($"OnUnitPlaced 被调用: unitIndex={unitIndex}, positionIndex={positionIndex}, soldier={soldier != null}, netId={placedSoldierNetId}");

        // 检查位置索引是否有效
        if (positionIndex < 0 || positionIndex >= maxHandSize)
        {
            Debug.LogError("无效的位置索引: " + positionIndex);
            return;
        }

        // 检查数组是否已初始化
        if (handCards == null || handCards.Length != maxHandSize)
        {
            Debug.LogError("手牌数组未正确初始化！");
            return;
        }

        // 检查队列是否已初始化
        if (cardQueue == null || cardQueue.Count == 0)
        {
            Debug.LogError("卡牌队列未正确初始化或为空！");
            return;
        }

        // 检查兵种索引是否有效
        if (unitIndex < 0 || unitIndex >= allUnits.Length)
        {
            Debug.LogError("无效的兵种索引: " + unitIndex);
            return;
        }

        // 获取当前使用的卡牌索引
        int usedCardIndex = handCards[positionIndex];

        // 将使用的卡牌放入队列末尾
        cardQueue.Enqueue(usedCardIndex);
        Debug.Log($"卡牌 {usedCardIndex} 已放入队列");

        // 从队列头部取出一张新卡牌
        int newCardIndex = cardQueue.Dequeue();
        Debug.Log($"从队列取出卡牌 {newCardIndex}");

        // 将新卡牌放入被使用的位置
        handCards[positionIndex] = newCardIndex;
        Debug.Log($"位置 {positionIndex} 的卡牌已替换为索引 {newCardIndex} 的卡牌");

        // 创建技能按钮（soldier 可能为 null：对象池复用或 TargetRpc 先于 Spawn 到达）
        int unitTypeIndex = usedCardIndex;  // 刚放置的单位类型索引
        if (soldier != null)
        {
            CreateSkillButton(unitTypeIndex, soldier);
        }
        else if (placedSoldierNetId != 0)
        {
            StartCoroutine(WaitForSoldierAndCreateSkillButton(unitTypeIndex, placedSoldierNetId));
        }

        // 更新UI显示
        CancelSelection();
        UpdateUnitQueueUI();

    }

    private System.Collections.IEnumerator WaitForSoldierAndCreateSkillButton(int unitTypeIndex, uint netId)
    {
        for (int i = 0; i < 30; i++)  // 最多等待约 0.5 秒
        {
            yield return null;
            if (NetworkClient.spawned.TryGetValue(netId, out var ni) && ni != null)
            {
                CreateSkillButton(unitTypeIndex, ni.gameObject);
                yield break;
            }
        }
        Debug.LogWarning($"[技能按钮] 放置后未能在超时内找到单位 netId={netId}，技能按钮未创建");
    }

    // 创建技能按钮
    private void CreateSkillButton(int unitIndex, GameObject soldier)
    {
        // 检查预制体和容器是否已设置
        if (skillButtonPrefab == null || skillButtonContainer == null)
        {
            Debug.LogError("技能按钮预制体或容器未设置！");
            return;
        }

        // 检查单位索引是否有效
        if (unitIndex < 0 || unitIndex >= allUnits.Length)
        {
            Debug.LogError("无效的单位索引: " + unitIndex);
            return;
        }

        // 获取当前单位的技能数据
        UnitData unitData = allUnits[unitIndex];

        // 创建技能按钮
        GameObject buttonObj = Instantiate(skillButtonPrefab, skillButtonContainer);
        buttonObj.transform.localRotation = Quaternion.identity;
        buttonObj.transform.localScale = Vector3.one;

        // 设置按钮大小
        RectTransform rectTransform = buttonObj.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.sizeDelta = new Vector2(buttonSize, buttonSize);
        }

        // 获取按钮组件
        Button buttonComp = buttonObj.GetComponent<Button>();
        if (buttonComp == null)
        {
            Debug.LogError("技能按钮预制体缺少Button组件！");
            Destroy(buttonObj);
            return;
        }

        // 获取或创建技能图标和冷却遮罩组件
        Image skillIcon = null;
        Image cooldownMask = null;

        Image[] existingImages = buttonObj.GetComponentsInChildren<Image>();

        // 如果没有足够的Image组件，创建新的
        if (existingImages.Length < 1)
        {
            // 创建技能图标
            GameObject iconObj = new GameObject("SkillIcon");
            iconObj.transform.SetParent(buttonObj.transform);
            skillIcon = iconObj.AddComponent<Image>();
        }
        else
        {
            skillIcon = existingImages[0];
        }

        if (existingImages.Length < 2)
        {
            // 创建冷却遮罩
            GameObject maskObj = new GameObject("CooldownMask");
            maskObj.transform.SetParent(buttonObj.transform);
            cooldownMask = maskObj.AddComponent<Image>();
        }
        else
        {
            cooldownMask = existingImages[1];
        }

        // 确保冷却遮罩在技能图标上方
        cooldownMask.transform.SetAsLastSibling();

        // 设置技能图标大小和位置
        RectTransform iconRect = skillIcon.GetComponent<RectTransform>();
        if (iconRect != null)
        {
            iconRect.sizeDelta = new Vector2(buttonSize * 0.8f, buttonSize * 0.8f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        }

        // 设置冷却遮罩大小、位置和样式
        RectTransform maskRect = cooldownMask.GetComponent<RectTransform>();
        if (maskRect != null)
        {
            maskRect.sizeDelta = new Vector2(buttonSize * 0.8f, buttonSize * 0.8f);
            maskRect.anchoredPosition = Vector2.zero;
            maskRect.anchorMin = new Vector2(0.5f, 0.5f);
            maskRect.anchorMax = new Vector2(0.5f, 0.5f);
        }

        // 设置技能图标
        if (unitData.skillIcon != null)
        {
            // 使用单位数据中定义的技能图标
            skillIcon.sprite = unitData.skillIcon;
            skillIcon.color = Color.white; // 恢复默认颜色，使用图标本身的颜色
        }
        else
        {
            // 如果没有技能图标，使用颜色区分不同兵种
            Color[] skillColors = new Color[]
            {
                Color.red,               // 兵种1
                Color.blue,              // 兵种2
                Color.green,             // 兵种3
                Color.yellow,            // 兵种4
                new Color(0.5f, 0f, 0.5f),// 兵种5：紫色
                new Color(1f, 0.5f, 0f),  // 兵种6：橙色
                Color.cyan,              // 兵种7
                Color.magenta            // 兵种8
            };

            int colorIndex = unitIndex % skillColors.Length;
            skillIcon.color = skillColors[colorIndex];
        }

        // 设置冷却遮罩为半透明黑色，填充方式为圆饼
        cooldownMask.color = new Color(0f, 0f, 0f, 0.7f);
        cooldownMask.type = Image.Type.Filled;
        cooldownMask.fillMethod = Image.FillMethod.Radial360;
        cooldownMask.fillOrigin = (int)Image.Origin360.Top;
        cooldownMask.fillAmount = 0f; // 初始为未冷却状态
        cooldownMask.preserveAspect = false; // 不保持纵横比，完全覆盖图标

        // 创建技能按钮信息
        SkillButtonInfo buttonInfo = new SkillButtonInfo();
        buttonInfo.skillButton = buttonObj;
        buttonInfo.skillIcon = skillIcon;
        buttonInfo.cooldownMask = cooldownMask;
        buttonInfo.buttonComponent = buttonComp;
        buttonInfo.cooldownTime = unitData.skillCooldownTime; // 使用单位数据中的冷却时间
        buttonInfo.soldier = soldier;
        if (soldier != null && soldier.TryGetComponent<NetworkIdentity>(out var ni))
            buttonInfo.soldierNetId = ni.netId;

        // 添加点击事件
        int buttonIndex = skillButtons.Count;
        buttonComp.onClick.AddListener(() => OnSkillButtonClicked(buttonIndex));

        // 添加到列表和映射
        skillButtons.Add(buttonInfo);
        soldierToButtonMap.Add(soldier, buttonInfo);

        // 自动排列技能按钮
        ArrangeSkillButtons();

        Debug.Log($"为兵种 {unitIndex} 创建了技能按钮，士兵: {soldier.name}");
    }

    // 自动排列技能按钮（靠右对齐）
    private void ArrangeSkillButtons()
    {
        if (skillButtonContainer == null)
            return;

        // 计算总宽度
        float totalWidth = skillButtons.Count * (buttonSize + buttonSpacing) - buttonSpacing;

        // 从右向左排列
        for (int i = 0; i < skillButtons.Count; i++)
        {
            SkillButtonInfo buttonInfo = skillButtons[i];
            RectTransform rectTransform = buttonInfo.skillButton.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                // 计算X位置：容器宽度 - 总宽度 + 当前按钮位置
                float xPos = -totalWidth + i * (buttonSize + buttonSpacing);
                rectTransform.anchoredPosition = new Vector2(xPos, 0f);
            }
        }
    }

    // 技能按钮点击事件
    private void OnSkillButtonClicked(int buttonIndex)
    {
        if (buttonIndex < 0 || buttonIndex >= skillButtons.Count)
        {
            Debug.LogError("无效的技能按钮索引: " + buttonIndex);
            return;
        }

        SkillButtonInfo buttonInfo = skillButtons[buttonIndex];

        // 检查冷却是否完成
        if (!buttonInfo.isCooldownComplete)
        {
            Debug.Log("技能冷却尚未完成！");
            return;
        }

        // 检查士兵是否存在且存活
        if (buttonInfo.soldier == null)
        {
            Debug.LogError("对应的士兵不存在！");
            return;
        }

        SoldierController soldierCtrl = buttonInfo.soldier.GetComponent<SoldierController>();
        if (soldierCtrl == null || !soldierCtrl.isAlive)
        {
            Debug.LogError("士兵控制器不存在或已死亡！");
            return;
        }

        NetworkIdentity soldierIdentity = buttonInfo.soldier.GetComponent<NetworkIdentity>();
        if (soldierIdentity == null)
        {
            Debug.LogError("技能目标缺少 NetworkIdentity，无法网络请求！");
            return;
        }

        // 获取士兵的单位ID（用于区分不同兵种）
        int unitID = soldierCtrl.unitID;

        // 检查单位ID是否有效
        if (unitID < 0 || unitID >= allUnits.Length)
        {
            Debug.LogError("无效的单位ID: " + unitID);
            return;
        }

        // 获取当前单位的技能数据
        UnitData unitData = allUnits[unitID];
        SkillEffect skillEffect = unitData.skillEffect;

        // 通过本地玩家对象发 Command，再由服务器定位士兵执行技能（避免无权限 Command）
        CmdActivateSoldierSkill(soldierIdentity.netId, (int)skillEffect.effectType, skillEffect.effectValue);
        Debug.Log($"士兵 {soldierCtrl.unitName} 技能【{unitData.skillName}】请求激活，{skillEffect.effectDescription}");

        // 开始冷却
        buttonInfo.isCooldownComplete = false;
        buttonInfo.currentCooldown = buttonInfo.cooldownTime;

        // 设置冷却遮罩初始状态
        buttonInfo.cooldownMask.fillAmount = 0f;
    }

    [Command]
    private void CmdActivateSoldierSkill(uint soldierNetId, int effectTypeInt, float effectValue)
    {
        if (!NetworkServer.spawned.TryGetValue(soldierNetId, out NetworkIdentity soldierIdentity))
        {
            Debug.LogWarning($"[技能] 服务器未找到士兵 netId={soldierNetId}");
            return;
        }

        SoldierController soldierCtrl = soldierIdentity.GetComponent<SoldierController>();
        if (soldierCtrl == null || !soldierCtrl.isAlive)
        {
            Debug.LogWarning($"[技能] 目标无效或已死亡 netId={soldierNetId}");
            return;
        }

        // 安全校验：只允许对本方单位释放技能
        PlayerElixir playerElixir = connectionToClient?.identity?.GetComponent<PlayerElixir>();
        if (playerElixir != null && soldierCtrl.teamIndex != playerElixir.playerIndex)
        {
            Debug.LogWarning($"[技能] 拒绝越权技能请求: conn={connectionToClient.connectionId}, soldierTeam={soldierCtrl.teamIndex}, playerTeam={playerElixir.playerIndex}");
            return;
        }

        soldierCtrl.ServerActivateSkill(effectTypeInt, effectValue);
    }

    // 删除士兵对应的技能按钮（支持按 GameObject 或 netId 删除，后者用于对象池 UnSpawn 后 Rpc 仍能正确删除）
    public void RemoveSkillButton(GameObject soldier)
    {
        if (soldier != null && soldierToButtonMap.ContainsKey(soldier))
        {
            RemoveSkillButtonInternal(soldierToButtonMap[soldier], soldier);
        }
    }

    /// <summary>按 netId 删除技能按钮，解决 Host 对象池 UnSpawn 先于 Rpc 导致技能图标未删除的问题</summary>
    public void RemoveSkillButtonByNetId(uint netId)
    {
        if (netId == 0) return;
        List<(GameObject key, SkillButtonInfo info)> toRemove = null;
        foreach (var kv in soldierToButtonMap)
        {
            if (kv.Value.soldierNetId == netId)
            {
                if (toRemove == null) toRemove = new List<(GameObject, SkillButtonInfo)>();
                toRemove.Add((kv.Key, kv.Value));
            }
        }
        if (toRemove == null) return;
        foreach (var (key, info) in toRemove)
        {
            skillButtons.Remove(info);
            soldierToButtonMap.Remove(key);
            if (info.skillButton != null) Destroy(info.skillButton);
        }
        ArrangeSkillButtons();
        Debug.Log($"已按 netId={netId} 删除技能按钮");
    }

    private void RemoveSkillButtonInternal(SkillButtonInfo buttonInfo, GameObject soldier)
    {
        skillButtons.Remove(buttonInfo);
        soldierToButtonMap.Remove(soldier);
        if (buttonInfo.skillButton != null) Destroy(buttonInfo.skillButton);
        ArrangeSkillButtons();
        Debug.Log($"已删除士兵 {soldier?.name} 的技能按钮");
    }

    // 清理已销毁单位对应的技能按钮（Rpc 可能丢失时的备用逻辑）
    private void CleanupDestroyedSkillButtons()
    {
        List<(GameObject key, SkillButtonInfo info)> toRemove = null;
        foreach (var kv in soldierToButtonMap)
        {
            if (kv.Key == null)
            {
                if (toRemove == null) toRemove = new List<(GameObject, SkillButtonInfo)>();
                toRemove.Add((kv.Key, kv.Value));
            }
        }
        if (toRemove == null) return;
        foreach (var (key, info) in toRemove)
        {
            skillButtons.Remove(info);
            soldierToButtonMap.Remove(key);
            if (info.skillButton != null) Destroy(info.skillButton);
        }
        ArrangeSkillButtons();
    }

    // 更新所有技能按钮的冷却进度
    private void UpdateSkillCooldowns()
    {
        CleanupDestroyedSkillButtons();
        for (int i = 0; i < skillButtons.Count; i++)
        {
            SkillButtonInfo buttonInfo = skillButtons[i];

            if (!buttonInfo.isCooldownComplete)
            {
                // 减少冷却时间
                buttonInfo.currentCooldown -= Time.deltaTime;

                if (buttonInfo.currentCooldown <= 0f)
                {
                    // 冷却完成
                    buttonInfo.currentCooldown = 0f;
                    buttonInfo.isCooldownComplete = true;
                    buttonInfo.cooldownMask.fillAmount = 0f; // 完全显示图标（遮罩隐藏）
                    Debug.Log($"技能按钮 {i} 冷却完成！");
                }
                else
                {
                    // 更新冷却遮罩
                    float cooldownPercentage = buttonInfo.currentCooldown / buttonInfo.cooldownTime;
                    buttonInfo.cooldownMask.fillAmount = cooldownPercentage; // 圆饼进度条显示剩余冷却时间
                }
            }
            else
            {
                // 冷却已完成，隐藏遮罩
                buttonInfo.cooldownMask.fillAmount = 0f;
            }
        }
    }

    // 更新UI显示当前数组中的兵种
    void UpdateUnitQueueUI()
    {
        // 确保数组已经初始化
        if (handCards == null || handCards.Length != maxHandSize)
        {
            return;
        }

        // 更新每个Toggle的显示
        for (int i = 0; i < unitToggles.Length; i++)
        {
            if (i < maxHandSize) // 只更新前4个Toggle（手牌）
            {
                int unitIndex = handCards[i];
                unitToggles[i].isOn = false;

                // 检查兵种索引是否有效
                if (unitIndex >= 0 && unitIndex < allUnits.Length)
                {
                    UnitData unit = allUnits[unitIndex];

                    // 更新图标
                    if (unitIcons[i] != null && unit.unitIcon != null)
                    {
                        unitIcons[i].sprite = unit.unitIcon;
                        unitIcons[i].enabled = true;
                    }

                    // 更新消耗文本
                    if (unitCostTexts[i] != null)
                    {
                        unitCostTexts[i].text = unit.elixirCost.ToString();
                        unitCostTexts[i].enabled = true;
                    }

                    // 启用Toggle
                    unitToggles[i].interactable = true;
                }
                else
                {
                    // 无效的兵种索引，隐藏显示
                    if (unitIcons[i] != null) unitIcons[i].enabled = false;
                    if (unitCostTexts[i] != null) unitCostTexts[i].enabled = false;
                    unitToggles[i].interactable = false;
                    unitToggles[i].isOn = false;
                }
            }
            else
            {
                // 超出4个位置的Toggle，隐藏显示
                if (unitIcons[i] != null) unitIcons[i].enabled = false;
                if (unitCostTexts[i] != null) unitCostTexts[i].enabled = false;
                unitToggles[i].interactable = false;
                unitToggles[i].isOn = false;
            }
        }
    }

    // 取消选择当前兵种
    public void CancelSelection()
    {
        selectedUnitIndex = -1;

        if (networkUnitPlacer != null)
        {
            networkUnitPlacer.CancelPlacementLocal();
        }
        //else
        //{
        //    // 备用：使用GameManager的取消放置逻辑
        //    GameManager.Instance.CancelPlacement();
        //}
    }

    /// <summary>
    /// 平移Toggle位置
    /// </summary>
    /// <param name="toggleIndex">Toggle索引</param>
    /// <param name="offset">偏移量</param>
    public void TranslateToggle(int toggleIndex, Vector2 offset)
    {
        if (toggleIndex >= 0 && toggleIndex < unitToggles.Length)
        {
            Toggle toggle = unitToggles[toggleIndex];
            RectTransform rectTransform = toggle.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition += offset;
                Debug.Log($"UIManager: Toggle {toggleIndex} 位置偏移: {offset}");
            }
        }
    }
}