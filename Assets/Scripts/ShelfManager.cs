using UnityEngine;
using DG.Tweening;

/// <summary>
/// 货架管理器：负责接收玩家/工作者的货物，并提供给顾客AI
/// </summary>
public class ShelfManager : MonoBehaviour
{
    [Header("货架设置 (Shelf Settings)")]
    [Tooltip("该货架专属接收的物品类型（如：鸡蛋）")]
    public ItemType acceptedItemType = ItemType.Egg;

    [Tooltip("拖入 Input_Gray 下的所有 Slot 子节点")]
    public Transform[] displaySlots;

    [Tooltip("物品摆放到货架上的动画时长")]
    public float placeAnimDuration = 0.3f;

    // 精准记录每个槽位上当前摆放的物品
    private GameObject[] slotOccupants;

    /// <summary>
    /// 判断货架是否已满
    /// </summary>
    public bool IsFull
    {
        get
        {
            if (slotOccupants == null) return true;
            foreach (var occ in slotOccupants)
            {
                if (occ == null) return false; // 有空位就不算满
            }
            return true;
        }
    }

    /// <summary>
    /// 判断货架是否为空（供顾客AI判断是否还有东西可拿）
    /// </summary>
    public bool IsEmpty
    {
        get
        {
            if (slotOccupants == null) return true;
            foreach (var occ in slotOccupants)
            {
                if (occ != null) return false; // 有东西就不算空
            }
            return true;
        }
    }

    private void Awake()
    {
        if (displaySlots != null)
        {
            slotOccupants = new GameObject[displaySlots.Length];
        }
    }

    /// <summary>
    /// 供【玩家 / 工作者AI】调用：尝试将物品放上货架
    /// </summary>
    public bool TryAddProduct(ItemType type, GameObject item)
    {
        // 1. 类型不对，或者货架满了，直接拒收
        if (type != acceptedItemType || IsFull) return false;

        // 2. 从前往后找一个空位 (Slot)
        for (int i = 0; i < slotOccupants.Length; i++)
        {
            if (slotOccupants[i] == null)
            {
                // 标记该槽位被占用
                slotOccupants[i] = item;

                // 设置父节点为对应的 Slot
                item.transform.SetParent(displaySlots[i], true);
                item.transform.DOKill();

                // 防抖缩放（防止继承奇怪的缩放比例）
                item.transform.DOScale(Vector3.one, 0.2f);

                // 抛物线飞到槽位正中心
                item.transform.DOLocalJump(Vector3.zero, 0.01f, 1, placeAnimDuration);
                item.transform.DOLocalRotate(Vector3.zero, placeAnimDuration);

                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 供【顾客AI】调用：从货架上拿走物品
    /// </summary>
    public GameObject TakeProduct()
    {
        if (IsEmpty) return null;

        // 顾客通常是从后往前拿（后进先出），视觉上比较符合堆叠逻辑
        for (int i = slotOccupants.Length - 1; i >= 0; i--)
        {
            if (slotOccupants[i] != null)
            {
                GameObject item = slotOccupants[i];
                slotOccupants[i] = null; // 腾出这个槽位
                return item;
            }
        }
        return null;
    }
}