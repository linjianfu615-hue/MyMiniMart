using UnityEngine;
using DG.Tweening;

/// <summary>
/// 垃圾桶管理器：接收玩家手里的废弃物品并销毁
/// </summary>
public class TrashBinManager : MonoBehaviour
{
    [Header("垃圾桶设置")]
    [Tooltip("物品飞向的中心点。可以在垃圾桶口处建一个空物体拖进来；如果不填，默认飞向垃圾桶底座中心。")]
    public Transform trashCenter;

    [Tooltip("物品抛入垃圾桶的动画时长")]
    public float throwAnimDuration = 0.3f;

    /// <summary>
    /// 接收玩家扔过来的垃圾（物品）
    /// </summary>
    public void ReceiveTrash(GameObject item)
    {
        // 1. 将物品的父节点设为垃圾桶（或指定的中心点）
        Transform targetParent = trashCenter != null ? trashCenter : transform;
        item.transform.SetParent(targetParent, true);

        // 2. 清除该物品身上可能残留的其他缩放/移动动画
        item.transform.DOKill();

        // 3. 【核心修改】：改用世界坐标系 DOJump，并把跳跃力度调大到 2f (你可以根据手感改为 1.5f 到 3f 之间)
        // 参数依次为：目标世界位置，跳跃高度(力度)，跳跃次数，动画时长
        item.transform.DOJump(targetParent.position, 2f, 1, throwAnimDuration);

        // 4. 伴随华丽的缩小和旋转，动画结束后彻底销毁 GameObject
        item.transform.DOLocalRotate(new Vector3(180, 360, 0), throwAnimDuration, RotateMode.FastBeyond360);
        item.transform.DOScale(Vector3.zero, throwAnimDuration)
            .SetEase(Ease.InBack)
            .OnComplete(() => Destroy(item));
    }
}