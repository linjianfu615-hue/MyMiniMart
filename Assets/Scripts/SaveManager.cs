using System.IO;
using UnityEngine;

/// <summary>
/// 存档管理器(处理游戏存档、读档、删除存档的逻辑)
/// </summary>

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    private string savePath;
    private const string SaveFileName = "minimart_save.json";

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }

        // 存档路径：在电脑上是 AppData，在手机上是沙盒内部存储，安全合规
        savePath = Path.Combine(Application.persistentDataPath, SaveFileName);
    }

    // 保存游戏
    public void SaveGame(GameSaveRoot dataToSave)
    {
        try
        {
            // 将对象转换为紧凑的 JSON 字符串（实际发布时把 true 改为 false 可以压缩体积）
            string json = JsonUtility.ToJson(dataToSave, true);

            // 写入文件
            File.WriteAllText(savePath, json);
            Debug.Log($"游戏保存成功！路径: {savePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"游戏保存失败: {e.Message}");
        }
    }

    // 读取游戏
    public GameSaveRoot LoadGame()
    {
        if (!File.Exists(savePath))
        {
            Debug.LogWarning("未找到存档文件，将创建全新游戏。");
            return null; // 返回 null 代表让游戏进行初次初始化
        }

        try
        {
            string json = File.ReadAllText(savePath);
            GameSaveRoot loadedData = JsonUtility.FromJson<GameSaveRoot>(json);
            Debug.Log("游戏存档读取成功！");
            return loadedData;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"存档解析失败，可能文件损坏: {e.Message}");
            return null;
        }
    }

    // 恢复出厂设置（清档）
    public void DeleteSave()
    {
        if (File.Exists(savePath))
        {
            File.Delete(savePath);
            Debug.Log("存档已清除。");
        }
    }
}