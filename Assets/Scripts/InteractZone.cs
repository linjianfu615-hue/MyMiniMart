using UnityEngine;

/// <summary>
/// 交互区视觉标识，挂载在各个 InputCollider 或 OutCollider 上
/// </summary>
public class InteractZone : MonoBehaviour
{
    [Tooltip("需要进行 Q弹缩放动画 的视觉地板模型（如 Mat_Input_Gray）。\n如果不填（留空），则踩上去没有任何动画。")]
    public Transform floorVisual;

    // 缓存父节点上的货架管理器
    private ShelfManager shelfManager;

    void Awake()
    {
        shelfManager = GetComponentInParent<ShelfManager>();
    }

    // 当任何带有碰撞体的物体进入此区域时 (要求玩家/AI身上带有 Rigidbody)
    private void OnTriggerEnter(Collider other)
    {
        // 只要它是角色（拥有 BaseCharacterController 基类，无论是玩家、顾客还是员工）
        if (other.GetComponent<BaseCharacterController>() != null)
        {
            if (shelfManager != null)
            {
                shelfManager.OnEntityEnter();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<BaseCharacterController>() != null)
        {
            if (shelfManager != null)
            {
                shelfManager.OnEntityExit();
            }
        }
    }
}