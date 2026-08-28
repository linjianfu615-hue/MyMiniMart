using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 游戏管理器(处理游戏初始化、存档、读档的逻辑)
/// </summary>

public class GameManager : MonoBehaviour
{
    [Header("场景中需要履约存档的建筑群")]
    public List<BaseStructure> allStructures;

    // 伪代码定义玩家引用
    // public PlayerController player; 
    // public int currentMoney;

    private void Start()
    {
        LoadGameProgress();
    }

    private void OnApplicationQuit()
    {
        SaveGameProgress();
    }

    // 切后台时自动存档（针对手机端常态优化）
    private void OnApplicationFocus(bool focus)
    {
        if (!focus) SaveGameProgress();
    }

    public void SaveGameProgress()
    {
        GameSaveRoot root = new GameSaveRoot();

        // 1. 采集玩家数据
        root.playerData.currentMoney = 1000; // 替换为真实的钱包变量
        root.playerData.playerSpeedLevel = 1;
        root.playerData.playerCapacityLevel = 1;

        // 2. 采集所有建筑状态
        foreach (var structObj in allStructures)
        {
            StructureSaveData sData = new StructureSaveData
            {
                structureID = structObj.structureID,
                isUnlocked = structObj.gameObject.activeSelf, // 如果物体隐藏代表未解锁
                currentItemCount = structObj.CurrentCount
            };
            root.structuresData.Add(sData);
        }

        SaveManager.Instance.SaveGame(root);
    }

    public void LoadGameProgress()
    {
        GameSaveRoot root = SaveManager.Instance.LoadGame();
        if (root == null)
        {
            // 执行第一关全新初始化（参考前文数值表格：解锁免费番茄地，隐藏其他）
            return;
        }

        // 1. 恢复玩家属性
        // wallet.SetMoney(root.playerData.currentMoney);

        // 2. 恢复场景设施
        foreach (var sData in root.structuresData)
        {
            // 在列表中匹配对应的物体
            BaseStructure match = allStructures.Find(x => x.structureID == sData.structureID);
            if (match != null)
            {
                match.gameObject.SetActive(sData.isUnlocked);

                // 还可以根据 sData.currentItemCount 用循环给它塞入对应数量的初始商品
            }
        }
    }
}