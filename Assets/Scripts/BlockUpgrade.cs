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

    [Header("节点与 UI 引用 (对应图1)")]
    public GameObject normalObj;
    public GameObject activeObj;
    public GameObject arrowObj;

    [Tooltip("包裹进度条底图、金币图标的背景")]
    public GameObject darkBg;
    public GameObject cashIcon;
    public Image progressFill; // Image Type 必须是 Filled[cite: 43]
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

    private void Start()
    {
        // 1. 从 JSON 管理器获取真实解锁价格
        if (LevelDataManager.Instance != null)
        {
            totalCost = LevelDataManager.Instance.GetUnlockCost(unlockId);
        }

        // 2. 隐藏真实设施，等待解锁
        if (targetFacility != null) targetFacility.SetActive(false);

        // 3. 初始化视觉状态：初始显示 active 和 arrow，隐藏 normal
        UpdateUI();
        SetVisualState(false);
    }

    /// <summary>
    /// 控制地块的显示状态
    /// </summary>
    private void SetVisualState(bool isPlayerTouching)
    {
        if (isUnlocked) return; // 解锁后永远保持 normal

        if (isPlayerTouching)
        {
            // 玩家踩在上面：显示 normal，隐藏 arrow 和 active
            if (normalObj != null) normalObj.SetActive(true);
            if (activeObj != null) activeObj.SetActive(false);
            if (arrowObj != null) arrowObj.SetActive(false);
        }
        else
        {
            // 玩家离开：显示 active 和 arrow，隐藏 normal，并播放箭头动画[cite: 41, 42]
            if (normalObj != null) normalObj.SetActive(false);
            if (activeObj != null) activeObj.SetActive(true);
            if (arrowObj != null)
            {
                arrowObj.SetActive(true);
                if (arrowAnim != null) arrowAnim.Play("arrow");
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isUnlocked) return;
        // ⚠️ 确保你的玩家模型身上被打上了 "Player" 的 Tag
        if (other.CompareTag("Player"))
        {
            SetVisualState(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (isUnlocked) return;
        if (other.CompareTag("Player"))
        {
            SetVisualState(false);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (isUnlocked) return;

        if (other.CompareTag("Player"))
        {
            // 按设定的时间间隔持续吸钱
            if (Time.time - lastPayTime > payInterval)
            {
                lastPayTime = Time.time;
                TryPay(other.transform.position);
            }
        }
    }

    /// <summary>
    /// 核心扣钱与飞行逻辑
    /// </summary>
    private void TryPay(Vector3 playerPos)
    {
        if (currentPaid >= totalCost) return;

        int cashAvailable = GameManager.Instance.totalCash;
        if (cashAvailable <= 0) return; // 没钱了

        // 计算这次扣多少钱 (不能超过剩余尾款，也不能超过身上带的钱)
        int needToPay = totalCost - currentPaid;
        int actualPay = Mathf.Min(payAmountPerTick, needToPay, cashAvailable);

        if (actualPay > 0)
        {
            // 1. 扣除 GameManager 的钱 (这里传入负数)
            GameManager.Instance.AddCash(-actualPay);

            // 2. 生成飞行动画
            SpawnFlyingCoin(playerPos, actualPay);
        }
    }

    private void SpawnFlyingCoin(Vector3 startPos, int amount)
    {
        if (flyingCoinPrefab == null) return;

        // 在玩家偏上一点的位置生成钱币
        GameObject coin = Instantiate(flyingCoinPrefab, startPos + Vector3.up * 1f, Quaternion.identity);

        // 钱飞向地块的中心偏上一点
        Vector3 targetPos = this.transform.position + Vector3.up * 0.5f;

        // DOTween 抛物线动画
        coin.transform.DOJump(targetPos, 2f, 1, 0.25f).OnComplete(() =>
        {
            // 钱币到达目的地后销毁
            Destroy(coin);

            // 增加已付金额并刷新 进度条 UI
            currentPaid += amount;
            UpdateUI();

            // 检查是否全款付清
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
            // 更新进度条填充比例[cite: 43]
            progressFill.fillAmount = (float)currentPaid / totalCost;
        }
        if (numText != null)
        {
            // 更新剩余所需金额
            numText.text = (totalCost - currentPaid).ToString();
        }
    }

    /// <summary>
    /// 完成解锁
    /// </summary>
    private void UnlockFacility()
    {
        isUnlocked = true;

        // 永久显示 normal，隐藏其他占位符
        // if (normalObj != null) normalObj.SetActive(false);
        // if (activeObj != null) activeObj.SetActive(false);
        // if (arrowObj != null) arrowObj.SetActive(false);

        // 隐藏整个付款 UI 节点
        // if (darkBg != null) darkBg.SetActive(false);
        // if (cashIcon != null) cashIcon.SetActive(false);
        // if (progressFill != null) progressFill.gameObject.SetActive(false);
        // if (numText != null) numText.gameObject.SetActive(false);

        // 激活真实的货架/设备
        if (targetFacility != null)
        {
            targetFacility.SetActive(true);

            // 给真实设备一个非常 Q 弹的“破土而出”特效
            targetFacility.transform.localScale = Vector3.zero;
            targetFacility.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);
        }

        // 这一步取决于你的场景设计，如果你想保留底座 (normal)，就不要销毁自己。
        // 如果底座也不需要了，可以把下面这行取消注释：
        // Destroy(gameObject, 1f); 
        gameObject.SetActive(false);
    }
}