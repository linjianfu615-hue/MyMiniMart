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

    [Header("动画设置 (Animation - Optional)")]
    [Tooltip("如果货架有开关门动画（如冰箱），拖入带有该动画的 Animation 组件")]
    public Animation animComponent;

    public string openAnimName = "fridge_open";
    public string closeAnimName = "fridge_close";

    // 用于记录交互区内角色数量，以正确播放开关门动画
    private int entitiesInZone = 0;

    private void OnEnable()
    {
        // 货架激活时，注册到全局 FacilityManager 中，让全场的 AI 都能找到它
        if (FacilityManager.Instance != null) FacilityManager.Instance.RegisterShelf(this);
    }

    private void OnDisable()
    {
        // 货架注销时移除，防止 AI 寻路报错
        if (FacilityManager.Instance != null) FacilityManager.Instance.UnregisterShelf(this);
    }

    public void OnEntityEnter()
    {
        entitiesInZone++;
        if (entitiesInZone == 1 && animComponent != null && !string.IsNullOrEmpty(openAnimName))
        {
            animComponent.CrossFade(openAnimName, 0.15f);
        }
    }

    public void OnEntityExit()
    {
        entitiesInZone--;
        if (entitiesInZone <= 0)
        {
            entitiesInZone = 0;
            if (animComponent != null && !string.IsNullOrEmpty(closeAnimName))
            {
                animComponent.CrossFade(closeAnimName, 0.15f);
            }
        }
    }

    /// <summary>
    /// 货架是否已被放满
    /// </summary>
    public bool IsFull
    {
        get
        {
            if (slotOccupants == null) return true;
            foreach (var occ in slotOccupants)
            {
                if (occ == null) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// 货架是否完全空了
    /// </summary>
    public bool IsEmpty
    {
        get
        {
            if (slotOccupants == null) return true;
            foreach (var occ in slotOccupants)
            {
                if (occ != null) return false;
            }
            return true;
        }
    }

    // =======================================================
    // 【核心数据】：暴露缺口数量，供 WorkerAI 进行优先级计算
    // =======================================================
    public int MissingCount
    {
        get
        {
            if (slotOccupants == null) return 0;
            int count = 0;
            // 遍历所有槽位，精准计算当前空着的坑位数量
            for (int i = 0; i < slotOccupants.Length; i++)
            {
                if (slotOccupants[i] == null) count++;
            }
            return count; // 返回当前究竟缺几个货
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
    /// 接收外部传来的物品并将其摆上货架
    /// </summary>
    public bool TryAddProduct(ItemType type, GameObject item)
    {
        if (type != acceptedItemType || IsFull) return false;

        for (int i = 0; i < slotOccupants.Length; i++)
        {
            // 找到第一个空槽位
            if (slotOccupants[i] == null)
            {
                slotOccupants[i] = item; // 登记占位

                item.transform.SetParent(displaySlots[i], true);
                item.transform.DOKill();
                item.transform.DOScale(Vector3.one, 0.2f);

                // 执行摆放动画
                item.transform.DOLocalJump(Vector3.zero, 0.01f, 1, placeAnimDuration);
                item.transform.DOLocalRotate(Vector3.zero, placeAnimDuration);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 顾客拿取物品时调用
    /// </summary>
    public GameObject TakeProduct()
    {
        if (IsEmpty) return null;

        // 从最后一个槽位往前取（后进先出，更符合视觉上的堆叠逻辑）
        for (int i = slotOccupants.Length - 1; i >= 0; i--)
        {
            if (slotOccupants[i] != null)
            {
                GameObject item = slotOccupants[i];
                slotOccupants[i] = null; // 解除占位
                return item;
            }
        }
        return null;
    }
}