using UnityEngine;
using DG.Tweening;

/// <summary>
/// 玩家专属控制器，处理玩家的触摸输入以及与环境触发器的智能交互
/// </summary>
public class PlayerController : BaseCharacterController
{
    [Header("玩家输入控制")]
    [Tooltip("虚拟触摸的死区，滑动距离小于此时不移动，防止手指轻微颤抖导致角色原地抽搐")]
    public float minDragThreshold = 10f;

    [Tooltip("虚拟摇杆动态跟随半径。滑出该范围后，摇杆锚点会自动跟随手指，彻底解决折返时的操作延迟感")]
    public float joystickMaxRadius = 100f;

    private Vector2 touchStartPos;
    private bool isDragging = false;
    private Camera mainCamera;

    // 交互触发的冷却时间，防止 1 帧内把所有货物瞬间丢光或收光
    private float interactCooldown = 0.15f;
    private float lastInteractTime;

    protected override void Awake()
    {
        base.Awake();
        mainCamera = Camera.main; // 缓存主相机引用
    }

    private void Update()
    {
        HandleTouchInput();
    }

    /// <summary>
    /// 处理移动端触摸与 PC 端鼠标拖拽
    /// </summary>
    private void HandleTouchInput()
    {
        Vector3 moveDirection = Vector3.zero;

        if (Input.GetMouseButtonDown(0))
        {
            // 按下屏幕时，记录初始锚点
            isDragging = true;
            touchStartPos = Input.mousePosition;
        }
        else if (Input.GetMouseButton(0) && isDragging)
        {
            Vector2 currentPos = Input.mousePosition;
            Vector2 delta = currentPos - touchStartPos;

            // 【动态摇杆跟随机制】
            // 如果手指拉动的距离超过了设定的最大半径，就把初始锚点硬拽过来。
            // 这样能确保手指往回滑时能立刻越过中心点，实现角色顺滑的瞬间转身，指哪打哪。
            if (delta.magnitude > joystickMaxRadius)
            {
                touchStartPos = currentPos - delta.normalized * joystickMaxRadius;
                delta = delta.normalized * joystickMaxRadius;
            }

            // 只有滑动距离大于死区，才视为有效移动
            if (delta.magnitude >= minDragThreshold)
            {
                // 获取屏幕上的 2D 滑动方向，映射为游戏里的 X/Z 3D 方向
                Vector3 rawDir = new Vector3(delta.x, 0, delta.y).normalized;

                // 配合斜 45 度等轴俯视角（Isometric）校准移动向量，让操作方向与相机视线完全一致
                if (mainCamera != null)
                {
                    Vector3 camForward = Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up).normalized;
                    Vector3 camRight = Vector3.ProjectOnPlane(mainCamera.transform.right, Vector3.up).normalized;

                    // 合成最终移动方向
                    moveDirection = (camRight * rawDir.x + camForward * rawDir.z).normalized;
                }
                else
                {
                    moveDirection = rawDir;
                }
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            // 松开手指，停止移动
            isDragging = false;
            moveDirection = Vector3.zero;
        }

        // 调用基类执行物理移动与动画切换
        MoveAndRotate(moveDirection);
    }

    // ==========================================
    // 视觉反馈：玩家靠近时放大机器/货架
    // ==========================================
    // 用一个字典自动记录每个地面碰撞体原本的初始缩放比例，防止变形
    private System.Collections.Generic.Dictionary<Transform, Vector3> originalScales = new System.Collections.Generic.Dictionary<Transform, Vector3>();
    private void OnTriggerEnter(Collider other)
    {
        // 如果碰到的属于交互区域
        if (IsInteractableZone(other))
        {
            Transform target = other.transform; // 只获取当前碰到的地面物体（不是整个货架）

            // 第一次碰到时，记录这个地面真正的 Scale
            if (!originalScales.ContainsKey(target))
            {
                originalScales[target] = target.localScale;
            }

            target.DOKill();
            // 在它原有比例的基础上，放大 1.1 倍
            target.DOScale(originalScales[target] * 1.1f, 0.2f).SetEase(Ease.OutBack);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsInteractableZone(other))
        {
            Transform target = other.transform;

            if (originalScales.ContainsKey(target))
            {
                target.DOKill();
                // 离开时，精确恢复到最初记录的大小
                target.DOScale(originalScales[target], 0.2f).SetEase(Ease.OutQuad);
            }
        }
    }

    /// <summary>
    /// 判断碰到的碰撞体是否属于可交互设施（货架或机器）
    /// </summary>
    private bool IsInteractableZone(Collider other)
    {
        // 只要它的父级或自身挂载了这两个管理器之一，就说明它是机器/货架的功能区
        return other.GetComponentInParent<ShelfManager>() != null ||
               other.GetComponentInParent<BaseProductionMachine>() != null;
    }

    /// <summary>
    /// 统一获取可交互物体（货架或机器）的根节点 Transform
    /// </summary>
    private Transform GetInteractableRoot(Collider other)
    {
        // 检查碰到的碰撞体父级是不是货架
        ShelfManager shelf = other.GetComponentInParent<ShelfManager>();
        if (shelf != null) return shelf.transform;

        // 检查碰到的碰撞体父级是不是生产/消耗机器
        BaseProductionMachine machine = other.GetComponentInParent<BaseProductionMachine>();
        if (machine != null) return machine.transform;

        return null; // 都不是则返回 null，不触发缩放
    }

    /// <summary>
    /// 触发器区域智能交互：站在输出区拿货，站在输入区送货。
    /// </summary>
    private void OnTriggerStay(Collider other)
    {
        // 冷却时间控制
        if (Time.time - lastInteractTime < interactCooldown) return;

        // ==========================================
        // 场景 3：优先判断如果碰到的物体是【货架】
        // ==========================================
        ShelfManager shelf = other.GetComponentInParent<ShelfManager>();
        if (shelf != null)
        {
            string colliderName = other.name;
            // 玩家站在货架的碰撞体（如 InputCollider）内，且手里有东西，且货架没满
            if (colliderName.Contains("Input") && HasItems && !shelf.IsFull)
            {
                // 看一眼手里最上面是什么
                GameObject topItem = PeekTopItem();
                if (topItem != null)
                {
                    ItemData itemData = topItem.GetComponent<ItemData>();
                    if (itemData != null)
                    {
                        // 尝试把手里的东西放上货架
                        if (shelf.TryAddProduct(itemData.itemType, topItem))
                        {
                            RemoveTopItem();
                            lastInteractTime = Time.time;
                        }
                    }
                }
            }
            // 碰到了货架执行完逻辑后，直接结束本次触发，不用再往下找机器了
            return;
        }

        // ==========================================
        // 场景 1 & 2：如果碰到的不是货架，尝试获取【机器基类组件】
        // ==========================================
        BaseProductionMachine baseMachine = other.GetComponentInParent<BaseProductionMachine>();
        if (baseMachine != null)
        {
            ConsumeProductionMachine consumeMachine = baseMachine as ConsumeProductionMachine;

            // 场景 1：碰到了【消耗型机器】（如鸡舍）
            if (consumeMachine != null)
            {
                string colliderName = other.name;

                // 站在【输入区】
                if (colliderName.Contains("Input") && HasItems)
                {
                    GameObject topItem = PeekTopItem();
                    if (topItem != null)
                    {
                        ItemData itemData = topItem.GetComponent<ItemData>();
                        if (itemData != null)
                        {
                            ItemType currentItemType = itemData.itemType;
                            if (consumeMachine.TryReceiveInput(currentItemType, topItem))
                            {
                                RemoveTopItem();
                                lastInteractTime = Time.time;
                            }
                        }
                    }
                }
                // 站在【输出区】
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
            // 场景 2：碰到了【自动生产机器】（如西红柿树）
            else
            {
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