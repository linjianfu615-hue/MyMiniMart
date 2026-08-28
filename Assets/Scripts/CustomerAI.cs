using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI; // 依赖 Unity 的 NavMesh 寻路系统

/// <summary>
/// 顾客AI状态机==顾客(处理顾客进入商店、挑选商品、排队买单、离开商店的逻辑)
/// </summary>

public enum CustomerState
{
    Entering,       // 入店
    Shopping,       // 正在货架挑商品
    GoingToCheckout,// 准备去排队
    WaitingInQueue, // 正在排队等结账
    Leaving         // 结账完成离开
}

[RequireComponent(typeof(NavMeshAgent))]
public class CustomerAI : MonoBehaviour
{
    [Header("顾客配置")]
    public float shoppingDurationPerRack = 1.5f; // 每个货架前停留挑选的时间
    public Transform exitPivot;                 // 离开商店的终点

    // 购物车数据
    public int PurchasedItemsCount => purchasedItems.Count;
    private List<string> purchasedItems = new List<string>();
    private Dictionary<string, int> itemPrices = new Dictionary<string, int>();

    // 组件与状态
    private NavMeshAgent agent;
    private CustomerState currentState = CustomerState.Entering;
    private List<StoreShelf> targetShelves = new List<StoreShelf>(); // 计划要逛的货架
    private int currentShelfIndex = 0;
    private CashDesk targetCashDesk;
    private bool isAtQueueDestination = false;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    // 外部生成顾客时调用，初始化目标
    public void Initialize(List<StoreShelf> shelvesToVisit, CashDesk cashDesk, Transform exit)
    {
        targetShelves = shelvesToVisit;
        targetCashDesk = cashDesk;
        exitPivot = exit;

        currentShelfIndex = 0;
        purchasedItems.Clear();
        itemPrices.Clear();

        SwitchState(CustomerState.Entering);
    }

    private void Update()
    {
        HandleStateLogic();
    }

    private void SwitchState(CustomerState newState)
    {
        currentState = newState;

        switch (currentState)
        {
            case CustomerState.Entering:
                if (targetShelves.Count > 0)
                {
                    MoveToDestination(targetShelves[currentShelfIndex].transform.position);
                    SwitchState(CustomerState.Shopping);
                }
                else
                {
                    SwitchState(CustomerState.GoingToCheckout);
                }
                break;

            case CustomerState.Shopping:
                // 状态转换由内部协程或距离检测控制
                break;

            case CustomerState.GoingToCheckout:
                // 尝试加入收银台排队
                if (targetCashDesk.TryJoinQueue(this, out Vector3 queuePosition))
                {
                    MoveToDestination(queuePosition);
                    SwitchState(CustomerState.WaitingInQueue);
                }
                else
                {
                    // 队列满了，在旁边等一秒再试
                    StartCoroutine(RetryJoinQueueCoroutine());
                }
                break;

            case CustomerState.WaitingInQueue:
                isAtQueueDestination = false;
                break;

            case CustomerState.Leaving:
                MoveToDestination(exitPivot.position);
                break;
        }
    }

    private void HandleStateLogic()
    {
        // 检查寻路是否到达目标点
        if (agent.pathPending) return;
        if (agent.remainingDistance > agent.stoppingDistance) return;

        switch (currentState)
        {
            case CustomerState.Shopping:
                // 到达了当前货架，开始挑选
                StartCoroutine(PickItemFromShelfCoroutine(targetShelves[currentShelfIndex]));
                break;

            case CustomerState.WaitingInQueue:
                if (!isAtQueueDestination)
                {
                    isAtQueueDestination = true;
                    // 播放站立排队动画
                }
                break;

            case CustomerState.Leaving:
                // 到达校门口/出口，销毁自身
                Destroy(gameObject);
                break;
        }
    }

    // 挑选货物的行为协程
    private IEnumerator PickItemFromShelfCoroutine(StoreShelf shelf)
    {
        // 站立等待一段时间，模拟挑选
        yield return new WaitForSeconds(shoppingDurationPerRack);

        // 尝试从货架拿走商品
        if (shelf.TryCustomerPurchase(out int price))
        {
            purchasedItems.Add(shelf.targetItemID);
            // 记录商品单价，结账时算总账
            if (!itemPrices.ContainsKey(shelf.targetItemID))
                itemPrices[shelf.targetItemID] = price;

            // 【视觉表现】在此处可以实例化一个小水果放入顾客手提篮里
        }

        // 决定下一个去向
        currentShelfIndex++;
        if (currentShelfIndex < targetShelves.Count)
        {
            MoveToDestination(targetShelves[currentShelfIndex].transform.position);
        }
        else
        {
            SwitchState(CustomerState.GoingToCheckout);
        }
    }

    private IEnumerator RetryJoinQueueCoroutine()
    {
        yield return new WaitForSeconds(1.0f);
        if (currentState != CustomerState.WaitingInQueue)
        {
            SwitchState(CustomerState.GoingToCheckout);
        }
    }

    // 被收银台调用：前面的人走了，通知后面的人挪位置
    public void MoveToQueueSlot(Vector3 nextQueuePosition)
    {
        MoveToDestination(nextQueuePosition);
        isAtQueueDestination = false;
    }

    // 被收银台调用：结完账了，放行
    public void LeaveStore()
    {
        SwitchState(CustomerState.Leaving);
    }

    // 计算这单一共多少钱
    public int CalculateTotalCartValue()
    {
        int total = 0;
        foreach (var itemID in purchasedItems)
        {
            if (itemPrices.TryGetValue(itemID, out int price))
            {
                total += price;
            }
        }
        return total;
    }

    private void MoveToDestination(Vector3 targetPos)
    {
        if (agent.isActiveAndEnabled)
        {
            agent.SetDestination(targetPos);
        }
    }
}