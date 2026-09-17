using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 独立的收银台管理器，用于管理属于自己的顾客队列和坐标点
/// </summary>
public class CheckoutCounter : MonoBehaviour
{
    [Header("收银台节点配置")]
    [Tooltip("顾客站立付款的位置 (payment_Point)")]
    public Transform paymentPoint;
    [Tooltip("打包纸箱生成的起点 (boxPoint)")]
    public Transform boxPoint;

    [Header("排队配置")]
    [Tooltip("排队时人与人之间的基础间距")]
    public float queueSpacing = 3.5f;

    // 属于这个特定收银台的排队队列
    [HideInInspector]
    public List<CustomerAIController> customerQueue = new List<CustomerAIController>();

    /// <summary>
    /// 顾客加入当前收银台队列
    /// </summary>
    public void JoinQueue(CustomerAIController customer)
    {
        if (!customerQueue.Contains(customer))
        {
            customerQueue.Add(customer);
        }
    }

    /// <summary>
    /// 顾客离开当前收银台队列
    /// </summary>
    public void LeaveQueue(CustomerAIController customer)
    {
        if (customerQueue.Contains(customer))
        {
            customerQueue.Remove(customer);
        }
    }

    /// <summary>
    /// 动态计算顾客在当前队列中的精确坐标位置（包含推车防穿模计算）
    /// </summary>
    public Vector3 GetQueuePosition(CustomerAIController customer)
    {
        int index = customerQueue.IndexOf(customer);

        // 如果还没入队（还在路上），以当前队尾为目标
        if (index == -1) index = customerQueue.Count;

        float offset = 0f;
        for (int i = 1; i <= index; i++)
        {
            float space = queueSpacing;

            // 判断当前占位的人是否有推车
            bool hasTrolley = (i < customerQueue.Count) ? customerQueue[i].HasTrolley : customer.HasTrolley;
            if (hasTrolley) space += 3.0f; // 推车额外间距

            offset += space;
        }

        // 沿着收银台 paymentPoint 的反向 right 延伸队伍
        return paymentPoint.position - paymentPoint.right * offset;
    }
}