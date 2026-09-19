using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class BlockUpgrade : MonoBehaviour
{
    [Header("基础配置")]
    [Tooltip("对应 JSON 中的解锁 id，如 tomato_shelf")]
    public string unlockId;

    [Tooltip("解锁后需要弹出的真实设备 (如：西红柿货架的完整模型)")]
    public GameObject targetFacility;

    [Header("节点与 UI 引用")]
    public GameObject normalObj;
    public GameObject activeObj;
    public GameObject arrowObj;

    [Tooltip("包裹进度条底图、金币图标的背景")]
    public GameObject darkBg;
    public GameObject cashIcon;
    public Image progressFill;
    public Text numText;

    [Header("特效与动画")]
    [Tooltip("拖入 arrow 节点上的 Animation 组件")]
    public Animation arrowAnim;

    [Tooltip("飞行的钱币预制体（用一沓钞票的模型即可）")]
    public GameObject flyingCoinPrefab;

    [Header("支付参数")]
    public int payAmountPerTick = 5;    // 每次飞一沓钱扣多少
    public float payInterval = 0.05f;   // 扣钱间隔 (越小飞得越快)

    private int totalCost = 100;
    private int currentPaid = 0;
    private bool isUnlocked = false;
    private float lastPayTime;

    // 【新增】：用于记录目标设施原本的缩放大小
    private Vector3 originalFacilityScale = Vector3.one;

    private void Start()
    {
        if (LevelDataManager.Instance != null)
        {
            totalCost = LevelDataManager.Instance.GetUnlockCost(unlockId);
        }

        if (targetFacility != null)
        {
            // 在隐藏它之前，先偷偷记住它在面板里设置的大小（比如 100, 100, 100）
            originalFacilityScale = targetFacility.transform.localScale;
            targetFacility.SetActive(false);
        }

        UpdateUI();
        SetVisualState(false);
    }

    private void SetVisualState(bool isPlayerTouching)
    {
        if (isUnlocked) return;

        if (isPlayerTouching)
        {
            if (normalObj != null) normalObj.SetActive(true);
            if (activeObj != null) activeObj.SetActive(false);
            if (arrowObj != null) arrowObj.SetActive(false);
        }
        else
        {
            if (normalObj != null) normalObj.SetActive(false);
            if (activeObj != null) activeObj.SetActive(true);
            if (arrowObj != null)
            {
                arrowObj.SetActive(true);
                if (arrowAnim != null) arrowAnim.Play("arrow");
            }
        }
    }

    // ==========================================
    // 接收来自子物体 UpgradeColliderRelay 的事件
    // ==========================================

    public void RelayTriggerEnter(Collider other)
    {
        if (isUnlocked) return;
        if (other.CompareTag("Player"))
        {
            SetVisualState(true);
        }
    }

    public void RelayTriggerExit(Collider other)
    {
        if (isUnlocked) return;
        if (other.CompareTag("Player"))
        {
            SetVisualState(false);
        }
    }

    public void RelayTriggerStay(Collider other)
    {
        if (isUnlocked) return;

        if (other.CompareTag("Player"))
        {
            if (Time.time - lastPayTime > payInterval)
            {
                lastPayTime = Time.time;
                TryPay(other.transform.position);
            }
        }
    }

    private void TryPay(Vector3 playerPos)
    {
        if (currentPaid >= totalCost) return;

        int cashAvailable = GameManager.Instance.totalCash;
        if (cashAvailable <= 0) return;

        int needToPay = totalCost - currentPaid;
        int actualPay = Mathf.Min(payAmountPerTick, needToPay, cashAvailable);

        if (actualPay > 0)
        {
            GameManager.Instance.AddCash(-actualPay);
            SpawnFlyingCoin(playerPos, actualPay);
        }
    }

    private void SpawnFlyingCoin(Vector3 startPos, int amount)
    {
        if (flyingCoinPrefab == null) return;

        GameObject coin = Instantiate(flyingCoinPrefab, startPos + Vector3.up * 1f, Quaternion.identity);
        Vector3 targetPos = this.transform.position + Vector3.up * 0.5f;

        coin.transform.DOJump(targetPos, 2f, 1, 0.25f).OnComplete(() =>
        {
            Destroy(coin);
            currentPaid += amount;
            UpdateUI();

            if (currentPaid >= totalCost && !isUnlocked)
            {
                UnlockFacility();
            }
        });
    }

    private void UpdateUI()
    {
        if (progressFill != null)
        {
            progressFill.fillAmount = (float)currentPaid / totalCost;
        }
        if (numText != null)
        {
            numText.text = (totalCost - currentPaid).ToString();
        }
    }

    private void UnlockFacility()
    {
        isUnlocked = true;

        // if (normalObj != null) normalObj.SetActive(true);
        // if (activeObj != null) activeObj.SetActive(false);
        // if (arrowObj != null) arrowObj.SetActive(false);

        // if (darkBg != null) darkBg.SetActive(false);
        // if (cashIcon != null) cashIcon.SetActive(false);
        // if (progressFill != null) progressFill.gameObject.SetActive(false);
        // if (numText != null) numText.gameObject.SetActive(false);

        if (targetFacility != null)
        {
            targetFacility.SetActive(true);
            targetFacility.transform.localScale = Vector3.zero;
            // 【关键修复】：缩放到之前记录的真实大小，而不是写死的 Vector3.one
            targetFacility.transform.DOScale(originalFacilityScale, 0.5f).SetEase(Ease.OutBack);
        }

        gameObject.SetActive(false);
    }
}