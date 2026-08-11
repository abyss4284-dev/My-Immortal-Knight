using UnityEngine;

public class FlameAnimationRelay : MonoBehaviour
{
    private PlayerSkillManager skillManager;

    void Awake()
    {
        // 自动向上寻找父物体（或组件所在物体）上的 PlayerSkillManager
        skillManager = GetComponentInParent<PlayerSkillManager>();

        if (skillManager == null)
        {
            Debug.LogError($"🚨 [{gameObject.name}] 未能在父物体上找到 PlayerSkillManager 脚本！");
        }
    }

    /// <summary>
    /// 🌟 动画事件方法：请在火焰爆裂动画的最后一帧绑定此方法
    /// </summary>
    public void OnRebirthAnimFinished()
    {
        if (skillManager != null)
        {
            skillManager.OnRebirthAnimFinished();
        }
        else
        {
            // 防护备用：如果没找到组件，尝试重新获取一次
            skillManager = GetComponentInParent<PlayerSkillManager>();
            if (skillManager != null)
            {
                skillManager.OnRebirthAnimFinished();
            }
        }
    }
}