using UnityEngine;

/// <summary>
/// 售卖货架(处理顾客买走商品时的行为、向外部广播“我缺货了”以便搬运工自动来补货)
/// </summary>
public class StoreShelf : BaseStructure
{
    [Header("货架售卖配置")]
    public string targetItemID; // 该货架指定的商品ID (如 "tomato")
    public int itemPrice = 1;   // 物品单价

    // 当货架没有物品或物品极少时，触发事件通知AI补货员
    public System.Action<StoreShelf> OnStockLow;

    public override bool TryAddItem(ItemData item)
    {
        // 严格检查：货架只能放匹配的物品
        if (item.itemID != targetItemID) return false;

        return base.TryAddItem(item);
    }

    // 顾客或者AI购买时调用
    public bool TryCustomerPurchase(out int earnedMoney)
    {
        earnedMoney = 0;
        if (IsEmpty) return false;

        ItemData item = TryRemoveItem();
        if (item != null)
        {
            earnedMoney = itemPrice;
            Destroy(item.gameObject); // 顾客吃掉/拿走销毁，或者触发收入特效

            // 检查是否触发缺货预警（如少于最大容量的 30%）
            if (CurrentCount <= maxCapacity * 0.3f)
            {
                OnStockLow?.Invoke(this);
            }
            return true;
        }

        return false;
    }
}