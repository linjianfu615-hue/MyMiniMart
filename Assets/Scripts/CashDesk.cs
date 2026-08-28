using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 顾客排队买单的收银台状态机==收银台(处理顾客排队、结账、生成钱堆的逻辑)收银台（CashDesk）需要管理一个排队队列（Queue），并且其自身状态会在空闲、等待玩家、收银中、结账完成之间切换。
/// </summary>

public enum CashDeskState
{
    Idle,           // 无人排队
    WaitingForPlayer,// 有顾客排队，但收银台没员工/没玩家，无法结账
    Processing,     // 正在数钱结账
    Done            // 结账完成，顾客离开，生成钱堆
}

public class CashDesk : MonoBehaviour
{
    [Header("排队配置")]
    public List<Transform> queueSlots; // 场景中预设的排队点（如5个点，越靠近收银台索引越小）
    public GameObject moneyPrefab;      // 钞票预制体
    public Transform moneySpawnPivot;   // 钞票在地面堆积的起点

    [Header("收银数值")]
    public float checkoutTimePerItem = 0.5f; // 顾客手里的每个商品需要数 0.5 秒

    // 运行时数据
    private List<CustomerAI> customerQueue = new List<CustomerAI>();
    private CashDeskState currentState = CashDeskState.Idle;
    private bool isPlayerAtDesk = false; // 玩家是否站在收银区内
    private float checkoutTimer = 0f;
    private int currentMoneyPileCount = 0; // 地面上现有的钱堆数量

    private void Update()
    {
        UpdateStateMachine();
    }

    private void UpdateStateMachine()
    {
        switch (currentState)
        {
            case CashDeskState.Idle:
                if (customerQueue.Count > 0)
                {
                    TransitionTo(CashDeskState.WaitingForPlayer);
                }
                break;

            case CashDeskState.WaitingForPlayer:
                if (customerQueue.Count == 0)
                {
                    TransitionTo(CashDeskState.Idle);
                }
                else if (isPlayerAtDesk) // 玩家来了，或者雇佣了收银员
                {
                    TransitionTo(CashDeskState.Processing);
                }
                break;

            case CashDeskState.Processing:
                if (!isPlayerAtDesk)
                {
                    TransitionTo(CashDeskState.WaitingForPlayer);
                    return;
                }

                if (customerQueue.Count == 0)
                {
                    TransitionTo(CashDeskState.Idle);
                    return;
                }

                // 开始为队列第一个顾客结账
                CustomerAI currentCustomer = customerQueue[0];
                checkoutTimer += Time.deltaTime;

                // 计算该顾客结账总时间 = 商品数量 * 单个时间
                float totalRequiredTime = currentCustomer.PurchasedItemsCount * checkoutTimePerItem;

                if (checkoutTimer >= totalRequiredTime)
                {
                    // 结账完成
                    GenerateMoney(currentCustomer.CalculateTotalCartValue());
                    currentCustomer.LeaveStore(); // 通知顾客可以走了

                    customerQueue.RemoveAt(0); // 移出队列
                    UpdateQueuePositions();    // 后面的人往前挪

                    checkoutTimer = 0f;
                    TransitionTo(CashDeskState.Idle); // 回到Idle，下一帧会自动判断是否还有人
                }
                break;
        }
    }

    private void TransitionTo(CashDeskState newState)
    {
        currentState = newState;
        // 在这里可以触发对应的 UI 变化（例如 WaitingForPlayer 时头顶亮起 "!" 号）
    }

    // 顾客AI调用：请求加入排队队列
    public bool TryJoinQueue(CustomerAI customer, out Vector3 targetPosition)
    {
        targetPosition = Vector3.zero;
        if (customerQueue.Count >= queueSlots.Count) return false; // 队列满了，拒客

        customerQueue.Add(customer);
        targetPosition = queueSlots[customerQueue.Count - 1].position; // 分配排队位置
        return true;
    }

    // 后面的人往前挪动一步
    private void UpdateQueuePositions()
    {
        for (int i = 0; i < customerQueue.Count; i++)
        {
            customerQueue[i].MoveToQueueSlot(queueSlots[i].position);
        }
    }

    // 结账产生地面的金币/钞票堆
    private void GenerateMoney(int amount)
    {
        // 每一张钞票代表一定面额，这里简化为生成一个物理钱堆
        GameObject moneyObj = Instantiate(moneyPrefab, moneySpawnPivot);

        // 经典的钞票物理堆叠计算
        float row = currentMoneyPileCount % 3;
        float layer = currentMoneyPileCount / 3;
        moneyObj.transform.localPosition = new Vector3(row * 0.3f, layer * 0.1f, 0);

        currentMoneyPileCount++;
        // 钱堆可以挂载脚本，当玩家走过去时，触发 Collide 瞬间把钱加到钱包里并 Destroy
    }

    // 用于检测玩家是否站在收银格子内
    public void SetPlayerPresence(bool present)
    {
        isPlayerAtDesk = present;
    }
}