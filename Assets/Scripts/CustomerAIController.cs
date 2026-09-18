using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;
using System.Collections;
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
    [Tooltip("手捧物品时离目标的距离")]
    public float handCarryStoppingDistance = 1.5f;
    [Tooltip("推推车时离目标的距离")]
    public float trolleyStoppingDistance = 2.5f;

    [Header("UI 及 打包设置")]
    public Sprite checkoutIcon;
    public Sprite happyIcon;
    public float payTime = 2.0f;
    public GameObject boxPackagePrefab;

    [Header("购物清单配置")]
    public List<ShoppingRequest> shoppingList = new List<ShoppingRequest>();

    private List<GameObject> collectedItemObjects = new List<GameObject>();

    [Header("UI 气泡组件")]
    public GameObject thoughtBubble;
    public Image iconImage;
    public Text numText;

    [Header("搬运节点配置")]
    public Transform carrySocket;
    public GameObject trolley;
    public Transform[] trolleySlots;
    public float itemHeightOffset = 1.5f;

    [Header("动画配置")]
    public Animation animComponent;
    [Range(0.1f, 3f)]
    public float animSpeedMultiplier = 1.0f;

    public string animIdle = "Idle_Happy";
    public string animRun = "Run";
    public string animIdleCarry = "Idle_Happy_Carry";
    public string animRunCarry = "Run_Carry";
    public string animRunTrolley = "Run_Net";

    public float interactRadius = 1.5f;

    public bool HasTrolley => useTrolley;

    private NavMeshAgent agent;
    private enum CustomerState { Entering, FindingItem, MovingToShelf, Collecting, GoingToCheckout, Paying, Leaving }
    private CustomerState currentState = CustomerState.Entering;

    private int currentRequestIndex = 0;
    private ShelfManager targetShelf;
    private ShelfManager currentOpenShelf;

    private CheckoutCounter targetCheckoutCounter;

    private bool useTrolley = false;
    private int totalCollectedItems = 0;
    private float interactTimer = 0f;
    private float interactDelay = 0.3f;
    private bool isPacking = false;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = false;
        agent.speed = moveSpeed;
    }

    public void InjectShoppingList(List<ShoppingRequest> dynamicList)
    {
        shoppingList = dynamicList;
        InitCustomer();
    }

    public void InitCustomer()
    {
        int totalItemsNeeded = 0;
        foreach (var req in shoppingList)
        {
            totalItemsNeeded += req.targetAmount;
        }

        useTrolley = (shoppingList.Count > 1) && (totalItemsNeeded >= 3);

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
                thoughtBubble.SetActive(true);
                iconImage.sprite = checkoutIcon;
                iconImage.rectTransform.sizeDelta = new Vector2(200f, 200f);
                numText.text = "";
            }

            targetCheckoutCounter = GameManager.Instance.GetBestCheckoutCounter();
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

                // ==========================================
                // 【已修复】：绝对遵从 Inspector 的距离设置！
                // 去货架时，完美还原为你填写的参数，不再强制 0.1
                // ==========================================
                agent.stoppingDistance = useTrolley ? trolleyStoppingDistance : handCarryStoppingDistance;
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
        if (targetCheckoutCounter == null) return;

        int myIndex = targetCheckoutCounter.customerQueue.IndexOf(this);
        Vector3 targetPos = targetCheckoutCounter.GetQueuePosition(this);

        if (myIndex == -1)
        {
            // 排队奔跑时临时把误差设小，以便精准入队
            agent.stoppingDistance = 0.2f;
            agent.isStopped = false;
            agent.SetDestination(targetPos);

            Vector3 pos2D = new Vector3(transform.position.x, 0, transform.position.z);
            Vector3 target2D = new Vector3(targetPos.x, 0, targetPos.z);

            if (Vector3.Distance(pos2D, target2D) <= 1.0f)
            {
                targetCheckoutCounter.JoinQueue(this);
            }
            return;
        }

        // 已经在队伍中时，必须强制脚踩着属于自己的排队圆点（所以队伍中是 0.1）
        agent.stoppingDistance = 0.1f;
        Vector3 myPos2D = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 myTarget2D = new Vector3(targetPos.x, 0, targetPos.z);
        float dist = Vector3.Distance(myPos2D, myTarget2D);

        if (dist <= 0.3f)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;

            Vector3 lookDir = (myIndex == 0) ? targetCheckoutCounter.paymentPoint.forward : (targetCheckoutCounter.paymentPoint.position - transform.position);
            lookDir.y = 0;
            if (lookDir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 10f);
            }

            if (myIndex == 0)
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
        if (targetCheckoutCounter == null) return;

        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(targetCheckoutCounter.paymentPoint.forward), Time.deltaTime * 10f);

        if (!isPacking)
        {
            isPacking = true;
            StartCoroutine(PackItemsRoutine());
        }
    }

    private IEnumerator PackItemsRoutine()
    {
        GameObject boxInstance = null;

        if (boxPackagePrefab != null && targetCheckoutCounter != null && targetCheckoutCounter.boxPoint != null)
        {
            boxInstance = Instantiate(boxPackagePrefab, targetCheckoutCounter.boxPoint.position, targetCheckoutCounter.boxPoint.rotation);
            Animation boxAnim = boxInstance.GetComponent<Animation>();
            if (boxAnim != null)
            {
                boxAnim.Play("BoxOpen");
                yield return new WaitForSeconds(0.4f);
            }
            else yield return new WaitForSeconds(0.2f);
        }

        if (boxInstance != null)
        {
            // ... 纸箱开启动画 ...
            Transform inputGray = boxInstance.transform.Find("Input_Gray");
            if (inputGray != null)
            {
                for (int i = 0; i < collectedItemObjects.Count; i++)
                {
                    GameObject item = collectedItemObjects[i];
                    if (item == null) continue;

                    Transform targetSlot = inputGray;
                    if (i < inputGray.childCount) targetSlot = inputGray.GetChild(i);

                    item.transform.SetParent(targetSlot);
                    item.transform.DOLocalJump(Vector3.zero, 1.5f, 1, 0.25f);
                    item.transform.DOLocalRotate(Vector3.zero, 0.25f);

                    // ==========================================
                    // 智能钞票拆分生成
                    // ==========================================
                    ItemData itemData = item.GetComponent<ItemData>();
                    if (itemData != null && targetCheckoutCounter != null && GameManager.Instance != null)
                    {
                        // 从 GameManager (最终是 json) 读取真实价格
                        int price = GameManager.Instance.GetItemPrice(itemData.itemType);

                        // 算法：基础1沓，每多 5 块钱多分裂1沓，单件商品最多弹出 3 沓钞票
                        int cashAmountToSpawn = Mathf.Clamp(price / 5, 1, 5);

                        // 将价格平分，余数塞给第一沓
                        int valuePerCash = price / cashAmountToSpawn;
                        int remainder = price % cashAmountToSpawn;

                        for (int j = 0; j < cashAmountToSpawn; j++)
                        {
                            int finalValue = valuePerCash + (j == 0 ? remainder : 0);
                            targetCheckoutCounter.GenerateCash(finalValue);
                        }
                    }

                    yield return new WaitForSeconds(0.15f);
                }
            }
            // ... 纸箱关闭动画 ...

            Animation boxAnim = boxInstance.GetComponent<Animation>();
            if (boxAnim != null)
            {
                boxAnim.Play("BoxClose");
                yield return new WaitForSeconds(0.5f);
            }
        }
        else
        {
            yield return new WaitForSeconds(1.0f);
        }

        foreach (var item in collectedItemObjects)
        {
            if (item != null) Destroy(item);
        }
        collectedItemObjects.Clear();

        if (useTrolley)
        {
            useTrolley = false;
            trolley.SetActive(false);
            carrySocket.gameObject.SetActive(true);
        }

        if (boxInstance != null)
        {
            boxInstance.transform.SetParent(carrySocket);
            Vector3 targetBoxPos = new Vector3(0, 0, 0.8f);
            boxInstance.transform.DOLocalJump(targetBoxPos, 1.5f, 1, 0.35f);
            boxInstance.transform.DOLocalRotate(Vector3.zero, 0.35f);
            yield return new WaitForSeconds(0.4f);
        }

        if (targetCheckoutCounter != null)
        {
            targetCheckoutCounter.LeaveQueue(this);
        }

        if (happyIcon != null)
        {
            thoughtBubble.SetActive(true);
            iconImage.sprite = happyIcon;
            iconImage.rectTransform.sizeDelta = new Vector2(200f, 200f);
            numText.text = "";
        }
        else
        {
            thoughtBubble.SetActive(false);
        }

        agent.stoppingDistance = 0.5f;
        agent.isStopped = false;

        if (GameManager.Instance != null && GameManager.Instance.exitPoint != null)
        {
            agent.SetDestination(GameManager.Instance.exitPoint.position);
        }
        else
        {
            agent.SetDestination(transform.position + new Vector3(0, 0, -30f));
        }

        currentState = CustomerState.Leaving;
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
        if (targetCheckoutCounter != null) targetCheckoutCounter.LeaveQueue(this);
        if (GameManager.Instance != null) GameManager.Instance.OnCustomerLeft();
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

        if (!collectedItemObjects.Contains(item))
        {
            collectedItemObjects.Add(item);
        }

        if (useTrolley)
        {
            int slotIndex = Mathf.Min(totalCollectedItems, trolleySlots.Length - 1);
            Transform targetSlot = trolleySlots[slotIndex];

            item.transform.SetParent(targetSlot);
            item.transform.DOScale(Vector3.one, 0.2f);
            item.transform.DOLocalJump(Vector3.zero, 0.5f, 1, 0.3f);
            item.transform.DOLocalRotate(Vector3.zero, 0.3f);
        }
        else
        {
            item.transform.SetParent(carrySocket);
            item.transform.DOScale(Vector3.one, 0.2f);

            // ==========================================
            // 【已修复】：Y轴彻底解决了，第一个必定为 0！
            // ==========================================
            Vector3 targetLocalPos = new Vector3(0, totalCollectedItems * itemHeightOffset, 0);
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
            iconImage.rectTransform.sizeDelta = new Vector2(150f, 150f);
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

            // 为了让停得比较远（如2.5）的推车也能触发交互，我们将交互半径设得和停止距离差不多
            float currentInteractRadius = useTrolley ? trolleyStoppingDistance + 0.5f : interactRadius;

            if (distToShelf <= currentInteractRadius && agent.velocity.sqrMagnitude < 0.05f) return true;
        }

        return false;
    }
}