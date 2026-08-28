using UnityEngine;

/// <summary>
/// 兼容层：番茄树请改用 GridProductionMachine 并配置 outputTargets / spawnOrigin / visualRoot。
/// </summary>
[RequireComponent(typeof(GridProductionMachine))]
public class BreathingTomatoTree : MonoBehaviour
{
    [Header("模型骨骼/挂点引用（自动同步到 GridProductionMachine）")]
    public Transform treeVisual;
    public Transform rootPoint;
    public Transform[] growSockets;

    [Header("预制体配置")]
    public GameObject tomatoPrefab;

    [Header("生长与动画参数")]
    public float spawnInterval = 1.8f;

    private GridProductionMachine machine;

    private void Awake()
    {
        machine = GetComponent<GridProductionMachine>();
        SyncLegacyConfig();
    }

    private void SyncLegacyConfig()
    {
        if (machine.visualRoot == null) machine.visualRoot = treeVisual;
        if (machine.spawnOrigin == null) machine.spawnOrigin = rootPoint;
        if (!HasFixedTargets(machine) && growSockets != null && growSockets.Length > 0)
            machine.outputTargets = growSockets;
        if (machine.outputPrefab == null) machine.outputPrefab = tomatoPrefab;
        machine.productionCycleTime = spawnInterval;
        machine.enableBreathing = true;
        machine.requiresInput = false;
    }

    private static bool HasFixedTargets(GridProductionMachine m) =>
        m.outputTargets != null && m.outputTargets.Length > 0;

    public ItemData TryHarvestOneTomato() => machine.TryRemoveItem();
}
