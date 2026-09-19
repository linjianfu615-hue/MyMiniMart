using UnityEngine;

/// <summary>
/// 挂载在 BlockUpgrade 的子物体 InputCollider 上，
/// 负责将 OnTrigger 事件转发给父级真正的处理中心 BlockUpgrade。
/// </summary>
[RequireComponent(typeof(Collider))]
public class UpgradeColliderRelay : MonoBehaviour
{
    private BlockUpgrade parentUpgrade;

    private void Awake()
    {
        // 向上寻找父节点挂载的核心控制脚本
        parentUpgrade = GetComponentInParent<BlockUpgrade>();
        if (parentUpgrade == null)
        {
            Debug.LogWarning("UpgradeColliderRelay 没有在父节点找到 BlockUpgrade 脚本！");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (parentUpgrade != null)
        {
            parentUpgrade.RelayTriggerEnter(other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (parentUpgrade != null)
        {
            parentUpgrade.RelayTriggerExit(other);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (parentUpgrade != null)
        {
            parentUpgrade.RelayTriggerStay(other);
        }
    }
}