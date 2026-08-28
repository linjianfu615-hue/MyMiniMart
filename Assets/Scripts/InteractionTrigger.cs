// using System.Collections.SerializeField;
using UnityEngine;

/// <summary>
/// 交互触发器(玩家靠近机器/货架时，自动进行物品的“吐出/吸入”操作)
/// </summary>

public class InteractionTrigger : MonoBehaviour
{
    public PlayerInventory inventory;
    public float transferInterval = 0.15f; // 物品传输间隔（秒），数值越小吐得越快，越爽

    private float transferTimer = 0f;

    private void Awake()
    {
        if (inventory == null) inventory = GetComponent<PlayerInventory>();
    }

    private void OnTriggerStay(Collider other)
    {
        transferTimer += Time.deltaTime;
        if (transferTimer < transferInterval) return;

        // 1. 碰到了【售卖货架】：玩家尝试把背包里的东西补到货架上
        if (other.TryGetComponent<StoreShelf>(out var shelf))
        {
            if (!shelf.IsFull && !inventory.IsEmpty)
            {
                // 检查背包顶部的物品是否匹配货架需求
                if (inventory.PeekItem().itemID == shelf.targetItemID)
                {
                    ItemData item = inventory.RemoveItem();
                    if (shelf.TryAddItem(item))
                    {
                        transferTimer = 0f;
                    }
                    else
                    {
                        inventory.AddItem(item); // 补货失败，还回背包
                    }
                }
            }
        }

        // 2. 碰到了【生产机器的原料口】：玩家把原料喂给机器
        if (other.TryGetComponent<ProductionMachine>(out var machine))
        {
            if (!inventory.IsEmpty && machine.requiresInput)
            {
                if (inventory.PeekItem().itemID == machine.inputItemID)
                {
                    // 尝试喂入机器
                    if (machine.TryFeedInput(inventory.PeekItem().itemID))
                    {
                        ItemData item = inventory.RemoveItem();
                        Destroy(item.gameObject); // 喂进去了，销毁背包里的物理模型
                        transferTimer = 0f;
                    }
                }
            }

            // 3. 碰到了【生产机器的产出区】：如果玩家背包没满，且机器有产出，自动吸入背包
            if (!machine.requiresInput || !machine.IsEmpty)
            {
                if (!inventory.IsFull && !machine.IsEmpty)
                {
                    ItemData item = machine.TryRemoveItem();
                    if (item != null)
                    {
                        inventory.AddItem(item);
                        transferTimer = 0f;
                    }
                }
            }
        }
    }
}