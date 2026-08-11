using UnityEngine;
using System.Collections;

public class BossAIController : MonoBehaviour
{
    public static bool isBoss = true;
    [Header("=== 基础属性 ===")]
    public static int maxHealth = 500;
    public static int currentHealth;
    public int attackDamage = 1;         // 普通攻击伤害
    public float moveSpeed = 3f;          // 追击移动速度

    [Header("=== 追击与攻击范围 ===")]
    public Transform attackPoint;         // 判定击中的子物体（需挂载带 IsTrigger 的 Collider2D）
    public float attackRadius = 0.8f;     // 攻击伤害判定圆圈半径
    public LayerMask playerLayer;         // 玩家图层

    [Header("=== 独立轴心点转向设置 ===")]
    public Transform visualTransform;    // 贴图/视觉子物体 Visual
    public Transform pivotTransform;     // 作为镜像中轴线的空物体 Pivot
    [Tooltip("预制件素材的初始默认朝向（如果预制件面向左，请取消勾选此项！）")]
    public bool defaultFacingRight = false; // 预制件默认朝左则为 false

    [Header("=== 普通攻击时间与硬直 ===")]
    public float attackPostDelay = 1.0f;  // 攻击结束后的强制停顿时间（后摇）
    public float attackCooldown = 1.5f;   // 两次普通攻击之间的最小间隔

    [Header("=== 新技能：传送刺杀 (Teleport Attack) ===")]
    public GameObject portalObject;       // 传送门子物体
    public float skillCooldown = 10f;     // 技能最长冷却时间（10s内未使用则强制触发）

    [Tooltip("Boss 完全隐身消失后，在异界停顿等待的时间（之后重新现身）")]
    public float invisibleDuration = 0.1f;// Boss 隐身消失后的停留延迟时间

    public float skillPostDelay = 1.2f;   // 技能攻击后的硬直/后摇时间

    [Header("=== 传送/背刺偏移设置 ===")]
    public float offsetBehindPlayer = 2.0f;// 出现在玩家背后的 X 轴水平距离
    public float offsetUpPlayer = 1.8f;    // 相对玩家向上偏移的 Y 轴高度

    // 🌟 控制当前背刺过程是否生成传送门（默认为 true，二阶段受击反击时会被置为 false）
    [HideInInspector]
    public bool usePortalForTeleport = true;

    // 内部组件自动抓取
    private Transform playerTransform;
    private Animator anim;
    private Rigidbody2D rb;
    private Collider2D bossCollider;     // Boss 自身的物理碰撞器

    private bool isBusy = false;          // 核心状态锁（包含攻击与后摇硬直）
    public bool IsBusy
    {
        get => isBusy;
        set => isBusy = value;
    }

    private bool isFacingRight;
    private bool isPlayerInTrigger = false;// 标记玩家是否真实处于 Trigger 攻击范围内

    private int missedAttackCount = 0;    // 连续未击中计数器
    private float skillTimer = 0f;        // 技能冷却计时器
    private bool disappearAnimFinished = false; // 动画播放完毕标记

    private float attackPointOffsetFromPivotX;
    private float originalGravity; // 保存初始重力

    // 在 BossAIController 类中增加对二阶段攻击点的动态偏移计算变量
    private float newAttackPointOffsetFromPivotX;
    private bool isNewAttackPointInitialized = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponentInChildren<Animator>();
        bossCollider = GetComponent<Collider2D>();

        if (visualTransform == null)
        {
            Transform foundVisual = transform.Find("Visual");
            if (foundVisual != null) visualTransform = foundVisual;
        }

        if (pivotTransform == null)
        {
            Transform foundPivot = transform.Find("Pivot");
            if (foundPivot != null) pivotTransform = foundPivot;
        }

        if (portalObject == null)
        {
            Transform foundPortal = transform.Find("Portal");
            if (foundPortal != null) portalObject = foundPortal.gameObject;
        }

        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            originalGravity = rb.gravityScale;
        }

        if (attackPoint != null && pivotTransform != null)
        {
            attackPointOffsetFromPivotX = Mathf.Abs(attackPoint.position.x - pivotTransform.position.x);
        }

        if (portalObject != null)
        {
            portalObject.SetActive(false);
        }
    }

    private void Start()
    {
        currentHealth = maxHealth;
        FindPlayer();
    }

    private void Update()
    {
        // 🌟 1. 转阶段期间阻断常规 AI
        if (BossPhaseController.isTransitioning)
        {
            return;
        }

        // 🌟 2. 动态刷新玩家引用
        if (playerTransform == null)
        {
            FindPlayer();
            if (playerTransform == null) return;
        }

        // 处于硬直/后摇状态（包括二阶段放技能）时，冻结移动
        if (isBusy)
        {
            if (rb != null)
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
            if (anim != null)
            {
                anim.SetFloat("Speed", 0f);
            }
            return;
        }

        // 🌟 3. 一阶段专属：背刺技能计时与触发判定（二阶段跳过此逻辑）
        if (!BossPhaseController.isPhaseTwoActive)
        {
            skillTimer += Time.deltaTime;

            // 检查一阶段背刺技能触发条件（附带传送门预警）
            if (missedAttackCount >= 3 || skillTimer >= skillCooldown)
            {
                StartCoroutine(ExecuteBackstabRoutine(withPortal: true));
                return;
            }
        }

        // 🌟 4. 二阶段技能排队拦截检查
        if (BossPhaseController.isPhaseTwoActive)
        {
            BossPhaseTwoController phaseTwo = GetComponent<BossPhaseTwoController>();
            if (phaseTwo != null && phaseTwo.IsPortalSkillPending())
            {
                // 如果二阶段技能处于排队等待状态，阻断普通攻击和追击，让位给 BossPhaseTwoController 的 Update 消费队列
                return;
            }
        }

        // 🌟 5. 通用：攻击与追击分支（一阶段和二阶段共享）
        if (isPlayerInTrigger)
        {
            StartCoroutine(AttackRoutine());
        }
        else
        {
            ChasePlayer();
        }
    }

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            isFacingRight = (playerTransform.position.x > transform.position.x);
            ApplyFacing();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) != 0 || other.CompareTag("Player"))
        {
            isPlayerInTrigger = true;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) != 0 || other.CompareTag("Player"))
        {
            isPlayerInTrigger = false;
        }
    }

    private void ChasePlayer()
    {
        Vector2 direction = (playerTransform.position - transform.position).normalized;

        rb.linearVelocity = new Vector2(direction.x * moveSpeed, rb.linearVelocity.y);

        if (anim != null)
        {
            anim.SetFloat("Speed", Mathf.Abs(moveSpeed));
        }

        if (direction.x > 0.05f && !isFacingRight)
        {
            Flip();
        }
        else if (direction.x < -0.05f && isFacingRight)
        {
            Flip();
        }
    }

    private IEnumerator AttackRoutine()
    {
        isBusy = true;

        if (rb != null)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }

        if (anim != null)
        {
            anim.SetFloat("Speed", 0f);
            anim.ResetTrigger("Attack");
            anim.SetTrigger("Attack");
        }

        Debug.Log("⚔️ Boss 发动普通攻击！");

        yield return new WaitForSeconds(attackPostDelay);

        float remainingCooldown = attackCooldown - attackPostDelay;
        if (remainingCooldown > 0)
        {
            yield return new WaitForSeconds(remainingCooldown);
        }

        isBusy = false;
    }

    /// <summary>
    /// 🌟 通用背刺技能协程（支持选择是否生成预警传送门）
    /// </summary>
    /// <param name="withPortal">是否生成传送门预警（true:一阶段背刺, false:二阶段无预警背刺）</param>
    public IEnumerator ExecuteBackstabRoutine(bool withPortal = true)
    {
        isBusy = true;
        disappearAnimFinished = false;
        usePortalForTeleport = withPortal;

        Debug.Log($"🌀 Boss 触发背刺！[(带有传送门: {withPortal})]");

        missedAttackCount = 0;
        skillTimer = 0f;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        if (anim != null)
        {
            anim.SetFloat("Speed", 0f);
            anim.ResetTrigger("Disappear");
            anim.SetTrigger("Disappear");
        }

        yield return new WaitUntil(() => disappearAnimFinished);

        if (invisibleDuration > 0)
        {
            yield return new WaitForSeconds(invisibleDuration);
        }

        if (visualTransform != null)
        {
            visualTransform.gameObject.SetActive(true);
        }

        if (bossCollider != null)
        {
            bossCollider.enabled = true;
        }

        if (playerTransform != null)
        {
            isFacingRight = (playerTransform.position.x > transform.position.x);
            ApplyFacing();
        }

        if (anim != null)
        {
            anim.ResetTrigger("Attack");
            anim.SetTrigger("Attack");
        }
        else
        {
            TriggerSkillAttackDamage();
        }

        yield return new WaitForSeconds(skillPostDelay);

        usePortalForTeleport = true; // 重置开关标志
        isBusy = false;
    }

    // ========================================================================
    // 🌟 动画事件（Animation Events）回调函数
    // ========================================================================

    /// <summary>
    /// 【动画事件】Disappear 动画第 1 帧调用
    /// </summary>
    public void OnDisappearStart()
    {
        if (bossCollider != null)
        {
            bossCollider.enabled = false;
        }

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
        }

        if (playerTransform == null) return;

        // 🌟 如果不开启传送门，或者处于转阶段状态，强行隐藏传送门并直接返回
        if (!usePortalForTeleport || BossPhaseController.isTransitioning)
        {
            if (portalObject != null)
            {
                portalObject.SetActive(false);
            }
            return;
        }

        // 常规生成预警传送门
        PlayerController playerCtrl = playerTransform.GetComponent<PlayerController>();
        float playerFacingDir = 1f;
        if (playerCtrl != null)
        {
            playerFacingDir = (playerCtrl.facingDirectionParam == "left") ? -1f : 1f;
        }

        float portalX = playerTransform.position.x - (playerFacingDir * offsetBehindPlayer);
        float portalY = playerTransform.position.y + offsetUpPlayer;
        Vector3 portalSpawnPos = new Vector3(portalX, portalY, playerTransform.position.z);

        if (portalObject != null)
        {
            portalObject.transform.position = portalSpawnPos;
            portalObject.transform.rotation = Quaternion.identity;
            portalObject.SetActive(true);
        }
    }

    /// <summary>
    /// 【动画事件】Disappear 动画最后一帧调用
    /// </summary>
    public void OnDisappearEnd()
    {
        // 1. 计算 Pivot 相对根物体的偏移
        float pivotOffset = 0f;
        if (pivotTransform != null)
        {
            pivotOffset = pivotTransform.position.x - transform.position.x;
        }

        // 🌟 模式 A：无传送门模式（二阶段受击无预警背刺，或转阶段）
        if (!usePortalForTeleport || BossPhaseController.isTransitioning)
        {
            if (playerTransform != null)
            {
                PlayerController playerCtrl = playerTransform.GetComponent<PlayerController>();
                float playerFacingDir = (playerCtrl != null && playerCtrl.facingDirectionParam == "left") ? -1f : 1f;

                // 直接根据玩家位置计算目标背刺点（以 Pivot 轴心对齐）
                float targetPivotX = playerTransform.position.x - (playerFacingDir * offsetBehindPlayer);
                float targetBossX = targetPivotX - pivotOffset;

                transform.position = new Vector3(targetBossX, transform.position.y, transform.position.z);

                // 传送后修正朝向面对玩家
                isFacingRight = (playerTransform.position.x > transform.position.x);
                ApplyFacing();
            }

            if (portalObject != null)
            {
                portalObject.SetActive(false);
            }
        }
        // 🌟 模式 B：带传送门模式（一阶段带预警背刺）
        else
        {
            if (portalObject != null)
            {
                float portalX = portalObject.transform.position.x;

                // 使用 Pivot 轴心点对齐传送门中心
                float targetBossX = portalX - pivotOffset;
                transform.position = new Vector3(targetBossX, transform.position.y, transform.position.z);
                portalObject.SetActive(false);
            }

            if (playerTransform != null)
            {
                isFacingRight = (playerTransform.position.x > transform.position.x);
                ApplyFacing();
            }
        }

        if (visualTransform != null)
        {
            visualTransform.gameObject.SetActive(false);
        }

        disappearAnimFinished = true;
    }

    public void TriggerAttackDamage()
    {
        if (attackPoint == null) return;

        Collider2D hitPlayer = Physics2D.OverlapCircle(attackPoint.position, attackRadius, playerLayer);

        if (hitPlayer != null)
        {
            PlayerController playerCtrl = hitPlayer.GetComponent<PlayerController>();
            if (playerCtrl != null)
            {
                playerCtrl.TakeDamage(attackDamage);
                missedAttackCount = 0;
            }
        }
        else
        {
            missedAttackCount++;
        }
    }

    private void TriggerSkillAttackDamage()
    {
        if (attackPoint == null) return;

        Collider2D hitPlayer = Physics2D.OverlapCircle(attackPoint.position, attackRadius, playerLayer);

        if (hitPlayer != null)
        {
            PlayerController playerCtrl = hitPlayer.GetComponent<PlayerController>();
            if (playerCtrl != null)
            {
                int doubleDamage = attackDamage * 2;
                playerCtrl.TakeDamage(doubleDamage);
            }
        }
    }

    public void TakeDamage(int damage)
    {
        if (currentHealth <= 0) return; // 防止重复触发死亡

        currentHealth -= damage;
        Debug.Log($"Boss 收到伤害: {damage}，当前血量: {currentHealth}");

        // 🌟 1. 检查是否死亡
        if (currentHealth <= 0)
        {
            currentHealth = 0;

            // 如果处于二阶段，交由二阶段控制器处理死亡演出
            if (BossPhaseController.isPhaseTwoActive)
            {
                BossPhaseTwoController phaseTwo = GetComponent<BossPhaseTwoController>();
                if (phaseTwo != null)
                {
                    phaseTwo.StartDeathSequence();
                }
            }
            return;
        }

        // 🌟 2. 未死亡，正常通知二阶段处理受击反击
        BossPhaseTwoController phaseTwoCtrl = GetComponent<BossPhaseTwoController>();
        if (phaseTwoCtrl != null)
        {
            phaseTwoCtrl.OnBossTakeDamage();
        }
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;
        ApplyFacing();
    }

    /// <summary>
    /// 注册二阶段攻击点（仅提取水平 X 轴偏移量）
    /// </summary>
    public void RegisterPhaseTwoAttackPoint(GameObject newAttackPoint)
    {
        if (newAttackPoint != null && pivotTransform != null)
        {
            newAttackPointOffsetFromPivotX = Mathf.Abs(newAttackPoint.transform.position.x - pivotTransform.position.x);
            isNewAttackPointInitialized = true;
        }
    }

    public void ApplyFacing()
    {
        bool isCurrentFacingRight = (isFacingRight == defaultFacingRight);
        float facingDirection = isFacingRight ? 1f : -1f;

        // 1. 贴图/视觉子物体（Visual）翻转
        if (visualTransform != null)
        {
            float desiredScaleX = isCurrentFacingRight ? Mathf.Abs(visualTransform.localScale.x) : -Mathf.Abs(visualTransform.localScale.x);

            if (pivotTransform != null)
            {
                if (Mathf.Sign(visualTransform.localScale.x) != Mathf.Sign(desiredScaleX))
                {
                    float pivotX = pivotTransform.position.x;
                    float currentVisualX = visualTransform.position.x;

                    float newVisualX = 2f * pivotX - currentVisualX;

                    Vector3 currentPos = visualTransform.position;
                    visualTransform.position = new Vector3(newVisualX, currentPos.y, currentPos.z);
                }
            }

            Vector3 scale = visualTransform.localScale;
            scale.x = desiredScaleX;
            visualTransform.localScale = scale;
        }

        // 2. 一阶段攻击点（AttackPoint）X 轴镜像
        if (attackPoint != null && pivotTransform != null)
        {
            float targetWorldX = pivotTransform.position.x + (attackPointOffsetFromPivotX * facingDirection);
            attackPoint.position = new Vector3(targetWorldX, attackPoint.position.y, attackPoint.position.z);
        }

        // 3. 二阶段攻击点（NewAttackPoint）X 轴镜像
        if (BossPhaseController.isPhaseTwoActive && isNewAttackPointInitialized)
        {
            BossPhaseTwoController phaseTwoCtrl = GetComponent<BossPhaseTwoController>();
            if (phaseTwoCtrl != null && phaseTwoCtrl.newAttackPointObject != null && pivotTransform != null)
            {
                Transform newAttackTrans = phaseTwoCtrl.newAttackPointObject.transform;

                float targetWorldX = pivotTransform.position.x + (newAttackPointOffsetFromPivotX * facingDirection);
                newAttackTrans.position = new Vector3(targetWorldX, newAttackTrans.position.y, newAttackTrans.position.z);
            }
        }
    }

    public void ResetSkillTimer()
    {
        skillTimer = 0f;
        missedAttackCount = 0;
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, attackRadius);
        }
    }
}