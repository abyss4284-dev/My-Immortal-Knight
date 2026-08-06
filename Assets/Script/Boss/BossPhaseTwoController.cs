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
    [Tooltip("反击技能冷却时间（8 秒）")]
    public float counterBackstabCooldown = 8.0f;
    [Tooltip("消失后在异界隐身停顿等待的时间")]
    public float invisibleDuration = 0.15f;
    [Tooltip("反击背刺造成伤害时的硬直/后摇时间")]
    public float counterBackstabPostDelay = 1.0f;

    [Header("=== 动画参数配置 ===")]
    [Tooltip("二阶段传送门技能持续播放的 Bool 动画参数名称")]
    public string portalSkillBoolParam = "IsCastingPortalSkill";

    [Header("=== 二阶段死亡演出设置 ===")]
    [Tooltip("死亡慢动作缩放（0.2 表示正常速度的 20%，即 5 倍减速）")]
    public float deathSlowMotionScale = 0.2f;

    [Tooltip("场景白光过曝强度（如 3.5～5.0）")]
    public float whiteLightIntensity = 4.0f;

    [Tooltip("白光平滑过渡时间（秒，受慢动作影响会自动延长）")]
    public float whiteFadeDuration = 0.6f;

    [Tooltip("死亡播放完毕后激活的胜利 UI（可选）")]
    public GameObject victoryUI;

    // 内部组件引用
    private BossAIController bossAI;
    private Animator anim;
    private Transform playerTransform;

    private bool hasInitializedPhaseTwo = false;
    private float skillCooldownTimer = 0f;            // 传送门技能冷却计时器
    private float counterBackstabCooldownTimer = 0f;  // 受击反击冷却计时器

    // 🌟 排队与占线管理变量
    private bool isPortalSkillQueued = false;         // 传送门技能排队/占线等待标记

    // 受击反击技能内部状态变量
    private int currentHitCount = 0;             // 当前受击次数
    private int targetHitThreshold = 3;          // 随机触发阈值 (3-5)
    private bool isCounterBackstabReady = false; // 是否进入“受击预备反击”状态

    private void Awake()
    {
        bossAI = GetComponent<BossAIController>();
        anim = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        FindPlayer();
        ResetHitCounter();
        counterBackstabCooldownTimer = counterBackstabCooldown; // 初始状态设为冷却完毕
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

        // 🌟 1. 受击反击 CD 独立计算（无论 Boss 是否 Busy）
        if (counterBackstabCooldownTimer < counterBackstabCooldown)
        {
            counterBackstabCooldownTimer += Time.deltaTime;
        }

        // 🌟 2. 传送门技能 CD 累加
        skillCooldownTimer += Time.deltaTime;

        // 🌟 3. 检查 CD 是否已满：满 5 秒则推入“排队队列”
        if (skillCooldownTimer >= skillCooldown)
        {
            isPortalSkillQueued = true;
        }

        // 🌟 4. 队列消费检查：如果技能处于排队状态，且 Boss 空闲（!IsBusy），立刻释放！
        if (isPortalSkillQueued && bossAI != null && !bossAI.IsBusy)
        {
            ExecutePortalSkill();
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

        skillCooldownTimer = 0f;
        counterBackstabCooldownTimer = counterBackstabCooldown; // 刚进入二阶段时反击冷却可用
        ResetHitCounter();
        hasInitializedPhaseTwo = true;
        Debug.Log("🔥【BossPhaseTwoController】二阶段初始化完成！");
    }

    /// <summary>
    /// 🌟 供外部查询：是否有传送门技能正处于排队待释放状态
    /// </summary>
    public bool IsPortalSkillPending()
    {
        return isPortalSkillQueued || (skillCooldownTimer >= skillCooldown);
    }

    /// <summary>
    /// 🌟 触发并执行传送门技能（消费排队）
    /// </summary>
    public void ExecutePortalSkill()
    {
        isPortalSkillQueued = false; // 清除排队标记
        skillCooldownTimer = 0f;      // 重置 CD 计时器
        StartCoroutine(BarragePortalSkillRoutine());
    }

    /// <summary>
    /// 🌟 受击触发接口（由 Boss 的 TakeDamage 逻辑统一调用）
    /// </summary>
    public void OnBossTakeDamage()
    {
        if (!BossPhaseController.isPhaseTwoActive || BossPhaseController.isTransitioning) return;

        // 🌟 1. 正常进行受击计数累加（无论是否处于 CD，受击均正常计数）
        if (!isCounterBackstabReady)
        {
            currentHitCount++;
            Debug.Log($"💥 Boss 二阶段受击！当前次数: {currentHitCount} / {targetHitThreshold}");

            if (currentHitCount >= targetHitThreshold)
            {
                isCounterBackstabReady = true;
                Debug.Log("⚠️ Boss 已激活受击反击预备状态！一旦 CD 结束将随时打断并无预警背刺！");
            }
        }

        // 🌟 2. 检查反击条件：【预备就绪】+【反击 8 秒 CD 已满】
        if (isCounterBackstabReady && counterBackstabCooldownTimer >= counterBackstabCooldown)
        {
            Debug.Log("⚡【受击反击】条件满足！立即打断当前行为并进行闪现背刺！");

            // 1. 强行打断二阶段脚本及主 AI 的所有运行中协程（包括正在放的传送门）
            StopAllCoroutines();
            if (bossAI != null) bossAI.StopAllCoroutines();

            // 2. 清理传送门动画 Bool 状态
            if (anim != null)
            {
                anim.SetBool(portalSkillBoolParam, false);
            }

            // 3. 传送门 CD 重置：被打断后，传送门技能从此时重新计算 5 秒 CD
            skillCooldownTimer = 0f;

            // 4. 重置受击计数并进入 8s 反击 CD
            ResetHitCounter();
            counterBackstabCooldownTimer = 0f;

            // 5. 启动无预警背刺协程
            StartCoroutine(InstantCounterBackstabRoutine());
        }
        else if (isCounterBackstabReady)
        {
            Debug.Log($"⏳ 反击预备就绪，但反击技能冷却中... 剩余冷却: {(counterBackstabCooldown - counterBackstabCooldownTimer):F1}s");
        }
    }

    /// <summary>
    /// 重置受击计数，并重新生成 3-5 次的随机阈值
    /// </summary>
    private void ResetHitCounter()
    {
        currentHitCount = 0;
        isCounterBackstabReady = false;
        isPortalSkillQueued = false; // 受击打断时清空排队队列
        targetHitThreshold = Random.Range(3, 6); // 生成 3, 4 或 5
    }

    /// <summary>
    /// 🌟 技能 2：受击反击背刺协程（直接调用 BossAIController 的无预警背刺）
    /// </summary>
    private IEnumerator InstantCounterBackstabRoutine()
    {
        if (bossAI == null) yield break;

        yield return StartCoroutine(bossAI.ExecuteBackstabRoutine(withPortal: false));

        skillCooldownTimer = 0f; // 再次确保背刺结束后传送门 CD 处于正常重置状态
    }

    // ========================================================================
    // 🌟 动画事件（Animation Events）兼容转接函数
    // ========================================================================

    public void OnCounterDisappearStart()
    {
        if (bossAI != null)
        {
            bossAI.OnDisappearStart();
        }
    }

    public void OnCounterDisappearEnd()
    {
        if (bossAI != null)
        {
            bossAI.OnDisappearEnd();
        }
    }

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

        bossAI.IsBusy = true;

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        if (anim != null)
        {
            anim.SetFloat("Speed", 0f);
            anim.SetBool(portalSkillBoolParam, true);
        }

        yield return new WaitForSeconds(0.4f);

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

        if (anim != null)
        {
            anim.SetBool(portalSkillBoolParam, false);
        }

        bossAI.IsBusy = false;
        skillCooldownTimer = 0f; // 顺利完整释放完毕，传送门进入 5s 冷却
    }

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }
    }

    /// <summary>
    /// 🌟 死亡流程总入口（由 BossAIController 在 HP <= 0 时调用）
    /// </summary>
    public void StartDeathSequence()
    {
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        Debug.Log("💀【Boss】血量归零，开启二阶段死亡史诗演出！");

        // 1. 强行终止 Boss 主 AI 和二阶段的所有活动协程
        if (bossAI != null)
        {
            bossAI.StopAllCoroutines();
            bossAI.IsBusy = true;
            bossAI.enabled = false;
        }
        StopAllCoroutines();

        // 2. 禁用碰撞体与刚体，防止玩家继续攻击或发生物理挤压
        Collider2D bossCol = GetComponent<Collider2D>();
        if (bossCol != null) bossCol.enabled = false;

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false; // 冻结物理
        }

        // 3. 🌟 开启慢动作（子弹时间）
        Time.timeScale = deathSlowMotionScale;
        Time.fixedDeltaTime = 0.02f * Time.timeScale; // 保证物理/动画插值平滑

        // 4. 🌟 背景陷入白光（提高 Global Light2D 强度与颜色）
        UnityEngine.Rendering.Universal.Light2D globalLight = null;
        GameObject lanternObj = GameObject.Find("Lantern");
        if (lanternObj != null)
        {
            globalLight = lanternObj.GetComponentInChildren<UnityEngine.Rendering.Universal.Light2D>(true);
        }

        if (globalLight != null)
        {
            StartCoroutine(FadeToWhiteLight(globalLight, whiteLightIntensity, Color.white, whiteFadeDuration));
        }

        // 5. 🌟 播放消散动画（复用之前的 Disappear Trigger）
        if (anim != null)
        {
            anim.ResetTrigger("Disappear");
            anim.SetTrigger("Disappear");
        }

        // 6. 等待动画与停顿（受 Time.timeScale 影响，1.5 秒会被放大为慢动作特写）
        yield return new WaitForSeconds(1.5f);

        // 7. 🌟 恢复游戏正常时间缩放
        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = 0.02f;

        // 8. 场景恢复/清理 Boss
        if (bossAI != null && bossAI.visualTransform != null)
        {
            bossAI.visualTransform.gameObject.SetActive(false);
        }

        // 9. 弹出胜利界面
        if (victoryUI != null)
        {
            victoryUI.SetActive(true);
        }

        Debug.Log("🏆【Boss】已完全消散，战斗胜利！");
        gameObject.SetActive(false); // 彻底隐藏 Boss 游戏物体
    }

    /// <summary>
    /// 平滑过度场景灯光至高亮纯白
    /// </summary>
    private IEnumerator FadeToWhiteLight(UnityEngine.Rendering.Universal.Light2D light, float targetIntensity, Color targetColor, float duration)
    {
        float startIntensity = light.intensity;
        Color startColor = light.color;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime; // 自动受 timeScale 影响
            float t = elapsed / duration;

            light.intensity = Mathf.Lerp(startIntensity, targetIntensity, t);
            light.color = Color.Lerp(startColor, targetColor, t);

            yield return null;
        }

        light.intensity = targetIntensity;
        light.color = targetColor;
    }
}