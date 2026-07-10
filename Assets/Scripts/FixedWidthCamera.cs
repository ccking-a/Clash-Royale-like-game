﻿﻿﻿﻿﻿﻿﻿﻿﻿using UnityEngine;

public class FixedWidthCamera : MonoBehaviour
{
    [Header("相机设置")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float targetWidth = 18f; // 期望的固定宽度（世界单位），初始化为18

    [Header("参考分辨率")]
    [SerializeField] private float referenceWidth = 1080f;
    [SerializeField] private float referenceHeight = 1920f;

    void Start()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("FixedWidthCamera: 找不到主相机！");
                return;
            }
        }
        
        if (!mainCamera.orthographic)
        {
            Debug.LogError("FixedWidthCamera: 相机必须是正交相机！");
            return;
        }
        
        // 初始化屏幕尺寸
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        
        UpdateCameraSize();
        Debug.Log($"FixedWidthCamera: 初始化完成，当前targetWidth: {targetWidth}");
    }

    void Update()
    {
        // 如果窗口大小改变，重新适配
        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
        {
            UpdateCameraSize();
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
        }
    }

    private int lastScreenWidth;
    private int lastScreenHeight;

    void UpdateCameraSize()
    {
        if (mainCamera == null || !mainCamera.orthographic) return;

        // 计算当前屏幕的宽高比
        float currentAspect = (float)Screen.width / Screen.height;

        // 根据固定宽度计算所需的orthographicSize
        // orthographicSize = (目标宽度 / 2) / 宽高比
        float orthographicSize = (targetWidth / 2f) / currentAspect;

        mainCamera.orthographicSize = orthographicSize;
    }
    
    /// <summary>
    /// 平滑设置目标宽度，持续指定时间
    /// </summary>
    /// <param name="newWidth">新的目标宽度</param>
    /// <param name="duration">过渡持续时间（秒）</param>
    public void SetTargetWidth(float newWidth, float duration = 0f)
    {
        if (duration <= 0f)
        {
            // 立即改变
            targetWidth = newWidth;
            UpdateCameraSize();
        }
        else
        {
            // 平滑过渡
            StartCoroutine(SmoothWidthTransition(targetWidth, newWidth, duration));
        }
    }
    
    /// <summary>
    /// 平滑宽度过渡协程
    /// </summary>
    /// <param name="startWidth">起始宽度</param>
    /// <param name="endWidth">目标宽度</param>
    /// <param name="duration">过渡持续时间</param>
    private System.Collections.IEnumerator SmoothWidthTransition(float startWidth, float endWidth, float duration)
    {
        float elapsedTime = 0f;
        
        while (elapsedTime < duration)
        {
            // 使用线性插值计算当前宽度
            targetWidth = Mathf.Lerp(startWidth, endWidth, elapsedTime / duration);
            UpdateCameraSize();
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // 确保最终值准确
        targetWidth = endWidth;
        UpdateCameraSize();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (mainCamera != null && mainCamera.orthographic)
        {
            UpdateCameraSize();
        }
    }
#endif
}