using UnityEngine;
using System.Collections;

public class PlayerSkillManager : MonoBehaviour
{
    // 🌟 静态变量：用于跨场景记忆下砸技能与单次复活状态
    public static bool savedHasGroundSlam = false;
    public static bool hasUsedRebirthInBattle = false; // 🌟 标志位：本次面对 Boss 是否已使用过复活

    [Header("蓝条 (Mana) 设置")]
    public int maxMana = 100;
    public int currentMana = 0;

    [Header("闪现技能设置")]
    public int blinkManaCost = 10;
    public float blinkDistance = 4f;
    public float blinkDelay = 0.15f;
    public float blinkCooldown = 0.6f;
    private float nextBlinkTime = 0f;

    [Header("水炮技能设置")]
    public GameObject waterProjectilePrefab; // 水炮与下砸共用此预制体
    public int waterManaCost = 20;
    public Transform spawnPoint;             // 水炮/下砸水流生成点
    public float waterCooldown = 0.3f;
    private float nextWaterTime = 0f;

    [Header("下砸技能设置 (Ground Slam)")]
    public bool hasGroundSlam = false;       // 是否已解锁下砸
    public KeyCode slamKey = KeyCode.S;      // 下砸按键（支持 S 键或 S + J 触发）
    public int slamManaCost = 20;            // 下砸蓝量消耗
    public float slamSpeed = 25f;            // 向下冲刺速度
    public int slamDamage = 40;              // 敌人受击伤害
    public float reboundForce = 12f;         // 击中敌人时的向上反推力
    public GameObject waterBurstVFXPrefab;  // 触地/击中时在 GroundCheck 点播放的水花迸发特效
    public LayerMask slamHitLayers;          // 撞击层（勾选 Ground 和 Enemy）
    private bool isSlamming = false;
    public float burstYOffset = 0.5f;        // 向上偏离距离（在 Inspector 中可微调）
    public float burstVFXDestroyDelay = 0.8f;// 手动指定的特效销毁时长（备用）

    [Header("🔥 复活被动设置 (Resurrection Passive)")]
    [Tooltip("玩家身上的火焰子物体（包含 Animator 播放火焰与爆裂动画）")]
    public GameObject flameChildObject;
    [Tooltip("玩家身上的复活确认 UI 子物体")]
    public GameObject rebirthUIObject;
    [Tooltip("火焰爆裂动画的 Trigger 参数名称")]
    public string explosionTriggerParam = "Explode";
    [Tooltip("Boss 触发复活被动的血量百分比阈值 (0.4 代表 40%)")]
    public float bossHealthThreshold = 0.4f;

    // 内部状态控制标记
    private bool isAwaitingRebirthChoice = false; // 是否处于等待选择复活状态
    private bool isRebirthing = false;            // 是否处于正在播放复活爆裂动画状态

    private PlayerController playerController;
    private Rigidbody2D rb;
    private SpriteRenderer[] playerSprites;
    private Animator flameAnimator;

    [Header("安全探测设置")]
    public Transform blinkTargetMarker;
    public LayerMask obstacleLayers;

    void Awake()
    {
        playerController = GetComponent<PlayerController>();
        rb = GetComponent<Rigidbody2D>();

        // 获取火焰物体上的 Animator，并支持暂停时不受 Time.timeScale 影响
        if (flameChildObject != null)
        {
            flameAnimator = flameChildObject.GetComponent<Animator>();
            if (flameAnimator != null)
            {
                flameAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            }
        }
    }

    void Start()
    {
        // 初始确保火焰和 UI 处于关闭状态
        if (flameChildObject != null) flameChildObject.SetActive(false);
        if (rebirthUIObject != null) rebirthUIObject.SetActive(false);

        // 恢复蓝量
        if (PlayerController.savedMana != -1)
        {
            currentMana = PlayerController.savedMana;
        }
        else
        {
            currentMana = maxMana;
        }

        // 跨场景恢复技能解锁状态
        hasGroundSlam = savedHasGroundSlam;

        playerSprites = GetComponentsInChildren<SpriteRenderer>();
    }

    void Update()
    {
        // 🌟 优先处理“复活确认环节”的按键响应 (Y / N)，此时 Time.timeScale 为 0，不受暂停影响
        if (isAwaitingRebirthChoice)
        {
            HandleRebirthInput();
            return; // 处于等待响应状态时禁用其他技能按键输入
        }

        if (isRebirthing) return; // 播放复活动画期间禁用技能

        // 按 L 键触发闪现
        if (Input.GetKeyDown(KeyCode.L) && Time.time >= nextBlinkTime)
        {
            TryCastBlink();
        }

        // 按空格键 (Space) 触发水炮技能
        if (Input.GetKeyDown(KeyCode.Space) && Time.time >= nextWaterTime)
        {
            TryCastWaterSkill();
        }

        // 监听下砸技能输入（只有在空中且解锁技能后才可使用）
        if (hasGroundSlam && !isSlamming && (Input.GetKeyDown(slamKey) || (Input.GetKey(KeyCode.S) && Input.GetKeyDown(KeyCode.J))))
        {
            TryCastGroundSlam();
        }
    }

    /// <summary>
    /// 🌟 死亡条件检测逻辑：由 PlayerController 在致命伤处调用
    /// 返回 true：代表成功拦截死亡，进入复活选择环节
    /// 返回 false：代表不满足复活条件（如超过使用次数或 Boss 血量不足），直接进入常规死亡流程
    /// </summary>
    public bool CheckRebirthCondition()
    {
        // 🌟 检查：如果本次战斗已经触发过复活，则不再触发
        if (hasUsedRebirthInBattle)
        {
            Debug.Log("⚠️【复活被动】本次 Boss 战已使用过复活机制，无法再次复活！");
            return false;
        }

        // 读取 BossAIController 内部的静态 int 变量 currentHealth 与 maxHealth
        if (BossAIController.isBoss && BossAIController.maxHealth > 0)
        {
            float healthRatio = (float)BossAIController.currentHealth / BossAIController.maxHealth;

            if (healthRatio < bossHealthThreshold)
            {
                EnterRebirthConfirmationPhase();
                return true; // 成功拦截常规死亡
            }
        }

        return false; // 不满足复活条件
    }

    /// <summary>
    /// 开启复活确认环节
    /// </summary>
    private void EnterRebirthConfirmationPhase()
    {
        isAwaitingRebirthChoice = true;
        hasUsedRebirthInBattle = true; // 🌟 标记本次 Boss 战中复活已使用

        // 1. 隐藏主角本身的渲染，并挂起刚体运动/物理
        SetPlayerVisibility(false);
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
        }

        // 2. 激活摇曳火焰和复活选择 UI
        if (flameChildObject != null) flameChildObject.SetActive(true);
        if (rebirthUIObject != null) rebirthUIObject.SetActive(true);

        // 🌟 3. 游戏暂停
        Time.timeScale = 0f;

        Debug.Log("🔥【复活被动】Boss 血量低于 40%！游戏暂停，玩家化作摇曳火焰等待确认 (Y/N)...");
    }

    /// <summary>
    /// 监听按键输入 (Y / N)
    /// </summary>
    private void HandleRebirthInput()
    {
        // 🌟 按下 Y 键确认复活
        if (Input.GetKeyDown(KeyCode.Y))
        {
            isAwaitingRebirthChoice = false;
            isRebirthing = true;

            // 🌟 恢复游戏时间流逝
            Time.timeScale = 1f;

            if (rebirthUIObject != null) rebirthUIObject.SetActive(false);

            // 触发火焰爆裂动画
            if (flameAnimator != null)
            {
                flameAnimator.SetTrigger(explosionTriggerParam);
            }

            Debug.Log("💥 玩家按下了 [Y] 键，时间恢复，触发火焰爆裂准备复活！");
        }
        // 🌟 按下 N 键拒绝复活
        else if (Input.GetKeyDown(KeyCode.N))
        {
            isAwaitingRebirthChoice = false;

            // 🌟 恢复游戏时间流逝
            Time.timeScale = 1f;

            if (rebirthUIObject != null) rebirthUIObject.SetActive(false);
            if (flameChildObject != null) flameChildObject.SetActive(false);

            SetPlayerVisibility(true);
            if (rb != null) rb.simulated = true;

            // 拒绝复活，显现后调用 PlayerController 的 Die() 方法
            if (playerController != null)
            {
                playerController.SendMessage("Die", SendMessageOptions.DontRequireReceiver);
            }

            Debug.Log("💀 玩家按下了 [N] 键，时间恢复，拒绝复活进入死亡流程。");
        }
    }

    /// <summary>
    /// 🌟 动画事件（Animation Event）：挂载于“火焰爆裂动画”最后一帧
    /// 作用：爆裂结束后，主角重新显现，恢复全满状态并获得短暂无敌
    /// </summary>
    public void OnRebirthAnimFinished()
    {
        isRebirthing = false;

        // 1. 隐藏火焰子物体
        if (flameChildObject != null) flameChildObject.SetActive(false);

        // 2. 重新显示玩家并开启物理
        SetPlayerVisibility(true);
        if (rb != null) rb.simulated = true;

        // 3. 恢复满血满蓝
        currentMana = maxMana;
        PlayerController.savedMana = currentMana;

        if (playerController != null)
        {
            playerController.currentHealth = playerController.maxHealth;
            PlayerController.savedHealth = playerController.currentHealth;
            playerController.isDead = false;

            // 🌟 4. 触发玩家短暂无敌状态（直接启动 PlayerController 中的 InvincibleRoutine）
            playerController.StartCoroutine("InvincibleRoutine");
        }

        Debug.Log("✨【复活成功】主角恢复满血满蓝，并进入短暂无敌状态！");
    }

    /// <summary>
    /// 🌟 静态方法：在存档点交互或加载存档时调用，重置复活次数
    /// </summary>
    public static void ResetRebirthCount()
    {
        hasUsedRebirthInBattle = false;
        Debug.Log("💾【存档重置】复活次数已充能完毕！");
    }

    /// <summary>
    /// 解锁下砸能力（由 AbilityFruit 拾取时调用）
    /// </summary>
    public void UnlockGroundSlam()
    {
        hasGroundSlam = true;
        savedHasGroundSlam = true;
        Debug.Log("✨ [PlayerSkillManager] 下砸技能已正式解锁并存入跨场景记忆！");
    }

    /// <summary>
    /// 尝试释放下砸技能
    /// </summary>
    private void TryCastGroundSlam()
    {
        if (currentMana < slamManaCost)
        {
            Debug.Log("❌ 蓝量不足，无法释放下砸技能！");
            return;
        }

        currentMana -= slamManaCost;
        PlayerController.savedMana = currentMana;

        StartCoroutine(GroundSlamRoutine());
    }

    /// <summary>
    /// 下砸核心逻辑协程
    /// </summary>
    private IEnumerator GroundSlamRoutine()
    {
        isSlamming = true;

        SetPlayerVisibility(false);

        Vector3 spawnPos = (spawnPoint != null)
            ? spawnPoint.position
            : transform.position + new Vector3(0f, -0.8f, 0f);

        GameObject slamFX = null;
        if (waterProjectilePrefab != null)
        {
            slamFX = Instantiate(waterProjectilePrefab, spawnPos, Quaternion.Euler(0, 0, -90f));
            slamFX.transform.SetParent(transform);

            WaterProjectile proj = slamFX.GetComponent<WaterProjectile>();
            if (proj != null) proj.enabled = false;

            Rigidbody2D fxRb = slamFX.GetComponent<Rigidbody2D>();
            if (fxRb != null)
            {
                fxRb.linearVelocity = Vector2.zero;
                fxRb.bodyType = RigidbodyType2D.Kinematic;
            }

            Collider2D fxCol = slamFX.GetComponent<Collider2D>();
            if (fxCol != null) fxCol.enabled = false;
        }

        bool hasHit = false;
        Transform groundCheck = playerController != null ? playerController.groundCheck : transform;

        while (!hasHit)
        {
            rb.linearVelocity = new Vector2(0f, -slamSpeed);

            Collider2D hit = Physics2D.OverlapCircle(groundCheck.position, 0.4f, slamHitLayers);

            if (hit != null)
            {
                hasHit = true;

                if (slamFX != null) Destroy(slamFX);

                Vector3 burstSpawnPos = groundCheck.position + Vector3.up * burstYOffset;

                if (waterBurstVFXPrefab != null)
                {
                    GameObject burst = Instantiate(waterBurstVFXPrefab, burstSpawnPos, Quaternion.identity);

                    float destroyTime = burstVFXDestroyDelay;

                    ParticleSystem ps = burst.GetComponent<ParticleSystem>();
                    if (ps == null) ps = burst.GetComponentInChildren<ParticleSystem>();

                    if (ps != null)
                    {
                        var main = ps.main;
                        destroyTime = main.duration + main.startLifetime.constantMax;
                    }
                    else
                    {
                        Animator anim = burst.GetComponent<Animator>();
                        if (anim == null) anim = burst.GetComponentInChildren<Animator>();

                        if (anim != null && anim.GetCurrentAnimatorClipInfo(0).Length > 0)
                        {
                            destroyTime = anim.GetCurrentAnimatorClipInfo(0)[0].clip.length;
                        }
                    }

                    Destroy(burst, destroyTime);
                }

                SetPlayerVisibility(true);

                if (hit.CompareTag("Enemy"))
                {
                    hit.SendMessageUpwards("TakeDamage", slamDamage, SendMessageOptions.DontRequireReceiver);
                    Debug.Log($"💥 下砸命中敌人 [{hit.name}]，造成 {slamDamage} 点伤害！");

                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, reboundForce);
                }
                else if (hit.CompareTag("Ground"))
                {
                    Debug.Log("🌊 下砸砸中地面！");
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
                }
            }

            yield return null;
        }

        isSlamming = false;
    }

    private void TryCastWaterSkill()
    {
        if (waterProjectilePrefab == null)
        {
            Debug.LogWarning("⚠️ [PlayerSkillManager] 未分配 waterProjectilePrefab (水炮预制体)！");
            return;
        }

        if (currentMana >= waterManaCost)
        {
            currentMana -= waterManaCost;
            PlayerController.savedMana = currentMana;

            Vector2 shootDirection = Vector2.right;
            if (playerController != null && playerController.facingDirectionParam == "left")
            {
                shootDirection = Vector2.left;
            }

            Vector3 spawnPos = (spawnPoint != null)
                ? spawnPoint.position
                : transform.position + new Vector3(shootDirection.x * 0.8f, 0f, 0f);

            GameObject waterObj = Instantiate(waterProjectilePrefab, spawnPos, Quaternion.identity);

            WaterProjectile projectile = waterObj.GetComponent<WaterProjectile>();
            if (projectile != null)
            {
                projectile.Initialize(shootDirection);
            }

            nextWaterTime = Time.time + waterCooldown;
            Debug.Log($"🌊 释放水炮技能！方向: {shootDirection}，剩余蓝量: {currentMana}");
        }
        else
        {
            Debug.Log("❌ 蓝量不足，无法释放水炮技能！");
        }
    }

    private void TryCastBlink()
    {
        if (currentMana >= blinkManaCost)
        {
            currentMana -= blinkManaCost;
            PlayerController.savedMana = currentMana;

            StartCoroutine(BlinkWithDetectorRoutine());
            nextBlinkTime = Time.time + blinkCooldown;
        }
        else
        {
            Debug.Log("❌ 蓝量不足，无法闪现！");
        }
    }

    private IEnumerator BlinkWithDetectorRoutine()
    {
        SetPlayerVisibility(false);

        Rigidbody2D playerRb = GetComponent<Rigidbody2D>();
        Vector3 finalBlinkPosition = transform.position;

        if (blinkTargetMarker != null && playerController != null)
        {
            Vector3 markerPos = blinkTargetMarker.localPosition;
            float xOffset = Mathf.Abs(markerPos.x);

            if (playerController.facingDirectionParam == "left")
            {
                blinkTargetMarker.localPosition = new Vector3(-xOffset, markerPos.y, markerPos.z);
            }
            else
            {
                blinkTargetMarker.localPosition = new Vector3(xOffset, markerPos.y, markerPos.z);
            }
        }

        if (playerRb != null && blinkTargetMarker != null)
        {
            Vector2 direction = (blinkTargetMarker.position - transform.position).normalized;
            float distance = Vector2.Distance(transform.position, blinkTargetMarker.position);

            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(obstacleLayers);
            filter.useLayerMask = true;
            filter.useTriggers = false;

            RaycastHit2D[] hits = new RaycastHit2D[1];
            int hitCount = playerRb.Cast(direction, filter, hits, distance);

            if (hitCount > 0)
            {
                float safeDistance = Mathf.Max(0, hits[0].distance - 0.1f);
                finalBlinkPosition = (Vector2)transform.position + direction * safeDistance;
                Debug.Log($"🚧 [闪现防护] 前方有墙壁/障碍，已修正落脚点，避免卡入 {hits[0].collider.name}");
            }
            else
            {
                finalBlinkPosition = blinkTargetMarker.position;
            }
        }

        yield return new WaitForSeconds(blinkDelay);

        transform.position = finalBlinkPosition;

        SetPlayerVisibility(true);
        Debug.Log($"🔮 向 [{playerController?.facingDirectionParam}] 闪现成功！");
    }

    public void AddManaOnHit()
    {
        currentMana = Mathf.Clamp(currentMana + 10, 0, maxMana);
        PlayerController.savedMana = currentMana;
        Debug.Log($"✨ 击中怪物，恢复 10 点蓝量。当前蓝量: {currentMana}");
    }

    private void SetPlayerVisibility(bool isVisible)
    {
        if (playerSprites == null) return;
        foreach (SpriteRenderer sprite in playerSprites)
        {
            if (sprite != null) sprite.enabled = isVisible;
        }
    }
}