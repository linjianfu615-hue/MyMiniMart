using UnityEngine;

/// <summary>
/// 全局物品类型枚举 (Global Enum)
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
/// 物品的身份标签，挂载在各种物品的预制体上
/// </summary>
public class ItemData : MonoBehaviour
{
    [Header("物品类型")]
    [Tooltip("在预制体面板中选择该物品的真实类型")]
    public ItemType itemType;
}