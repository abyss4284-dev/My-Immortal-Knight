using UnityEngine;
using System.Collections;

/// <summary>
/// 二阶段独立控制脚本
/// 职责：激活强化攻击点、切换二阶段动画控制器、释放二阶段全新技能、受击反击背刺
/// </summary>
public class BossPhaseTwoController : MonoBehaviour
{
    [Header("=== 二阶段动画控制器 ===")]
    [Tooltip("二阶段专属的 RuntimeAnimatorController")]
    public RuntimeAnimatorController phaseTwoController;

    [Header("=== 二阶段攻击判定修改 ===")]
    [Tooltip("一阶段原有的攻击判定点（将被禁用）")]
    public GameObject oldAttackPointObject;
    [Tooltip("二阶段使用的包含碰撞箱的新攻击判定点子物体")]
    public GameObject newAttackPointObject;
    private Collider2D newAttackCollider;

    [Tooltip("普通攻击伤害")]
    public int attackDamage = 1;
    [Tooltip("玩家图层")]
    public LayerMask playerLayer;

    [Header("=== 二阶段技能 1：弹幕传送门 ===")]
    [Tooltip("传送门预制体（内含弹幕攻击判定与动画，由自身动画事件控制销毁）")]
    public GameObject barragePortalPrefab;
    [Tooltip("技能冷却时间：未发动技能 5s 后使用")]
    public float skillCooldown = 5.0f;
    [Tooltip("传送门攻击次数")]
    public int portalAttackCount = 5;
    [Tooltip("每次传送门生成之间的间隔时间（秒）")]
    public float timeBetweenPortals = 0.8f;
    [Tooltip("传送门相对玩家头顶的偏移高度")]
    public float portalUpOffset = 2.5f;

    [Header("=== 二阶段技能 2：受击反击（无预警背刺） ===")]
    [Tooltip("反击背刺造成伤害时的硬直/后摇时间")]
    public float counterBackstabPostDelay = 1.0f;
    [Tooltip("消失后在异界隐身停顿等待的时间")]
    public float invisibleDuration = 0.15f;

    [Header("=== 动画参数配置 ===")]
    [Tooltip("二阶段传送门技能持续播放的 Bool 动画参数名称")]
    public string portalSkillBoolParam = "IsCastingPortalSkill";

    // 内部组件引用
    private BossAIController bossAI;
    private Animator anim;
    private Transform playerTransform;

    private bool hasInitializedPhaseTwo = false;
    private float skillCooldownTimer = 0f; // 传送门技能冷却计时器

    // 受击反击技能内部状态变量
    private int currentHitCount = 0;             // 当前受击次数
    private int targetHitThreshold = 3;          // 随机触发阈值 (3-5)
    private bool isCounterBackstabReady = false; // 是否进入“下次受击必定触发反击”状态

    private void Awake()
    {
        bossAI = GetComponent<BossAIController>();
        anim = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        FindPlayer();
        ResetHitCounter();
    }

    private void Update()
    {
        if (!BossPhaseController.isPhaseTwoActive || BossPhaseController.isTransitioning)
        {
            return;
        }

        if (!hasInitializedPhaseTwo)
        {
            InitializePhaseTwo();
        }

        if (playerTransform == null)
        {
            FindPlayer();
            if (playerTransform == null) return;
        }

        // 🌟 如果主 AI 处于 busy 状态（正在移动/普通攻击/处于背刺/正在放传送门等），不累加冷却
        if (bossAI != null && bossAI.IsBusy)
        {
            return;
        }

        // 🌟 仅在 Boss 处于空闲（非 Busy）时累加冷却时间
        skillCooldownTimer += Time.deltaTime;

        // 🌟 满 5 秒未发动技能，触发传送门技能
        if (skillCooldownTimer >= skillCooldown)
        {
            StartCoroutine(BarragePortalSkillRoutine());
        }
    }

    private void InitializePhaseTwo()
    {
        // 1. 替换二阶段 Animator Controller
        if (anim != null && phaseTwoController != null)
        {
            anim.runtimeAnimatorController = phaseTwoController;
        }

        // 2. 切换攻击判定点
        if (oldAttackPointObject != null)
        {
            oldAttackPointObject.SetActive(false);
        }

        if (newAttackPointObject != null)
        {
            newAttackPointObject.SetActive(true);
            newAttackCollider = newAttackPointObject.GetComponent<Collider2D>();

            // 🌟 将新攻击点注册到 BossAIController 中，并刷新一次朝向镜像
            if (bossAI != null)
            {
                bossAI.RegisterPhaseTwoAttackPoint(newAttackPointObject);
                bossAI.Invoke("ApplyFacing", 0f);
            }
        }

        // 3. 确保碰撞体和 Visual 显示
        if (bossAI != null)
        {
            Collider2D bossCol = GetComponent<Collider2D>();
            if (bossCol != null) bossCol.enabled = true;

            if (bossAI.visualTransform != null)
            {
                bossAI.visualTransform.gameObject.SetActive(true);
            }

            bossAI.ResetSkillTimer();
        }

        skillCooldownTimer = 0f; // 刚进入二阶段时重置计时
        ResetHitCounter();
        hasInitializedPhaseTwo = true;
        Debug.Log("🔥【BossPhaseTwoController】二阶段初始化完成！");
    }

    /// <summary>
    /// 🌟 受击触发接口（由 Boss 的 TakeDamage 逻辑统一调用）
    /// </summary>
    public void OnBossTakeDamage()
    {
        if (!BossPhaseController.isPhaseTwoActive || BossPhaseController.isTransitioning) return;

        // 🌟 状态 1：当前已处于“准备反击”状态，下一次受击立即打断并背刺
        if (isCounterBackstabReady)
        {
            Debug.Log("⚡【受击反击】触发！立即打断当前行为并进行闪现背刺！");

            // 1. 强行打断二阶段脚本及主 AI 的所有运行中协程
            StopAllCoroutines();
            if (bossAI != null) bossAI.StopAllCoroutines();

            // 2. 清理传送门动画 Bool 状态，确保动画状态机复位
            if (anim != null)
            {
                anim.SetBool(portalSkillBoolParam, false);
            }

            // 3. 重置受击计数并启动无预警背刺协程
            ResetHitCounter();
            StartCoroutine(InstantCounterBackstabRoutine());
            return;
        }

        // 🌟 状态 2：常规受击计数累加
        currentHitCount++;
        Debug.Log($"💥 Boss 二阶段受击！当前次数: {currentHitCount} / {targetHitThreshold}");

        if (currentHitCount >= targetHitThreshold)
        {
            isCounterBackstabReady = true;
            Debug.Log("⚠️ Boss 已激活受击反击预备状态！下一次受击将强制打断并无预警背刺！");
        }
    }

    /// <summary>
    /// 重置受击计数，并重新生成 3-5 次的随机阈值
    /// </summary>
    private void ResetHitCounter()
    {
        currentHitCount = 0;
        isCounterBackstabReady = false;
        targetHitThreshold = Random.Range(3, 6); // 生成 3, 4 或 5
    }

    /// <summary>
    /// 🌟 技能 2：无预警/无传送门受击反击背刺协程
    /// </summary>
    private IEnumerator InstantCounterBackstabRoutine()
    {
        if (bossAI == null) yield break;

        bossAI.IsBusy = true; // 锁定状态

        // 1. 清空移动速度，停止当前移动
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        // 2. 隐藏 Visual 与关闭碰撞，播放 Disappear 动画
        Collider2D bossCol = GetComponent<Collider2D>();
        if (bossCol != null) bossCol.enabled = false;

        if (anim != null)
        {
            anim.SetTrigger("Disappear");
        }

        if (bossAI.visualTransform != null)
        {
            bossAI.visualTransform.gameObject.SetActive(false);
        }

        // 3. 隐身停顿
        if (invisibleDuration > 0f)
        {
            yield return new WaitForSeconds(invisibleDuration);
        }

        // 4. 瞬间传送至玩家身后（借用 BossAIController 中定义的偏移逻辑，但不开启预警传送门）
        if (playerTransform != null)
        {
            PlayerController playerCtrl = playerTransform.GetComponent<PlayerController>();
            float playerFacingDir = (playerCtrl != null && playerCtrl.facingDirectionParam == "left") ? -1f : 1f;

            // 转向面对玩家
            bool targetFacingRight = (playerTransform.position.x > transform.position.x);
            System.Reflection.FieldInfo facingField = typeof(BossAIController).GetField("isFacingRight", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (facingField != null) facingField.SetValue(bossAI, targetFacingRight);
            bossAI.ApplyFacing();

            // 计算 Pivot 相对根物体的偏移
            float pivotOffset = 0f;
            if (bossAI.pivotTransform != null)
            {
                pivotOffset = bossAI.pivotTransform.position.x - transform.position.x;
            }

            // 目标背刺位置计算
            float targetPivotX = playerTransform.position.x - (playerFacingDir * bossAI.offsetBehindPlayer);
            float targetBossX = targetPivotX - pivotOffset;

            // 瞬间改变坐标
            transform.position = new Vector3(targetBossX, transform.position.y, transform.position.z);
        }

        // 5. 重新显形并恢复碰撞箱
        if (bossAI.visualTransform != null)
        {
            bossAI.visualTransform.gameObject.SetActive(true);
        }
        if (bossCol != null) bossCol.enabled = true;

        // 6. 立即触发攻击动画与伤害判定
        if (anim != null)
        {
            anim.ResetTrigger("Attack");
            anim.SetTrigger("Attack");
        }

        yield return new WaitForSeconds(counterBackstabPostDelay); // 背刺后摇

        bossAI.IsBusy = false; // 解锁 AI 状态
        skillCooldownTimer = 0f; // 重置传送门技能冷却，防止刚背刺完立刻接着使用传送门
    }

    /// <summary>
    /// 触发二阶段新攻击点伤害判定（通过动画事件 Animation Event 调用）
    /// </summary>
    public void TriggerNewAttackDamage()
    {
        if (newAttackCollider == null) return;

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(playerLayer);
        filter.useLayerMask = true;

        Collider2D[] results = new Collider2D[5];
        int hitCount = newAttackCollider.Overlap(filter, results);

        for (int i = 0; i < hitCount; i++)
        {
            if (results[i] != null)
            {
                PlayerController playerCtrl = results[i].GetComponent<PlayerController>();
                if (playerCtrl != null)
                {
                    playerCtrl.TakeDamage(attackDamage);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// 🌟 技能 1：二阶段传送门弹幕攻击（连续 5 次）
    /// </summary>
    private IEnumerator BarragePortalSkillRoutine()
    {
        if (bossAI == null) yield break;

        // 1. 锁定 Boss AI 状态
        bossAI.IsBusy = true;

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        // 2. 播放技能持续动画（使用 Bool 控制）
        if (anim != null)
        {
            anim.SetFloat("Speed", 0f);
            anim.SetBool(portalSkillBoolParam, true);
        }

        yield return new WaitForSeconds(0.4f);

        // 3. 循环生成 5 次传送门攻击
        for (int i = 0; i < portalAttackCount; i++)
        {
            if (playerTransform != null && barragePortalPrefab != null)
            {
                Vector3 spawnPos = new Vector3(
                    playerTransform.position.x,
                    playerTransform.position.y + portalUpOffset,
                    playerTransform.position.z
                );
                Instantiate(barragePortalPrefab, spawnPos, Quaternion.identity);
            }

            yield return new WaitForSeconds(timeBetweenPortals);
        }

        yield return new WaitForSeconds(0.5f);

        // 4. 退出持续动画状态
        if (anim != null)
        {
            anim.SetBool(portalSkillBoolParam, false);
        }

        // 5. 解锁 Boss AI 状态
        bossAI.IsBusy = false;

        // 6. 技能完整结束后重新开始 5s 冷却
        skillCooldownTimer = 0f;
    }

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }
    }
}