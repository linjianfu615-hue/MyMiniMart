using System.Collections.Generic;
using UnityEngine;

// ==========================================
// 1. JSON 数据结构映射
// ==========================================
[System.Serializable]
public class LevelDataRoot
{
    public List<PriceData> prices;
    public List<UnlockData> unlock_sequence; // 【新增】线性解锁顺序
    public LevelUpgrades level_1_upgrades;
}

[System.Serializable]
public class PriceData
{
    public string itemType;
    public int price;
}

[System.Serializable]
public class UnlockData
{
    public string id;
    public string name;
    public int cost;
}

[System.Serializable]
public class LevelUpgrades
{
    public List<EntityUpgradeData> shelves; // 【新增】货架类
    public List<EntityUpgradeData> workers;
    public List<EntityUpgradeData> machines;
    public List<EntityUpgradeData> animals;
    public List<EntityUpgradeData> producers;
    public List<EntityUpgradeData> customers;
}

[System.Serializable]
public class EntityUpgradeData
{
    public string id;
    public string name;
    public UpgradeCategories upgrades;
}

[System.Serializable]
public class UpgradeCategories
{
    public List<UpgradeStep> stack;
    public List<UpgradeStep> speed;
    public List<UpgradeStep> count;
}

[System.Serializable]
public class UpgradeStep
{
    public int level;
    public int cost;
    public float value;
}

// ==========================================
// 2. 关卡数据管理器 (Level Data Manager)
// ==========================================
public class LevelDataManager : MonoBehaviour
{
    public static LevelDataManager Instance { get; private set; }

    [Header("数据配置")]
    [Tooltip("将 Assets/Data/level1.json 拖入此处")]
    public TextAsset jsonFile;

    public LevelDataRoot levelData { get; private set; }

    private Dictionary<string, int> currentLevels = new Dictionary<string, int>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        LoadData();
    }

    private void LoadData()
    {
        if (jsonFile == null) return;
        levelData = JsonUtility.FromJson<LevelDataRoot>(jsonFile.text);
    }

    /// <summary>
    /// 【新增接口】：根据当前的解锁进度索引，获取下一个需要解锁的设施数据
    /// （供场景中的“解锁地垫”读取并显示金币需求）
    /// </summary>
    public UnlockData GetNextUnlock(int currentUnlockIndex)
    {
        if (levelData == null || levelData.unlock_sequence == null) return null;
        if (currentUnlockIndex >= levelData.unlock_sequence.Count) return null; // 已经全部解锁完毕

        return levelData.unlock_sequence[currentUnlockIndex];
    }

    public UpgradeStep GetCurrentUpgradeStep(string entityId, string category)
    {
        string progressKey = $"{entityId}_{category}";
        int currentLevel = currentLevels.ContainsKey(progressKey) ? currentLevels[progressKey] : 1;

        List<UpgradeStep> steps = GetUpgradeList(entityId, category);
        if (steps == null || steps.Count == 0) return null;

        foreach (var step in steps)
        {
            if (step.level == currentLevel) return step;
        }
        return steps[steps.Count - 1];
    }

    public bool TryUpgrade(string entityId, string category)
    {
        string progressKey = $"{entityId}_{category}";
        int currentLevel = currentLevels.ContainsKey(progressKey) ? currentLevels[progressKey] : 1;

        List<UpgradeStep> steps = GetUpgradeList(entityId, category);
        if (steps == null) return false;

        UpgradeStep nextStep = null;
        foreach (var step in steps)
        {
            if (step.level == currentLevel + 1)
            {
                nextStep = step;
                break;
            }
        }

        if (nextStep == null) return false;

        if (GameManager.Instance.totalCash >= nextStep.cost)
        {
            GameManager.Instance.AddCash(-nextStep.cost);
            currentLevels[progressKey] = nextStep.level;
            return true;
        }
        return false;
    }

    private List<UpgradeStep> GetUpgradeList(string entityId, string category)
    {
        EntityUpgradeData targetEntity =
            FindEntityInList(levelData.level_1_upgrades.shelves, entityId) ?? // 【新增】查询货架
            FindEntityInList(levelData.level_1_upgrades.machines, entityId) ??
            FindEntityInList(levelData.level_1_upgrades.producers, entityId) ??
            FindEntityInList(levelData.level_1_upgrades.animals, entityId) ??
            FindEntityInList(levelData.level_1_upgrades.workers, entityId) ??
            FindEntityInList(levelData.level_1_upgrades.customers, entityId);

        if (targetEntity == null || targetEntity.upgrades == null) return null;

        switch (category.ToLower())
        {
            case "stack": return targetEntity.upgrades.stack;
            case "speed": return targetEntity.upgrades.speed;
            case "count": return targetEntity.upgrades.count;
        }
        return null;
    }

    private EntityUpgradeData FindEntityInList(List<EntityUpgradeData> list, string id)
    {
        if (list == null) return null;
        foreach (var item in list)
        {
            if (item.id == id) return item;
        }
        return null;
    }

    public int GetPriceFromJson(string typeString)
    {
        if (levelData == null || levelData.prices == null) return 0;
        foreach (var p in levelData.prices)
        {
            if (p.itemType == typeString) return p.price;
        }
        return 0;
    }

    /// <summary>
    /// 供地块升级脚本调用：根据 id 获取解锁所需的金额
    /// </summary>
    public int GetUnlockCost(string id)
    {
        if (levelData == null || levelData.unlock_sequence == null) return 100;
        foreach (var u in levelData.unlock_sequence)
        {
            if (u.id == id) return u.cost;
        }
        return 100; // 如果没查到，保底返回 100
    }
}