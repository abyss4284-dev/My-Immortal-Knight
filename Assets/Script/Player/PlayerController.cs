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

    [Header("地面与墙体检测")]
    public Transform groundCheck;
    public Transform wallCheck;           // 挂在玩家前方的墙体检测点
    public LayerMask groundLayer;
    public float wallCheckRadius = 0.2f;  // 墙体检测半径
    private bool isGrounded;
    private bool isTouchingWall;          // 是否触碰到墙壁

    [Header("蹬墙跳/滑墙设置")]
    public float wallSlidingSpeed = 2f;    // 滑墙/停在墙上的最大下滑速度
    public Vector2 wallJumpForce = new Vector2(10f, 12f); // 蹬墙跳力度 (X: 反推力, Y: 向上力)
    public float wallJumpDuration = 0.15f; // 蹬墙跳瞬间锁住玩家输入的控制时间
    private bool isWallSliding;
    private bool isWallJumping;

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

    // 跳跃特效
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
    public bool isDead = false;
    public static int savedHealth = -1;
    public static int savedMana = -1;

    [Header("受击与无敌设置")]
    [Tooltip("受击后的无敌持续时间")]
    public float invincibleDuration = 0.8f;
    public float hurtRecoilForce = 8f;      // 纯水平受击反推力
    public float hurtRecoilDuration = 0.15f;

    [HideInInspector] public bool isInvincible = false;
    private bool isHurtRecoiling = false;

    private SpriteRenderer[] playerSprites;

    [Header("方向控制")]
    public string facingDirectionParam = "right"; // 保存当前的玩家朝向指令（"left" 或 "right"）

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (savedHealth != -1) currentHealth = savedHealth;
        else currentHealth = maxHealth;
    }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponentInChildren<Animator>();
        playerSprites = GetComponentsInChildren<SpriteRenderer>();
    }

    void Update()
    {
        if (isDead || isHurtRecoiling || isRecoiling) return;

        horizontalInput = Input.GetAxisRaw("Horizontal");

        // 读取玩家指令，更新方向参数与视觉朝向，强行同步 WallCheck
        if (!isWallJumping)
        {
            if (horizontalInput > 0) // 按右键
            {
                facingDirectionParam = "right";
                graphicsNode.localRotation = Quaternion.Euler(0, 0, 0);

                if (wallCheck != null)
                {
                    Vector3 pos = wallCheck.localPosition;
                    wallCheck.localPosition = new Vector3(Mathf.Abs(pos.x), pos.y, pos.z);
                }
            }
            else if (horizontalInput < 0) // 按左键
            {
                facingDirectionParam = "left";
                graphicsNode.localRotation = Quaternion.Euler(0, 180, 0);

                if (wallCheck != null)
                {
                    Vector3 pos = wallCheck.localPosition;
                    wallCheck.localPosition = new Vector3(-Mathf.Abs(pos.x), pos.y, pos.z);
                }
            }
        }

        // 物理碰撞检测
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, 0.15f, groundLayer);
        if (wallCheck != null)
        {
            isTouchingWall = Physics2D.OverlapCircle(wallCheck.position, wallCheckRadius, groundLayer);
        }

        // 地面重置跳跃
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

        // 滑墙与重置跳跃逻辑
        CheckWallSlide();

        // 跳跃输入预输入（K 键）
        if (Input.GetKeyDown(KeyCode.K))
        {
            jumpBufferCounter = jumpBufferTime;
        }
        else
        {
            jumpBufferCounter -= Time.deltaTime;
        }

        // 触发跳跃分支：滑墙跳 OR 普通/二段跳
        if (jumpBufferCounter > 0f)
        {
            if (isWallSliding)
            {
                ExecuteWallJump();
            }
            else if (coyoteCounter > 0f)
            {
                ExecuteJump(jumpForce);
            }
            else if (jumbCountRemaining > 0)
            {
                ExecuteJump(doubleJumpForce);
                OnDoubleJumpEffects();
            }
        }

        // 松开 K 键小跳
        if (Input.GetKeyUp(KeyCode.K) && rb.linearVelocity.y > 0f && !isWallJumping)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * 0.5f);
            coyoteCounter = 0f;
        }

        // 攻击输入 (J 键或 Fire1)
        if (Time.time >= nextAttackTime)
        {
            if (Input.GetButtonDown("Fire1") || Input.GetKeyDown(KeyCode.J))
            {
                Attack();
                nextAttackTime = Time.time + attackRate;
            }
        }

        // 动画控制
        if (anim != null)
        {
            anim.SetFloat("Speed", Mathf.Abs(horizontalInput));
            anim.SetBool("isGrounded", isGrounded);
        }
    }

    void FixedUpdate()
    {
        if (!isRecoiling && !isHurtRecoiling && !isDead && !isWallJumping)
        {
            rb.linearVelocity = new Vector2(horizontalInput * moveSpeed * attackSpeedMultiplier, rb.linearVelocity.y);
        }
    }

    private void CheckWallSlide()
    {
        if (wallCheck == null) return;

        bool wallIsOnRight = wallCheck.localPosition.x > 0;
        bool wallIsOnLeft = wallCheck.localPosition.x < 0;

        bool isPushingAgainstWall = (wallIsOnRight && horizontalInput > 0) || (wallIsOnLeft && horizontalInput < 0);

        if (isTouchingWall && !isGrounded && isPushingAgainstWall)
        {
            isWallSliding = true;
            jumbCountRemaining = maxJumps;

            if (rb.linearVelocity.y < -wallSlidingSpeed)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, -wallSlidingSpeed);
            }
        }
        else
        {
            isWallSliding = false;
        }
    }

    private void ExecuteWallJump()
    {
        isWallJumping = true;
        isWallSliding = false;
        jumpBufferCounter = 0f;

        float facingDirection = (facingDirectionParam == "left") ? -1f : 1f;
        float jumpDirection = -facingDirection;

        facingDirectionParam = (jumpDirection > 0) ? "right" : "left";

        rb.linearVelocity = new Vector2(jumpDirection * wallJumpForce.x, wallJumpForce.y);
        graphicsNode.localRotation = Quaternion.Euler(0, jumpDirection > 0 ? 0 : 180, 0);

        if (wallCheck != null)
        {
            Vector3 pos = wallCheck.localPosition;
            float xOffset = Mathf.Abs(pos.x);
            wallCheck.localPosition = new Vector3(jumpDirection > 0 ? xOffset : -xOffset, pos.y, pos.z);
        }

        StartCoroutine(StopWallJumpRoutine());
    }

    private IEnumerator StopWallJumpRoutine()
    {
        yield return new WaitForSeconds(wallJumpDuration);
        isWallJumping = false;
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
            enemy.SendMessageUpwards("TakeDamage", attackDamage, SendMessageOptions.DontRequireReceiver);
        }

        StartCoroutine(AttackSpeedDebuff());

        if (hitEnemies.Length > 0)
        {
            PlayerSkillManager skillManager = GetComponent<PlayerSkillManager>();
            if (skillManager != null) skillManager.AddManaOnHit();
            ApplyAttackRecoil();
        }
    }

    private void ApplyAttackRecoil()
    {
        float faceDirection = (facingDirectionParam == "left") ? -1f : 1f;
        Vector2 recoilDirection = new Vector2(-faceDirection, 0f);
        float currentRecoilForce = isGrounded ? groundRecoilForce : airRecoilForce;

        rb.linearVelocity = new Vector2(recoilDirection.x * currentRecoilForce, rb.linearVelocity.y);
        StartCoroutine(RecoilRoutine());
    }

    private IEnumerator RecoilRoutine()
    {
        isRecoiling = true;
        yield return new WaitForSeconds(recoilDuration);
        isRecoiling = false;
    }

    private IEnumerator AttackSpeedDebuff()
    {
        attackSpeedMultiplier = 0.3f;
        yield return new WaitForSeconds(0.15f);
        attackSpeedMultiplier = 1f;
    }

    private void OnDrawGizmos()
    {
        if (groundCheck != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, 0.15f);
        }

        if (wallCheck != null)
        {
            Gizmos.color = isTouchingWall ? Color.blue : Color.yellow;
            Gizmos.DrawWireSphere(wallCheck.position, wallCheckRadius);
        }
    }

    /// <summary>
    /// 🌟 1. 玩家物理接触 Boss/怪物身体时触发扣血并施加背向反推力
    /// </summary>
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Enemy"))
        {
            if (isInvincible || isDead) return;

            // 1. 先进行扣血结算
            TakeDamage(1);

            // 2. 直接在此处计算反推力（方向与玩家当前朝向相反）
            float knockbackDir = (facingDirectionParam == "right") ? -1f : 1f;
            rb.linearVelocity = new Vector2(knockbackDir * hurtRecoilForce, rb.linearVelocity.y);
            StartCoroutine(HurtRecoilLockRoutine());
        }
    }

    /// <summary>
    /// 🌟 2. 持续停留在 Boss/怪物碰撞盒中时触发
    /// </summary>
    private void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Enemy"))
        {
            if (isInvincible || isDead) return;

            // 1. 先进行扣血结算
            TakeDamage(1);

            // 2. 直接在此处计算反推力（方向与玩家当前朝向相反）
            float knockbackDir = (facingDirectionParam == "right") ? -1f : 1f;
            rb.linearVelocity = new Vector2(knockbackDir * hurtRecoilForce, rb.linearVelocity.y);
            StartCoroutine(HurtRecoilLockRoutine());
        }
    }

    /// <summary>
    /// 🌟 通用受击方法（仅处理扣血、死亡和无敌，不包含任何反推逻辑）
    /// </summary>
    public void TakeDamage(int damage)
    {
        if (isInvincible || isDead) return;

        currentHealth -= damage;
        Debug.Log($"💔 玩家受到伤害！失去 {damage} 点血，剩余血量: {currentHealth}");

        if (currentHealth <= 0)
        {
            // 🌟 必须在 Die() 前面加这一段！
            PlayerSkillManager skillManager = GetComponent<PlayerSkillManager>();
            if (skillManager != null && skillManager.CheckRebirthCondition())
            {
                return; // 成功拦截！直接 return，不执行下面的 Die()
            }

            Die(); // 只有不满足条件才进入真正的死亡
            return;
        }

        StartCoroutine(InvincibleRoutine());
    }

    private IEnumerator HurtRecoilLockRoutine()
    {
        isHurtRecoiling = true;
        yield return new WaitForSeconds(hurtRecoilDuration);
        isHurtRecoiling = false;
    }

    /// <summary>
    /// 🌟 严格按时间结算的无敌协程：时间一到立刻解除无敌
    /// </summary>
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

        // 还原 Sprite 透明度
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

    private void Die()
    {
        isDead = true;
        rb.linearVelocity = Vector2.zero;
        Debug.Log("💀 战败，准备复活...");
        StartCoroutine(RespawnSequence());
    }

    private IEnumerator RespawnSequence()
    {
        yield return new WaitForSeconds(1.5f);

        PlayerSpawnManager spawnManager = Object.FindFirstObjectByType<PlayerSpawnManager>();
        if (spawnManager != null) spawnManager.RespawnPlayer();
        else Debug.LogError("🚨 场景中找不到 PlayerSpawnManager，无法进行复活！");
    }
}