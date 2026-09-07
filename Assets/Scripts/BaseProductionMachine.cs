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

    // 【新增】所有产品统一的初始生成点（比如：树的中心、草地的中心点）
    [Tooltip("产品刚生成时的初始位置（如树干中心）")]
    public Transform rootSpawnPoint;

    [Tooltip("按顺序配置每个产品的生成位置（如：树上的4个不同树枝空物体，或地上的9个网格空物体）")]
    public Transform[] spawnPoints;

    [Tooltip("存放产出物的父节点(方便层级管理，可为空)")]
    public Transform productContainer;

    [Tooltip("生成时的弹跳动画时长")]
    public float spawnAnimDuration = 0.5f;

    /// <summary>
    /// 最大堆叠/生产容量（直接由配置的生成点数量决定）
    /// </summary>
    public int MaxCapacity => spawnPoints != null ? spawnPoints.Length : 0;

    // 当前已产出且未被收集的产品列表
    protected List<GameObject> readyProducts = new List<GameObject>();

    /// <summary>
    /// 核心生成逻辑，供子类在满足各自条件时（时间到了/吃饱了）调用
    /// </summary>
    protected virtual void GenerateProduct()
    {
        // 防错：如果没有配置生成点，或者容量已满，则停止生成
        if (spawnPoints == null || spawnPoints.Length == 0) return;
        if (readyProducts.Count >= MaxCapacity) return;

        //获取当前对应的生成点 (例如列表里有0个产品，就用第0个位置生成)
        Transform targetSpawnPoint = spawnPoints[readyProducts.Count];

        // 【修改1】在根节点（rootSpawnPoint）实例化，而不是直接在目标点实例化
        Vector3 startPos = rootSpawnPoint != null ? rootSpawnPoint.position : transform.position;

        //实例化目标产品
        GameObject newProduct = Instantiate(targetProductPrefab, startPos, Quaternion.identity);

        //设置父节点存放
        if (productContainer != null)
        {
            newProduct.transform.SetParent(productContainer);
        }
        else
        {
            newProduct.transform.SetParent(targetSpawnPoint);
        }

        // 加入成品列表
        readyProducts.Add(newProduct);

        //设置旋转角度（0，0，0）
        newProduct.transform.localEulerAngles = new Vector3(0, 0, 0);

        // 【修改2】将目标点的位置传递给动画函数
        PlaySpawnAnimation(newProduct, targetSpawnPoint.position);

        ArrangeProducts();
    }

    /// <summary>
    /// 通用的生成动画逻辑
    /// </summary>
    protected virtual void PlaySpawnAnimation(GameObject obj, Vector3 targetPos)
    {
        // 初始大小设为0
        obj.transform.localScale = Vector3.zero;

        // 【修改3】弹性放大
        obj.transform.DOScale(Vector3.one, spawnAnimDuration).SetEase(Ease.OutBack);

        // 【修改4】删除了 DOLocalJump，改为从根节点平滑移动 (DOMove) 到目标点
        // 使用 OutBack 或 OutQuad 曲线，会有飞过去并微微弹一下的感觉
        obj.transform.DOMove(targetPos, spawnAnimDuration).SetEase(Ease.OutBack);
    }

    /// <summary>
    /// 物品排列逻辑（预留给子类重写）
    /// 对于树（西红柿）和草（小麦）这种已经在各自 SpawnPoint 绝对位置生成的，不需要特殊处理。
    /// 对于机器产出（同一点生成，再排成矩阵阵列的），可以在子类中 override 覆盖此方法。
    /// </summary>
    protected virtual void ArrangeProducts()
    {
        // 基类留空
    }

    /// <summary>
    /// 供玩家或搬运工 (Worker) 收集产品的外部接口
    /// </summary>
    /// <returns>返回被收集的物品 GameObject，如果没有则返回 null</returns>
    public virtual GameObject CollectProduct()
    {
        if (readyProducts.Count > 0)
        {
            // 取出最后生成的一个产品
            int lastIndex = readyProducts.Count - 1;
            GameObject product = readyProducts[lastIndex];

            // 从当前机器的管理列表中移除
            readyProducts.RemoveAt(lastIndex);

            // 返回给玩家，玩家脚本接管该物体的移动（如飞向玩家背包）
            return product;
        }

        return null; // 没有产品可收集
    }
}