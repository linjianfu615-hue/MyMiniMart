using UnityEngine;

/// <summary>
/// 交互区视觉标识，挂载在各个 InputCollider 或 OutCollider 上
/// </summary>
public class InteractZone : MonoBehaviour
{
    [Tooltip("需要进行 Q弹缩放动画 的视觉地板模型（如 Mat_Input_Gray）。\n如果不填（留空），则踩上去没有任何动画。")]
    public Transform floorVisual;
}