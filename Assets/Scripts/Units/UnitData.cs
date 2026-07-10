﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿using UnityEngine;

// 技能效果类型枚举
public enum SkillEffectType
{
    IncreaseAttackDamage,     // 增加物理攻击力
    IncreaseMagicDamage,       // 增加法术攻击力
    IncreaseDefense,           // 增加防御力
    IncreaseMoveSpeed,         // 增加移动速度
    HealHealth,               // 恢复生命值
    DecreaseAttackCooldown,    // 减少攻击冷却
    IncreaseAttackRange,       // 增加攻击范围
    IncreaseMaxHealth,         // 增加最大生命值
    IncreaseAllStats,          // 所有属性小幅提升
    CustomEffect               // 自定义效果
}

// 技能效果数据
[System.Serializable]
public class SkillEffect
{
    public SkillEffectType effectType;  // 技能效果类型
    public float effectValue;           // 效果数值
    public string effectDescription;    // 效果描述
}

// 兵种数据结构，用于存储每个兵种的完整信息
[System.Serializable]
public class UnitData
{
    public string unitName;           // 兵种名称
    public GameObject unitPrefab;     // 兵种预制体
    public float elixirCost;          // 消耗的源石
    public Sprite unitIcon;           // 兵种图标
    public string unitDescription;    // 兵种描述
    public int unitType;              // 兵种类型（0=近战，1=远程，2=治疗等）
    
    // 技能相关属性
    public Sprite skillIcon;          // 技能图标
    public float skillCooldownTime;   // 技能冷却时间
    public string skillName;          // 技能名称
    public string skillDescription;   // 技能描述
    public SkillEffect skillEffect;   // 技能效果
    
    // 构造函数
    public UnitData(string name, GameObject prefab, float cost, Sprite icon = null, string description = "", int type = 0)
    {
        unitName = name;
        unitPrefab = prefab;
        elixirCost = cost;
        unitIcon = icon;
        unitDescription = description;
        unitType = type;
        
        // 初始化默认技能数据
        skillCooldownTime = 3f;        // 默认冷却时间3秒
        skillName = "技能";
        skillDescription = "基础技能";
        skillEffect = new SkillEffect { 
            effectType = SkillEffectType.IncreaseAttackDamage, 
            effectValue = 10f, 
            effectDescription = "增加10点攻击力"
        };
    }
}
