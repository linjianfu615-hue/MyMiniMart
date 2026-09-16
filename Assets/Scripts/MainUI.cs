using UnityEngine;
using UnityEngine.UI;

public class MainUI : MonoBehaviour
{
    [Header("顾客购买 UI")]
    [Tooltip("拖入底部的增加顾客按钮 (Button Legacy)")]
    public Button addCustomerButton;
    [Tooltip("拖入按钮下的文字组件 (Text Legacy)")]
    public Text addCustomerText;

    private void Start()
    {
        // 自动绑定按钮事件，无需在面板手动拖拽
        if (addCustomerButton != null)
        {
            addCustomerButton.onClick.AddListener(OnAddCustomerClicked);
        }

        RefreshUI();
    }

    private void OnAddCustomerClicked()
    {
        // 尝试购买顾客
        bool success = GameManager.Instance.TryBuyCustomer();
        if (success)
        {
            // 播放一个购买音效或特效 (可后期扩展)
            RefreshUI();
        }
        else
        {
            Debug.LogWarning("金币不足，或顾客数量已达上限(8/8)！");
        }
    }

    public void RefreshUI()
    {
        if (addCustomerText == null) return;

        int currentLvl = GameManager.Instance.currentCustomerCapacity;
        int maxLvl = GameManager.Instance.maxCustomerCapacity;

        if (currentLvl >= maxLvl)
        {
            addCustomerText.text = "MAX (8/8)";
            addCustomerButton.interactable = false; // 满级后按钮置灰
        }
        else
        {
            int cost = GameManager.Instance.GetNextCustomerCost();
            addCustomerText.text = $"增加顾客\n${cost} ({currentLvl}/{maxLvl})";
        }
    }
}