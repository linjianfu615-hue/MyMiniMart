using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// 角色控制器基类，包含移动逻辑、动画状态机和物品搬运堆叠功能。
/// 可被玩家 (PlayerController) 和 AI (WorkerAIController) 共用。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public abstract class BaseCharacterController : MonoBehaviour
{
    [Header("移动组件")]
    public CharacterController characterController;
    public Animation animComponent;
    public float moveSpeed = 6f;

    [Tooltip("角色转身的速度，数值越大转身越干脆，适合街机手感")]
    public float rotateSpeed = 30f;

    [Header("堆叠位置 (Carry Settings)")]
    [Tooltip("角色用来堆叠拿取物品的根节点（如双手之间或头顶）")]
    public Transform carrySocket;

    [Tooltip("每个物品叠加时向上的 Y 轴偏移量")]
    public float itemHeightOffset = 0.45f;

    [Tooltip("角色最大能搬运的物品数量")]
    public int maxCarryCapacity = 5;

    [Header("动画配置 (Legacy Animation)")]
    public string animIdle = "Idle_Happy";
    public string animRun = "Run";
    public string animIdleCarry = "Idle_Happy_Carry";
    public string animRunCarry = "Run_Carry";

    // 角色当前端着的物品列表
    protected List<GameObject> carriedItems = new List<GameObject>();
    private string currentPlayingAnim = "";

    // 状态判定属性
    public bool HasItems => carriedItems.Count > 0;
    public bool IsFull => carriedItems.Count >= maxCarryCapacity;
    public int CarriedCount => carriedItems.Count;

    protected virtual void Awake()
    {
        if (characterController == null) characterController = GetComponent<CharacterController>();
        if (animComponent == null) animComponent = GetComponentInChildren<Animation>();
    }

    protected virtual void Start()
    {
        // 初始化时重置为静止动画
        UpdateAnimationState(Vector3.zero);
    }

    /// <summary>
    /// 统一驱动物理移动与角色朝向
    /// </summary>
    public void MoveAndRotate(Vector3 moveDir)
    {
        if (moveDir.sqrMagnitude > 0.001f)
        {
            // 计算水平移动向量，并施加向下的重力以贴合地面
            Vector3 motion = moveDir.normalized * (moveSpeed * Time.deltaTime);
            motion.y = -9.81f * Time.deltaTime;
            characterController.Move(motion);

            // 让角色平滑转向输入的移动方向
            Quaternion targetRot = Quaternion.LookRotation(new Vector3(moveDir.x, 0, moveDir.z));
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotateSpeed * Time.deltaTime);
        }
        else
        {
            // 没有输入时，仅施加重力防止角色悬空
            characterController.Move(new Vector3(0, -9.81f * Time.deltaTime, 0));
        }

        // 根据移动状态更新动画
        UpdateAnimationState(moveDir);
    }

    /// <summary>
    /// 处理四大基础动画的自动切换
    /// </summary>
    private void UpdateAnimationState(Vector3 moveDir)
    {
        if (animComponent == null) return;

        bool isMoving = moveDir.sqrMagnitude > 0.001f;
        string targetAnim;

        // 根据是否有物品、是否在移动，决定播放哪个动画
        if (HasItems)
        {
            targetAnim = isMoving ? animRunCarry : animIdleCarry;
        }
        else
        {
            targetAnim = isMoving ? animRun : animIdle;
        }

        // 避免每帧重复调用 Play 导致动画卡死，只有状态改变时才应用
        if (currentPlayingAnim != targetAnim)
        {
            currentPlayingAnim = targetAnim;
            animComponent.CrossFade(targetAnim, 0.15f); // 使用 CrossFade 实现 0.15 秒的平滑过渡
        }
    }

    /// <summary>
    /// 将从机器获取的物品添加到角色的手上
    /// </summary>
    public virtual bool AddItem(GameObject item)
    {
        if (IsFull) return false;

        carriedItems.Add(item);

        Transform parentTransform = carrySocket != null ? carrySocket : transform;
        // 将物品的父节点设为角色的携带点，参数 true 保持其原有的世界坐标系位置不突变
        item.transform.SetParent(parentTransform, true);
        item.transform.DOKill(); // 中断该物品身上残留的其他动画

        // 防抖处理：恢复物品的缩放，防止受上一任父节点(如树枝)的 Scale 影响
        // item.transform.DOScale(Vector3.one, 0.2f);

        // 计算物品在角色手上的最终堆叠高度 (Y轴累加)
        Vector3 targetLocalPos = new Vector3(0, (carriedItems.Count - 1) * itemHeightOffset, 0);

        // 【防飞天修复】跳跃高度参数设为极小的 0.1f。
        // 这能让树上的西红柿以微小的平滑弧度落到手中，而不会往上冲向天空。
        item.transform.DOLocalJump(targetLocalPos, 0.01f, 1, 0.25f);
        item.transform.DOLocalRotate(Vector3.zero, 0.25f); // 顺便让物品旋转归正

        UpdateAnimationState(Vector3.zero);
        return true;
    }

    /// <summary>
    /// 交付物品时，移除并返回堆叠在最上层的那一个
    /// </summary>
    public virtual GameObject RemoveTopItem()
    {
        if (!HasItems) return null;

        int lastIndex = carriedItems.Count - 1;
        GameObject item = carriedItems[lastIndex];
        carriedItems.RemoveAt(lastIndex);

        UpdateAnimationState(Vector3.zero);
        return item;
    }

    /// <summary>
    /// 查看最上层的物品 (用于比对类型，不实际移除)
    /// </summary>
    public GameObject PeekTopItem()
    {
        return HasItems ? carriedItems[carriedItems.Count - 1] : null;
    }
}