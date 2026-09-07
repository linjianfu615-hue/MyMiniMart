using UnityEngine;

/// <summary>
/// 全局物品类型枚举 (Global Enum)
/// 可以在任何脚本中直接通过 ItemType.Tomato 来调用
/// </summary>
public enum ItemType
{
    None = 0,       // 空 / 无
    Money,          // 钱/金币
    Egg,            // 鸡蛋
    Tomato,         // 西红柿
    Wheat,          // 小麦
    Bread,          // 面包
    Milk,           // 牛奶
    CannedFood,     // 罐头
    Flour           // 面粉
}

/// <summary>
/// 物品数据配置类（基础结构）
/// 加上 [System.Serializable] 后，可以在 Unity 属性面板中直接可视化配置
/// </summary>
[System.Serializable]
public class ItemData
{
    [Header("物品类型")]
    public ItemType itemType;

    [Header("物品预制体 (用于生成和掉落)")]
    public GameObject itemPrefab;

    // 如果后续你需要做UI背包或商店，可以取消下面这行的注释
    // [Header("物品UI图标")]
    // public Sprite itemIcon; 
}