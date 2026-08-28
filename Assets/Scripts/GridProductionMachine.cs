using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// 三维矩阵堆叠生产机器(处理“物品堆叠”逻辑。适用于“鸡蛋架”（三维矩阵堆叠）)
/// 适用于“鸡蛋架”（三维矩阵堆叠），需要输入番茄，产出鸡蛋
/// 适用于“番茄地”（无输入），产出番茄
/// 适用于“鸡圈”（无输入），产出鸡蛋
/// </summary>
public class GridProductionMachine : MonoBehaviour
{
    [Header("基础结构配置")]
    public string structureID;
    public Transform stackPivot;         // 物品在场景中视觉堆叠的起点位置
    public float itemHeightOffset = 0.2f; // 每层物品的垂直间距

    [Header("三维矩阵堆叠配置")]
    public int rows = 2;                 // X 轴：并排横向数量
    public int columns = 2;              // Z 轴：前后纵向数量
    public int maxLayers = 3;            // Y 轴：每堆最多往上叠几层
    public float xSpacing = 0.3f;        // 横向两堆之间的间距
    public float zSpacing = 0.3f;        // 前后两堆之间的间距

    [Header("固定挂点（可选，优先于矩阵计算，如番茄树树枝）")]
    public Transform[] outputTargets;

    [Header("生产配置")]
    public bool requiresInput = false;    // 是否需要原材料（番茄地为 false，鸡圈为 true）
    public string inputItemID;            // 输入物品ID (如 "tomato")
    public int inputCostPerCount = 2;     // 每次生产消耗的输入数量
    public GameObject outputPrefab;       // 产出物品的 Prefab (如 鸡蛋)
    public float productionCycleTime = 4f;// 生产周期（秒）

    [Header("产出动画配置")]
    public Transform spawnOrigin;         // 产出物生成原点（如树根 Root_Point）
    public Transform visualRoot;          // 可呼吸的视觉根节点（如树干 Tree_Visual）
    public bool enableBreathing = false;  // 未满时是否播放呼吸动画
    public float flyDuration = 0.45f;     // 从原点到目标点的飞行时长
    public float jumpHeight = 0.6f;       // 弹跳抛物线高度
    public Vector3 outputTargetEulerAngles = Vector3.zero; // 产出物落地后的本地欧拉角
    public Vector3 outputTargetScale = Vector3.one;        // 产出物目标缩放（从 0 缓动到此值）

    [Header("输入暂存区")]
    public int maxInputStorage = 6;
    [SerializeField] private int currentInputCount = 0; // 当前囤积的原材料数量

    [Header("UI 引用")]
    public ProductionUI productionUI;

    private List<ItemData> gridItems = new List<ItemData>();

    private int maxCapacity;
    private float productionTimer = 0f;
    private bool isProducing = false;
    private bool isSpawning = false;
    private Tween breatheTween;

    public int CurrentCount => gridItems.Count;
    public bool IsFull => gridItems.Count >= maxCapacity;
    public bool IsEmpty => gridItems.Count == 0;

    private void Awake()
    {
        maxCapacity = HasFixedTargets() ? outputTargets.Length : rows * columns * maxLayers;
    }

    private void Update()
    {
        HandleProduction();
    }

    private void HandleProduction()
    {
        if (isSpawning) return;

        if (IsFull)
        {
            StopBreathing();
            if (productionUI != null) productionUI.UpdateProgress(0, productionCycleTime);
            return;
        }

        if (!isProducing)
        {
            if (!requiresInput || currentInputCount >= inputCostPerCount)
            {
                isProducing = true;
                productionTimer = 0f;
                if (enableBreathing) StartBreathing();
            }
            else if (productionUI != null)
            {
                productionUI.UpdateProgress(0, productionCycleTime);
            }
            return;
        }

        productionTimer += Time.deltaTime;

        if (productionUI != null)
            productionUI.UpdateProgress(productionTimer, productionCycleTime);

        if (productionTimer >= productionCycleTime)
        {
            isProducing = false;
            if (requiresInput) currentInputCount -= inputCostPerCount;
            StartCoroutine(SpawnProductRoutine());
            if (productionUI != null) productionUI.UpdateProgress(0, productionCycleTime);
        }
    }

    private IEnumerator SpawnProductRoutine()
    {
        if (outputPrefab == null) yield break;

        isSpawning = true;

        GameObject newObj = Instantiate(outputPrefab);
        ItemData newItem = newObj.GetComponent<ItemData>();

        if (!TryReserveGridSlot(newItem, out Transform targetParent, out Vector3 targetLocalPos, out Vector3 targetWorldPos))
        {
            Destroy(newObj);
            isSpawning = false;
            yield break;
        }

        Vector3 origin = GetSpawnOriginPosition();
        newObj.transform.SetParent(null);
        newObj.transform.position = origin;
        newObj.transform.localScale = Vector3.zero;

        Collider col = null;
        if (newObj.TryGetComponent<Collider>(out col)) col.enabled = false;

        if (enableBreathing) StartBreathing();

        Sequence seq = DOTween.Sequence();
        // seq.Join(newObj.transform.DOJump(targetWorldPos, jumpHeight, 1, flyDuration).SetEase(Ease.OutQuad));
        seq.Join(newObj.transform.DOMove(targetWorldPos, flyDuration).SetEase(Ease.Linear));
        seq.Join(newObj.transform.DOScale(outputTargetScale, flyDuration).SetEase(Ease.Linear));

        yield return seq.WaitForCompletion();

        if (newObj != null)
        {
            newObj.transform.SetParent(targetParent);
            newObj.transform.localPosition = targetLocalPos;
            newObj.transform.localRotation = Quaternion.Euler(outputTargetEulerAngles);
            newObj.transform.localScale = outputTargetScale;

            if (enableBreathing && visualRoot != null)
                visualRoot.DOPunchScale(new Vector3(0.04f, -0.04f, 0.04f), 0.15f, 4, 0.5f);

            if (col != null) col.enabled = true;
        }

        isSpawning = false;

        if (IsFull) StopBreathing();
    }

    private bool TryReserveGridSlot(ItemData item, out Transform targetParent, out Vector3 targetLocalPos, out Vector3 targetWorldPos)
    {
        targetParent = null;
        targetLocalPos = Vector3.zero;
        targetWorldPos = Vector3.zero;

        if (IsFull) return false;

        gridItems.Add(item);
        int index = gridItems.Count - 1;

        if (HasFixedTargets())
        {
            if (index >= outputTargets.Length)
            {
                gridItems.RemoveAt(index);
                return false;
            }

            Transform target = outputTargets[index];
            targetParent = target;
            targetLocalPos = Vector3.zero;
            targetWorldPos = target.position;
            return true;
        }

        if (stackPivot == null)
        {
            gridItems.RemoveAt(index);
            return false;
        }

        targetParent = stackPivot;

        int itemsPerLayer = rows * columns;
        int layer = index / itemsPerLayer;
        int indexInLayer = index % itemsPerLayer;
        int r = indexInLayer % rows;
        int c = indexInLayer / rows;

        targetLocalPos = new Vector3(r * xSpacing, layer * itemHeightOffset, c * zSpacing);
        targetWorldPos = stackPivot.TransformPoint(targetLocalPos);
        return true;
    }

    private Vector3 GetSpawnOriginPosition()
    {
        if (spawnOrigin != null) return spawnOrigin.position;
        if (stackPivot != null) return stackPivot.position;
        return transform.position;
    }

    private bool HasFixedTargets() => outputTargets != null && outputTargets.Length > 0;

    private void StartBreathing()
    {
        if (!enableBreathing || IsFull) return;
        if (breatheTween != null && breatheTween.IsActive()) return;

        Transform root = visualRoot != null ? visualRoot : transform;
        breatheTween = root.DOScale(new Vector3(1.05f, 0.94f, 1.05f), 0.6f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    private void StopBreathing()
    {
        if (breatheTween != null)
        {
            breatheTween.Kill();
            breatheTween = null;
        }

        if (!enableBreathing) return;

        Transform root = visualRoot != null ? visualRoot : transform;
        root.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutQuad);
    }

    public ItemData TryRemoveItem()
    {
        if (IsEmpty) return null;

        int lastIndex = gridItems.Count - 1;
        ItemData item = gridItems[lastIndex];
        gridItems.RemoveAt(lastIndex);

        item.transform.SetParent(null);

        if (enableBreathing && visualRoot != null)
            visualRoot.DOPunchScale(new Vector3(-0.04f, 0.06f, -0.04f), 0.2f, 3, 0.5f);

        return item;
    }

    public bool TryFeedInput(string itemID)
    {
        if (!requiresInput || itemID != inputItemID) return false;
        if (currentInputCount >= maxInputStorage) return false;

        currentInputCount++;
        return true;
    }

    private void OnDestroy()
    {
        breatheTween?.Kill();
    }
}
