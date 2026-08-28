using System.Collections;
using UnityEngine;

/// <summary>
/// 钱堆(处理钱堆被玩家捡起时，自动飞向玩家并消失的逻辑)
/// 地面钞票“吸入玩家背包”动态特效 在 My Mini Mart 中，玩家收钱是最爽的时刻。钞票不应该瞬间消失，而应该像一块磁铁一样，先往上弹起，然后划出一条平滑的弧线飞向玩家的口袋。
/// </summary>

public class MoneyItem : MonoBehaviour
{
    [Header("吸入动效参数")]
    public float flyDuration = 0.4f;       // 飞行总时长
    public float popHeight = 0.8f;          // 被捡起时向上弹跳的高度（增加灵动感）

    private bool isCollected = false;

    // 触发检测：玩家走过来碰到钱堆
    private void OnTriggerEnter(Collider other)
    {
        if (isCollected) return;

        if (other.CompareTag("Player"))
        {
            isCollected = true;
            // 开始飞向玩家（传入玩家的 Transform）
            StartCoroutine(FlyToPlayerCoroutine(other.transform));
        }
    }

    private IEnumerator FlyToPlayerCoroutine(Transform playerTransform)
    {
        Vector3 startPosition = transform.position;
        float elapsedTime = 0f;

        // 禁用原有的碰撞体，防止二次触发
        if (TryGetComponent<Collider>(out var col)) col.enabled = false;

        while (elapsedTime < flyDuration)
        {
            if (playerTransform == null) break;

            elapsedTime += Time.deltaTime;
            float t = elapsedTime / flyDuration;

            // 核心算法：使用贝塞尔曲线或双曲线模拟“先弹起后吸入”
            // 在线形插值（Lerp）的基础上，给 Y 轴加一个由正弦波控制的向上弧度
            Vector3 currentTargetPos = playerTransform.position + Vector3.up * 0.5f; // 目标是玩家腰部

            // 基础线形移动
            Vector3 linearPos = Vector3.Lerp(startPosition, currentTargetPos, t);

            // 向上抛物线加成 (t=0时为0，t=0.5时达到最高，t=1时回到0)
            float arcY = Mathf.Sin(t * Mathf.PI) * popHeight;

            // 最终位置
            transform.position = new Vector3(linearPos.x, linearPos.y + arcY, linearPos.z);

            // 旋转动画：钞票在空中旋转飞过去
            transform.Rotate(Vector3.up * 720f * Time.deltaTime, Space.Self);

            yield return null;
        }

        // 飞行结束，到达玩家口袋
        CompleteCollection();
    }

    private void CompleteCollection()
    {
        // 【UI/钱包数据】在这里通知您的全局钱包管理器，增加金币
        // GameManager.Instance.AddMoney(1);

        // 播放一个清脆的“叮”收钱音效
        // AudioManager.Instance.Play("coin_collect");

        // 销毁物理钞票
        Destroy(gameObject);
    }
}