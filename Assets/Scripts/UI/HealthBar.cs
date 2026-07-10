using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    // 血条填充图像引用
    public Image healthBarFill;
    // 目标单位的Transform引用
    public Transform targetTransform;
    // 血条的水平偏移量
    public float offsetX = 0f;
    // 血条的垂直偏移量
    public float offsetY = 1.5f;
    // 血条的 Z 轴偏移量（可调，用于深度/层级微调）
    public float offsetZ = 0f;
    // 主相机引用
    private Camera mainCamera;
    // 最大生命值
    private float maxHealth;
    // 当前生命值
    private float currentHealth;
    // 血条动画过渡速度
    private float healthBarSpeed = 0.5f;
    // 目标填充值（用于平滑过渡）
    private float targetFillAmount;
    // 是否显示血条的标志
    private bool isVisible = true;
    // 隐藏计时器
    private float hideTimer = 0f;
    // 血条自动隐藏时间
    private float autoHideTime = 3f;
    // 是否正在进行生命值变化的动画
    private bool isAnimating = false;
    // CanvasGroup组件引用
    private CanvasGroup canvasGroup;
    // 当前填充值
    private float currentFillValue;
    // 自动隐藏计时器
    private float autoHideTimer;

    void Start()
    {
        // 获取主相机引用
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("找不到主相机！");
        }
        
        // 初始化目标填充值
        if (healthBarFill != null)
        {
            targetFillAmount = healthBarFill.fillAmount;
        }
        
        canvasGroup = GetComponent<CanvasGroup>();
        currentFillValue = targetFillAmount;
        
        // 确保在访问healthBarFill.fillAmount之前进行空引用检查
        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = currentFillValue;
        }
        
        autoHideTimer = autoHideTime;
        
        // 添加Canvas配置检查
        CheckCanvasConfiguration();
    }
    
    // 检查Canvas配置
    private void CheckCanvasConfiguration()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("HealthBar: Canvas component not found!");
            return;
        }
        
        Debug.Log("HealthBar Canvas Configuration:");
        Debug.Log($"  Render Mode: {canvas.renderMode}");
        Debug.Log($"  Sorting Order: {canvas.sortingOrder}");
        Debug.Log($"  Sorting Layer: {canvas.sortingLayerName}");
        Debug.Log($"  World Camera: {(canvas.worldCamera != null ? canvas.worldCamera.name : "None")}");
        Debug.Log($"  Plane Distance: {canvas.planeDistance}");
        
        // 确保Canvas设置正确
        if (canvas.renderMode != RenderMode.WorldSpace)
        {
            Debug.LogWarning("HealthBar: Canvas render mode is not WorldSpace, changing to WorldSpace");
            canvas.renderMode = RenderMode.WorldSpace;
        }
        
        if (canvas.worldCamera == null)
        {
            Debug.LogWarning("HealthBar: Canvas world camera is null, setting to main camera");
            canvas.worldCamera = Camera.main;
        }
        
        if (canvas.planeDistance < 0.1f)
        {
            Debug.LogWarning("HealthBar: Canvas plane distance is too small, setting to 1.0f");
            canvas.planeDistance = 1.0f;
        }
    }

    void Update()
    {
        // 直接更新血条填充值
        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = targetFillAmount;
        }
        HandleVisibility();
    }

    void LateUpdate()
    {
        // 目标已销毁或已死亡则删除血条（处理网络延迟：玩家二可能先收到 isAlive 同步，Rpc 尚未到达）
        if (targetTransform == null)
        {
            Destroy(gameObject);
            return;
        }
        var sc = targetTransform.GetComponent<SoldierController>();
        if (sc != null && !sc.isAlive)
        {
            Destroy(gameObject);
            return;
        }
        if (mainCamera == null)
            mainCamera = Camera.main;
        if (mainCamera == null) return;

        // 确保 Canvas 的 worldCamera 有效（避免 "Canvas world camera is null" 警告）
        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null && canvas.worldCamera == null)
            canvas.worldCamera = mainCamera;

        // 使血条面向相机
        transform.rotation = mainCamera.transform.rotation;

        // 更新血条位置（世界空间偏移；玩家二摄像头角度不同，offsetZ 取反）
        float effectiveZ = (PlayerElixir.LocalInstance != null && PlayerElixir.LocalInstance.IsPlayer2) ? -offsetZ : offsetZ;
        transform.position = targetTransform.position + new Vector3(offsetX, offsetY, effectiveZ);

        // 血条已挂到场景根节点，无父物体 scale 继承问题
    }
    
    // 检查血条组件状态的调试方法
    private void CheckHealthBarComponents()
    {
        // 检查Canvas组件
        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            Debug.Log($"血条Canvas状态: renderMode={canvas.renderMode}, sortingOrder={canvas.sortingOrder}");
        }
        
        // 检查CanvasGroup组件
        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            Debug.Log($"血条CanvasGroup状态: alpha={canvasGroup.alpha}, interactable={canvasGroup.interactable}, blocksRaycasts={canvasGroup.blocksRaycasts}");
        }
        
        // 检查所有子对象
        Debug.Log($"血条子对象数量: {transform.childCount}");
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            Image image = child.GetComponent<Image>();
            if (image != null)
            {
                Debug.Log($"子对象 {i}: {child.name} - Color: {image.color}, Size: {image.rectTransform.sizeDelta}, Active: {child.gameObject.activeSelf}, SiblingIndex: {child.GetSiblingIndex()}");
            }
            else
            {
                Debug.Log($"子对象 {i}: {child.name} - No Image Component, Active: {child.gameObject.activeSelf}, SiblingIndex: {child.GetSiblingIndex()}");
            }
        }
    }
    
    // 处理血条的显示/隐藏逻辑
    private void HandleVisibility()
    {
        // 如果正在进行生命值变化动画，确保血条可见
        if (isAnimating)
        {
            ShowHealthBar();
            hideTimer = autoHideTime;
            return;
        }
        
        // 如果血条当前可见且未隐藏，则递减计时器
        if (isVisible && hideTimer > 0)
        {
            hideTimer -= Time.deltaTime;
            if (hideTimer <= 0)
            {
                HideHealthBar();
            }
        }
    }
    
    // 显示血条
    private void ShowHealthBar()
    {
        isVisible = true;
        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }
    }
    
    // 隐藏血条（降低透明度）
    private void HideHealthBar()
    {
        isVisible = false;
        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0.7f; // 半透明效果，仍然可以看到但不那么明显
        }
    }

    // 初始化血条
    public void Initialize(Transform target, float initialMaxHealth, float customOffsetY = 1.5f, float customOffsetX = 0f, float customOffsetZ = 0f)
    {
        targetTransform = target;
        maxHealth = initialMaxHealth;
        currentHealth = initialMaxHealth;
        
        // 设置 offset 为传入的值
        offsetY = customOffsetY;
        offsetX = customOffsetX;
        offsetZ = customOffsetZ;
        
        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = 1f;
            targetFillAmount = 1f;
        }
        
       // Debug.Log($"血条初始化完成，目标: {target.name}, 最大生命值: {maxHealth}, offsetX: {offsetX}, offsetY: {offsetY}");
    }
    
    // 设置血条的自动隐藏时间
    public void SetAutoHideTime(float time)
    {
        autoHideTime = Mathf.Max(0.5f, time); // 确保最小为0.5秒
    }
    
    // 设置血条动画速度
    public void SetAnimationSpeed(float speed)
    {
        healthBarSpeed = Mathf.Max(0.5f, speed); // 确保最小为0.5
    }
    
    // 设置血条的垂直偏移量
    public void SetOffsetY(float offset)
    {
        offsetY = Mathf.Abs(offset);
    }
    
    // 设置血条的水平偏移量
    public void SetOffsetX(float offset)
    {
        offsetX = offset;
    }
    
    // 设置血条大小
    public void SetHealthBarSize(float width, float height)
    {
        // 获取血条背景、边框和填充容器的RectTransform
        Transform backgroundTransform = transform.Find("Background");
        Transform borderTransform = transform.Find("Border");
        Transform fillContainerTransform = transform.Find("FillContainer");
        
        // 计算填充容器的内边距
        float paddingX = 0.2f;
        float paddingY = 0.15f;
        
        // 更新背景大小
        if (backgroundTransform != null)
        {
            RectTransform backgroundRect = backgroundTransform.GetComponent<RectTransform>();
            if (backgroundRect != null)
            {
                backgroundRect.sizeDelta = new Vector2(width, height);
            }
        }
        
        // 更新边框大小
        if (borderTransform != null)
        {
            RectTransform borderRect = borderTransform.GetComponent<RectTransform>();
            if (borderRect != null)
            {
                borderRect.sizeDelta = new Vector2(width, height);
            }
        }
        
        // 更新填充容器大小
        if (fillContainerTransform != null)
        {
            RectTransform fillContainerRect = fillContainerTransform.GetComponent<RectTransform>();
            if (fillContainerRect != null)
            {
                fillContainerRect.sizeDelta = new Vector2(width - paddingX, height - paddingY);
            }
        }
    }

    // 更新血条显示
    public void UpdateHealthBar(float newCurrentHealth, float newMaxHealth)
    {
        // 更新当前和最大生命值
        currentHealth = newCurrentHealth;
        maxHealth = newMaxHealth;
        
        // 计算填充比例并应用限制
        float newFillAmount = Mathf.Clamp01(currentHealth / maxHealth);
        targetFillAmount = newFillAmount;
        
        // 直接更新血条填充值
        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = newFillAmount;
        }
        
        // 开始生命值变化动画
        StartHealthChangeAnimation();
        
        // 重置隐藏计时器
        hideTimer = autoHideTime;
        ShowHealthBar();
    }
    
    // 开始生命值变化动画
    private void StartHealthChangeAnimation()
    {
        if (!isAnimating)
        {
            isAnimating = true;
            StartCoroutine(HealthChangeAnimationCoroutine());
        }
    }
    
    // 生命值变化动画协程
    private IEnumerator HealthChangeAnimationCoroutine()
    {
        // 获取CanvasGroup组件用于闪烁效果
        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        
        if (canvasGroup != null)
        {
            // 血条闪烁效果
            float originalAlpha = canvasGroup.alpha;
            canvasGroup.alpha = Mathf.Clamp01(originalAlpha + 0.3f); // 增加透明度
            
            yield return new WaitForSeconds(0.1f);
            
            // 恢复原来的透明度
            canvasGroup.alpha = originalAlpha;
        }
        
        // 等待动画完成
        yield return new WaitForSeconds(0.3f);
        
        isAnimating = false;
    }
}