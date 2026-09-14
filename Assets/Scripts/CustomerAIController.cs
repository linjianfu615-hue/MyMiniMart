using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;
using System.Collections.Generic;
using DG.Tweening;

[System.Serializable]
public class ShoppingRequest
{
    public ItemType itemType;
    public Sprite itemIcon;
    public int targetAmount;
    [HideInInspector] public int collectedAmount = 0;
}

[RequireComponent(typeof(NavMeshAgent), typeof(CharacterController))]
public class CustomerAIController : MonoBehaviour
{
    [Header("属性配置")]
    public float moveSpeed = 3.5f;
    public float handCarryStoppingDistance = 5f;
    public float trolleyStoppingDistance = 8f;

    [Header("收银台排队设置")]
    public Sprite checkoutIcon;
    [Tooltip("排队时向右侧间隔的距离")]
    public float queueSpacing = 3.0f;
    public float payTime = 2.0f;

    // ==========================================
    // 全局静态数据
    // ==========================================
    private static List<CustomerAIController> checkoutQueue = new List<CustomerAIController>();
    public static List<CustomerAIController> allActiveCustomers = new List<CustomerAIController>();
    private static Transform globalPaymentPoint;

    [Header("购物清单配置")]
    public List<ShoppingRequest> shoppingList = new List<ShoppingRequest>();

    [Header("UI 气泡组件")]
    public GameObject thoughtBubble;
    public Image iconImage;
    public Text numText;

    [Header("搬运节点配置")]
    public Transform carrySocket;
    public GameObject trolley;
    public Transform[] trolleySlots;
    public float itemHeightOffset = 0.3f;

    [Header("动画配置")]
    public Animation animComponent;
    [Range(0.1f, 3f)]
    public float animSpeedMultiplier = 1.0f;

    public string animIdle = "Idle_Happy";
    public string animRun = "Run";
    public string animIdleCarry = "Idle_Happy_Carry";
    public string animRunCarry = "Run_Carry";
    public string animRunTrolley = "Run_Net";

    [Header("防拥挤设置")]
    public float interactRadius = 1.5f;

    private NavMeshAgent agent;
    private enum CustomerState { Entering, FindingItem, MovingToShelf, Collecting, GoingToCheckout, Paying, Leaving }
    private CustomerState currentState = CustomerState.Entering;

    private int currentRequestIndex = 0;
    private ShelfManager targetShelf;
    private ShelfManager currentOpenShelf;

    private bool useTrolley = false;
    private int totalCollectedItems = 0;
    private float interactTimer = 0f;
    private float interactDelay = 0.3f;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = false;
        agent.speed = moveSpeed;
    }

    private void Start()
    {
        if (!allActiveCustomers.Contains(this)) allActiveCustomers.Add(this);
        InitCustomer();
    }

    public void SetMoveSpeed(float newSpeed)
    {
        moveSpeed = newSpeed;
        if (agent != null) agent.speed = moveSpeed;
    }

    public void InitCustomer()
    {
        useTrolley = shoppingList.Count >= 3;

        if (useTrolley)
        {
            carrySocket.gameObject.SetActive(false);
            trolley.SetActive(true);
            if (agent != null) agent.stoppingDistance = trolleyStoppingDistance;
        }
        else
        {
            carrySocket.gameObject.SetActive(true);
            trolley.SetActive(false);
            if (agent != null) agent.stoppingDistance = handCarryStoppingDistance;
        }

        currentState = CustomerState.FindingItem;
        UpdateThoughtUI();
    }

    private void Update()
    {
        HandleMovementAndAnimation();

        switch (currentState)
        {
            case CustomerState.FindingItem:
                FindTargetShelf();
                break;
            case CustomerState.MovingToShelf:
                if (HasReachedTarget())
                {
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;

                    currentOpenShelf = targetShelf;
                    if (currentOpenShelf != null) currentOpenShelf.OnEntityEnter();

                    currentState = CustomerState.Collecting;
                }
                break;
            case CustomerState.Collecting:
                if (targetShelf != null)
                {
                    Vector3 lookDir = targetShelf.transform.position - transform.position;
                    lookDir.y = 0;
                    if (lookDir.sqrMagnitude > 0.01f)
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 8f);
                }
                CollectItemFromShelf();
                break;
            case CustomerState.GoingToCheckout:
                HandleQueueing();
                break;
            case CustomerState.Paying:
                HandlePaying();
                break;
            case CustomerState.Leaving:
                HandleLeaving();
                break;
        }
    }

    private void FindTargetShelf()
    {
        if (currentRequestIndex >= shoppingList.Count)
        {
            if (checkoutIcon != null)
            {
                iconImage.sprite = checkoutIcon;
                numText.text = "";
            }

            agent.stoppingDistance = 0.5f;
            currentState = CustomerState.GoingToCheckout;
            return;
        }

        ShoppingRequest currentReq = shoppingList[currentRequestIndex];

        if (currentReq.collectedAmount >= currentReq.targetAmount)
        {
            currentRequestIndex++;
            UpdateThoughtUI();
            return;
        }

        foreach (var shelf in FacilityManager.Instance.allShelves)
        {
            if (shelf.acceptedItemType == currentReq.itemType)
            {
                targetShelf = shelf;
                Vector3 targetPos = GetCustomerStandPosition(targetShelf);
                Vector2 rand = Random.insideUnitCircle * 0.2f;
                targetPos += new Vector3(rand.x, 0, rand.y);

                agent.isStopped = false;
                agent.SetDestination(targetPos);
                currentState = CustomerState.MovingToShelf;
                return;
            }
        }

        agent.isStopped = true;
        agent.velocity = Vector3.zero;
    }

    private void HandleQueueing()
    {
        if (globalPaymentPoint == null)
        {
            GameObject pt = GameObject.Find("payment_Point");
            if (pt != null) globalPaymentPoint = pt.transform;
            else return;
        }

        int targetIndex = 0;

        if (checkoutQueue.Contains(this))
        {
            targetIndex = checkoutQueue.IndexOf(this);
        }
        else
        {
            int officialCount = checkoutQueue.Count;
            List<CustomerAIController> walkers = new List<CustomerAIController>();

            foreach (var c in allActiveCustomers)
            {
                if (c != null && c.currentState == CustomerState.GoingToCheckout && !checkoutQueue.Contains(c))
                {
                    walkers.Add(c);
                }
            }

            walkers.Sort((a, b) =>
            {
                float distA = Vector3.Distance(a.transform.position, globalPaymentPoint.position);
                float distB = Vector3.Distance(b.transform.position, globalPaymentPoint.position);
                return distA.CompareTo(distB);
            });

            targetIndex = officialCount + walkers.IndexOf(this);
        }

        // 【修改点】：恢复为你测试成功的局部 -right 方向！
        Vector3 targetPos = globalPaymentPoint.position - globalPaymentPoint.right * (targetIndex * queueSpacing);

        Vector3 pos2D = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 target2D = new Vector3(targetPos.x, 0, targetPos.z);
        float distToTarget = Vector3.Distance(pos2D, target2D);

        if (distToTarget <= agent.stoppingDistance + 0.2f)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;

            Vector3 lookDir;
            if (targetIndex == 0)
            {
                lookDir = globalPaymentPoint.forward; // 第一名看向收银员
            }
            else
            {
                lookDir = globalPaymentPoint.position - transform.position; // 后排看往前排
            }

            lookDir.y = 0;
            if (lookDir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 10f);
            }

            if (!checkoutQueue.Contains(this))
            {
                checkoutQueue.Add(this);
            }

            if (checkoutQueue.IndexOf(this) == 0)
            {
                interactTimer = 0f;
                currentState = CustomerState.Paying;
            }
        }
        else
        {
            agent.isStopped = false;
            agent.SetDestination(targetPos);
        }
    }

    private void HandlePaying()
    {
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(globalPaymentPoint.forward), Time.deltaTime * 10f);

        interactTimer += Time.deltaTime;

        if (interactTimer >= payTime)
        {
            if (checkoutQueue.Contains(this)) checkoutQueue.Remove(this);
            thoughtBubble.SetActive(false);

            agent.stoppingDistance = useTrolley ? trolleyStoppingDistance : handCarryStoppingDistance;

            agent.isStopped = false;
            agent.SetDestination(transform.position + new Vector3(0, 0, -30f));

            currentState = CustomerState.Leaving;
        }
    }

    private void HandleLeaving()
    {
        if (HasReachedTarget())
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (checkoutQueue.Contains(this)) checkoutQueue.Remove(this);
        if (allActiveCustomers.Contains(this)) allActiveCustomers.Remove(this);
        CloseFridgeDoor();
    }

    private Vector3 GetCustomerStandPosition(ShelfManager shelf)
    {
        Transform cSlot = shelf.transform.Find("CustomerSlot");
        if (cSlot != null) return cSlot.position;

        Vector3[] safeDirections = new Vector3[] {
            shelf.transform.right,
            -shelf.transform.right,
            shelf.transform.forward
        };

        Vector3 pickedDir = safeDirections[Random.Range(0, safeDirections.Length)];
        return shelf.transform.position + pickedDir * 1.2f;
    }

    private void CollectItemFromShelf()
    {
        ShoppingRequest currentReq = shoppingList[currentRequestIndex];

        if (targetShelf == null || targetShelf.acceptedItemType != currentReq.itemType)
        {
            CloseFridgeDoor();
            currentState = CustomerState.FindingItem;
            return;
        }

        if (targetShelf.IsEmpty) return;

        interactTimer += Time.deltaTime;
        if (interactTimer >= interactDelay)
        {
            interactTimer = 0f;
            GameObject item = targetShelf.TakeProduct();
            if (item != null)
            {
                AttachItemToCustomer(item);
                currentReq.collectedAmount++;
                totalCollectedItems++;
                UpdateThoughtUI();

                if (currentReq.collectedAmount >= currentReq.targetAmount)
                {
                    CloseFridgeDoor();
                    currentState = CustomerState.FindingItem;
                }
            }
        }
    }

    private void CloseFridgeDoor()
    {
        if (currentOpenShelf != null)
        {
            currentOpenShelf.OnEntityExit();
            currentOpenShelf = null;
        }
    }

    private void AttachItemToCustomer(GameObject item)
    {
        item.transform.DOKill();
        if (useTrolley)
        {
            int slotIndex = Mathf.Min(totalCollectedItems - 1, trolleySlots.Length - 1);
            Transform targetSlot = trolleySlots[Mathf.Max(0, slotIndex)];

            item.transform.SetParent(targetSlot);
            item.transform.DOScale(Vector3.one, 0.2f);
            item.transform.DOLocalJump(Vector3.zero, 0.5f, 1, 0.3f);
            item.transform.DOLocalRotate(Vector3.zero, 0.3f);
        }
        else
        {
            item.transform.SetParent(carrySocket);
            item.transform.DOScale(Vector3.one, 0.2f);

            Vector3 targetLocalPos = new Vector3(0, (totalCollectedItems - 1) * itemHeightOffset, 0);
            item.transform.DOLocalJump(targetLocalPos, 0.5f, 1, 0.3f);
            item.transform.DOLocalRotate(Vector3.zero, 0.3f);
        }
    }

    private void UpdateThoughtUI()
    {
        if (currentRequestIndex < shoppingList.Count)
        {
            thoughtBubble.SetActive(true);
            ShoppingRequest currentReq = shoppingList[currentRequestIndex];

            iconImage.sprite = currentReq.itemIcon;
            numText.text = $"{currentReq.collectedAmount}/{currentReq.targetAmount}";
        }
        else
        {
            thoughtBubble.SetActive(false);
        }
    }

    private void PlayAnimation(string animName)
    {
        if (animComponent != null && animComponent[animName] != null)
        {
            animComponent[animName].speed = animSpeedMultiplier;
            animComponent.CrossFade(animName, 0.2f);
        }
    }

    private void HandleMovementAndAnimation()
    {
        Vector3 velocity = agent.velocity;
        bool isMoving = velocity.sqrMagnitude > 0.01f && !agent.isStopped;

        if (isMoving)
        {
            Vector3 direction = new Vector3(velocity.x, 0, velocity.z).normalized;
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 15f);
        }

        if (animComponent != null)
        {
            if (useTrolley)
            {
                if (isMoving) PlayAnimation(animRunCarry);
                else PlayAnimation(animIdleCarry);
            }
            else
            {
                if (totalCollectedItems > 0)
                {
                    if (isMoving) PlayAnimation(animRunCarry);
                    else PlayAnimation(animIdleCarry);
                }
                else
                {
                    if (isMoving) PlayAnimation(animRun);
                    else PlayAnimation(animIdle);
                }
            }
        }
    }

    private bool HasReachedTarget()
    {
        if (agent.pathPending) return false;

        if (agent.remainingDistance <= agent.stoppingDistance + 0.1f) return true;

        if (targetShelf != null)
        {
            float distToShelf = Vector3.Distance(transform.position, targetShelf.transform.position);
            if (distToShelf <= interactRadius && agent.velocity.sqrMagnitude < 0.05f) return true;
        }

        return false;
    }
}