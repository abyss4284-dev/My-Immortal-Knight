using UnityEngine;

/// <summary>
/// 挂载在传送门预制体（barragePortalPrefab）根节点或子节点上的控制脚本
/// </summary>
public class BarragePortal : MonoBehaviour
{
    [Header("=== 攻击设置 ===")]
    [Tooltip("挂载在传送门子物体上的攻击判定碰撞箱 (IsTrigger 勾选)")]
    public Collider2D attackCollider;
    [Tooltip("传送门攻击伤害")]
    public int portalDamage = 1;
    [Tooltip("玩家图层")]
    public LayerMask playerLayer;

    private void Awake()
    {
        // 若未在 Inspector 手动拖入，自动寻找名为 AttackPoint 的子物体碰撞箱
        if (attackCollider == null)
        {
            Transform foundAttackPoint = transform.Find("AttackPoint");
            if (foundAttackPoint != null)
            {
                attackCollider = foundAttackPoint.GetComponent<Collider2D>();
            }
            else
            {
                attackCollider = GetComponentInChildren<Collider2D>();
            }
        }
    }

    // ==========================================
    // 🎬 供传送门 Animator 调用的动画事件函数
    // ==========================================

    /// <summary>
    /// 🌟 绑定在传送门攻击/击中关键帧：通过子物体碰撞箱检测玩家并造成伤害
    /// </summary>
    public void TriggerPortalDamage()
    {
        if (attackCollider == null)
        {
            Debug.LogError("🚨 [BarragePortal] 未指定攻击碰撞盒 Collider2D！");
            return;
        }

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(playerLayer);
        filter.useLayerMask = true;

        Collider2D[] results = new Collider2D[5];
        int hitCount = attackCollider.Overlap(filter, results);

        for (int i = 0; i < hitCount; i++)
        {
            if (results[i] != null)
            {
                PlayerController playerCtrl = results[i].GetComponent<PlayerController>();
                if (playerCtrl != null)
                {
                    // 对玩家造成伤害
                    playerCtrl.TakeDamage(portalDamage);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// 🌟 绑定在传送门消失/销毁动画【最后一帧】
    /// </summary>
    public void OnPortalAnimEnd()
    {
        Destroy(gameObject);
    }
}