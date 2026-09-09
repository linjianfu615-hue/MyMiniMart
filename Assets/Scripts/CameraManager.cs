using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraManager : MonoBehaviour
{
    [Header("跟随目标")]
    [Tooltip("把你的玩家 (Player) 拖到这里")]
    public Transform target;

    [Tooltip("跟随的平滑度。数值越大跟得越紧，如果设为 0 则瞬间锁定在屏幕绝对正中心")]
    public float smoothSpeed = 15f;

    [Header("偏移设置")]
    [Tooltip("勾选后，游戏开始时会自动把你当前在场景里摆放的摄像机位置作为最佳偏移量，免去手动填数字的烦恼！")]
    public bool autoCalculateOffset = true;
    public Vector3 offset;

    [Header("多分辨率/横竖屏自适应")]
    [Tooltip("是否开启自动适配屏幕比例")]
    public bool adaptToScreenRatio = true;

    [Tooltip("你开发时参考的基础比例 (通常横屏是 16/9，竖屏是 9/16)")]
    public float referenceAspect = 16f / 9f;

    [Tooltip("摄像机的基础视野大小 (如果是正交相机填 Orthographic Size，透视相机填 FOV)")]
    public float baseSizeOrFOV = 8f;

    private Camera cam;
    private Vector3 desiredPosition;
    private float currentAspect;

    void Start()
    {
        cam = GetComponent<Camera>();

        // 自动计算偏移量，让你在 Editor 里怎么摆，运行后就怎么跟
        if (target != null && autoCalculateOffset)
        {
            offset = transform.position - target.position;
        }

        AdjustCameraScale();
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 1. 处理跟随逻辑
        desiredPosition = target.position + offset;

        if (smoothSpeed > 0)
        {
            // 平滑跟随
            transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        }
        else
        {
            // 绝对锁定在正中心 (硬跟随)
            transform.position = desiredPosition;
        }

        // 2. 实时监测屏幕比例变化 (适配手机旋转横竖屏或拉伸窗口)
        if (adaptToScreenRatio && Mathf.Abs(currentAspect - (float)Screen.width / Screen.height) > 0.01f)
        {
            AdjustCameraScale();
        }
    }

    /// <summary>
    /// 核心算法：保证在窄屏（如竖屏）下，依然能看到足够宽的画面
    /// </summary>
    private void AdjustCameraScale()
    {
        currentAspect = (float)Screen.width / Screen.height;

        // 如果当前屏幕比参考屏幕还要窄（比如从横屏变成了竖屏）
        if (currentAspect < referenceAspect)
        {
            // 计算需要放大的倍数
            float ratio = referenceAspect / currentAspect;

            if (cam.orthographic)
            {
                cam.orthographicSize = baseSizeOrFOV * ratio;
            }
            else
            {
                cam.fieldOfView = baseSizeOrFOV * ratio;
            }
        }
        else
        {
            // 如果屏幕足够宽（比如超宽屏），保持基础大小不变即可
            if (cam.orthographic)
            {
                cam.orthographicSize = baseSizeOrFOV;
            }
            else
            {
                cam.fieldOfView = baseSizeOrFOV;
            }
        }
    }
}