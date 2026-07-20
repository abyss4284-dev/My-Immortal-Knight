using UnityEngine;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    [Header("移动参数")]
    public float moveSpeed = 8f;
    public float jumpForce = 12f;
    public float doubleJumpForce = 10f;
    private float attackSpeedMultiplier = 1f;

    [Header("视觉节点")]
    public Transform graphicsNode;

    [Header("地面检测")]
    public Transform groundCheck;
    public LayerMask groundLayer;
    private bool isGrounded;

    [Header("手感补正参数")]
    public float coyoteTime = 0.15f;
    private float coyoteCounter;
    public float jumpBufferTime = 0.15f;
    private float jumpBufferCounter;

    [Header("二段跳控制")]
    public int maxJumps = 2;
    private int jumbCountRemaining;

    private Rigidbody2D rb;
    private float horizontalInput;

    //跳跃特效
    public GameObject doubleJumpVFXPrefab;

    [Header("攻击设置")]
    public Transform attackPoint;
    public float attackRange = 1.2f;
    public LayerMask enemyLayers;
    public int attackDamage = 10;
    public float attackRate = 0.3f;
    private float nextAttackTime = 0f;

    [Header("攻击特效")]
    public GameObject slashVFXPrefab;

    [Header("攻击后坐力设置")]
    public float groundRecoilForce = 5f;
    public float airRecoilForce = 6f;
    public float recoilDuration = 0.1f;
    private bool isRecoiling = false;

    private Animator anim;

    [Header("玩家属性")]
    public int maxHealth = 5;
    public int currentHealth;
    private bool isDead = false;
    public int maxMana = 50;
    public int currentMana;
    public static int savedHealth = -1;
    public static int savedMana = -1;

    [Header("受击与无敌设置")]
    public float invincibleDuration = 1.5f;
    public float hurtRecoilForce = 8f;
    public float hurtRecoilDuration = 0.15f;

    private bool isInvincible = false;
    private bool isHurtRecoiling = false;

    private SpriteRenderer[] playerSprites;

    [Header("UI 联动 (动态获取，无需拖拽)")]
    public UIManager uiManager;

    private int currentSoul = 0;
    private int maxSoul = 100;

    void Awake()
    {
        // 🌟 场景加载时，优先读取并继承旧场景状态
        if (savedHealth != -1)
        {
            currentHealth = savedHealth;
            currentMana = savedMana;
            Debug.Log($"🎒 跨场景成功！已读取并继承旧状态。当前血量: {currentHealth}, 法力: {currentMana}");
        }
        else
        {
            currentHealth = maxHealth;
            currentMana = maxMana;
        }
    }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponentInChildren<Animator>();

        // 🌟 修复：删除原先的 currentHealth = maxHealth; 避免覆盖跨场景血量数据！

        playerSprites = GetComponentsInChildren<SpriteRenderer>();

        // 🌟 优化：如果动态生成时 SpawnManager 没来得及绑定 UI，这里进行全自动兜底寻找
        if (uiManager == null)
        {
            uiManager = Object.FindFirstObjectByType<UIManager>();
        }

        // 🌟 优化：将 UI 初始化逻辑提取，确保此时的数据是最准确的继承数据
        SyncAllUIToCurrentStats();
    }

    // 🌟 新增：提供一个可以让 UIManager 或者是 SpawnManager 外部调用的主动绑定方法[cite: 1]
    public void SetupUI(UIManager manager)
    {
        uiManager = manager;
        SyncAllUIToCurrentStats();
    }

    // 🌟 新增：封装一个统一将当前属性刷新到 UI 的方法
    private void SyncAllUIToCurrentStats()
    {
        if (uiManager != null)
        {
            uiManager.InitializeHealthUI(maxHealth);
            uiManager.UpdateHealthUI(currentHealth);
            uiManager.UpdateSoulUI(currentSoul, maxSoul);
            // 如果你的 UIManager 有蓝条更新方法，可以在这里补充：
            // uiManager.UpdateManaUI(currentMana, maxMana);
        }
    }

    void Update()
    {
        if (isDead) return;
        if (isHurtRecoiling) return;
        if (isRecoiling) return;

        horizontalInput = Input.GetAxisRaw("Horizontal");
        if (horizontalInput > 0) graphicsNode.localRotation = Quaternion.Euler(0, 0, 0);
        else if (horizontalInput < 0) graphicsNode.localRotation = Quaternion.Euler(0, 180, 0);

        isGrounded = Physics2D.OverlapCircle(groundCheck.position, 0.15f, groundLayer);

        if (isGrounded)
        {
            if (rb.linearVelocity.y <= 0.1f)
            {
                coyoteCounter = coyoteTime;
                jumbCountRemaining = maxJumps;
            }
        }
        else
        {
            coyoteCounter -= Time.deltaTime;
        }

        if (Input.GetButtonDown("Jump"))
        {
            jumpBufferCounter = jumpBufferTime;
        }
        else
        {
            jumpBufferCounter -= Time.deltaTime;
        }

        if (jumpBufferCounter > 0f)
        {
            if (coyoteCounter > 0f)
            {
                ExecuteJump(jumpForce);
            }
            else if (jumbCountRemaining > 0)
            {
                ExecuteJump(doubleJumpForce);
                OnDoubleJumpEffects();
            }
        }

        if (Input.GetButtonUp("Jump") && rb.linearVelocity.y > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * 0.5f);
            coyoteCounter = 0f;
        }

        if (Time.time >= nextAttackTime)
        {
            if (Input.GetButtonDown("Fire1") || Input.GetKeyDown(KeyCode.J))
            {
                Attack();
                nextAttackTime = Time.time + attackRate;
            }
        }

        if (anim != null)
        {
            anim.SetFloat("Speed", Mathf.Abs(horizontalInput));
            anim.SetBool("isGrounded", isGrounded);
        }
    }

    private void ExecuteJump(float force)
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, force);
        jumbCountRemaining--;
        jumpBufferCounter = 0f;
        coyoteCounter = 0f;
    }

    private void OnDoubleJumpEffects()
    {
        if (doubleJumpVFXPrefab != null)
        {
            GameObject vfx = Instantiate(doubleJumpVFXPrefab, groundCheck.position, Quaternion.identity);
            Destroy(vfx, 1.0f);
        }
    }

    private void Attack()
    {
        if (slashVFXPrefab != null && attackPoint != null)
        {
            GameObject slash = Instantiate(slashVFXPrefab, attackPoint.position, attackPoint.rotation);
            Destroy(slash, 0.4f);
        }

        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayers);

        foreach (Collider2D enemy in hitEnemies)
        {
            Debug.Log($"砍中了怪物: {enemy.name}！造成了 {attackDamage} 点伤害。");

            Enemy_small_dragon enemyAI = enemy.GetComponent<Enemy_small_dragon>();
            if (enemyAI != null)
            {
                enemyAI.TakeDamage(attackDamage);
            }
        }

        StartCoroutine(AttackSpeedDebuff());

        // 🌟 新增：如果成功砍中了怪物，让技能管理器给自己充能 10 点蓝！
        if (hitEnemies.Length > 0)
        {
            PlayerSkillManager skillManager = GetComponent<PlayerSkillManager>();
            if (skillManager != null)
            {
                skillManager.AddManaOnHit();
            }
            ApplyAttackRecoil();
        }
    }

    private void ApplyAttackRecoil()
    {
        float faceDirection = (graphicsNode.localRotation.eulerAngles.y == 180f) ? -1f : 1f;
        Vector2 recoilDirection = new Vector2(-faceDirection, 0f);

        float currentRecoilForce = isGrounded ? groundRecoilForce : airRecoilForce;

        rb.linearVelocity = new Vector2(recoilDirection.x * currentRecoilForce, rb.linearVelocity.y);
        StartCoroutine(RecoilRoutine());
    }

    private System.Collections.IEnumerator RecoilRoutine()
    {
        isRecoiling = true;
        yield return new WaitForSeconds(recoilDuration);
        isRecoiling = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }

    private System.Collections.IEnumerator AttackSpeedDebuff()
    {
        attackSpeedMultiplier = 0.3f;
        yield return new WaitForSeconds(0.15f);
        attackSpeedMultiplier = 1f;
    }

    void FixedUpdate()
    {
        if (!isRecoiling)
        {
            rb.linearVelocity = new Vector2(horizontalInput * moveSpeed * attackSpeedMultiplier, rb.linearVelocity.y);
        }
    }

    private void OnDrawGizmos()
    {
        if (groundCheck != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, 0.15f);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Enemy"))
        {
            if (isInvincible || isDead) return;
            TakeDamage(1, collision.transform);
        }
    }

    public void TakeDamage(int damage, Transform damageSource)
    {
        if (isInvincible || isDead) return;

        currentHealth -= damage;
        Debug.Log($"💔 玩家受到伤害！失去 {damage} 点血，剩余血量: {currentHealth}");

        // 🌟 即使动态开局，只要绑定成功，这里就能完美刷新 UI 
        if (uiManager != null) uiManager.UpdateHealthUI(currentHealth);

        if (currentHealth <= 0)
        {
            Die();
            return;
        }

        if (damageSource != null)
        {
            ApplyHurtRecoil(damageSource);
        }

        StartCoroutine(InvincibleRoutine());
    }

    private void ApplyHurtRecoil(Transform enemyTransform)
    {
        float knockbackDirection = (transform.position.x > enemyTransform.position.x) ? 1f : -1f;
        rb.linearVelocity = new Vector2(knockbackDirection * hurtRecoilForce, hurtRecoilForce * 0.5f);
        StartCoroutine(HurtRecoilLockRoutine());
    }

    private IEnumerator HurtRecoilLockRoutine()
    {
        isHurtRecoiling = true;
        yield return new WaitForSeconds(hurtRecoilDuration);
        isHurtRecoiling = false;
    }

    private IEnumerator InvincibleRoutine()
    {
        isInvincible = true;

        float elapsed = 0f;
        while (elapsed < invincibleDuration)
        {
            if (playerSprites != null && playerSprites.Length > 0)
            {
                float targetAlpha = (playerSprites[0].color.a == 1f) ? 0.2f : 1f;

                foreach (SpriteRenderer sprite in playerSprites)
                {
                    if (sprite != null)
                    {
                        Color color = sprite.color;
                        color.a = targetAlpha;
                        sprite.color = color;
                    }
                }
            }

            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }

        if (playerSprites != null)
        {
            foreach (SpriteRenderer sprite in playerSprites)
            {
                if (sprite != null)
                {
                    Color finalColor = sprite.color;
                    finalColor.a = 1f;
                    sprite.color = finalColor;
                }
            }
        }

        isInvincible = false;
    }

    public void AddSoul(int amount)
    {
        currentSoul = Mathf.Clamp(currentSoul + amount, 0, maxSoul);
        if (uiManager != null) uiManager.UpdateSoulUI(currentSoul, maxSoul);
    }

    private void Die()
    {
        isDead = true;
        rb.linearVelocity = Vector2.zero;
        Debug.Log("💀 战败，游戏结束！");
    }
}