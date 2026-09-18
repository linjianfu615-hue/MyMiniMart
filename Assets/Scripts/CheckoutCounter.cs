using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

public class CheckoutCounter : MonoBehaviour
{
    [Header("收银台节点配置")]
    public Transform paymentPoint;
    public Transform boxPoint;

    [Header("收银员动画配置")]
    [Tooltip("拖入站在收银台里的收银员模型 (带有 Animation 组件)")]
    public Animation cashierAnim;
    public string animIdle = "Idle_Happy";
    public string animWork = "Idle_Happy_Carry";

    [Header("排队配置")]
    public float queueSpacing = 3.5f;

    [Header("钞票排版与生成配置")]
    [Tooltip("钞票的预制体 (cash)")]
    public GameObject cashPrefab;

    [Tooltip("拖入 Output 空节点，作为所有生成钞票的父物体")]
    public Transform cashOutputParent;

    [Tooltip("第一张钞票的初始局部坐标")]
    public Vector3 startLocalPos = new Vector3(3f, 2.4f, -1f);

    [Tooltip("钞票生成的默认旋转角度")]
    public Vector3 cashRotation = new Vector3(0f, 90f, 0f);

    [Header("3D 矩阵堆叠参数")]
    [Tooltip("X轴方向（横向）一排最多放几个")]
    public int itemsPerRowX = 4;

    [Tooltip("Z轴方向（纵向）最多放几排")]
    public int itemsPerColZ = 2;

    [Tooltip("X轴方向的间距")]
    public float spacingX = 1.5f;

    [Tooltip("Z轴方向的间距")]
    public float spacingZ = 1.86f;

    [Tooltip("Y轴方向（向上堆叠）的层高间距")]
    public float spacingY = 0.9f;

    [Tooltip("同屏最多允许存在多少个钞票实体模型（超过此数量只增加面值）")]
    public int maxVisualCashCount = 80;

    [HideInInspector]
    public List<CustomerAIController> customerQueue = new List<CustomerAIController>();

    private List<GameObject> activeCashList = new List<GameObject>();
    private bool isWorking = false;

    private void Start()
    {
        // 初始状态播放空闲动画
        if (cashierAnim != null && cashierAnim[animIdle] != null)
        {
            cashierAnim.Play(animIdle);
            isWorking = false;
        }
    }

    private void Update()
    {
        // 动态监控队列，控制收银员动画状态
        if (cashierAnim != null)
        {
            bool hasCustomer = customerQueue.Count > 0;

            if (hasCustomer && !isWorking)
            {
                isWorking = true;
                if (cashierAnim[animWork] != null)
                {
                    cashierAnim.CrossFade(animWork, 0.2f);
                }
            }
            else if (!hasCustomer && isWorking)
            {
                isWorking = false;
                if (cashierAnim[animIdle] != null)
                {
                    cashierAnim.CrossFade(animIdle, 0.2f);
                }
            }
        }
    }

    public void JoinQueue(CustomerAIController customer)
    {
        if (!customerQueue.Contains(customer)) customerQueue.Add(customer);
    }

    public void LeaveQueue(CustomerAIController customer)
    {
        if (customerQueue.Contains(customer)) customerQueue.Remove(customer);
    }

    public Vector3 GetQueuePosition(CustomerAIController customer)
    {
        int index = customerQueue.IndexOf(customer);
        if (index == -1) index = customerQueue.Count;

        float offset = 0f;
        for (int i = 1; i <= index; i++)
        {
            float space = queueSpacing;
            bool hasTrolley = (i < customerQueue.Count) ? customerQueue[i].HasTrolley : customer.HasTrolley;
            if (hasTrolley) space += 3.0f;
            offset += space;
        }
        return paymentPoint.position - paymentPoint.right * offset;
    }

    /// <summary>
    /// 生成实体钞票（采用 3D 网格数学动态计算位置）
    /// </summary>
    public void GenerateCash(int value)
    {
        if (cashPrefab == null || cashOutputParent == null) return;

        if (activeCashList.Count < maxVisualCashCount)
        {
            int currentIndex = activeCashList.Count;

            // 【核心排版算法：3D 矩阵展开】
            int itemsPerLayer = itemsPerRowX * itemsPerColZ; // 每层 4*2=8 个

            int layer = currentIndex / itemsPerLayer;             // 当前在第几层 (Y)
            int indexInLayer = currentIndex % itemsPerLayer;      // 在当前层的第几个 (0~7)

            int zRow = indexInLayer / itemsPerRowX;               // 在当前层的第几排 (Z)
            int xCol = indexInLayer % itemsPerRowX;               // 在当前排的第几列 (X)

            // 计算精准的相对坐标
            Vector3 targetLocalPos = new Vector3(
                startLocalPos.x + (xCol * spacingX),
                startLocalPos.y + (layer * spacingY),
                startLocalPos.z + (zRow * spacingZ)
            );

            // 实例化并挂载
            GameObject cash = Instantiate(cashPrefab, cashOutputParent);
            cash.transform.localPosition = targetLocalPos;
            cash.transform.localRotation = Quaternion.Euler(cashRotation);

            // 记录真实价值
            CashData cd = cash.GetComponent<CashData>();
            if (cd == null)
            {
                cd = cash.AddComponent<CashData>();
            }
            cd.value = value;

            // 弹出动画
            cash.transform.localScale = Vector3.zero;
            cash.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);

            activeCashList.Add(cash);
        }
        else
        {
            // 达到上限，直接把钱塞给最顶层的一张钞票
            GameObject lastCash = activeCashList[activeCashList.Count - 1];
            CashData cd = lastCash.GetComponent<CashData>();
            if (cd != null) cd.value += value;

            lastCash.transform.DOKill(true);
            lastCash.transform.DOPunchScale(Vector3.one * 0.3f, 0.2f);
        }
    }

    /// <summary>
    /// 供玩家拿走钞票，返回这叠钞票包含的真实价值
    /// </summary>
    public int TakeCashAndGetValue()
    {
        if (activeCashList.Count == 0) return 0;

        // 后进先出，从最高层/最后面开始拿
        int lastIndex = activeCashList.Count - 1;
        GameObject lastCash = activeCashList[lastIndex];
        activeCashList.RemoveAt(lastIndex);

        int moneyValue = 0;
        CashData cd = lastCash.GetComponent<CashData>();
        if (cd != null) moneyValue = cd.value;

        Destroy(lastCash);
        return moneyValue;
    }

    /// <summary>
    /// 供玩家吸收钞票使用，交出最后一张钞票的实体控制权
    /// </summary>
    public GameObject TakeCashEntity()
    {
        if (activeCashList.Count == 0) return null;

        int lastIndex = activeCashList.Count - 1;
        GameObject lastCash = activeCashList[lastIndex];
        activeCashList.RemoveAt(lastIndex); // 从收银台列表中除名

        return lastCash; // 交给玩家去处理吸收动画和销毁
    }
}