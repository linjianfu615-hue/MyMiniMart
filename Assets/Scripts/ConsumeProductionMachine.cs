using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using System;

/// <summary>
/// 原料需求配置类 (可以在 Inspector 面板中自由添加多种原料)
/// </summary>
[Serializable]
public class InputRequirement
{
    [Tooltip("需要的原材料类型 (如：鸡蛋、面粉)")]
    public ItemType requiredType;

    [Tooltip("生成一个成品需要消耗几个该原料")]
    public int amountNeeded = 1;

    [Tooltip("该原料对应的视觉槽位 (如：拖入 Mat_Input_1 下的所有 Slot)")]
    public Transform[] slots;

    // 运行时存放当前已经放上来的具体物品 (隐藏，仅供代码内部计算)
    [HideInInspector]
    public List<GameObject> currentItems = new List<GameObject>();
}

public class ConsumeProductionMachine : BaseProductionMachine
{
    [Header("消耗生产设置 (多原料配方)")]
    [Tooltip("机器的配方需求列表。需要几种原料就添加几个元素。")]
    public List<InputRequirement> inputRequirements = new List<InputRequirement>();

    [Tooltip("单次加工/消化所需的时间")]
    public float processingTime = 2f;

    // ================== 吸收动画设置 ==================
    [Header("吸收动画设置 (Consume Animation)")]
    [Tooltip("原料消耗时飞向的中心点")]
    public Transform consumePoint;

    [Tooltip("原料被吸入的动画时长")]
    public float consumeAnimDuration = 0.3f;
    // ====================================================

    [Header("通用动画设置 (Animation)")]
    public Animation animComponent;
    public string idleAnimName = "Idle";
    public string workAnimName = "Work";

    private bool isProcessing = false;

    private void Start()
    {
        if (animComponent != null && !string.IsNullOrEmpty(idleAnimName))
        {
            animComponent.Play(idleAnimName);
        }
    }

    /// <summary>
    /// 检查是否所有种类的原料都达到了加工的最低数量要求
    /// </summary>
    private bool CheckAllInputsReady()
    {
        foreach (var req in inputRequirements)
        {
            if (req.currentItems.Count < req.amountNeeded)
            {
                return false; // 只要有任何一种原料不够，就不能开工
            }
        }
        return true;
    }

    /// <summary>
    /// 尝试接收外部投递的原料
    /// </summary>
    public bool TryReceiveInput(ItemType inputType, GameObject inputItem)
    {
        // 遍历所有的配方需求，看看当前投递的物品属于哪一种
        foreach (var req in inputRequirements)
        {
            if (req.requiredType == inputType)
            {
                // 如果属于这种原料，且对应的专属槽位还没满
                if (req.currentItems.Count < req.slots.Length)
                {
                    Transform targetSlot = req.slots[req.currentItems.Count];

                    // 放置逻辑
                    inputItem.transform.SetParent(targetSlot);
                    inputItem.transform.DOKill();
                    inputItem.transform.DOLocalJump(Vector3.zero, 0.01f, 1, 0.3f);
                    inputItem.transform.DOLocalRotate(Vector3.zero, 0.3f);

                    req.currentItems.Add(inputItem);

                    // 如果当前没在加工，且所有原料都齐了，启动加工流水线
                    if (!isProcessing && CheckAllInputsReady())
                    {
                        StartCoroutine(ProcessRoutine());
                    }

                    return true; // 成功接收
                }
            }
        }

        // 如果遍历完了发现类型都不匹配，或者匹配的类型槽位满了，则拒收
        return false;
    }

    private IEnumerator ProcessRoutine()
    {
        isProcessing = true;

        if (animComponent != null && !string.IsNullOrEmpty(workAnimName))
        {
            animComponent.CrossFade(workAnimName, 0.2f);
        }

        // 只要原料齐全，且输出区没满，就一直生产
        while (CheckAllInputsReady() && readyProducts.Count < MaxCapacity)
        {
            // 等待加工时间
            yield return new WaitForSeconds(processingTime);

            // 遍历所有原料配方，分别扣除它们所需消耗的数量
            foreach (var req in inputRequirements)
            {
                for (int i = 0; i < req.amountNeeded; i++)
                {
                    int lastIdx = req.currentItems.Count - 1;
                    GameObject itemToConsume = req.currentItems[lastIdx];
                    req.currentItems.RemoveAt(lastIdx);

                    if (consumePoint != null)
                    {
                        itemToConsume.transform.DOKill();
                        itemToConsume.transform.DOMove(consumePoint.position, consumeAnimDuration).SetEase(Ease.Linear);
                        itemToConsume.transform.DOScale(Vector3.zero, consumeAnimDuration).SetEase(Ease.Linear)
                            .OnComplete(() => Destroy(itemToConsume));
                    }
                    else
                    {
                        Destroy(itemToConsume);
                    }
                }
            }

            if (consumePoint != null)
            {
                yield return new WaitForSeconds(consumeAnimDuration);
            }

            GenerateProduct();
        }

        isProcessing = false;

        if (animComponent != null && !string.IsNullOrEmpty(idleAnimName))
        {
            animComponent.CrossFade(idleAnimName, 0.2f);
        }
    }

    /// <summary>
    /// 当玩家拿走成品时，尝试唤醒停机的流水线
    /// </summary>
    public override GameObject CollectProduct()
    {
        GameObject collectedItem = base.CollectProduct();

        // 拿走成品后，如果机器是停机状态，检查下原料是否够继续生产，够就再次启动
        if (collectedItem != null && !isProcessing)
        {
            if (CheckAllInputsReady())
            {
                StartCoroutine(ProcessRoutine());
            }
        }

        return collectedItem;
    }
}