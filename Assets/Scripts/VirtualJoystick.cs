using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 动态虚拟摇杆：随点随现，松手隐藏
/// </summary>
public class VirtualJoystick : MonoBehaviour
{
    [Header("UI 引用")]
    [Tooltip("摇杆中心的控制球 (拖入 circle)")]
    public RectTransform handle;

    // 向外暴露的输入方向向量 (-1 到 1)
    public Vector2 InputDirection { get; private set; }

    private RectTransform background;
    private CanvasGroup canvasGroup;
    private float maxRadius;

    private bool isDragging = false;
    private Vector2 touchStartPos;

    private void Start()
    {
        background = GetComponent<RectTransform>();

        // 自动添加 CanvasGroup 组件，用于优雅地控制整个摇杆（背景+小球）的显示与隐藏
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // 计算摇杆的最大滑动半径 (背景宽度的一半)
        maxRadius = background.sizeDelta.x / 2f;

        // 游戏开始时默认隐藏摇杆
        HideJoystick();
    }

    private void Update()
    {
        // 1. 当手指按下屏幕 (或鼠标左键点击)
        if (Input.GetMouseButtonDown(0))
        {
            // 防呆判断：如果玩家点到了屏幕上的其他 UI 按钮（比如设置、商店），就不呼出摇杆
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                // 移动端安全兼容：确保只有点击非 UI 区域才生效
#if UNITY_EDITOR || UNITY_STANDALONE
                return;
#else
                    if (Input.touchCount > 0 && EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId)) return;
#endif
            }

            isDragging = true;
            touchStartPos = Input.mousePosition;

            // 瞬间将摇杆背景瞬移到手指按下的屏幕坐标位置
            background.position = touchStartPos;
            // 将中心小球归零到背景中心
            handle.anchoredPosition = Vector2.zero;

            ShowJoystick();
        }

        // 2. 当手指按住并拖拽时
        else if (Input.GetMouseButton(0) && isDragging)
        {
            Vector2 currentPos = Input.mousePosition;
            Vector2 delta = currentPos - touchStartPos;

            // 限制中心小球不能飞出背景圆圈的范围
            if (delta.magnitude > maxRadius)
            {
                delta = delta.normalized * maxRadius;
            }

            handle.anchoredPosition = delta;

            // 计算标准化的方向输出给 PlayerController (-1 到 1)
            InputDirection = delta / maxRadius;
        }

        // 3. 当手指松开抬起时
        else if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
            InputDirection = Vector2.zero;
            handle.anchoredPosition = Vector2.zero;

            HideJoystick();
        }
    }

    private void ShowJoystick()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f; // 显示可见
            canvasGroup.blocksRaycasts = true; // 可选：阻挡底层射线
        }
    }

    private void HideJoystick()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f; // 完全透明
            canvasGroup.blocksRaycasts = false; // 隐藏时不阻挡任何点击
        }
    }
}