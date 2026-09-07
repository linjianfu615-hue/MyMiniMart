using UnityEngine;

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

    /// <summary>
    /// 触发器区域智能交互：站在输出区拿货，站在输入区送货。
    /// 需要各机器周边配置带 Is Trigger 的 Collider。
    /// </summary>
    private void OnTriggerStay(Collider other)
    {
        // 冷却时间控制
        if (Time.time - lastInteractTime < interactCooldown) return;

        // 尝试获取机器基类组件
        BaseProductionMachine baseMachine = other.GetComponentInParent<BaseProductionMachine>();
        if (baseMachine == null) return;

        // 尝试将其转换为“消耗型机器”（如果转换失败，说明它是西红柿树这样的全自动机器）
        ConsumeProductionMachine consumeMachine = baseMachine as ConsumeProductionMachine;

        // ==========================================
        // 场景 1：如果碰到的机器是【消耗型机器】（如鸡舍、磨粉机）
        // ==========================================
        if (consumeMachine != null)
        {
            // 获取触发碰撞体的名字，用来严格区分玩家到底站在输入区还是输出区
            string colliderName = other.name;

            Debug.Log("colliderName:" + colliderName);
            // 1. 玩家站在【输入区】(如命名为 InputCollider)
            if (colliderName.Contains("Input") && HasItems)
            {
                GameObject topItem = PeekTopItem();
                if (topItem != null)
                {
                    ItemType currentItemType = consumeMachine.requiredInputType;

                    // 尝试交付物品，如果机器收下了，玩家再移除手里的物品
                    if (consumeMachine.TryReceiveInput(currentItemType, topItem))
                    {
                        RemoveTopItem();
                        lastInteractTime = Time.time;
                    }
                }
            }
            // 2. 玩家站在【输出区】(如命名为 OutCollider)
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
        // ==========================================
        // 场景 2：如果碰到的机器是【自动生产机器】（如西红柿树、草地）
        // ==========================================
        else
        {
            // 自动机器只有输出功能，碰到了就直接尝试收集
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