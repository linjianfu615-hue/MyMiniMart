using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 生产进度UI(处理生产机器的进度条显示)
/// </summary>

public class ProductionUI : MonoBehaviour
{
    public Slider progressSlider;
    public GameObject uiContainer; // 用于控制整个进度条的显示/隐藏

    private Camera mainCamera;

    private void Start()
    {
        mainCamera = Camera.main;
        if (progressSlider != null) progressSlider.value = 0f;
        if (uiContainer != null) uiContainer.SetActive(false);
    }

    private void LateUpdate()
    {
        // 核心细节：让 3D 世界中的 UI 始终正对摄像机（看板效应），防止旋转错位
        if (uiContainer.activeSelf && mainCamera != null)
        {
            transform.LookAt(transform.position + mainCamera.transform.forward);
        }
    }

    // 供机器脚本调用：更新进度
    public void UpdateProgress(float current, float max)
    {
        if (progressSlider == null || uiContainer == null) return;

        if (current <= 0 || current >= max)
        {
            uiContainer.SetActive(false); // 没在生产或生产结束时隐藏
        }
        else
        {
            uiContainer.SetActive(true);
            progressSlider.value = current / max;
        }
    }
}