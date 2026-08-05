using UnityEngine;
using System.Collections;

public class BossAIController : MonoBehaviour
{
    [Header("=== 基础属性 ===")]
    public int maxHealth = 500;
    public int currentHealth;
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

            // 检查一阶段背刺技能触发条件
            if (missedAttackCount >= 3 || skillTimer >= skillCooldown)
            {
                StartCoroutine(TeleportSkillRoutine());
                return;
            }
        }

        // 🌟 4. 通用：攻击与追击分支（一阶段和二阶段共享）
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

    private IEnumerator TeleportSkillRoutine()
    {
        isBusy = true;
        disappearAnimFinished = false;
        Debug.Log("🌀 Boss 触发技能【传送刺杀】！");

        missedAttackCount = 0;
        skillTimer = 0f;

        if (rb != null)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
        if (anim != null)
        {
            anim.SetFloat("Speed", 0f);
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

        isBusy = false;
    }

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

        if (BossPhaseController.isTransitioning)
        {
            if (portalObject != null)
            {
                portalObject.SetActive(false);
            }
            return;
        }

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

    public void OnDisappearEnd()
    {
        if (playerTransform != null)
        {
            // 1. 强制让 Boss 转向面对玩家
            isFacingRight = (playerTransform.position.x > transform.position.x);
            ApplyFacing();
        }

        // 2. 计算 Pivot 相对根物体的偏移
        float pivotOffset = 0f;
        if (pivotTransform != null)
        {
            pivotOffset = pivotTransform.position.x - transform.position.x;
        }

        if (BossPhaseController.isTransitioning)
        {
            if (playerTransform != null)
            {
                PlayerController playerCtrl = playerTransform.GetComponent<PlayerController>();
                float playerFacingDir = (playerCtrl != null && playerCtrl.facingDirectionParam == "left") ? -1f : 1f;

                // 目标背刺点（以轴心为准）
                float targetPivotX = playerTransform.position.x - (playerFacingDir * offsetBehindPlayer);

                // 🌟 减去轴心偏移，得出根物体的正确坐标
                float targetBossX = targetPivotX - pivotOffset;

                transform.position = new Vector3(targetBossX, transform.position.y, transform.position.z);
            }

            if (portalObject != null)
            {
                portalObject.SetActive(false);
            }
        }
        else
        {
            if (portalObject != null)
            {
                float portalX = portalObject.transform.position.x;

                // 🌟 使用 Pivot 轴心点对齐传送门中心
                float targetBossX = portalX - pivotOffset;
                transform.position = new Vector3(targetBossX, transform.position.y, transform.position.z);
                portalObject.SetActive(false);
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
        currentHealth -= damage;
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
            // 🌟 仅计算 X 轴的绝对水平距离，完全忽略 Y 轴高度差异
            newAttackPointOffsetFromPivotX = Mathf.Abs(newAttackPoint.transform.position.x - pivotTransform.position.x);
            isNewAttackPointInitialized = true;
        }
    }

    public void ApplyFacing()
    {
        bool isCurrentFacingRight = (isFacingRight == defaultFacingRight);
        float facingDirection = isFacingRight ? 1f : -1f;

        // ----------------------------------------------------
        // 1. 贴图/视觉子物体（Visual）翻转
        // ----------------------------------------------------
        if (visualTransform != null)
        {
            float desiredScaleX = isCurrentFacingRight ? Mathf.Abs(visualTransform.localScale.x) : -Mathf.Abs(visualTransform.localScale.x);

            if (pivotTransform != null)
            {
                // 只有当朝向发生改变时才执行 X 轴的对称镜像计算
                if (Mathf.Sign(visualTransform.localScale.x) != Mathf.Sign(desiredScaleX))
                {
                    float pivotX = pivotTransform.position.x;
                    float currentVisualX = visualTransform.position.x;

                    // 🌟 严格只计算新的 X 轴坐标
                    float newVisualX = 2f * pivotX - currentVisualX;

                    // 保持现有的 Y 和 Z 完全不动
                    Vector3 currentPos = visualTransform.position;
                    visualTransform.position = new Vector3(newVisualX, currentPos.y, currentPos.z);
                }
            }

            // 仅翻转 Scale 的 X 轴
            Vector3 scale = visualTransform.localScale;
            scale.x = desiredScaleX;
            visualTransform.localScale = scale;
        }

        // ----------------------------------------------------
        // 2. 一阶段攻击点（AttackPoint）X 轴镜像
        // ----------------------------------------------------
        if (attackPoint != null && pivotTransform != null)
        {
            // 🌟 仅仅根据 Pivot 的 X 坐标加上 X 轴偏移量，Y 轴维持原 position.y 不受影响
            float targetWorldX = pivotTransform.position.x + (attackPointOffsetFromPivotX * facingDirection);
            attackPoint.position = new Vector3(targetWorldX, attackPoint.position.y, attackPoint.position.z);
        }

        // ----------------------------------------------------
        // 3. 二阶段攻击点（NewAttackPoint）X 轴镜像
        // ----------------------------------------------------
        if (BossPhaseController.isPhaseTwoActive && isNewAttackPointInitialized)
        {
            BossPhaseTwoController phaseTwoCtrl = GetComponent<BossPhaseTwoController>();
            if (phaseTwoCtrl != null && phaseTwoCtrl.newAttackPointObject != null && pivotTransform != null)
            {
                Transform newAttackTrans = phaseTwoCtrl.newAttackPointObject.transform;

                // 🌟 仅计算目标 X 轴位置，Y 轴保持该物体自身当前的高度，互不干扰
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