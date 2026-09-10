using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

[System.Serializable]
public class WorkerTask
{
    [Header("任务起点")]
    [Tooltip("去哪里拿货 (如：鸡舍、小麦田)")]
    public BaseProductionMachine sourceMachine;

    [Header("任务终点 (根据需求二选一即可)")]
    public ShelfManager targetShelf;
    public ConsumeProductionMachine targetMachine;

    public Vector3 GetSourcePosition()
    {
        if (sourceMachine == null) return Vector3.zero;
        Transform outSlot = sourceMachine.transform.Find("OutputWorkerSlot");
        if (outSlot != null) return outSlot.position;
        Transform workerSlot = sourceMachine.transform.Find("WorkerSlot");
        if (workerSlot != null) return workerSlot.position;
        return sourceMachine.transform.position;
    }

    public Vector3 GetTargetPosition()
    {
        if (targetShelf != null)
        {
            Transform workerSlot = targetShelf.transform.Find("WorkerSlot");
            if (workerSlot != null) return workerSlot.position;
            return targetShelf.transform.position;
        }
        if (targetMachine != null)
        {
            Transform inSlot = targetMachine.transform.Find("InputWorkerSlot");
            if (inSlot != null) return inSlot.position;
            return targetMachine.transform.position;
        }
        return Vector3.zero;
    }
}

[RequireComponent(typeof(NavMeshAgent))]
public class WorkerAIController : BaseCharacterController
{
    [Header("AI 任务配置 (按优先级从上到下)")]
    public List<WorkerTask> tasks;
    public TrashBinManager trashBin;

    private NavMeshAgent agent;
    private Vector3 startPosition;
    private WorkerTask currentTask;

    private enum AIState { Idle, MovingToSource, MovingToDest, MovingToTrash, ReturningHome }
    private AIState currentState = AIState.Idle;

    private float interactTimer = 0f;
    private float interactDelay = 0.2f;

    protected override void Awake()
    {
        base.Awake();
        agent = GetComponent<NavMeshAgent>();
        startPosition = transform.position;
        agent.updateRotation = false;
    }

    private void Update()
    {
        // =========================================================
        // 【修复Bug 1：颤动与原地打转】
        // 如果已经彻底刹车，或者速度极小，强制传给基类 Vector3.zero 让其完全静止
        // =========================================================
        if (agent.isStopped || agent.velocity.sqrMagnitude < 0.01f)
        {
            MoveAndRotate(Vector3.zero);
        }
        else
        {
            MoveAndRotate(agent.velocity.normalized);
        }

        // 状态机分发
        switch (currentState)
        {
            case AIState.Idle:
                FindNextTask();
                break;

            case AIState.ReturningHome:
                if (HasReachedDestination()) currentState = AIState.Idle;
                else FindNextTask(); // 回家路上也要持续环顾四周找活干
                break;

            case AIState.MovingToSource:
                if (HasReachedDestination()) HandleCollection();
                break;

            case AIState.MovingToDest:
                if (HasReachedDestination()) HandleDelivery();
                break;

            case AIState.MovingToTrash:
                if (HasReachedDestination()) HandleTrash();
                break;
        }
    }

    // =========================================================
    // 【修复Bug 2：智能满载收集机制】
    // =========================================================
    private void FindNextTask()
    {
        // 第一阶段：如果手里没拿满，优先去各个产出点【进货】
        if (!IsFull)
        {
            foreach (var task in tasks)
            {
                if (task.sourceMachine != null && (task.targetShelf != null || task.targetMachine != null))
                {
                    // 判断条件：终点缺货 + 起点有货 + 手上的货和这个任务匹配（空手接一切）
                    if (!IsTargetFull(task) && !IsSourceEmpty(task) && IsHoldingItemTypeForTask(task))
                    {
                        currentTask = task;
                        SetDestination(task.GetSourcePosition(), AIState.MovingToSource);
                        return; // 找到活了，出发进货！
                    }
                }
            }
        }

        // 第二阶段：如果没法进货了（起点全空，或者手已拿满），且手里有东西，赶紧去【送货】
        if (HasItems)
        {
            foreach (var task in tasks)
            {
                // 找一个能收我手里这批货的目的地
                if (!IsTargetFull(task) && IsHoldingItemTypeForTask(task))
                {
                    currentTask = task;
                    SetDestination(task.GetTargetPosition(), AIState.MovingToDest);
                    return;
                }
            }

            // 兜底防呆：如果手里拿着东西，但所有对应的货架全满了，只能忍痛扔掉
            SetDestination(GetTrashPosition(), AIState.MovingToTrash);
            return;
        }

        // 第三阶段：手里没东西，全场也没货，走回原点【待机休息】
        if (currentState != AIState.ReturningHome)
        {
            SetDestination(startPosition, AIState.ReturningHome);
        }
    }

    private void HandleCollection()
    {
        // 收集时，如果突然满载了，立刻切回 Idle 让大脑(FindNextTask)分配去送货
        if (IsFull || IsTargetFull(currentTask))
        {
            currentState = AIState.Idle;
            return;
        }

        interactTimer += Time.deltaTime;
        if (interactTimer >= interactDelay)
        {
            interactTimer = 0f;
            GameObject item = currentTask.sourceMachine.CollectProduct();

            if (item != null)
            {
                AddItem(item);
            }
            else
            {
                // 这棵树薅秃了没货了，立刻切回 Idle
                // 下一帧 FindNextTask 发现没满，会自动派他去下一棵树凑齐 5 个！
                currentState = AIState.Idle;
            }
        }
    }

    private void HandleDelivery()
    {
        // 送货时发现全满了，没地方放，切回 Idle 让大脑重新计算去哪里
        if (IsTargetFull(currentTask))
        {
            currentState = AIState.Idle;
            return;
        }

        interactTimer += Time.deltaTime;
        if (interactTimer >= interactDelay)
        {
            interactTimer = 0f;
            GameObject topItem = PeekTopItem();

            if (topItem != null)
            {
                ItemData itemData = topItem.GetComponent<ItemData>();
                if (itemData != null)
                {
                    bool delivered = DeliverItemToTarget(currentTask, itemData.itemType, topItem);
                    if (delivered) RemoveTopItem();
                    else
                    {
                        currentState = AIState.Idle;
                        return;
                    }
                }
            }

            if (!HasItems) currentState = AIState.Idle; // 送空了，找新活
        }
    }

    private void HandleTrash()
    {
        interactTimer += Time.deltaTime;
        if (interactTimer >= interactDelay)
        {
            interactTimer = 0f;
            GameObject topItem = RemoveTopItem();
            if (topItem != null && trashBin != null) trashBin.ReceiveTrash(topItem);

            if (!HasItems) currentState = AIState.Idle;
        }
    }

    // ================== 核心功能封装辅助方法 ==================

    private void SetDestination(Vector3 targetPos, AIState newState)
    {
        if (agent.isStopped) agent.isStopped = false; // 出发前确保解除刹车
        agent.SetDestination(targetPos);
        currentState = newState;
    }

    private bool HasReachedDestination()
    {
        // 加上 0.1f 缓冲距离，防止由于模型 Collider 碰撞导致永远到不了绝对中心点
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
        {
            agent.isStopped = true; // 【修复Bug 1】到达目标后彻底拉起手刹！
            return true;
        }
        return false;
    }

    private Vector3 GetTrashPosition()
    {
        if (trashBin == null) return transform.position;
        Transform slot = trashBin.transform.Find("WorkerSlot");
        return slot != null ? slot.position : trashBin.transform.position;
    }

    private bool IsTargetFull(WorkerTask task)
    {
        if (task.targetShelf != null) return task.targetShelf.IsFull;
        if (task.targetMachine != null)
        {
            foreach (var req in task.targetMachine.inputRequirements)
            {
                if (req.currentItems.Count < req.slots.Length) return false;
            }
            return true;
        }
        return true;
    }

    private bool IsSourceEmpty(WorkerTask task)
    {
        if (task.sourceMachine == null) return true;
        return task.sourceMachine.readyProducts.Count == 0;
    }

    /// <summary>
    /// 判断手里的物品类型是否属于当前分配任务的目标需求（防止拿着鸡蛋跑去拿西红柿）
    /// </summary>
    private bool IsHoldingItemTypeForTask(WorkerTask task)
    {
        if (!HasItems) return true; // 空手的话什么任务都能接

        GameObject topItem = PeekTopItem();
        if (topItem == null) return true;

        ItemData itemData = topItem.GetComponent<ItemData>();
        if (itemData == null) return true;

        if (task.targetShelf != null)
        {
            return task.targetShelf.acceptedItemType == itemData.itemType;
        }
        else if (task.targetMachine != null)
        {
            foreach (var req in task.targetMachine.inputRequirements)
            {
                if (req.requiredType == itemData.itemType) return true;
            }
        }
        return false;
    }

    private bool DeliverItemToTarget(WorkerTask task, ItemType itemType, GameObject item)
    {
        if (task.targetShelf != null) return task.targetShelf.TryAddProduct(itemType, item);
        if (task.targetMachine != null) return task.targetMachine.TryReceiveInput(itemType, item);
        return false;
    }
}