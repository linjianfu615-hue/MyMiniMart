using UnityEngine;
using UnityEngine.UI;
using DG.Tweening; // 引入 DoTween 做金币跳动动画

public class MainUI : MonoBehaviour
{
    [Header("金币 UI")]
    [Tooltip("拖入 Canvas -> MainUI -> cash -> num 文本框")]
    public Text cashNumText;

    [Header("顾客购买 UI")]
    [Tooltip("拖入底部的增加顾客按钮 (Button Legacy)")]
    public Button addCustomerButton;
    [Tooltip("拖入按钮下的文字组件 (Text Legacy)")]
    public Text addCustomerText;

    private void Start()
    {
        if (addCustomerButton != null)
        {
            addCustomerButton.onClick.AddListener(OnAddCustomerClicked);
        }

        // 向 GameManager 订阅金币更新事件
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnCashChanged += UpdateCashUI;
            // 初始化界面时同步一次当前的钱
            UpdateCashUI(GameManager.Instance.totalCash);
        }

        RefreshUI();
    }

    private void OnDestroy()
    {
        // 养成好习惯：UI销毁时注销事件，防止内存泄漏
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnCashChanged -= UpdateCashUI;
        }
    }

    /// <summary>
    /// 当玩家吸收到钱时，自动触发此方法更新数字
    /// </summary>
    private void UpdateCashUI(int currentCash)
    {
        if (cashNumText != null)
        {
            cashNumText.text = currentCash.ToString();

            // 给包裹文字和图标的父节点 (cash) 来个轻微的缩放弹跳反馈
            cashNumText.transform.parent.DOKill(true);
            cashNumText.transform.parent.DOPunchScale(Vector3.one * 0.15f, 0.2f);
        }
    }

    private void OnAddCustomerClicked()
    {
        bool success = GameManager.Instance.TryBuyCustomer();
        if (success)
        {
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
            addCustomerButton.interactable = false;
        }
        else
        {
            int cost = GameManager.Instance.GetNextCustomerCost();
            addCustomerText.text = $"增加顾客\n${cost} ({currentLvl}/{maxLvl})";
        }
    }
}