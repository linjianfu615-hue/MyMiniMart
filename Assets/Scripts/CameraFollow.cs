using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;          // 玩家的 Transform
    public Vector3 offset = new Vector3(-8f, 14f, -8f); // 配合 X:60, Y:45 旋转的黄金偏移量
    public float smoothSpeed = 5f;    // 跟随平滑度

    void LateUpdate()
    {
        if (target == null) return;

        // 计算目标位置
        Vector3 targetPosition = target.position + offset;
        // 平滑插值过渡
        transform.position = Vector3.Lerp(transform.position, targetPosition, smoothSpeed * Time.deltaTime);
    }
}