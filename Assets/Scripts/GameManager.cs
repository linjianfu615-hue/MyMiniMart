using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class ItemIconMapping
{
    public ItemType itemType;
    public Sprite itemIcon;
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("收银台管理 (支持多收银台)")]
    [Tooltip("将场景中所有挂载了 CheckoutCounter 的收银台拖入此列表")]
    public List<CheckoutCounter> allCheckoutCounters = new List<CheckoutCounter>();

    [Header("顾客生成配置")]
    public GameObject customerPrefab;
    public Transform spawnPointParent;
    public Transform exitPoint;

    [Header("图标配置字典")]
    public List<ItemIconMapping> itemIconDatabase = new List<ItemIconMapping>();

    [Header("升级数据")]
    public int currentCustomerCapacity = 1;
    public int maxCustomerCapacity = 8;
    public int currentActiveCustomers = 0;

    private int[] upgradeCosts = { 0, 50, 150, 300, 600, 1000, 1500, 2500 };

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        for (int i = 0; i < currentCustomerCapacity; i++)
        {
            SpawnSingleCustomer();
        }
    }

    /// <summary>
    /// 【新增核心】：智能分配最空闲的收银台给顾客
    /// </summary>
    public CheckoutCounter GetBestCheckoutCounter()
    {
        if (allCheckoutCounters == null || allCheckoutCounters.Count == 0) return null;

        CheckoutCounter bestCounter = null;
        int minQueue = int.MaxValue;

        // 遍历所有激活的收银台，寻找排队人数最少的
        foreach (var counter in allCheckoutCounters)
        {
            if (counter.gameObject.activeInHierarchy && counter.customerQueue.Count < minQueue)
            {
                bestCounter = counter;
                minQueue = counter.customerQueue.Count;
            }
        }

        return bestCounter;
    }

    public int GetNextCustomerCost()
    {
        if (currentCustomerCapacity < maxCustomerCapacity)
            return upgradeCosts[currentCustomerCapacity];
        return -1;
    }

    public bool TryBuyCustomer()
    {
        if (currentCustomerCapacity >= maxCustomerCapacity) return false;

        currentCustomerCapacity++;
        SpawnSingleCustomer();
        return true;
    }

    public void SpawnSingleCustomer()
    {
        if (customerPrefab == null || spawnPointParent == null || spawnPointParent.childCount == 0) return;

        int randomIndex = Random.Range(0, spawnPointParent.childCount);
        Transform spawnPt = spawnPointParent.GetChild(randomIndex);

        GameObject newCustomerObj = Instantiate(customerPrefab, spawnPt.position, spawnPt.rotation);
        CustomerAIController customerAI = newCustomerObj.GetComponent<CustomerAIController>();

        List<ShoppingRequest> dynamicList = GenerateDynamicShoppingList();

        if (customerAI != null) customerAI.InjectShoppingList(dynamicList);

        currentActiveCustomers++;
    }

    private List<ShoppingRequest> GenerateDynamicShoppingList()
    {
        List<ShoppingRequest> requests = new List<ShoppingRequest>();
        List<ItemType> unlockedItems = new List<ItemType>();

        foreach (var shelf in FacilityManager.Instance.allShelves)
        {
            if (shelf.gameObject.activeInHierarchy && shelf.acceptedItemType != ItemType.None)
            {
                if (!unlockedItems.Contains(shelf.acceptedItemType))
                    unlockedItems.Add(shelf.acceptedItemType);
            }
        }

        if (unlockedItems.Count == 0) return requests;

        int kindsWanted = Random.Range(1, Mathf.Min(4, unlockedItems.Count + 1));
        ShuffleList(unlockedItems);

        int totalItemsCount = 0;

        for (int i = 0; i < kindsWanted; i++)
        {
            ItemType wantedType = unlockedItems[i];
            ShoppingRequest newReq = new ShoppingRequest();
            newReq.itemType = wantedType;
            newReq.targetAmount = Random.Range(1, 4);

            if (totalItemsCount + newReq.targetAmount > 12)
                newReq.targetAmount = 12 - totalItemsCount;

            if (newReq.targetAmount <= 0) break;

            newReq.itemIcon = GetIconForType(wantedType);
            requests.Add(newReq);
            totalItemsCount += newReq.targetAmount;
        }
        return requests;
    }

    private Sprite GetIconForType(ItemType type)
    {
        foreach (var mapping in itemIconDatabase)
        {
            if (mapping.itemType == type) return mapping.itemIcon;
        }
        return null;
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            T temp = list[i];
            int randomIndex = Random.Range(i, list.Count);
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }

    public void OnCustomerLeft()
    {
        currentActiveCustomers--;
        Invoke(nameof(SpawnSingleCustomer), Random.Range(2f, 4f));
    }
}