using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家背包(处理“玩家拿取/丢弃”逻辑)玩家的背包同样继承自物品管理逻辑，但在物理表现上需要有“叠高高”的轻微晃动感。同时，通过 Collider 的 Trigger 检测，实现自动与机器、货架进行物品的高速交换。
/// </summary>

public class PlayerInventory : MonoBehaviour
{
    [Header("容量配置")]
    public int maxCapacity = 4; // 对应表1中的初始容量
    public Transform backpackPivot; // 背包挂载点（玩家后背）
    public float itemHeightOffset = 0.25f;

    [Header("物理晃动参数")]
    public float wobbleSpeed = 5f;
    public float wobbleAmount = 0.05f;

    private Stack<ItemData> stack = new Stack<ItemData>();
    public int CurrentCount => stack.Count;
    public bool IsFull => stack.Count >= maxCapacity;
    public bool IsEmpty => stack.Count == 0;

    private void Update()
    {
        ApplyBackpackWobble();
    }

    // 捡起/吸入物品
    public bool AddItem(ItemData item)
    {
        if (IsFull) return false;

        stack.Push(item);
        item.transform.SetParent(backpackPivot);

        // 计算叠高高位置
        Vector3 targetPos = new Vector3(0, (stack.Count - 1) * itemHeightOffset, 0);
        item.transform.localPosition = targetPos;
        item.transform.localRotation = Quaternion.identity;

        return true;
    }

    // 丢出/吐出物品
    public ItemData RemoveItem()
    {
        if (IsEmpty) return null;
        return stack.Pop();
    }

    // 检查栈顶物品类型（用于定点投放）
    public ItemData PeekItem()
    {
        if (IsEmpty) return null;
        return stack.Peek();
    }

    // 核心爽点：根据玩家移动，让背后的物品产生物理摇晃感
    private void ApplyBackpackWobble()
    {
        if (IsEmpty) return;

        // 获取玩家在 XZ 平面的速度（需从玩家移动脚本传入，此处用伪代码/简单模拟）
        float speed = GetComponent<Rigidbody>() != null ? GetComponent<Rigidbody>().velocity.magnitude : 0f;

        int index = 0;
        foreach (var item in stack)
        {
            if (index == 0) { index++; continue; } // 最底层不晃动

            // 层数越高，晃动幅度越大
            float wobble = Mathf.Sin(Time.time * wobbleSpeed) * wobbleAmount * speed * index;
            Vector3 localPos = item.transform.localPosition;
            localPos.x = wobble; // 左右晃动
            item.transform.localPosition = localPos;
            index++;
        }
    }
}