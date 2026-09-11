using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

/// <summary>
/// 表示 AI 工作者的一项具体任务，包含起点和终点。
/// 终点可以是普通货架 (targetShelf) 或加工机器 (targetMachine)。
/// </summary>
[System.Serializable]
public class WorkerTask
{
    [Header("任务起点")]
    [Tooltip("去哪里拿货 (如：鸡舍、西红柿树)")]
    public BaseProductionMachine sourceMachine;

    [Header("任务终点 (根据需求二选一即可)")]
    public ShelfManager targetShelf;
    public ConsumeProductionMachine targetMachine;

    /// <summary>
    /// 获取收集物品时的最佳目标位置。
    /// 优先寻找专门的 OutputWorkerSlot，其次找 WorkerSlot。
    /// 如果都没找到，则返回机器本身的中心点，并添加随机偏移以防止多AI拥挤。
    /// </summary>
    public Vector3 GetSourcePosition()
    {
        if (sourceMachine == null) return Vector3.zero;

        Transform slot = sourceMachine.transform.Find("OutputWorkerSlot");
        if (slot == null) slot = sourceMachine.transform.Find("WorkerSlot");

        Vector3 basePos = slot != null ? slot.position : sourceMachine.transform.position;
        return GetRandomizedPosition(basePos);
    }

    /// <summary>
    /// 获取交付物品时的最佳目标位置。
    /// 同样优先寻找 InputWorkerSlot 或 WorkerSlot，并添加随机偏移。
    /// </summary>
    public Vector3 GetTargetPosition()
    {
        Transform slot = null;
        Vector3 basePos = Vector3.zero;

        if (targetShelf != null)
        {
            slot = targetShelf.transform.Find("WorkerSlot");
            basePos = slot != null ? slot.position : targetShelf.transform.position;
        }
        else if (targetMachine != null)
        {
            slot = targetMachine.transform.Find("InputWorkerSlot");
            basePos = slot != null ? slot.position : targetMachine.transform.position;
        }

        return GetRandomizedPosition(basePos);
    }

    /// <summary>
    /// 为给定坐标添加一个微小的随机偏移量。
    /// 这能有效避免多个 AI 代理试图占据同一个绝对精确的点而发生物理碰撞或死锁。
    /// </summary>
    private Vector3 GetRandomizedPosition(Vector3 originalPos)
    {
        Vector2 randomCircle = Random.insideUnitCircle * 0.3f;
        return new Vector3(originalPos.x + randomCircle.x, originalPos.y, originalPos.z + randomCircle.y);
    }
}

/// <summary>
/// 控制工作者 AI 行为的组件，负责任务分配、寻路、拾取和交付。
/// 依赖 NavMeshAgent 进行路径规划，继承自 BaseCharacterController 处理基础动画和物理。
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class WorkerAIController : BaseCharacterController
{
    [Header("AI 任务配置 (按优先级从上到下)")]
    [Tooltip("AI 将按列表顺序评估并执行任务")]
    public List<WorkerTask> tasks;

    [Tooltip("当遇到无法处理的物品或没有有效目标时，AI 会把东西扔进垃圾桶")]
    public TrashBinManager trashBin;

    private NavMeshAgent agent;
    private Vector3 startPosition; // 记录AI生成时的初始位置，无任务时会返回这里
    private WorkerTask currentTask; // 当前正在执行的任务

    /// <summary>
    /// 定义 AI 的各种行为状态。
    /// 细分了 MovingToSource/Collecting 和 MovingToDest/Delivering 状态，以防止状态切换冲突。
    /// </summary>
    private enum AIState { Idle, MovingToSource, Collecting, MovingToDest, Delivering, MovingToTrash, Trashing, ReturningHome }
    private AIState currentState = AIState.Idle;

    private float interactTimer = 0f;
    private float interactDelay = 0.2f; // 处理单个物品的延时，模拟真实的交互速度

    // 全局静态集合，用于实现简单的设施锁。防止多个AI同时尝试操作同一个槽位。
    private static HashSet<string> globalLocks = new HashSet<string>();
    private string myCurrentLock = "";

    protected override void Awake()
    {
        base.Awake();
        agent = GetComponent<NavMeshAgent>();
        startPosition = transform.position;
        agent.updateRotation = false; // 禁用 NavMeshAgent 的自动旋转，交由我们自己的逻辑处理以保证动画平滑
    }

    /// <summary>
    /// 当组件被禁用时，确保释放任何持有的锁，防止游戏出现死锁。
    /// </summary>
    private void OnDisable()
    {
        ReleaseLock();
    }

    private void Update()
    {
        // 如果代理已经停止或者速度极低，强制将移动速度设为 zero，这能有效防止角色原地发抖。
        if (agent.isStopped || agent.velocity.sqrMagnitude < 0.01f)
            MoveAndRotate(Vector3.zero);
        else
            MoveAndRotate(agent.velocity.normalized);

        // 基于当前状态执行对应的逻辑
        switch (currentState)
        {
            case AIState.Idle:
            case AIState.ReturningHome:
                FindNextTask(); // 空闲或回家途中不断评估新任务
                break;

            case AIState.MovingToSource:
                if (HasReachedDestination()) ChangeState(AIState.Collecting);
                break;

            case AIState.Collecting:
                HandleCollection();
                break;

            case AIState.MovingToDest:
                if (HasReachedDestination()) ChangeState(AIState.Delivering);
                break;

            case AIState.Delivering:
                HandleDelivery();
                break;

            case AIState.MovingToTrash:
                if (HasReachedDestination()) ChangeState(AIState.Trashing);
                break;

            case AIState.Trashing:
                HandleTrash();
                break;
        }
    }

    /// <summary>
    /// 统一的状态切换方法。
    /// 当进入交互状态 (Collecting, Delivering, Trashing, Idle) 时，强制停止 NavMeshAgent 寻路。
    /// </summary>
    private void ChangeState(AIState newState)
    {
        currentState = newState;
        if (newState == AIState.Collecting || newState == AIState.Delivering || newState == AIState.Trashing || newState == AIState.Idle)
        {
            if (agent.isOnNavMesh) agent.isStopped = true;
        }
    }

    /// <summary>
    /// 核心任务决策逻辑：决定 AI 下一步该干什么。
    /// 分为三大阶段：1. 进货（找东西拿），2. 送货（把手里的东西送出去），3. 待机（回家）。
    /// </summary>
    private void FindNextTask()
    {
        ReleaseLock(); // 每次做新决定前，先释放可能持有的旧锁

        ItemType? holdingType = GetHoldingItemType();

        // =========================================================
        // 【第一阶段：进货】只要手里没满，就尝试去源设施获取物品
        // =========================================================
        if (!IsFull)
        {
            foreach (var task in tasks)
            {
                if (task.sourceMachine != null && !IsSourceEmpty(task) && (task.targetShelf != null || task.targetMachine != null))
                {
                    ItemType? sourceType = GetSourceItemType(task);

                    if (sourceType.HasValue)
                    {
                        // 跨任务防御：如果手里已经有东西，且类型与当前正在看的这台机器产出不同，直接跳过。
                        if (holdingType.HasValue && holdingType.Value != sourceType.Value)
                        {
                            continue;
                        }

                        // 如果目标设施确实需要这种类型的物品
                        if (IsItemTypeMatchForTask(task, sourceType.Value))
                        {
                            int targetMissing = GetTargetRemainingNeedForType(task, sourceType.Value);

                            // 只有当目标设施未满，且 AI 手里的数量还不足以满足缺口时，才去拿。
                            if (!IsTargetFullForType(task, sourceType.Value) && carriedItems.Count < targetMissing)
                            {
                                string outLock = task.sourceMachine.GetInstanceID() + "_Out";
                                if (!IsLockOccupied(outLock))
                                {
                                    currentTask = task;
                                    ClaimLock(outLock);
                                    GoToDestination(task.GetSourcePosition(), AIState.MovingToSource);
                                    return;
                                }
                            }
                        }
                    }
                }
            }
        }

        // =========================================================
        // 【第二阶段：送货】如果手里拿着东西，首要任务是把它们送出去。
        // =========================================================
        if (HasItems && holdingType.HasValue)
        {
            bool isAllTargetsReallyFull = true;

            foreach (var task in tasks)
            {
                // 只考虑能接收当前持有类型物品的任务
                if (IsItemTypeMatchForTask(task, holdingType.Value))
                {
                    if (!IsTargetFullForType(task, holdingType.Value))
                    {
                        isAllTargetsReallyFull = false; // 找到一个理论上没满的目标

                        Component targetFacility = task.targetShelf != null ? (Component)task.targetShelf : (Component)task.targetMachine;
                        string inLock = targetFacility.GetInstanceID() + "_In";

                        // 尝试获取该目标的输入锁
                        if (!IsLockOccupied(inLock))
                        {
                            currentTask = task;
                            ClaimLock(inLock);
                            GoToDestination(task.GetTargetPosition(), AIState.MovingToDest);
                            return;
                        }
                    }
                }
            }

            // 如果所有匹配的目标设施都满了
            if (isAllTargetsReallyFull)
            {
                if (trashBin != null)
                {
                    string trashLock = trashBin.GetInstanceID() + "_In";
                    if (!IsLockOccupied(trashLock))
                    {
                        ClaimLock(trashLock);
                        GoToDestination(GetTrashPosition(), AIState.MovingToTrash);
                        return;
                    }
                }
                else
                {
                    ClearInventory(); // 没有垃圾桶的最后手段：直接清空，防止死循环
                }
            }
            else
            {
                // 目标没满，但目前锁被占用了，先切到 Idle 稍后重试
                if (currentState != AIState.Idle) ChangeState(AIState.Idle);
                return;
            }
        }

        // =========================================================
        // 【第三阶段：待机/返回原点】无事可做时。
        // =========================================================
        if (!HasItems)
        {
            float distToHome = Vector3.Distance(transform.position, startPosition);
            // 距离大于停止距离+缓冲才移动，减少寻路调用频率
            if (distToHome > agent.stoppingDistance + 0.3f)
            {
                if (currentState != AIState.ReturningHome) GoToDestination(startPosition, AIState.ReturningHome);
            }
            else
            {
                if (currentState != AIState.Idle) ChangeState(AIState.Idle);
            }
        }
        else
        {
            // 拿着东西但找不到目标，先原地站着。
            if (currentState != AIState.Idle) ChangeState(AIState.Idle);
        }
    }

    /// <summary>
    /// 处理从源机器收集物品的过程。
    /// </summary>
    private void HandleCollection()
    {
        ItemType? sourceType = GetSourceItemType(currentTask);
        if (!sourceType.HasValue)
        {
            ChangeState(AIState.Idle);
            return;
        }

        int targetMissing = GetTargetRemainingNeedForType(currentTask, sourceType.Value);
        int stillNeedToCollect = targetMissing - carriedItems.Count;

        // 停止收集条件：背包满、目标不再需要该类型、已拿够目标缺口、源机器空了
        if (IsFull || IsTargetFullForType(currentTask, sourceType.Value) || stillNeedToCollect <= 0 || IsSourceEmpty(currentTask))
        {
            ChangeState(AIState.Idle);
            return;
        }

        interactTimer += Time.deltaTime;
        if (interactTimer >= interactDelay)
        {
            interactTimer = 0f;
            GameObject item = currentTask.sourceMachine.CollectProduct();

            if (item != null) AddItem(item);
            else ChangeState(AIState.Idle);
        }
    }

    /// <summary>
    /// 处理将物品交付给目标设施的过程。
    /// </summary>
    private void HandleDelivery()
    {
        ItemType? holdingType = GetHoldingItemType();

        // 停止交付条件：持有类型无法判定、目标的该类型槽位已满
        if (!holdingType.HasValue || IsTargetFullForType(currentTask, holdingType.Value))
        {
            ChangeState(AIState.Idle);
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
                        ChangeState(AIState.Idle);
                        return;
                    }
                }
            }

            if (!HasItems) ChangeState(AIState.Idle);
        }
    }

    /// <summary>
    /// 处理丢弃物品到垃圾桶的过程。
    /// </summary>
    private void HandleTrash()
    {
        interactTimer += Time.deltaTime;
        if (interactTimer >= interactDelay)
        {
            interactTimer = 0f;
            GameObject topItem = RemoveTopItem();
            if (topItem != null && trashBin != null) trashBin.ReceiveTrash(topItem);

            if (!HasItems) ChangeState(AIState.Idle);
        }
    }

    // ================== 简单的字符串互斥锁机制 ==================

    private void ClaimLock(string lockKey)
    {
        ReleaseLock();
        if (!string.IsNullOrEmpty(lockKey))
        {
            myCurrentLock = lockKey;
            globalLocks.Add(myCurrentLock);
        }
    }

    private void ReleaseLock()
    {
        if (!string.IsNullOrEmpty(myCurrentLock))
        {
            globalLocks.Remove(myCurrentLock);
            myCurrentLock = "";
        }
    }

    private bool IsLockOccupied(string lockKey)
    {
        if (string.IsNullOrEmpty(lockKey)) return false;
        return globalLocks.Contains(lockKey);
    }

    // ================== 寻路控制 ==================

    private void GoToDestination(Vector3 targetPos, AIState newState)
    {
        if (agent.isOnNavMesh && agent.isStopped) agent.isStopped = false;
        agent.SetDestination(targetPos);
        currentState = newState;
    }

    private bool HasReachedDestination()
    {
        // 添加 0.25f 容差，避免因物体 Collider 阻挡无法到达精确坐标导致的问题
        return (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.25f);
    }

    private Vector3 GetTrashPosition()
    {
        if (trashBin == null) return transform.position;
        Transform slot = trashBin.transform.Find("WorkerSlot");
        return slot != null ? slot.position : trashBin.transform.position;
    }

    // ================== 基于特定 ItemType 的逻辑判断 ==================

    /// <summary>
    /// 预判当前任务的源机器产出的是什么物品。
    /// </summary>
    private ItemType? GetSourceItemType(WorkerTask task)
    {
        if (task != null && task.sourceMachine != null && task.sourceMachine.readyProducts.Count > 0)
        {
            GameObject readyItem = task.sourceMachine.readyProducts[0];
            if (readyItem != null)
            {
                ItemData data = readyItem.GetComponent<ItemData>();
                if (data != null) return data.itemType;
            }
        }
        return null;
    }

    /// <summary>
    /// 检查 AI 手里拿着什么类型的物品。
    /// </summary>
    private ItemType? GetHoldingItemType()
    {
        if (HasItems)
        {
            GameObject topItem = PeekTopItem();
            if (topItem != null)
            {
                ItemData data = topItem.GetComponent<ItemData>();
                if (data != null) return data.itemType;
            }
        }
        return null;
    }

    /// <summary>
    /// 判断某类型的物品是否可以放置在任务目标中。
    /// </summary>
    private bool IsItemTypeMatchForTask(WorkerTask task, ItemType type)
    {
        if (task.targetShelf != null)
        {
            return task.targetShelf.acceptedItemType == type;
        }
        else if (task.targetMachine != null)
        {
            foreach (var req in task.targetMachine.inputRequirements)
            {
                if (req.requiredType == type) return true;
            }
            return false;
        }
        return true;
    }

    /// <summary>
    /// 针对特定物品类型，判断目标设施的相关槽位是否已满。
    /// </summary>
    private bool IsTargetFullForType(WorkerTask task, ItemType type)
    {
        if (task.targetShelf != null) return task.targetShelf.IsFull;
        if (task.targetMachine != null)
        {
            foreach (var req in task.targetMachine.inputRequirements)
            {
                if (req.requiredType == type)
                {
                    return req.currentItems.Count >= req.slots.Length;
                }
            }
            return true; // 如果机器不需要这个类型，也当作已满处理
        }
        return true;
    }

    /// <summary>
    /// 获取目标设施还需要多少个特定类型的物品。
    /// </summary>
    private int GetTargetRemainingNeedForType(WorkerTask task, ItemType type)
    {
        if (task.targetShelf != null) return task.targetShelf.IsFull ? 0 : 99;
        if (task.targetMachine != null)
        {
            foreach (var req in task.targetMachine.inputRequirements)
            {
                if (req.requiredType == type)
                {
                    return req.slots.Length - req.currentItems.Count;
                }
            }
            return 0;
        }
        return 0;
    }

    private bool IsSourceEmpty(WorkerTask task)
    {
        if (task.sourceMachine == null) return true;
        return task.sourceMachine.readyProducts.Count == 0;
    }

    /// <summary>
    /// 执行具体的放置物品操作。
    /// </summary>
    private bool DeliverItemToTarget(WorkerTask task, ItemType itemType, GameObject item)
    {
        if (task.targetShelf != null) return task.targetShelf.TryAddProduct(itemType, item);
        if (task.targetMachine != null) return task.targetMachine.TryReceiveInput(itemType, item);
        return false;
    }

    /// <summary>
    /// 兜底方法：强制销毁所有持有的物品。
    /// </summary>
    private void ClearInventory()
    {
        while (HasItems)
        {
            GameObject item = RemoveTopItem();
            if (item != null) Destroy(item);
        }
    }
}