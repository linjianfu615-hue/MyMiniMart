using System.Collections.Generic;

/// <summary>
/// 存档数据类(处理玩家数据、建筑数据、游戏数据)
/// </summary>

[System.Serializable]
public class PlayerSaveData
{
    public int currentMoney;        // 玩家当前持有的金币
    public int playerSpeedLevel;   // 玩家速度等级
    public int playerCapacityLevel;// 玩家容量等级
}

[System.Serializable]
public class StructureSaveData
{
    public string structureID;     // 建筑/设施唯一ID
    public bool isUnlocked;        // 是否已解锁
    public int currentItemCount;   // 存档时里面堆积了多少个商品
}

[System.Serializable]
public class GameSaveRoot
{
    public PlayerSaveData playerData = new PlayerSaveData();
    public List<StructureSaveData> structuresData = new List<StructureSaveData>();
}