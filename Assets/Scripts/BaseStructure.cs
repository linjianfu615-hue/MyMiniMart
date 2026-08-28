using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// 基础交互建筑(所有能存放物品的建筑的基类，处理最核心的“物品进出”与“堆叠空间”逻辑)
/// </summary>
public abstract class BaseStructure : MonoBehaviour
{
    [Header("基础配置")]
    public string structureID;
    public int maxCapacity = 10;
    public Transform stackPivot; // 物品在场景中视觉堆叠的起点位置
    public float itemHeightOffset = 0.2f; // 每个物品堆叠时的垂直间距

    // 运行时存储的物品堆栈
    protected Stack<ItemData> itemStack = new Stack<ItemData>();

    public int CurrentCount => itemStack.Count;
    public bool IsFull => itemStack.Count >= maxCapacity;
    public bool IsEmpty => itemStack.Count == 0;

    // 尝试推入一个物品（补货/输入）
    public virtual bool TryAddItem(ItemData item)
    {
        if (IsFull) return false;
        itemStack.Push(item);

        // 物理/视觉表现：将物品移到货架的对应堆叠位置
        item.transform.SetParent(stackPivot);
        Vector3 targetLocalPos = new Vector3(0, (itemStack.Count - 1) * itemHeightOffset, 0);

        // 实际项目中推荐使用 DOTween 缓动，这里用直接赋值演示
        // item.transform.localPosition = targetLocalPos;
        // item.transform.localRotation = Quaternion.identity;

        //用DoTween 缓动
        DOTween.To(() => item.transform.localPosition, x => item.transform.localPosition = x, targetLocalPos, 0.3f).SetEase(Ease.OutBack);
        DOTween.To(() => item.transform.localRotation.eulerAngles, x => item.transform.localRotation = Quaternion.Euler(x), Quaternion.identity.eulerAngles, 0.3f).SetEase(Ease.OutBack);


        return true;
    }

    // 尝试拿走一个物品（取货/售卖）
    public virtual ItemData TryRemoveItem()
    {
        if (IsEmpty) return null;

        ItemData item = itemStack.Pop();
        item.transform.SetParent(null); // 解除父子关系
        return item;
    }
}