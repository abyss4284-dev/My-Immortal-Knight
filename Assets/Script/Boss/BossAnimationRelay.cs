using UnityEngine;

/// <summary>
/// 动画事件中继器（挂载于 Visual 子物体）
/// 负责将 Animator 的 Animation Event 转发至最外层父物体的 Boss 控制器
/// </summary>
public class BossAnimationRelay : MonoBehaviour
{
    private BossAIController mainController;
    private BossPhaseTwoController phaseTwoController;

    private void Awake()
    {
        // 自动从父级（最外层）获取一阶段与二阶段的主控制器
        mainController = GetComponentInParent<BossAIController>();
        phaseTwoController = GetComponentInParent<BossPhaseTwoController>();
    }

    // ==========================================
    // ⚔️ 1. 攻击伤害判定事件 (Animation Event)
    // ==========================================

    /// <summary>
    /// 统一攻击伤害判定接口（绑定在 Attack 动画关键帧）
    /// 根据当前阶段自动转发至对应的控制器
    /// </summary>
    public void TriggerAttackDamage()
    {
        // 🌟 二阶段判定
        if (BossPhaseController.isPhaseTwoActive)
        {
            if (phaseTwoController == null)
            {
                phaseTwoController = GetComponentInParent<BossPhaseTwoController>();
            }

            if (phaseTwoController != null)
            {
                phaseTwoController.TriggerNewAttackDamage(); // 调用二阶段专属的新判定点逻辑
            }
            else
            {
                Debug.LogError("🚨 [BossAnimationRelay] 进入二阶段但未在父级找到 BossPhaseTwoController！");
            }
        }
        // 🌟 一阶段判定
        else
        {
            if (mainController != null)
            {
                mainController.TriggerAttackDamage();
            }
            else
            {
                Debug.LogError("🚨 [BossAnimationRelay] 未在父级找到 BossAIController！");
            }
        }
    }

    // 为兼容旧资源保留此方法，内部直接转接给统一方法
    public void TriggerNewAttackDamage()
    {
        TriggerAttackDamage();
    }

    // ==========================================
    // 🌀 2. 一阶段【传送刺杀】技能动画事件
    // ==========================================

    /// <summary>
    /// 绑定在一阶段 Disappear 消失动画【第一帧】
    /// </summary>
    public void OnDisappearStart()
    {
        if (mainController != null)
        {
            mainController.OnDisappearStart();
        }
    }

    /// <summary>
    /// 绑定在一阶段 Disappear 消失动画【最后一帧】
    /// </summary>
    public void OnDisappearEnd()
    {
        if (mainController != null)
        {
            mainController.OnDisappearEnd();
        }
    }
}