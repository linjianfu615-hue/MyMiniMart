using UnityEngine;
using DG.Tweening;

/// <summary>
/// 自动产出的机器 (西红柿树、小麦根、鸡舍等)，自己随时间产出物品。
/// 由于继承自 BaseProductionMachine，它已自动拥有注册到 FacilityManager 的能力。
/// </summary>
public class AutoProductionMachine : BaseProductionMachine
{
    [Header("自动生产设置 (Auto Production)")]
    public float productionInterval = 3f;
    private float timer;

    // 【新增】用于保存抖动动画的引用，方便随时播放和暂停
    private Tween shakeTween;

    private void Start()
    {
        // 【新增】在Start时创建一个无限循环的呼吸/抖动动画，并默认暂停
        // 这里的参数可以根据你的模型调整：如拉伸形变(DOScale) 或 旋转抖动(DOShakeRotation)
        shakeTween = transform.DOScale(new Vector3(1.05f, 0.95f, 1.05f), 0.4f)
            .SetLoops(-1, LoopType.Yoyo) // -1表示无限循环，Yoyo表示来回往复
            .SetEase(Ease.InOutSine)
            .Pause(); // 初始处于暂停状态
    }

    private void Update()
    {
        // 如果还没有达到最大容量，说明机器正在运转/生产
        if (readyProducts.Count < MaxCapacity)
        {
            // 如果抖动动画没在播放，就让它开始播放
            if (!shakeTween.IsPlaying())
            {
                shakeTween.Play();
            }

            timer += Time.deltaTime;
            if (timer >= productionInterval)
            {
                timer = 0f;
                OnProduce();
            }
        }
        else
        {
            // 【新增】如果装满了，停止生产，并停止抖动动画
            if (shakeTween.IsPlaying())
            {
                shakeTween.Pause();
                // 停止后让模型平滑恢复到原始比例 (1,1,1)
                transform.DOScale(Vector3.one, 0.2f);
            }

            timer = 0f;
        }
    }

    private void OnProduce()
    {
        // 生产产品
        GenerateProduct();
    }

    private void OnDestroy()
    {
        // 防止物体销毁时DOTween报错
        if (shakeTween != null) shakeTween.Kill();
    }
}