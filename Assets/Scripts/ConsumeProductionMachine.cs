using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

public class ConsumeProductionMachine : BaseProductionMachine
{
    [Header("消耗生产设置 (Consume Production)")]
    [Tooltip("机器需要的原料类型")]
    public ItemType requiredInputType = ItemType.Tomato;

    [Tooltip("生成一个目标物品需要消耗多少个原料")]
    public int inputNeededPerOutput = 1;

    [Tooltip("单次加工/消化所需的时间")]
    public float processingTime = 2f;

    [Header("输入槽位设置 (Input Slots)")]
    [Tooltip("拖入 Mat_Input_Gray 下的所有的 InputSlots 子节点")]
    public Transform[] inputSlots;

    // ================== 新增吸收动画设置 ==================
    [Header("吸收动画设置 (Consume Animation)")]
    [Tooltip("原料消耗时飞向的中心点（如：鸡舍的 FactoryInputSlot、机器的漏斗）")]
    public Transform consumePoint;

    [Tooltip("原料被吸入的动画时长")]
    public float consumeAnimDuration = 0.3f;
    // ====================================================

    [Header("通用动画设置 (Animation)")]
    [Tooltip("把机器或动物模型身上的 Animation 组件拖进来（如果没有可为空）")]
    public Animation animComponent;

    [Tooltip("机器空闲 / 动物待机时的动画名称")]
    public string idleAnimName = "Idle";

    [Tooltip("机器运转 / 动物进食生产时的动画名称")]
    public string workAnimName = "Work";

    private List<GameObject> currentInputItems = new List<GameObject>();
    private bool isProcessing = false;

    private void Start()
    {
        if (animComponent != null && !string.IsNullOrEmpty(idleAnimName))
        {
            animComponent.Play(idleAnimName);
        }
    }

    public bool TryReceiveInput(ItemType inputType, GameObject inputItem)
    {
        if (inputType != requiredInputType || currentInputItems.Count >= inputSlots.Length)
        {
            return false;
        }

        Transform targetSlot = inputSlots[currentInputItems.Count];

        inputItem.transform.SetParent(targetSlot);
        inputItem.transform.DOKill();
        inputItem.transform.DOLocalJump(Vector3.zero, 0.01f, 1, 0.3f);
        inputItem.transform.DOLocalRotate(Vector3.zero, 0.3f);

        currentInputItems.Add(inputItem);

        if (!isProcessing)
        {
            StartCoroutine(ProcessRoutine());
        }

        return true;
    }

    private IEnumerator ProcessRoutine()
    {
        isProcessing = true;

        if (animComponent != null && !string.IsNullOrEmpty(workAnimName))
        {
            animComponent.CrossFade(workAnimName, 0.2f);
        }

        while (currentInputItems.Count >= inputNeededPerOutput && readyProducts.Count < MaxCapacity)
        {
            // 等待机器加工时间
            yield return new WaitForSeconds(processingTime);

            // 消耗指定数量的原料
            for (int i = 0; i < inputNeededPerOutput; i++)
            {
                int lastIdx = currentInputItems.Count - 1;
                GameObject itemToConsume = currentInputItems[lastIdx];
                currentInputItems.RemoveAt(lastIdx);

                // 【核心修改】：替换掉原本瞬间的 Destroy(itemToConsume)
                if (consumePoint != null)
                {
                    itemToConsume.transform.DOKill(); // 清理残留动画

                    // 1. 飞向吸收点（使用 InBack 产生一种被用力吸进去的视觉表现）
                    itemToConsume.transform.DOMove(consumePoint.position, consumeAnimDuration).SetEase(Ease.Linear);

                    // 2. 缩放到 0，并在彻底缩放完毕后销毁物体
                    itemToConsume.transform.DOScale(Vector3.zero, consumeAnimDuration).SetEase(Ease.Linear)
                        .OnComplete(() => Destroy(itemToConsume));
                }
                else
                {
                    // 如果没配置吸收点，则退化为直接销毁
                    Destroy(itemToConsume);
                }
            }

            // 等待西红柿飞进去的动画播完后，再产出鸡蛋，视觉上会更加连贯
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
}