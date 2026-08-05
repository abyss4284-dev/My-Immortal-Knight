using UnityEngine;
using System.Collections;

public class PlayerSkillManager : MonoBehaviour
{
    // 🌟 核心改进：定义静态变量用于跨场景保存下砸技能状态
    public static bool savedHasGroundSlam = false;

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

    private PlayerController playerController;
    private Rigidbody2D rb;
    private SpriteRenderer[] playerSprites;

    [Header("安全探测设置")]
    public Transform blinkTargetMarker;
    public LayerMask obstacleLayers;

    void Awake()
    {
        playerController = GetComponent<PlayerController>();
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        // 恢复蓝量
        if (PlayerController.savedMana != -1)
        {
            currentMana = PlayerController.savedMana;
        }
        else
        {
            currentMana = maxMana;
        }

        // 🌟 跨场景恢复技能解锁状态
        hasGroundSlam = savedHasGroundSlam;

        playerSprites = GetComponentsInChildren<SpriteRenderer>();
    }

    void Update()
    {
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

        // 🌟 监听下砸技能输入（只有在空中且解锁技能后才可使用）
        if (hasGroundSlam && !isSlamming && (Input.GetKeyDown(slamKey) || (Input.GetKey(KeyCode.S) && Input.GetKeyDown(KeyCode.J))))
        {
            TryCastGroundSlam();
        }
    }

    /// <summary>
    /// 解锁下砸能力（由 AbilityFruit 拾取时调用）
    /// </summary>
    public void UnlockGroundSlam()
    {
        hasGroundSlam = true;
        savedHasGroundSlam = true; // 🌟 同步保存到静态变量中，确保切场景后仍生效
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

        // 1. 隐藏玩家渲染
        SetPlayerVisibility(false);

        // 2. 确定下砸特效生成点（与水炮生成点一致）
        Vector3 spawnPos = (spawnPoint != null)
            ? spawnPoint.position
            : transform.position + new Vector3(0f, -0.8f, 0f);

        // 3. 生成共用的水流特效，并将其朝向修改为向下（旋转 -90 度）
        GameObject slamFX = null;
        if (waterProjectilePrefab != null)
        {
            slamFX = Instantiate(waterProjectilePrefab, spawnPos, Quaternion.Euler(0, 0, -90f));
            slamFX.transform.SetParent(transform);

            // 禁用 WaterProjectile 脚本逻辑
            WaterProjectile proj = slamFX.GetComponent<WaterProjectile>();
            if (proj != null) proj.enabled = false;

            // 清空刚体速度并设为 Kinematic
            Rigidbody2D fxRb = slamFX.GetComponent<Rigidbody2D>();
            if (fxRb != null)
            {
                fxRb.linearVelocity = Vector2.zero;
                fxRb.bodyType = RigidbodyType2D.Kinematic;
            }

            Collider2D fxCol = slamFX.GetComponent<Collider2D>();
            if (fxCol != null) fxCol.enabled = false;
        }

        // 4. 开始向下急速移动并持续检测撞击
        bool hasHit = false;
        Transform groundCheck = playerController != null ? playerController.groundCheck : transform;

        while (!hasHit)
        {
            // 向下赋予速度
            rb.linearVelocity = new Vector2(0f, -slamSpeed);

            // 以 groundCheck（地面检查点）为准进行圆圈碰撞检测
            Collider2D hit = Physics2D.OverlapCircle(groundCheck.position, 0.4f, slamHitLayers);

            if (hit != null)
            {
                hasHit = true;

                // 销毁跟随玩家的下砸水流特效
                if (slamFX != null) Destroy(slamFX);

                // 5. 计算水花特效生成位置（基于 groundCheck 向上偏移 burstYOffset）
                Vector3 burstSpawnPos = groundCheck.position + Vector3.up * burstYOffset;

                // 6. 生成水花特效并实现播放完毕后精准销毁
                if (waterBurstVFXPrefab != null)
                {
                    GameObject burst = Instantiate(waterBurstVFXPrefab, burstSpawnPos, Quaternion.identity);

                    // 动态计算特效精准销毁时长
                    float destroyTime = burstVFXDestroyDelay;

                    // 情况 A：如果是粒子系统（ParticleSystem），获取粒子实际播放总时长
                    ParticleSystem ps = burst.GetComponent<ParticleSystem>();
                    if (ps == null) ps = burst.GetComponentInChildren<ParticleSystem>();

                    if (ps != null)
                    {
                        var main = ps.main;
                        destroyTime = main.duration + main.startLifetime.constantMax;
                    }
                    // 情况 B：如果是 Animator 动画，获取当前动画剪辑的时长
                    else
                    {
                        Animator anim = burst.GetComponent<Animator>();
                        if (anim == null) anim = burst.GetComponentInChildren<Animator>();

                        if (anim != null && anim.GetCurrentAnimatorClipInfo(0).Length > 0)
                        {
                            destroyTime = anim.GetCurrentAnimatorClipInfo(0)[0].clip.length;
                        }
                    }

                    // 精准播放完毕后立刻销毁
                    Destroy(burst, destroyTime);
                }

                // 重新显现玩家
                SetPlayerVisibility(true);

                // 判断击中类型：Enemy 还是 Ground
                if (hit.CompareTag("Enemy"))
                {
                    hit.SendMessageUpwards("TakeDamage", slamDamage, SendMessageOptions.DontRequireReceiver);
                    Debug.Log($"💥 下砸命中敌人 [{hit.name}]，造成 {slamDamage} 点伤害！");

                    // 施加向上的反弹力
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