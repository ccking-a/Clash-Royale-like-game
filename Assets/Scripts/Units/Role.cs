﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿using Spine.Unity;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

public class Role : MonoBehaviour
{
    // Start is called before the first frame update

    [Header("----------------------角色模型----------------------")]

    [Header("角色模型配置")]
    [Tooltip("正面模型")]
    public GameObject CombatFront;

    [Tooltip("背面模型")]
    public GameObject CombatBack;

    [Tooltip("基建模型")]
    public GameObject Infrastructure;

    [Tooltip("基建模型")]
    public GameObject skill;

    [Header("角色模型的SkeletonAnimation组件")]
    [Tooltip("正面SkeletonAnimation")]
    public SkeletonAnimation CombatFrontSA;

    [Tooltip("背面SkeletonAnimation")]
    public SkeletonAnimation CombatBackSA;

    [Tooltip("基建SkeletonAnimation")]
    public SkeletonAnimation InfrastructureSA;

    [Tooltip("skillSkeletonAnimation")]
    public SkeletonAnimation skillani;

    // 缓存的Renderer组件，用于性能优化
    private Renderer CombatFrontRenderer;
    private Renderer CombatBackRenderer;
    private Renderer InfrastructureRenderer;
    private Renderer skillRenderer;

    public enum syncType
    {
        AUTO,
        MANUAL
    };


    [Header("--------------------同步缩放设置--------------------")]
    [Header("同步方式")]
    [Tooltip("选择同步方式")]
    public syncType syncMethod = syncType.AUTO;


    [Header("--------------------自动同步设置--------------------")]
    [Header("大小同步")]
    [Tooltip("同步参考对象")]
    public GameObject Target;

    [Header("需要同步的模型")]
    [Tooltip("正面")]
    public bool syncFront = true;
    [Tooltip("背面")]
    public bool syncBack = false;
    [Tooltip("基建")]
    public bool syncInfra = false;
    [Tooltip("特效")]
    public bool syncskill = false;

    [Header("同步缩放参数")]
    [Tooltip("同步参数")]
    public float syncScale = 1;
    [Tooltip("最大缩放")]
    public float maxScale = 1f;
    [Tooltip("最小缩放")]
    public float minScale = 1f;


    [Header("--------------------手动同步参数--------------------")]

    [Header("单独缩放比例")]
    [Tooltip("正面模型缩放比例")]
    public float FrontScale = 1;
    [Tooltip("背面模型缩放比例")]
    public float BackScale = 1;
    [Tooltip("基建模型缩放比例")]
    public float InfraScale = 1;


    [Header("单独同步缩放比例")]
    [Tooltip("缩放参数")]
    public float manualScale = 1;

    // 原始缩放比例
    private Vector3 originalFrontScale;
    private Vector3 originalBackScale;
    private Vector3 originalInfraScale;
    private Vector3 originalSkillScale;


    public enum RoleState
    {
        Defaut,
        AttackFront,
        AttackBack,
        Move,
        Die,
        OutArea
    };

    [Header("----------------------角色信息----------------------")]
    [Header("当前角色状态")]
    [Tooltip("Defaut:默认值\n" +
             "AttackFront:正面攻击状态\n" +
             "AttackBack:背面攻击状态\n" +
             "Move:移动状态\n" +
             "Die:死亡状态\n"+
             "OutArea:状态区域")]
    public RoleState state = RoleState.Defaut;

    [Header("角色攻击力")]
    [Tooltip("角色攻击力")]
    public float attack = 20;

    [Header("角色生命值")]
    [Tooltip("角色生命值")]
    public float health = 100;

    [Header("角色速度")]
    [Tooltip("角色速度")]
    public float speed = 5;

    [Header("角色最大SP")]
    [Tooltip("角色最大SP")]
    public float mSP = 50;

    [Header("角色当前SP")]
    [Tooltip("角色当前SP")]
    public float cSP = 0;

    [Header("是否应受到伤害")]
    [Tooltip("是否应受到伤害")]
    public bool isHurt = false;
    //public int state = 0;
    //private void Awake()
    //{
    //    front = this.transform.Find("/����").gameObject;
    //    back = this.transform.Find("/����").gameObject;
    //    defaut = this.transform.Find("/����").gameObject;

    //    frontSA = front.GetComponent<SkeletonAnimation>();
    //    backSA = back.GetComponent<SkeletonAnimation>();
    //    defautSA= defaut.GetComponent<SkeletonAnimation>();

    //}

    void Start()
    {
        CombatFront = this.transform.Find("正面").gameObject;
        CombatBack = this.transform.Find("背面").gameObject;
        Infrastructure = this.transform.Find("基建").gameObject;
        //skill = this.transform.Find("skill").gameObject;

        if (CombatFront == null) Debug.LogError("未找到正面模型");
        if (CombatBack == null) Debug.LogError("未找到背面模型");
        if (Infrastructure == null) Debug.LogError("未找到基建模型");
        //if (skill == null) Debug.LogError("未找到特效模型");

        CombatFrontSA = CombatFront.GetComponent<SkeletonAnimation>();
        CombatBackSA = CombatBack.GetComponent<SkeletonAnimation>();
        InfrastructureSA = Infrastructure.GetComponent<SkeletonAnimation>();
        //skillani = skill.GetComponent<SkeletonAnimation>();
        
        // 缓存Renderer组件，优化性能
        CombatFrontRenderer = CombatFront.GetComponent<Renderer>();
        CombatBackRenderer = CombatBack.GetComponent<Renderer>();
        InfrastructureRenderer = Infrastructure.GetComponent<Renderer>();
        //skillRenderer = skill.GetComponent<Renderer>();

        if (CombatFrontSA == null) Debug.LogError("正面模型未找到SkeletonAnimation组件");
        if (CombatBackSA == null) Debug.LogError("背面模型未找到SkeletonAnimation组件");
        if (InfrastructureSA == null) Debug.LogError("基建模型未找到SkeletonAnimation组件");
        //if (skillani == null) Debug.LogError("特效模型未找到SkeletonAnimation组件");

        // 保存原始缩放比例
        originalFrontScale = CombatFront.transform.localScale;
        originalBackScale = CombatBack.transform.localScale;
        originalInfraScale = Infrastructure.transform.localScale;
       // originalSkillScale = skill.transform.localScale;

        //设置默认状态
        setDefault();
        //同步
        switch (syncMethod)
        {
            case syncType.AUTO:
                AutoSynchronization();
                break;
            case syncType.MANUAL:
                ManulSynchronization();
                break;
        }

        CombatFrontSA.AnimationState.Event += onAttackHandle;
        CombatBackSA.AnimationState.Event += onAttackHandle;

        Hide(); 
        onIdle();
    }

    void onAttackHandle(Spine.TrackEntry trackEntry, Spine.Event e)
    {
        //e.Data
        Debug.Log(e.Data.Name );
        //trackEntry.Animation.Name;

        string name;
        if (CombatFront.activeSelf)
            name = CombatFront.name;
        else
            name = CombatBack.name;
        Debug.Log( name );

        if (e.Data.Name == "OnAttack")
            isHurt = true;

    }

    // Update is called once per frame
    void Update()
    {
        // 移除点击切换模型的功能，现在通过SoldierController脚本调用方法来切换模型
        // 保留Update方法，但不包含任何输入处理
    }
    void AutoSynchronization()
    {
        if (Target == null)
            Target = CombatBack;

        SkeletonAnimation targetSA = Target.GetComponent<SkeletonAnimation>();
        targetSA.skeleton.UpdateWorldTransform();

        float x, y, width, height;
        float[] vertexBuffer = null;
        targetSA.skeleton.GetBounds(out x, out y, out width, out height, ref vertexBuffer);

        // 确保最小高度值防止异常
        float minValidHeight = 0.1f;
        height = Mathf.Max(height, minValidHeight);

        if (syncFront && CombatFront != null && CombatFrontRenderer != null) 
        {
            Vector2 V = CombatFrontRenderer.bounds.size;
            
            // 防止V.y过小值导致异常
            float validVY = Mathf.Max(V.y, minValidHeight);
            float scale = height / validVY;
            
            // 限制缩放比例
            scale = Mathf.Clamp(scale, minScale, maxScale);
            
            Synchronization(CombatFront, scale);
            Debug.Log("战斗正面模型缩放:" + scale);
        }
        if (syncBack && CombatBack != null && CombatBackRenderer != null)
        {
            Vector2 V = CombatBackRenderer.bounds.size;
            
            // 防止V.y过小值导致异常
            float validVY = Mathf.Max(V.y, minValidHeight);
            float scale = height / validVY;
            
            // 限制缩放比例
            scale = Mathf.Clamp(scale, minScale, maxScale);
            
            Synchronization(CombatBack, scale);
            Debug.Log("战斗背面模型缩放:" + scale);
        }
        if (syncInfra && Infrastructure != null && InfrastructureRenderer != null)
        {
            Vector2 V = InfrastructureRenderer.bounds.size;
            
            // 防止V.y过小值导致异常
            float validVY = Mathf.Max(V.y, minValidHeight);
            float scale = height / validVY;
            
            // 限制缩放比例
            scale = Mathf.Clamp(scale, minScale, maxScale);
            
            Synchronization(Infrastructure, scale);
            Debug.Log("基建模型缩放:" + scale);
        }
        //if (syncskill && skill != null && skillRenderer != null)
        //{
        //    Vector2 V = skillRenderer.bounds.size;
            
        //    // 防止V.y过小值导致异常
        //    float validVY = Mathf.Max(V.y, minValidHeight);
        //    float scale = height / validVY;
            
        //    // 限制缩放比例
        //    scale = Mathf.Clamp(scale, minScale, maxScale);
            
        //    Synchronization(skill, scale);
        //    Debug.Log("特效模型缩放:" + scale);
        //}

        //应用同步缩放
        if (syncScale != 1)
        {
            // 应用同步缩放时也应限制
            float clampedSyncScale = Mathf.Clamp(syncScale, minScale, maxScale);
            Synchronization(CombatFront, clampedSyncScale);
            Synchronization(CombatBack, clampedSyncScale);
            Synchronization(Infrastructure, clampedSyncScale);
            //Synchronization(skill, clampedSyncScale);
        }
    }
    void ManulSynchronization()
    {
        if (CombatFront != null && FrontScale != 1) 
        {
            float clampedScale = Mathf.Clamp(FrontScale, minScale, maxScale);
            Synchronization(CombatFront, clampedScale);
        }

        if (CombatBack != null && BackScale != 1) 
        {
            float clampedScale = Mathf.Clamp(BackScale, minScale, maxScale);
            Synchronization(CombatBack, clampedScale);
        }

        if (Infrastructure != null && InfraScale != 1) 
        {
            float clampedScale = Mathf.Clamp(InfraScale, minScale, maxScale);
            Synchronization(Infrastructure, clampedScale);
        }

        if (manualScale != 1)
        {
            float clampedScale = Mathf.Clamp(manualScale, minScale, maxScale);
            Synchronization(CombatFront, clampedScale);
            Synchronization(CombatBack, clampedScale);
            Synchronization(Infrastructure, clampedScale);
        }
    }

    void Synchronization(GameObject gobj, float scale)
    {
        // �õ��ԭʼ����ϵ��
        Vector3 originalScale = Vector3.one;
        if (gobj == CombatFront)
            originalScale = originalFrontScale;
        else if (gobj == CombatBack)
            originalScale = originalBackScale;
        else if (gobj == Infrastructure)
            originalScale = originalInfraScale;
        //else if (gobj == skill)
        //    originalScale = originalSkillScale;
        
        // ���ҵ���ϵ��
        float clampedScale = Mathf.Clamp(scale, minScale, maxScale);
        
        // ��ԭʼϵ��Ϊ���ݣ���Ӧ����ϵ��
        Vector3 newScale = originalScale * clampedScale;
        
        gobj.transform.localScale = newScale;
    }


    /*---------------------------------------------------------------------------*/
    void onAttackF()
    {
        if (!CombatFront.activeSelf)
            CombatFront.SetActive(true);
        if (CombatBack.activeSelf)
            CombatBack.SetActive(false);
        if (Infrastructure.activeSelf)
            Infrastructure.SetActive(false);
        //if (skill.activeSelf)
        //    skill.SetActive(false);
        CombatFrontSA.AnimationState.SetAnimation(0, "Attack", true);
    }
    void onAttackB()
    {
        if (CombatFront.activeSelf)
            CombatFront.SetActive(false);
        if (!CombatBack.activeSelf)
            CombatBack.SetActive(true);
        if (Infrastructure.activeSelf)
            Infrastructure.SetActive(false);
        //if (skill.activeSelf)
        //    skill.SetActive(false);
        CombatBackSA.AnimationState.SetAnimation(0, "Attack", true);
    }
    void onMove()
    {
        if (CombatFront.activeSelf)
            CombatFront.SetActive(false);
        if (CombatBack.activeSelf)
            CombatBack.SetActive(false);
        if (!Infrastructure.activeSelf)
            Infrastructure.SetActive(true);
        //if (skill.activeSelf)
        //    skill.SetActive(false);
        InfrastructureSA.AnimationState.SetAnimation(0, "Move", true);
    }

    void onSkillF()
    {
        if (!CombatFront.activeSelf)
            CombatFront.SetActive(true);
        if (CombatBack.activeSelf)
            CombatBack.SetActive(false);
        if (Infrastructure.activeSelf)
            Infrastructure.SetActive(false);
        //if (skill.activeSelf)
        //    skill.SetActive(false);
        CombatFrontSA.AnimationState.SetAnimation(0, "Skill", true);
    }
    //void onSkillEffect()
    //{
    //    if (!skill.activeSelf)
    //        skill.SetActive(true);

    //    skillani.AnimationState.SetAnimation(0, "animation", false);
    //}

    void onIdle()
    {
        if (!CombatFront.activeSelf)
            CombatFront.SetActive(true);
        if (CombatBack.activeSelf)
            CombatBack.SetActive(false);
        if (Infrastructure.activeSelf)
            Infrastructure.SetActive(false); 
        //if (skill.activeSelf)
        //    skill.SetActive(false);
        CombatFrontSA.AnimationState.SetAnimation(0, "Idle", true);
    }

        void Hide()
    {
        if (CombatFront.activeSelf)
            CombatFront.SetActive(false);
        if (CombatBack.activeSelf)
            CombatBack.SetActive(false);
        if (Infrastructure.activeSelf)
            Infrastructure.SetActive(false);
        //if (skill.activeSelf)
        //    skill.SetActive(false);
    }

    void onDie()
    {
        if (!CombatFront.activeSelf)
            CombatFront.SetActive(true);
        if (CombatBack.activeSelf)
            CombatBack.SetActive(false);
        if (Infrastructure.activeSelf)
            Infrastructure.SetActive(false);
        //if (skill.activeSelf)
        //    skill.SetActive(false);
        CombatFrontSA.AnimationState.SetAnimation(0, "Die", false);
        isHurt = false;
    }
    void setDefault()
    {
        if (!CombatFront.activeSelf)
            CombatFront.SetActive(true);
        if (!CombatBack.activeSelf)
            CombatBack.SetActive(true);
        if (!Infrastructure.activeSelf)
            Infrastructure.SetActive(true);
        //if (!skill.activeSelf)
        //    skill.SetActive(true);

        CombatFrontSA.AnimationState.SetAnimation(0, "Default", true);
        CombatBackSA.AnimationState.SetAnimation(0, "Default", true);
        
        //skillani.AnimationState.SetAnimation(0, "animation", true);
        InfrastructureSA.AnimationState.SetAnimation(0, "Interact", true);
    }
}

