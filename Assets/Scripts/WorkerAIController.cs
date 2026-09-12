using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

[System.Serializable]
public class WorkerTask
{
    [Header("任务目标配置 (作为类型参考模板即可)")]
    [Tooltip("例如拖入一个面包机，AI会自动寻找全图所有的面包机，并自动为其凑齐配方原料")]
    public ShelfManager targetShelf;
    public ConsumeProductionMachine targetMachine;

    // 运行时动态锁定的具体实例
    [HideInInspector] public BaseProductionMachine dynamicSource;
    [HideInInspector] public ShelfManager dynamicTargetShelf;
    [HideInInspector] public ConsumeProductionMachine dynamicTargetMachine;

    public Vector3 GetSourcePosition()
    {
        if (dynamicSource == null) return Vector3.zero;
        Transform slot = dynamicSource.transform.Find("OutputWorkerSlot");
        if (slot == null) slot = dynamicSource.transform.Find("WorkerSlot");
        return GetRandomizedPosition(slot != null ? slot.position : dynamicSource.transform.position);
    }

    public Vector3 GetTargetPosition()
    {
        Transform slot = null;
        Vector3 basePos = Vector3.zero;

        if (dynamicTargetShelf != null)
        {
            slot = dynamicTargetShelf.transform.Find("WorkerSlot");
            basePos = slot != null ? slot.position : dynamicTargetShelf.transform.position;
        }
        else if (dynamicTargetMachine != null)
        {
            slot = dynamicTargetMachine.transform.Find("InputWorkerSlot");
            if (slot == null) slot = dynamicTargetMachine.transform.Find("WorkerSlot");
            basePos = slot != null ? slot.position : dynamicTargetMachine.transform.position;
        }
        return GetRandomizedPosition(basePos);
    }

    private Vector3 GetRandomizedPosition(Vector3 originalPos)
    {
        Vector2 randomCircle = Random.insideUnitCircle * 0.15f;
        return new Vector3(originalPos.x + randomCircle.x, originalPos.y, originalPos.z + randomCircle.y);
    }
}

[RequireComponent(typeof(NavMeshAgent))]
public class WorkerAIController : BaseCharacterController
{
    [Header("AI 任务配置")]
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
        if (FacilityManager.Instance == null) return;

        ItemType? holdingType = GetHoldingItemType();

        // =========================================================
        // 【第一阶段：进货】 (根据模板，智能读取配方缺口)
        // =========================================================
        if (!IsFull)
        {
            foreach (var task in tasks)
            {
                // 获取这个模板对应的所有真实机器，当前急需的原料类型
                List<ItemType> neededTypes = GetNeededTypesForTemplate(task);

                foreach (ItemType neededType in neededTypes)
                {
                    // 防止左手拿鸡蛋，右手去拿面粉
                    if (holdingType.HasValue && holdingType.Value != neededType) continue;

                    // 计算全场真实机器的该原料总缺口
                    int totalMissing = GetTotalMissingNeedsForTemplate(task, neededType);

                    if (totalMissing > 0 && carriedItems.Count < totalMissing)
                    {
                        // 去全场找有这种原料产出的机器
                        BaseProductionMachine availableSource = FindAvailableSourceFromManager(neededType);
                        if (availableSource != null)
                        {
                            task.dynamicSource = availableSource;
                            currentTask = task;
                            ClaimLock(availableSource.GetInstanceID() + "_Out");
                            GoToDestination(task.GetSourcePosition(), AIState.MovingToSource);
                            return;
                        }
                    }
                }
            }
        }

        // =========================================================
        // 【第二阶段：送货】 (寻找符合模板的真实机器投递)
        // =========================================================
        if (HasItems && holdingType.HasValue)
        {
            bool isAllTargetsReallyFull = true;

            foreach (var task in tasks)
            {
                // 1. 如果模板是货架
                if (task.targetShelf != null)
                {
                    foreach (var runtimeShelf in FacilityManager.Instance.allShelves)
                    {
                        // 【黑科技匹配】：判断类型是否一致，直接对比接受的物品类型！
                        if (runtimeShelf.acceptedItemType == task.targetShelf.acceptedItemType)
                        {
                            if (runtimeShelf.acceptedItemType == holdingType.Value && !runtimeShelf.IsFull)
                            {
                                isAllTargetsReallyFull = false;
                                string inLock = runtimeShelf.GetInstanceID() + "_In";
                                if (!IsLockOccupied(inLock))
                                {
                                    task.dynamicTargetShelf = runtimeShelf;
                                    task.dynamicTargetMachine = null;
                                    currentTask = task;
                                    ClaimLock(inLock);
                                    GoToDestination(task.GetTargetPosition(), AIState.MovingToDest);
                                    return;
                                }
                            }
                        }
                    }
                }

                // 2. 如果模板是加工机器
                if (task.targetMachine != null)
                {
                    foreach (var runtimeMachine in FacilityManager.Instance.allConsumers)
                    {
                        // 【黑科技匹配】：通过判断产出物是否一样，来确定是不是同一种机器！（不怕改名字，极其稳定）
                        if (runtimeMachine.targetProductPrefab == task.targetMachine.targetProductPrefab)
                        {
                            if (MachineNeedsType(runtimeMachine, holdingType.Value))
                            {
                                isAllTargetsReallyFull = false;
                                string inLock = runtimeMachine.GetInstanceID() + "_In";
                                if (!IsLockOccupied(inLock))
                                {
                                    task.dynamicTargetMachine = runtimeMachine;
                                    task.dynamicTargetShelf = null;
                                    currentTask = task;
                                    ClaimLock(inLock);
                                    GoToDestination(task.GetTargetPosition(), AIState.MovingToDest);
                                    return;
                                }
                            }
                        }
                    }
                }
            }

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
                else ClearInventory();
            }
            else
            {
                if (currentState != AIState.Idle) ChangeState(AIState.Idle);
                return;
            }
        }

        // =========================================================
        // 【第三阶段：回家】
        // =========================================================
        if (!HasItems)
        {
            float distToHome = Vector3.Distance(transform.position, startPosition);
            if (distToHome > agent.stoppingDistance + 0.3f)
            {
                if (currentState != AIState.ReturningHome) GoToDestination(startPosition, AIState.ReturningHome);
            }
            else if (currentState != AIState.Idle) ChangeState(AIState.Idle);
        }
        else if (currentState != AIState.Idle) ChangeState(AIState.Idle);
    }

    private void HandleCollection()
    {
        ItemType? sourceType = GetSourceItemType(currentTask.dynamicSource);
        if (!sourceType.HasValue) { ChangeState(AIState.Idle); return; }

        int totalMissing = GetTotalMissingNeedsForTemplate(currentTask, sourceType.Value);
        int stillNeedToCollect = totalMissing - carriedItems.Count;

        if (IsFull || stillNeedToCollect <= 0 || currentTask.dynamicSource.readyProducts.Count == 0)
        {
            ChangeState(AIState.Idle);
            return;
        }

        interactTimer += Time.deltaTime;
        if (interactTimer >= interactDelay)
        {
            interactTimer = 0f;
            GameObject item = currentTask.dynamicSource.CollectProduct();

            if (item != null) AddItem(item);
            else ChangeState(AIState.Idle);
        }
    }

    private void HandleDelivery()
    {
        ItemType? holdingType = GetHoldingItemType();

        if (!holdingType.HasValue || IsDynamicTargetFull(currentTask.dynamicTargetShelf, currentTask.dynamicTargetMachine, holdingType.Value))
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
                    bool delivered = false;
                    if (currentTask.dynamicTargetShelf != null) delivered = currentTask.dynamicTargetShelf.TryAddProduct(itemData.itemType, topItem);
                    else if (currentTask.dynamicTargetMachine != null) delivered = currentTask.dynamicTargetMachine.TryReceiveInput(itemData.itemType, topItem);

                    if (delivered) RemoveTopItem();
                    else { ChangeState(AIState.Idle); return; }
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

    // =========================================================
    // 【模板匹配核心算法】
    // =========================================================

    /// <summary>
    /// 根据任务模板，分析全场同类机器当前【急需】的所有原料类型
    /// </summary>
    private List<ItemType> GetNeededTypesForTemplate(WorkerTask task)
    {
        List<ItemType> neededTypes = new List<ItemType>();

        if (task.targetShelf != null)
        {
            foreach (var runtimeShelf in FacilityManager.Instance.allShelves)
            {
                if (runtimeShelf.acceptedItemType == task.targetShelf.acceptedItemType && !runtimeShelf.IsFull)
                {
                    if (!neededTypes.Contains(runtimeShelf.acceptedItemType)) neededTypes.Add(runtimeShelf.acceptedItemType);
                }
            }
        }
        else if (task.targetMachine != null)
        {
            foreach (var runtimeMachine in FacilityManager.Instance.allConsumers)
            {
                // 通过比对产出预制体，判断是否是同一种机器（最安全稳定的方法）
                if (runtimeMachine.targetProductPrefab == task.targetMachine.targetProductPrefab)
                {
                    foreach (var req in runtimeMachine.inputRequirements)
                    {
                        if (req.currentItems.Count < req.slots.Length)
                        {
                            if (!neededTypes.Contains(req.requiredType)) neededTypes.Add(req.requiredType);
                        }
                    }
                }
            }
        }
        return neededTypes;
    }

    /// <summary>
    /// 根据任务模板，计算全场同类机器对某种原料的【总缺口数量】
    /// </summary>
    private int GetTotalMissingNeedsForTemplate(WorkerTask task, ItemType type)
    {
        int totalNeed = 0;
        if (task.targetShelf != null)
        {
            foreach (var runtimeShelf in FacilityManager.Instance.allShelves)
            {
                if (runtimeShelf.acceptedItemType == task.targetShelf.acceptedItemType && runtimeShelf.acceptedItemType == type && !runtimeShelf.IsFull)
                {
                    totalNeed += 99;
                }
            }
        }
        else if (task.targetMachine != null)
        {
            foreach (var runtimeMachine in FacilityManager.Instance.allConsumers)
            {
                if (runtimeMachine.targetProductPrefab == task.targetMachine.targetProductPrefab)
                {
                    foreach (var req in runtimeMachine.inputRequirements)
                    {
                        if (req.requiredType == type) totalNeed += (req.slots.Length - req.currentItems.Count);
                    }
                }
            }
        }
        return totalNeed;
    }

    private BaseProductionMachine FindAvailableSourceFromManager(ItemType requiredType)
    {
        foreach (var source in FacilityManager.Instance.allProducers)
        {
            if (source == null || source.readyProducts.Count == 0) continue;

            GameObject readyItem = source.readyProducts[0];
            if (readyItem != null)
            {
                ItemData data = readyItem.GetComponent<ItemData>();
                if (data != null && data.itemType == requiredType)
                {
                    string lockKey = source.GetInstanceID() + "_Out";
                    if (!IsLockOccupied(lockKey)) return source;
                }
            }
        }
        return null;
    }

    // ================== 判断与辅助方法 ==================

    private bool IsDynamicTargetFull(ShelfManager shelf, ConsumeProductionMachine machine, ItemType type)
    {
        if (shelf != null) return shelf.IsFull;
        if (machine != null)
        {
            foreach (var req in machine.inputRequirements)
                if (req.requiredType == type) return req.currentItems.Count >= req.slots.Length;
        }
        return true;
    }

    private bool MachineNeedsType(ConsumeProductionMachine machine, ItemType type)
    {
        foreach (var req in machine.inputRequirements)
        {
            if (req.requiredType == type && req.currentItems.Count < req.slots.Length) return true;
        }
        return false;
    }

    private ItemType? GetSourceItemType(BaseProductionMachine machine)
    {
        if (machine != null && machine.readyProducts.Count > 0)
        {
            GameObject readyItem = machine.readyProducts[0];
            if (readyItem != null)
            {
                ItemData data = readyItem.GetComponent<ItemData>();
                if (data != null) return data.itemType;
            }
        }
        return null;
    }

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

    private void ClearInventory()
    {
        while (HasItems)
        {
            GameObject item = RemoveTopItem();
            if (item != null) Destroy(item);
        }
    }
}