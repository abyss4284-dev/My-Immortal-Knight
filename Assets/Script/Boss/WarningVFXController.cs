using UnityEngine;

public class WarningVFXController : MonoBehaviour
{
    private BossPhaseController bossPhaseCtrl;

    /// <summary>
    /// 🌟 初始化方法：由 Boss 在 Instantiate(生成) 它时调用，把 Boss 传进来
    /// </summary>
    public void Init(BossPhaseController controller)
    {
        bossPhaseCtrl = controller;
    }

    // ==========================================
    // 🌟 以下方法专门给【特效 Prefab 的动画关键帧】调用
    // ==========================================

    /// <summary>
    /// 动画事件 1：特效亮起/达到可反制帧时触发
    /// </summary>
    public void AE_OnWarningStart()
    {
        BossPhaseController.canBeCountered = true;
        Debug.Log("<color=green>⚡ [VFX 动画事件] 特效关键帧触发：反制窗口正式开启！</color>");
    }

    /// <summary>
    /// 动画事件 2：特效消退/超过反制判定帧时触发
    /// </summary>
    public void AE_OnWarningEnd()
    {
        BossPhaseController.canBeCountered = false;
        Debug.Log("<color=red>🔴 [VFX 动画事件] 特效关键帧触发：反制窗口关闭！</color>");
    }

    /// <summary>
    /// 动画事件 3：特效播放完最后一帧，自我销毁
    /// </summary>
    public void AE_DestroySelf()
    {
        Destroy(gameObject);
    }
}