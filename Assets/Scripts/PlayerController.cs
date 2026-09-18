using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;

/// <summary>
/// 玩家专属控制器，处理玩家的虚拟摇杆输入，以及与环境设施（货架、机器、垃圾桶、收银台）的智能交互
/// </summary>
public class PlayerController : BaseCharacterController
{
    [Header("玩家输入控制")]
    [Tooltip("拖入场景中挂载了 VirtualJoystick 脚本的摇杆背景 (joystickUI)")]
    public VirtualJoystick joystick;

    [Tooltip("摇杆移动死区，防止轻微误触导致角色原地抽搐")]
    public float joystickDeadzone = 0.1f;

    private Camera mainCamera;

    // 交互触发的冷却时间，拿物品时慢一点（0.15s），吸钱时快一点
    private float interactCooldown = 0.15f;
    private float lastInteractTime;

    private Dictionary<Transform, Vector3> originalScales = new Dictionary<Transform, Vector3>();

    protected override void Awake()
    {
        base.Awake();
        mainCamera = Camera.main;
    }

    private void Update()
    {
        HandleJoystickInput();
    }

    private void HandleJoystickInput()
    {
        Vector3 moveDirection = Vector3.zero;

        if (joystick != null && joystick.InputDirection.magnitude >= joystickDeadzone)
        {
            Vector3 rawDir = new Vector3(joystick.InputDirection.x, 0, joystick.InputDirection.y).normalized;

            if (mainCamera != null)
            {
                Vector3 camForward = Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up).normalized;
                Vector3 camRight = Vector3.ProjectOnPlane(mainCamera.transform.right, Vector3.up).normalized;
                moveDirection = (camRight * rawDir.x + camForward * rawDir.z).normalized;
            }
            else
            {
                moveDirection = rawDir;
            }
        }
        MoveAndRotate(moveDirection);
    }

    private void OnTriggerEnter(Collider other)
    {
        InteractZone zone = other.GetComponent<InteractZone>();
        if (zone != null && zone.floorVisual != null)
        {
            Transform target = zone.floorVisual;
            if (!originalScales.ContainsKey(target))
            {
                originalScales[target] = target.localScale;
            }
            target.DOKill();
            target.DOScale(originalScales[target] * 1.1f, 0.2f).SetEase(Ease.OutBack);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        InteractZone zone = other.GetComponent<InteractZone>();
        if (zone != null && zone.floorVisual != null)
        {
            Transform target = zone.floorVisual;
            if (originalScales.ContainsKey(target))
            {
                target.DOKill();
                target.DOScale(originalScales[target], 0.2f).SetEase(Ease.OutQuad);
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        // ==========================================
        // 场景 0：碰到了【收银台】吸收钞票
        // ==========================================
        CheckoutCounter checkout = other.GetComponentInParent<CheckoutCounter>();
        if (checkout != null)
        {
            if (other.name.Contains("Input"))
            {
                // 吸钱的速度保持极快，形成“流水线”效果
                if (Time.time - lastInteractTime < 0.03f) return;

                GameObject cashObj = checkout.TakeCashEntity();
                if (cashObj != null)
                {
                    lastInteractTime = Time.time;

                    int actualValue = 5;
                    CashData cd = cashObj.GetComponent<CashData>();
                    if (cd != null) actualValue = cd.value;

                    // 【核心修复】：脱离父节点，在世界空间独立飞行，杜绝乱转
                    cashObj.transform.SetParent(null);
                    cashObj.transform.DOKill();

                    float flyDuration = 0.22f; // 飞行时间适中，干脆利落

                    // 计算玩家胸口的实时坐标
                    Vector3 targetPos = this.transform.position + Vector3.up * 1.2f;

                    // 1. 抛物线飞向玩家，高度 1.2f
                    cashObj.transform.DOJump(targetPos, 1.2f, 1, flyDuration);

                    // 2. 飞行时把钞票角度强制拉平 (0,0,0)，保持美观平整
                    cashObj.transform.DORotate(Vector3.zero, flyDuration);

                    // 3. 微微缩小到 0.5 倍（不再缩到 0！保留实体的爽快感），到达后立刻销毁加钱
                    cashObj.transform.DOScale(Vector3.one * 0.5f, flyDuration).SetEase(Ease.OutQuad).OnComplete(() =>
                    {
                        if (GameManager.Instance != null) GameManager.Instance.AddCash(actualValue);
                        Destroy(cashObj);
                    });
                }
            }
            return;
        }

        // 下方的设施交互保持标准的 0.15s 冷却
        if (Time.time - lastInteractTime < interactCooldown) return;

        ShelfManager shelf = other.GetComponentInParent<ShelfManager>();
        if (shelf != null)
        {
            if (other.name.Contains("Input") && HasItems && !shelf.IsFull)
            {
                GameObject topItem = PeekTopItem();
                if (topItem != null)
                {
                    ItemData itemData = topItem.GetComponent<ItemData>();
                    if (itemData != null && shelf.TryAddProduct(itemData.itemType, topItem))
                    {
                        RemoveTopItem();
                        lastInteractTime = Time.time;
                    }
                }
            }
            return;
        }

        TrashBinManager trashBin = other.GetComponentInParent<TrashBinManager>();
        if (trashBin != null)
        {
            if (other.name.Contains("Input") && HasItems)
            {
                GameObject topItem = RemoveTopItem();
                if (topItem != null)
                {
                    trashBin.ReceiveTrash(topItem);
                    lastInteractTime = Time.time;
                }
            }
            return;
        }

        BaseProductionMachine baseMachine = other.GetComponentInParent<BaseProductionMachine>();
        if (baseMachine != null)
        {
            ConsumeProductionMachine consumeMachine = baseMachine as ConsumeProductionMachine;
            if (consumeMachine != null)
            {
                if (other.name.Contains("Input") && HasItems)
                {
                    GameObject topItem = PeekTopItem();
                    if (topItem != null)
                    {
                        ItemData itemData = topItem.GetComponent<ItemData>();
                        if (itemData != null && consumeMachine.TryReceiveInput(itemData.itemType, topItem))
                        {
                            RemoveTopItem();
                            lastInteractTime = Time.time;
                        }
                    }
                }
                else if (other.name.Contains("Out") && !IsFull)
                {
                    GameObject product = consumeMachine.CollectProduct();
                    if (product != null)
                    {
                        AddItem(product);
                        lastInteractTime = Time.time;
                    }
                }
            }
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