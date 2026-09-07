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

    [Header("通用动画设置 (Animation)")]
    [Tooltip("把机器或动物模型身上的 Animation 组件拖进来（如果没有可为空）")]
    public Animation animComponent;

    [Tooltip("机器空闲 / 动物待机时的动画名称")]
    public string idleAnimName = "Idle";

    [Tooltip("机器运转 / 动物进食生产时的动画名称")]
    public string workAnimName = "Work"; // 【规范化】将 eat 改为更通用的 work

    private List<GameObject> currentInputItems = new List<GameObject>();
    private bool isProcessing = false;

    private void Start()
    {
        // 游戏一开始，默认播放空闲动画
        if (animComponent != null && !string.IsNullOrEmpty(idleAnimName))
        {
            animComponent.Play(idleAnimName);
        }
    }

    /// <summary>
    /// 接收原料的接口（由玩家或NPC在碰撞时主动调用）
    /// </summary>
    public bool TryReceiveInput(ItemType inputType, GameObject inputItem)
    {
        // 1. 如果类型不匹配，或者 输入区槽位 已经被放满了，则拒收
        if (inputType != requiredInputType || currentInputItems.Count >= inputSlots.Length)
        {
            return false;
        }

        // 2. 找到当前对应的输入槽位
        Transform targetSlot = inputSlots[currentInputItems.Count];

        // 3. 将投入的物体设为槽位的子物体，并让它飞到槽位上
        inputItem.transform.SetParent(targetSlot);
        inputItem.transform.DOKill();
        inputItem.transform.DOLocalJump(Vector3.zero, 0.01f, 1, 0.3f);
        inputItem.transform.DOLocalRotate(Vector3.zero, 0.3f);

        // 4. 将物体加入管理列表
        currentInputItems.Add(inputItem);

        // 5. 如果没有在加工，就启动加工协程
        if (!isProcessing)
        {
            StartCoroutine(ProcessRoutine());
        }

        return true;
    }

    /// <summary>
    /// 核心加工流程（内部自动循环）
    /// </summary>
    private IEnumerator ProcessRoutine()
    {
        isProcessing = true;

        // 【动画】开始加工，播放“运转/吃”的动画
        if (animComponent != null && !string.IsNullOrEmpty(workAnimName))
        {
            animComponent.CrossFade(workAnimName, 0.2f);
        }

        // 当 输入区原料足够 && 输出区未满时，持续执行！
        while (currentInputItems.Count >= inputNeededPerOutput && readyProducts.Count < MaxCapacity)
        {
            // 等待加工时间
            yield return new WaitForSeconds(processingTime);

            // 消耗指定数量的原料
            for (int i = 0; i < inputNeededPerOutput; i++)
            {
                int lastIdx = currentInputItems.Count - 1;
                GameObject itemToConsume = currentInputItems[lastIdx];
                currentInputItems.RemoveAt(lastIdx);

                Destroy(itemToConsume);
            }

            // 调用基类方法，在输出区生成产品
            GenerateProduct();
        }

        isProcessing = false;

        // 【动画】加工结束，恢复“空闲”动画
        if (animComponent != null && !string.IsNullOrEmpty(idleAnimName))
        {
            animComponent.CrossFade(idleAnimName, 0.2f);
        }
    }
}