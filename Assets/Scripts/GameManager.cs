using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class ItemIconMapping
{
    public ItemType itemType;
    public Sprite itemIcon;
}

[System.Serializable]
public class ItemPriceMapping
{
    public ItemType itemType;
    public int price;
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("玩家财富")]
    public int totalCash = 0;
    public System.Action<int> OnCashChanged;

    [Header("收银台管理 (支持多收银台)")]
    public List<CheckoutCounter> allCheckoutCounters = new List<CheckoutCounter>();

    [Header("顾客生成配置")]
    public GameObject customerPrefab;
    public Transform spawnPointParent;
    public Transform exitPoint;

    [Header("物品配置字典")]
    public List<ItemIconMapping> itemIconDatabase = new List<ItemIconMapping>();
    public List<ItemPriceMapping> itemPriceDatabase = new List<ItemPriceMapping>();

    [Header("升级数据")]
    public int currentCustomerCapacity = 1;
    public int maxCustomerCapacity = 8;
    public int currentActiveCustomers = 0;

    private int[] upgradeCosts = { 0, 50, 150, 300, 600, 1000, 1500, 2500 };

    // 【新增】：雷达缓存，每秒自动扫描场景，彻底告别手动拖拽！
    private ShelfManager[] cachedShelves;
    private float lastCacheTime = -1f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(0.5f);
        for (int i = 0; i < currentCustomerCapacity; i++)
        {
            SpawnSingleCustomer();
        }
    }

    public void AddCash(int amount)
    {
        totalCash += amount;
        OnCashChanged?.Invoke(totalCash);
    }

    public int GetItemPrice(ItemType type)
    {
        int jsonPrice = 0;
        if (LevelDataManager.Instance != null)
        {
            jsonPrice = LevelDataManager.Instance.GetPriceFromJson(type.ToString());
        }

        if (jsonPrice > 0) return jsonPrice;

        foreach (var mapping in itemPriceDatabase)
        {
            if (mapping.itemType == type) return mapping.price > 0 ? mapping.price : 5;
        }
        return 5;
    }

    public CheckoutCounter GetBestCheckoutCounter()
    {
        if (allCheckoutCounters == null || allCheckoutCounters.Count == 0) return null;

        CheckoutCounter bestCounter = null;
        int minQueue = int.MaxValue;

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
        if (currentCustomerCapacity < maxCustomerCapacity) return upgradeCosts[currentCustomerCapacity];
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

    // =========================================================
    // 【完美防呆逻辑】：彻底抛弃 FacilityManager，全自动扫描场景
    // =========================================================
    private IEnumerable<ShelfManager> GetActiveShelves()
    {
        if (Time.time - lastCacheTime > 1.0f || cachedShelves == null)
        {
            cachedShelves = FindObjectsOfType<ShelfManager>();
            lastCacheTime = Time.time;
        }
        return cachedShelves;
    }

    private List<ShoppingRequest> GenerateDynamicShoppingList()
    {
        List<ShoppingRequest> requests = new List<ShoppingRequest>();
        List<ItemType> unlockedItems = new List<ItemType>();

        foreach (var shelf in GetActiveShelves())
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

            if (totalItemsCount + newReq.targetAmount > 12) newReq.targetAmount = 12 - totalItemsCount;
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