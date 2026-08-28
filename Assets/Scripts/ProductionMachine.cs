using UnityEngine;

/// <summary>
/// 生产机器(处理原材料消耗、计时器、产出物品的逻辑。适用于“番茄地”（无输入）和“鸡圈”（需要输入番茄，产出鸡蛋）)
/// </summary>
public class ProductionMachine : BaseStructure
{
    [Header("UI 引用")]
    public ProductionUI productionUI;


    [Header("生产配置")]
    public bool requiresInput = false;    // 是否需要原材料（番茄地为 false，鸡圈为 true）
    public string inputItemID;            // 输入物品ID (如 "tomato")
    public int inputCostPerCount = 2;     // 每次生产消耗的输入数量

    public GameObject outputPrefab;       // 产出物品的 Prefab (如 鸡蛋)
    public float productionCycleTime = 4f;// 生产周期（秒）

    [Header("输入暂存区")]
    public int maxInputStorage = 6;
    private int currentInputCount = 0;    // 当前囤积的原材料数量

    private float productionTimer = 0f;
    private bool isProducing = false;

    private void Update()
    {
        HandleProduction();
    }

    // private void HandleProduction()
    // {
    //     // 如果产出堆栈满了，暂停生产
    //     if (IsFull) return;

    //     // 状态机切换：如果不处于生产状态，检查条件是否满足以启动生产
    //     if (!isProducing)
    //     {
    //         if (!requiresInput || currentInputCount >= inputCostPerCount)
    //         {
    //             isProducing = true;
    //             productionTimer = 0f;
    //         }
    //         return;
    //     }

    //     // 正在生产中，累加计时器
    //     productionTimer += Time.deltaTime;

    //     // 可以通过事件将 (productionTimer / productionCycleTime) 传给 UI 进度条

    //     if (productionTimer >= productionCycleTime)
    //     {
    //         CompleteProduction();
    //     }
    // }

    // 修改 ProductionMachine 的 HandleProduction 方法
    private void HandleProduction()
    {
        // 如果产出堆栈满了，暂停生产
        if (IsFull)
        {
            if (productionUI != null) productionUI.UpdateProgress(0, productionCycleTime);
            return;
        }

        // 状态机切换：如果不处于生产状态，检查条件是否满足以启动生产
        if (!isProducing)
        {
            if (!requiresInput || currentInputCount >= inputCostPerCount)
            {
                isProducing = true;
                productionTimer = 0f;
            }
            else
            {
                // 缺少原料，进度条清空隐藏
                if (productionUI != null) productionUI.UpdateProgress(0, productionCycleTime);
            }
            return;
        }

        // 正在生产中，累加计时器
        productionTimer += Time.deltaTime;

        // 【核心联动】实时通知 UI 更新进度条
        if (productionUI != null)
        {
            productionUI.UpdateProgress(productionTimer, productionCycleTime);
        }

        if (productionTimer >= productionCycleTime)
        {
            CompleteProduction();
            if (productionUI != null) productionUI.UpdateProgress(0, productionCycleTime);
        }
    }

    private void CompleteProduction()
    {
        isProducing = false;

        // 扣除原材料
        if (requiresInput)
        {
            currentInputCount -= inputCostPerCount;
        }

        // 实例化产生物品
        GameObject newObj = Instantiate(outputPrefab);
        ItemData newItem = newObj.GetComponent<ItemData>();

        // 将物品推入自身的产出堆栈中
        if (!TryAddItem(newItem))
        {
            // 如果刚好满了塞不进，直接销毁防内存泄漏（理论上上面IsFull挡住了）
            Destroy(newObj);
        }
    }

    // 外部（玩家/搬运工）往机器里喂原材料时调用
    public bool TryFeedInput(string itemID)
    {
        if (!requiresInput || itemID != inputItemID) return false;
        if (currentInputCount >= maxInputStorage) return false;

        currentInputCount++;
        return true;
    }

    // 调试用：显示机器状态
    private void OnGUI()
    {
        // 实际开发中用位移UI(WorldSpace UI)替代
        if (requiresInput && isProducing)
        {
            GUILayout.Label($"{name} 生产中... 进度: {(productionTimer / productionCycleTime) * 100:F0}% | 原料剩: {currentInputCount}");
        }
    }


}