using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 全局设施管理类：负责统一记录场景中所有处于激活状态的机器和货架。
/// </summary>
public class FacilityManager : MonoBehaviour
{
    public static FacilityManager Instance { get; private set; }

    // 三大核心设施名单
    public List<BaseProductionMachine> allProducers = new List<BaseProductionMachine>();
    public List<ConsumeProductionMachine> allConsumers = new List<ConsumeProductionMachine>();
    public List<ShelfManager> allShelves = new List<ShelfManager>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // ================= 注册与注销 =================
    // 利用 Unity 的生命周期，物体激活(显示)时自动注册，隐藏(销毁)时自动注销

    public void RegisterProducer(BaseProductionMachine machine)
    { if (!allProducers.Contains(machine)) allProducers.Add(machine); }
    public void UnregisterProducer(BaseProductionMachine machine)
    { if (allProducers.Contains(machine)) allProducers.Remove(machine); }

    public void RegisterConsumer(ConsumeProductionMachine machine)
    { if (!allConsumers.Contains(machine)) allConsumers.Add(machine); }
    public void UnregisterConsumer(ConsumeProductionMachine machine)
    { if (allConsumers.Contains(machine)) allConsumers.Remove(machine); }

    public void RegisterShelf(ShelfManager shelf)
    { if (!allShelves.Contains(shelf)) allShelves.Add(shelf); }
    public void UnregisterShelf(ShelfManager shelf)
    { if (allShelves.Contains(shelf)) allShelves.Remove(shelf); }
}