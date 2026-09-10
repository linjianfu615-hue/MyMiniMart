using UnityEngine;

/// <summary>
/// 广告牌效果：让世界空间的 3D UI 永远正视摄像机
/// </summary>
public class Billboard : MonoBehaviour
{
    private Camera mainCamera;

    void Start()
    {
        // 自动获取主相机
        mainCamera = Camera.main;
    }

    void LateUpdate()
    {
        if (mainCamera != null)
        {
            // 让 UI 的正方向与相机的正方向完全一致
            // 这样无论相机在什么角度，UI 永远是正对着屏幕的
            transform.forward = mainCamera.transform.forward;
        }
    }
}