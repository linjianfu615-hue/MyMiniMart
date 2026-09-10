using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// 生产设备的抽象基类（适用于树木、农田、加工机器、动物等）
/// </summary>
public abstract class BaseProductionMachine : MonoBehaviour
{
    [Header("基础设置 (Base Settings)")]
    [Tooltip("目标产品预制体 (如: 西红柿、小麦、鸡蛋)")]
    public GameObject targetProductPrefab;

    [Tooltip("产品刚生成时的初始位置（如树干中心），如果不填则默认使用机器自身的中心点")]
    public Transform rootSpawnPoint;

    [Tooltip("按顺序配置每个产品的最终存放位置（如：树上的4个不同树枝空物体，或地上的9个网格空物体）")]
    public Transform[] spawnPoints;

    [Tooltip("存放产出物的父节点(方便层级管理，可为空)")]
    public Transform productContainer;

    [Tooltip("生成时飞向目标点的动画时长")]
    public float spawnAnimDuration = 0.5f;

    /// <summary>
    /// 最大堆叠/生产容量（直接由配置的生成点数量决定）
    /// </summary>
    public int MaxCapacity => spawnPoints != null ? spawnPoints.Length : 0;

    /// <summary>
    /// 精准记录每个槽位上当前绑定的物体。
    /// 解决玩家中途拿走物品导致的槽位错乱和重叠Bug。
    /// </summary>
    protected GameObject[] slotOccupants;

    /// <summary>
    /// 存放“已经完全到达目标点，且可被玩家收集”的产品列表
    /// </summary>
    public List<GameObject> readyProducts = new List<GameObject>();

    /// <summary>
    /// 判断机器的所有槽位是否都被占满
    /// </summary>
    public bool IsMachineFull
    {
        get
        {
            if (slotOccupants == null) return true;
            // 遍历所有槽位，只要有一个坑位是空的(null)，就不算满
            foreach (var occupant in slotOccupants)
            {
                if (occupant == null) return false;
            }
            return true;
        }
    }

    protected virtual void Awake()
    {
        // 根据配置的生成点数量，初始化槽位占用数组
        if (spawnPoints != null)
        {
            slotOccupants = new GameObject[spawnPoints.Length];
        }
    }

    /// <summary>
    /// 核心生成逻辑，供子类在满足各自条件时调用
    /// </summary>
    protected virtual void GenerateProduct()
    {
        // 防错：如果没有配置生成点，则停止生成
        if (spawnPoints == null || spawnPoints.Length == 0) return;

        // 1. 遍历寻找第一个为空(null)的槽位索引
        int freeSlotIndex = -1;
        for (int i = 0; i < slotOccupants.Length; i++)
        {
            if (slotOccupants[i] == null)
            {
                freeSlotIndex = i;
                break;
            }
        }

        // 如果没有空槽位，说明已经满了，直接返回
        if (freeSlotIndex == -1) return;

        // 2. 获取目标生成点
        Transform targetSpawnPoint = spawnPoints[freeSlotIndex];
        Vector3 startPos = rootSpawnPoint != null ? rootSpawnPoint.position : transform.position;

        // 3. 实例化目标产品
        GameObject newProduct = Instantiate(targetProductPrefab, startPos, Quaternion.identity);

        // 4. 【核心机制】立即将该空槽位标记为被此物品占用！防止动画期间其它生成的物品抢占同一个坑位
        slotOccupants[freeSlotIndex] = newProduct;

        // 5. 设置父节点以保持场景层级整洁
        if (productContainer != null)
            newProduct.transform.SetParent(productContainer);
        else
            newProduct.transform.SetParent(targetSpawnPoint);

        newProduct.transform.localEulerAngles = Vector3.zero;

        // 6. 播放生成动画。传入回调函数，确保动画彻底播放完毕、物品到达树枝后，才加入成熟列表供玩家收集
        PlaySpawnAnimation(newProduct, targetSpawnPoint.position, () =>
        {
            readyProducts.Add(newProduct);
        });

        // 触发额外的排列逻辑（供特殊的子类重写使用）
        ArrangeProducts();
    }

    /// <summary>
    /// 播放物品的生成和移动动画
    /// </summary>
    protected virtual void PlaySpawnAnimation(GameObject obj, Vector3 targetPos, System.Action onComplete)
    {
        // 【核心修复】：不要用 Vector3.one！
        // 在 SetParent 之后，Unity 已经自动算好了这个物品为了保持真实大小，在这个鸡舍下应该有的 LocalScale（比如 0.01）
        // 我们先把它原本正确的缩放值记录下来：
        Vector3 targetScale = obj.transform.localScale;

        // 然后把它缩小到0，准备播放“长出来”的动画
        obj.transform.localScale = Vector3.zero;

        // 放大时，放大到刚才记录的 targetScale，而不是强制的 Vector3.one
        obj.transform.DOScale(targetScale, spawnAnimDuration).SetEase(Ease.OutBack);

        // 平滑移动到目标点
        obj.transform.DOMove(targetPos, spawnAnimDuration)
                     .SetEase(Ease.OutBack)
                     .OnComplete(() => onComplete?.Invoke());
    }

    protected virtual void ArrangeProducts() { }

    /// <summary>
    /// 供玩家或搬运工 (Worker) 收集产品的外部接口
    /// </summary>
    /// <returns>返回被收集的物品 GameObject，如果没有则返回 null</returns>
    public virtual GameObject CollectProduct()
    {
        // 玩家只会从 readyProducts (完全成熟且已就位) 的列表里拿东西
        if (readyProducts.Count > 0)
        {
            // 取出最后成熟的一个产品
            int lastIndex = readyProducts.Count - 1;
            GameObject product = readyProducts[lastIndex];
            readyProducts.RemoveAt(lastIndex);

            // 【核心机制】玩家拿走物品后，遍历占用数组，精准清空对应的那个具体槽位
            for (int i = 0; i < slotOccupants.Length; i++)
            {
                if (slotOccupants[i] == product)
                {
                    slotOccupants[i] = null; // 释放槽位，允许机器在该位置继续生产新物品
                    break;
                }
            }

            return product;
        }
        return null;
    }
}