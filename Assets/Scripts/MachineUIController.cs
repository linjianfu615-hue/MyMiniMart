using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 机器顶部悬浮 UI 的控制器，负责实时刷新原料/产出的数量显示
/// </summary>
public class MachineUIController : MonoBehaviour
{
    [Header("UI 组件配置")]
    [Tooltip("用来显示当前原料或产出数量的 Text 组件 (如: 0/8)")]
    public Text amountText;

    [Tooltip("如果你有多个原料 (比如面包机)，可以把它们存成数组。单原料机器只需填 1 个")]
    [Header("多原料 UI 支持 (面包机专用)")]
    public Text[] multiAmountTexts;

    // 缓存对机器核心逻辑的引用
    private ConsumeProductionMachine consumeMachine;
    private BaseProductionMachine baseMachine;

    void Start()
    {
        // 尝试获取消耗型机器 (如鸡舍、面包机)
        consumeMachine = GetComponent<ConsumeProductionMachine>();

        // 如果不是消耗型机器，尝试获取基础机器 (如不需要原料直接产出的西红柿树)
        if (consumeMachine == null)
        {
            baseMachine = GetComponent<BaseProductionMachine>();
        }

        // 初始化时刷新一次 UI
        UpdateUI();
    }

    /// <summary>
    /// 我们在 Update 中每帧刷新 UI。
    /// 这种写法最简单直接，不需要在核心逻辑里到处写回调事件。
    /// </summary>
    void Update()
    {
        UpdateUI();
    }

    /// <summary>
    /// 核心刷新逻辑：读取机器内部状态并更新文字
    /// </summary>
    private void UpdateUI()
    {
        // ==========================================
        // 场景 1：如果这是消耗型机器 (有输入原料的机器)
        // ==========================================
        if (consumeMachine != null)
        {
            // 针对只有单一种类原料的普通机器 (如鸡舍，只需要西红柿)
            if (amountText != null && consumeMachine.inputRequirements.Count > 0)
            {
                var req = consumeMachine.inputRequirements[0];
                int currentAmount = req.currentItems.Count;
                int maxCapacity = req.slots.Length;

                // 拼接字符串："当前数量/最大容量"
                amountText.text = currentAmount + "/" + maxCapacity;
            }

            // 针对有多种原料的复杂机器 (如面包机，需要鸡蛋和面粉)
            if (multiAmountTexts != null && multiAmountTexts.Length > 0)
            {
                for (int i = 0; i < multiAmountTexts.Length; i++)
                {
                    // 确保 UI 数组和机器的配方数组对齐
                    if (i < consumeMachine.inputRequirements.Count && multiAmountTexts[i] != null)
                    {
                        var req = consumeMachine.inputRequirements[i];
                        multiAmountTexts[i].text = req.currentItems.Count + "/" + req.slots.Length;
                    }
                }
            }
        }
        // ==========================================
        // 场景 2：如果这是全自动机器 (如西红柿树，只需显示成熟了多少个)
        // ==========================================
        else if (baseMachine != null && amountText != null)
        {
            // 对于基础机器，我们显示它当前的产出区满了没有
            // 注意：baseMachine.readyProducts 目前是 protected 的，如果这里报错，
            // 需要你去 BaseProductionMachine.cs 里把 readyProducts 改为 public，或者写一个 Get 方法。
            // 为了演示，这里假设你可以访问它的数量：
            // int currentReady = baseMachine.readyProducts.Count;
            // int maxCap = baseMachine.MaxCapacity;
            // amountText.text = currentReady + "/" + maxCap;
        }
    }
}