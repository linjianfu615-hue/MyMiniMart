using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;

/// <summary>
/// 玩家专属控制器，处理玩家的虚拟摇杆输入，以及与环境设施（货架、机器、垃圾桶）的智能交互
/// </summary>
public class PlayerController : BaseCharacterController
{
    [Header("玩家输入控制")]
    [Tooltip("拖入场景中挂载了 VirtualJoystick 脚本的摇杆背景 (joystickUI)")]
    public VirtualJoystick joystick;

    [Tooltip("摇杆移动死区，输入向量的绝对值小于此时不移动，防止轻微误触导致角色原地抽搐")]
    public float joystickDeadzone = 0.1f;

    // 缓存主相机引用，用于将 2D 的屏幕摇杆输入转换为 3D 的等轴俯视角移动
    private Camera mainCamera;

    // 交互触发的冷却时间，防止 1 帧内把所有货物瞬间丢光或收光
    private float interactCooldown = 0.15f;
    private float lastInteractTime;

    // 用一个字典自动记录每个地面交互区碰撞体原本的初始缩放比例，防止反复放大导致变形
    private Dictionary<Transform, Vector3> originalScales = new Dictionary<Transform, Vector3>();

    protected override void Awake()
    {
        base.Awake();
        mainCamera = Camera.main; // 缓存主相机，避免 Update 中频繁调用 Camera.main 带来性能损耗
    }

    private void Update()
    {
        HandleJoystickInput();
    }

    /// <summary>
    /// 处理来自虚拟摇杆的输入，并转化为 3D 世界坐标下的移动指令
    /// </summary>
    private void HandleJoystickInput()
    {
        Vector3 moveDirection = Vector3.zero;

        // 确保摇杆已赋值，并且玩家推动摇杆的力度超过了设定的死区阈值
        if (joystick != null && joystick.InputDirection.magnitude >= joystickDeadzone)
        {
            // 获取摇杆的 2D 滑动方向，并映射为游戏里的 X/Z 3D 水平面方向
            Vector3 rawDir = new Vector3(joystick.InputDirection.x, 0, joystick.InputDirection.y).normalized;

            // 配合斜 45 度等轴俯视角（Isometric）校准移动向量
            // 使得玩家往屏幕上方推摇杆时，角色会向着相机的“前上方”走，而不是世界坐标的绝对北边
            if (mainCamera != null)
            {
                // 获取相机在水平面上的投影方向
                Vector3 camForward = Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up).normalized;
                Vector3 camRight = Vector3.ProjectOnPlane(mainCamera.transform.right, Vector3.up).normalized;

                // 根据相机的朝向，合成角色最终的移动方向
                moveDirection = (camRight * rawDir.x + camForward * rawDir.z).normalized;
            }
            else
            {
                moveDirection = rawDir;
            }
        }

        // 调用基类 BaseCharacterController 执行物理移动与动画切换
        MoveAndRotate(moveDirection);
    }

    // ==========================================
    // 视觉反馈：玩家靠近交互区时，机器/货架底部的指示板微微放大
    // ==========================================

    private void OnTriggerEnter(Collider other)
    {
        // 尝试获取碰撞体身上的“交互区标识”组件
        InteractZone zone = other.GetComponent<InteractZone>();

        // 如果有这个标识，且配置了视觉底板
        if (zone != null && zone.floorVisual != null)
        {
            Transform target = zone.floorVisual;

            // 第一次碰到时，记录这个底板真正的 Scale，作为后续恢复的基准
            if (!originalScales.ContainsKey(target))
            {
                originalScales[target] = target.localScale;
            }

            target.DOKill(); // 清除之前可能未完成的缩放动画
            // 在它原有比例的基础上，放大 1.1 倍，使用 OutBack 曲线产生 Q 弹的效果
            target.DOScale(originalScales[target] * 1.1f, 0.2f).SetEase(Ease.OutBack);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        InteractZone zone = other.GetComponent<InteractZone>();

        if (zone != null && zone.floorVisual != null)
        {
            Transform target = zone.floorVisual;

            // 离开交互区时，精确恢复到最初记录的原始大小
            if (originalScales.ContainsKey(target))
            {
                target.DOKill();
                target.DOScale(originalScales[target], 0.2f).SetEase(Ease.OutQuad);
            }
        }
    }

    // ==========================================
    // 核心交互逻辑：站在不同区域执行进货/出货
    // ==========================================

    private void OnTriggerStay(Collider other)
    {
        // 冷却时间控制：玩家站着不动时，按 interactCooldown 的频率连续交互
        if (Time.time - lastInteractTime < interactCooldown) return;

        // ------------------------------------------
        // 场景 1：如果碰到的物体是【货架】
        // ------------------------------------------
        ShelfManager shelf = other.GetComponentInParent<ShelfManager>();
        if (shelf != null)
        {
            string colliderName = other.name;
            // 玩家站在货架的碰撞体内，且手里有东西，且货架还没放满
            if (colliderName.Contains("Input") && HasItems && !shelf.IsFull)
            {
                // 窥探手里最上面的一件货物
                GameObject topItem = PeekTopItem();
                if (topItem != null)
                {
                    ItemData itemData = topItem.GetComponent<ItemData>();
                    if (itemData != null)
                    {
                        // 尝试把手里的东西放上货架（内部会校验类型是否匹配）
                        if (shelf.TryAddProduct(itemData.itemType, topItem))
                        {
                            RemoveTopItem(); // 放置成功，从手里移除
                            lastInteractTime = Time.time;
                        }
                    }
                }
            }
            // 碰到了货架执行完逻辑后，直接 return 结束本次触发，不用再往下找机器了
            return;
        }

        // ------------------------------------------
        // 场景 2：如果碰到的物体是【垃圾桶】
        // ------------------------------------------
        TrashBinManager trashBin = other.GetComponentInParent<TrashBinManager>();
        if (trashBin != null)
        {
            string colliderName = other.name;

            // 站在垃圾桶区，且手里有东西
            if (colliderName.Contains("Input") && HasItems)
            {
                // 垃圾桶什么都吃，不需要核对 ItemType，直接拿走最上面的物品
                GameObject topItem = RemoveTopItem();
                if (topItem != null)
                {
                    trashBin.ReceiveTrash(topItem); // 播放丢弃动画并销毁
                    lastInteractTime = Time.time;
                }
            }
            return;
        }

        // ------------------------------------------
        // 场景 3 & 4：如果碰到的物体是【生产设施】
        // ------------------------------------------
        BaseProductionMachine baseMachine = other.GetComponentInParent<BaseProductionMachine>();
        if (baseMachine != null)
        {
            // 尝试转换为带有多原料配方的“消耗型加工机”
            ConsumeProductionMachine consumeMachine = baseMachine as ConsumeProductionMachine;

            // 场景 3：碰到了【消耗型加工机】（如面包机、面粉机）
            if (consumeMachine != null)
            {
                string colliderName = other.name;

                // 站在【输入区 Input】且手里有货 -> 尝试投喂原料
                if (colliderName.Contains("Input") && HasItems)
                {
                    GameObject topItem = PeekTopItem();
                    if (topItem != null)
                    {
                        ItemData itemData = topItem.GetComponent<ItemData>();
                        if (itemData != null)
                        {
                            ItemType currentItemType = itemData.itemType;
                            // 尝试把原料喂给机器（内部会核对配方及槽位是否已满）
                            if (consumeMachine.TryReceiveInput(currentItemType, topItem))
                            {
                                RemoveTopItem();
                                lastInteractTime = Time.time;
                            }
                        }
                    }
                }
                // 站在【输出区 Out】且手里没满 -> 尝试收集成品
                else if (colliderName.Contains("Out") && !IsFull)
                {
                    GameObject product = consumeMachine.CollectProduct();
                    if (product != null)
                    {
                        AddItem(product);
                        lastInteractTime = Time.time;
                    }
                }
            }
            // 场景 4：碰到了【自动生产设施】（如西红柿树、小麦田、鸡舍）
            else
            {
                // 自动生产设施只有输出功能，手里只要没满就一直拿
                if (!IsFull)
                {
                    GameObject product = baseMachine.CollectProduct();
                    if (product != null)
                    {
                        AddItem(product);
                        lastInteractTime = Time.time;
                    }
                }
            }
        }
    }
}