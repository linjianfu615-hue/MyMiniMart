using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

[System.Serializable]
public class WorkerTask
{
    [Header("任务起点")]
    [Tooltip("去哪里拿货 (如：鸡舍、西红柿树)")]
    public BaseProductionMachine sourceMachine;

    [Header("任务终点 (根据需求二选一即可)")]
    public ShelfManager targetShelf;
    public ConsumeProductionMachine targetMachine;

    public Vector3 GetSourcePosition()
    {
        if (sourceMachine == null) return Vector3.zero;

        Transform slot = sourceMachine.transform.Find("OutputWorkerSlot");
        if (slot == null) slot = sourceMachine.transform.Find("WorkerSlot");

        return slot != null ? slot.position : sourceMachine.transform.position;
    }

    public Vector3 GetTargetPosition()
    {
        Transform slot = null;
        if (targetShelf != null)
        {
            slot = targetShelf.transform.Find("WorkerSlot");
            return slot != null ? slot.position : targetShelf.transform.position;
        }
        else if (targetMachine != null)
        {
            slot = targetMachine.transform.Find("InputWorkerSlot");
            return slot != null ? slot.position : targetMachine.transform.position;
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

    private enum AIState { Idle, MovingToSource, Collecting, MovingToDest, Delivering, MovingToTrash, Trashing, ReturningHome }
    private AIState currentState = AIState.Idle;

    private float interactTimer = 0f;
    private float interactDelay = 0.2f;

    private static HashSet<string> globalLocks = new HashSet<string>();
    private string myCurrentLock = "";

    protected override void Awake()
    {
        base.Awake();
        agent = GetComponent<NavMeshAgent>();
        startPosition = transform.position;
        agent.updateRotation = false;
    }

    private void OnDisable()
    {
        ReleaseLock();
    }

    private void Update()
    {
        if (agent.isStopped || agent.velocity.sqrMagnitude < 0.01f)
            MoveAndRotate(Vector3.zero);
        else
            MoveAndRotate(agent.velocity.normalized);

        switch (currentState)
        {
            case AIState.Idle:
            case AIState.ReturningHome:
                FindNextTask();
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

    private void ChangeState(AIState newState)
    {
        currentState = newState;
        if (newState == AIState.Collecting || newState == AIState.Delivering || newState == AIState.Trashing || newState == AIState.Idle)
        {
            if (agent.isOnNavMesh) agent.isStopped = true;
        }
    }

    private void FindNextTask()
    {
        ReleaseLock();

        ItemType? holdingType = GetHoldingItemType(); // 看看手里拿着啥

        // =========================================================
        // 【第一阶段：进货】只要手里没满，就尽量去装满！
        // =========================================================
        if (!IsFull)
        {
            foreach (var task in tasks)
            {
                // 如果源头机器有货
                if (task.sourceMachine != null && !IsSourceEmpty(task) && (task.targetShelf != null || task.targetMachine != null))
                {
                    ItemType? sourceType = GetSourceItemType(task); // 这台机器产出的是啥

                    if (sourceType.HasValue)
                    {
                        // 【跨机器收集的核心防御】：如果手里已经有货了，但跟这台机器产出的不一样，绝对不拿！（防止左手小麦右手西红柿）
                        if (holdingType.HasValue && holdingType.Value != sourceType.Value)
                        {
                            continue; // 跳过这个任务，看下一个
                        }

                        // 如果目标机器需要这种货
                        if (IsItemTypeMatchForTask(task, sourceType.Value))
                        {
                            int targetMissing = GetTargetRemainingNeedForType(task, sourceType.Value);

                            // 只要目标没满，并且我手里的数量还没凑够缺口，就继续去拿！
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
        // 【第二阶段：送货】如果上面没进到货（装满了，或者没别的机器能凑了），且手里有货，立刻去送
        // =========================================================
        if (HasItems && holdingType.HasValue)
        {
            bool isAllTargetsReallyFull = true;

            foreach (var task in tasks)
            {
                if (IsItemTypeMatchForTask(task, holdingType.Value))
                {
                    if (!IsTargetFullForType(task, holdingType.Value))
                    {
                        isAllTargetsReallyFull = false;

                        Component targetFacility = task.targetShelf != null ? (Component)task.targetShelf : (Component)task.targetMachine;
                        string inLock = targetFacility.GetInstanceID() + "_In";

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

            // 如果能送的地方全满了
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
                    ClearInventory();
                }
            }
            else
            {
                // 没满，但正被别的同事锁着，原地等一下
                if (currentState != AIState.Idle) ChangeState(AIState.Idle);
                return;
            }
        }

        // =========================================================
        // 第三阶段：回家待机
        // =========================================================
        if (!HasItems) // 只有真正空手才回家，拿着东西即使卡住也原地等
        {
            float distToHome = Vector3.Distance(transform.position, startPosition);
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
            if (currentState != AIState.Idle) ChangeState(AIState.Idle);
        }
    }

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

        // 如果拿满了上限、或者已经拿够了机器缺口、或者这块地薅秃了
        if (IsFull || IsTargetFullForType(currentTask, sourceType.Value) || stillNeedToCollect <= 0 || IsSourceEmpty(currentTask))
        {
            // 关键：切换回 Idle 后，下一帧会立刻执行 FindNextTask。
            // 此时由于 carriedItems.Count < targetMissing，AI 会聪明地去【下一块小麦地】继续拿货！
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

    private void HandleDelivery()
    {
        ItemType? holdingType = GetHoldingItemType();

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

    // ================== 锁机制与寻路 ==================

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

    private void GoToDestination(Vector3 targetPos, AIState newState)
    {
        if (agent.isOnNavMesh && agent.isStopped) agent.isStopped = false;
        agent.SetDestination(targetPos);
        currentState = newState;
    }

    private bool HasReachedDestination()
    {
        return (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.25f);
    }

    private Vector3 GetTrashPosition()
    {
        if (trashBin == null) return transform.position;
        Transform slot = trashBin.transform.Find("WorkerSlot");
        return slot != null ? slot.position : trashBin.transform.position;
    }

    // ================== 基于 ItemType 的精准判断 ==================

    /// <summary>
    /// 只检查源机器产出的物品类型
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
    /// 只检查手里拿着的物品类型
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
            return true;
        }
        return true;
    }

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

    private bool DeliverItemToTarget(WorkerTask task, ItemType itemType, GameObject item)
    {
        if (task.targetShelf != null) return task.targetShelf.TryAddProduct(itemType, item);
        if (task.targetMachine != null) return task.targetMachine.TryReceiveInput(itemType, item);
        return false;
    }

    private void ClearInventory()
    {
        while (HasItems)
        {
            GameObject item = RemoveTopItem();
            if (item != null) Destroy(item);
        }
    }
}