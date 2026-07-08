using UnityEngine;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    [Header("移动参数")]
    public float moveSpeed = 8f;
    public float jumpForce = 12f;
    public float doubleJumpForce = 10f; // 👈 二段跳的力（通常比一段跳略小，手感更扎实）
    private float attackSpeedMultiplier = 1f; // 移动速度系数

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
    public int maxJumps = 2;             // 👈 最大跳跃次数（2代表二段跳）
    private int jumbCountRemaining;      // 👈 剩余跳跃次数

    private Rigidbody2D rb;
    private float horizontalInput;

    //跳跃特效
    public GameObject doubleJumpVFXPrefab;

    [Header("攻击设置")]
    public Transform attackPoint;         // 👈 斩击判定的中心点（挂在 Graphics 下面，随转身自动换向）
    public float attackRange = 1.2f;       // 👈 斩击的物理攻击半径
    public LayerMask enemyLayers;         // 👈 伤害的目标图层（选 Enemy）
    public int attackDamage = 10;          // 👈 攻击伤害数值
    public float attackRate = 0.3f;        // 👈 攻击冷却时间（秒）
    private float nextAttackTime = 0f;

    [Header("攻击特效")]
    public GameObject slashVFXPrefab;     // 👈 斩击特效预制体

    [Header("攻击后坐力设置")]
    public float groundRecoilForce = 5f;   // 👈 地面砍中怪物时的后退力度
    public float airRecoilForce = 6f;      // 👈 空中砍中怪物时的后退力度
    public float recoilDuration = 0.1f;    // 👈 击退造成的硬直时间（期间不能控制移动）
    private bool isRecoiling = false;      // 👈 是否正处于后坐力状态

    private Animator anim;

    [Header("玩家生命值设置")]
    public int maxHealth = 5;
    private int currentHealth;
    private bool isDead = false;

    [Header("受击与无敌设置")]
    public float invincibleDuration = 1.5f; // 👈 受伤后的无敌时间（秒）
    public float hurtRecoilForce = 8f;     // 👈 撞怪后的被击退力度
    public float hurtRecoilDuration = 0.15f;// 👈 被击退时的硬直时间（期间无法控制移动）

    private bool isInvincible = false;      // 是否处于无敌状态
    private bool isHurtRecoiling = false;   // 是否正处于受击后坐力状态

    private SpriteRenderer[] playerSprites;    // 用于做无敌时的“闪烁”视觉反馈

    [Header("UI 联动")]
    public UIManager uiManager; // 拖入挂了 UIManager 的 Canvas

    private int currentSoul = 0;
    private int maxSoul = 100;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponentInChildren<Animator>();
        currentHealth = maxHealth;

        // 获取 Graphics 节点上的 SpriteRenderer，用来做闪烁效果
        // 如果你的组件挂在子节点上，请用 GetComponentInChildren
        playerSprites = GetComponentsInChildren<SpriteRenderer>();
        // 初始化 UI
        if (uiManager != null)
        {
            uiManager.InitializeHealthUI(maxHealth);
            uiManager.UpdateSoulUI(currentSoul, maxSoul);
        }
        else
        {
            Debug.LogError("🚨 警告：PlayerController 身上没有挂载 UIManager！");
        }
    }

    void Update()
    {
        if (isDead) return;
        //切断玩家控制
        if (isHurtRecoiling) return;
        if (isRecoiling) return;
        // 1. 获取输入与转向（使用上一动旋转 Y 轴的完美转向方案）
        horizontalInput = Input.GetAxisRaw("Horizontal");
        if (horizontalInput > 0) graphicsNode.localRotation = Quaternion.Euler(0, 0, 0);
        else if (horizontalInput < 0) graphicsNode.localRotation = Quaternion.Euler(0, 180, 0);

        // 2. 地面检测
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, 0.15f, groundLayer);

        // --- 刷新跳跃次数与土狼时间 ---
        if (isGrounded)
        {
            if (isGrounded && rb.linearVelocity.y <= 0.1f)
            {
                coyoteCounter = coyoteTime;
                jumbCountRemaining = maxJumps;
            }
        }
        else
        {
            coyoteCounter -= Time.deltaTime;
        }

        // --- 跳跃缓冲计数器 ---
        if (Input.GetButtonDown("Jump"))
        {
            jumpBufferCounter = jumpBufferTime;
        }
        else
        {
            jumpBufferCounter -= Time.deltaTime;
        }

        // 3. 触发跳跃逻辑（核心改动）
        if (jumpBufferCounter > 0f)
        {
            // 情况 A：满足一段跳条件（在地面上，或者处于土狼时间窗内）
            if (coyoteCounter > 0f)
            {
                ExecuteJump(jumpForce);
            }
            // 情况 B：不满足一段跳，但人在空中，且还有多余的跳跃次数可用（触发二段跳）
            else if (jumbCountRemaining > 0)
            {
                ExecuteJump(doubleJumpForce);
                OnDoubleJumpEffects(); // 👈 触发二段跳的视觉特效（见阶段二）
            }
        }

        // 短按短跳，长按高跳的手感微调
        if (Input.GetButtonUp("Jump") && rb.linearVelocity.y > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * 0.5f);
            coyoteCounter = 0f;
        }
        // --- 攻击输入判定 ---
        if (Time.time >= nextAttackTime)
        {
            if (Input.GetButtonDown("Fire1") || Input.GetKeyDown(KeyCode.J)) // 鼠标左键或 J 键攻击
            {
                Attack();
                nextAttackTime = Time.time + attackRate; // 进入冷却
            }
        }

        // --- 新增：每一帧将物理状态同步给动画状态机 ---
        if (anim != null)
        {
            // 1. 绝对值化水平输入，用来控制 Idle 和 Run 的切换
            // 使用 Mathf.Abs 确保往左走（-1）和往右走（1）时 Speed 都是正数
            anim.SetFloat("Speed", Mathf.Abs(horizontalInput));


            // 2. 实时更新地面状态，用来触发着地或起飞
            anim.SetBool("isGrounded", isGrounded);
        }
    }

    // 抽出一个统一的跳跃执行方法
    private void ExecuteJump(float force)
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, force);
        jumbCountRemaining--;   // 可用次数减 1
        jumpBufferCounter = 0f; // 清空缓冲
        coyoteCounter = 0f;     // 离开地面，强制关闭土狼时间
    }

    // 二段跳特有的视觉反馈
    private void OnDoubleJumpEffects()
    {
        if (doubleJumpVFXPrefab != null)
        {
            // 在角色脚底板（groundCheck的位置）生成这团烟雾/气流
            GameObject vfx = Instantiate(doubleJumpVFXPrefab, groundCheck.position, Quaternion.identity);

            // 1秒后自动在宇宙中销毁这团特效，防止内存泄漏
            Destroy(vfx, 1.0f);
        }
    }

    private void Attack()
    {
        // 1. 播放/召唤斩击特效
        if (slashVFXPrefab != null && attackPoint != null)
        {
            // 斩击特效直接生成在攻击点，并跟随玩家当前的旋转（面向左时特效也会自动镜像）
            GameObject slash = Instantiate(slashVFXPrefab, attackPoint.position, attackPoint.rotation);
            Destroy(slash, 0.4f); // 特效通常播完就销毁，0.4秒根据你特效动画长度微调
        }

        // 2. 核心物理判定：圈出攻击范围内的所有物体
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayers);

        // 3. 伤害应用：遍历所有被砍到的怪物
        foreach (Collider2D enemy in hitEnemies)
        {
            Debug.Log($"砍中了怪物: {enemy.name}！造成了 {attackDamage} 点伤害。");

            // 👈 核心连通：获取怪物身上的 AI 脚本，并对它造成伤害！
            Enemy_small_dragon enemyAI = enemy.GetComponent<Enemy_small_dragon>();
            if (enemyAI != null)
            {
                enemyAI.TakeDamage(attackDamage); // 扣血并触发受击/死亡动作
            }
        }
        // 触发攻击时，立刻把速度压制到 30% (产生顿挫感)
        StartCoroutine(AttackSpeedDebuff());
        // 4. 伤害与后坐力应用
        if (hitEnemies.Length > 0)
        {
            // 只要砍中了至少一只怪物，就触发后坐力
            ApplyAttackRecoil();
        }
    }
    // --- 新增：处理玩家攻击后坐力的核心逻辑 ---
private void ApplyAttackRecoil()
    {
        // 1. 确定后退的方向：朝向当前玩家面朝方向的反方向
        // 借助你之前的旋转逻辑：当 y 轴旋转为 180 时代表面朝左，否则面朝右
        float faceDirection = (graphicsNode.localRotation.eulerAngles.y == 180f) ? -1f : 1f;
        Vector2 recoilDirection = new Vector2(-faceDirection, 0f); // 取反方向

        // 2. 根据在地面还是在空中，选择不同的击退力度
        float currentRecoilForce = isGrounded ? groundRecoilForce : airRecoilForce;

        // 3. 施加瞬间爆发力（清除 Y 轴微弱速度干扰，确保水平震退）
        rb.linearVelocity = new Vector2(recoilDirection.x * currentRecoilForce, rb.linearVelocity.y);

        // 4. 开启击退硬直协程，让玩家短暂失去控制权
        StartCoroutine(RecoilRoutine());
    }

    private System.Collections.IEnumerator RecoilRoutine()
    {
        isRecoiling = true;
        yield return new WaitForSeconds(recoilDuration); // 震退 0.1 秒
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
        attackSpeedMultiplier = 0.3f; // 速度骤降
        yield return new WaitForSeconds(0.15f); // 持续 0.15 秒（挥刀动作吃力感）
        attackSpeedMultiplier = 1f;   // 恢复正常
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
        // 检查碰撞到的物体是否带有 "Enemy" 标签，或者挂载了怪物的 AI 脚本
        if (collision.gameObject.CompareTag("Enemy"))
        {
            // 如果是在无敌期间，直接无视碰撞伤害
            if (isInvincible || isDead) return;

            // 触发受伤：传入怪物的 Transform 用于计算击退方向，并扣除 15 点血
            TakeDamage(1, collision.transform);
        }
    }

    // --- 👑 核心受伤控制中心 ---
    public void TakeDamage(int damage, Transform damageSource)
    {
        if (isInvincible || isDead) return;

        // 1. 扣除生命值
        currentHealth -= damage;
        Debug.Log($"💔 玩家受到伤害！失去 {damage} 点血，剩余血量: {currentHealth}");
        // 🌟 核心：通知 UI 刷新面具
        if (uiManager != null) uiManager.UpdateHealthUI(currentHealth);

        // 2. 检查死亡
        if (currentHealth <= 0)
        {
            Die();
            return;
        }

        // 3. 触发受到怪物碰撞的“远离方向”物理击退
        if (damageSource != null)
        {
            ApplyHurtRecoil(damageSource);
        }

        // 4. 开启无敌状态与视觉闪烁
        StartCoroutine(InvincibleRoutine());
    }

    // --- 🏃‍♂️ 计算并施加远离怪物的击退力 ---
    private void ApplyHurtRecoil(Transform enemyTransform)
    {
        // 计算远离怪物的水平方向：如果玩家在怪物右边，方向为 1；在左边则为 -1
        float knockbackDirection = (transform.position.x > enemyTransform.position.x) ? 1f : -1f;

        // 给刚体一个爆发力，往远离怪物的方向弹开（同时 Y 轴微微向上弹起一点，手感更好）
        rb.linearVelocity = new Vector2(knockbackDirection * hurtRecoilForce, hurtRecoilForce * 0.5f);

        // 开启硬直锁，短暂剥夺操作权
        StartCoroutine(HurtRecoilLockRoutine());
    }

    private IEnumerator HurtRecoilLockRoutine()
    {
        isHurtRecoiling = true;
        yield return new WaitForSeconds(hurtRecoilDuration); // 硬直 0.15 秒
        isHurtRecoiling = false;
    }

    // --- ⏱️ 无敌时间与闪烁协程 ---
    private IEnumerator InvincibleRoutine()
    {
        isInvincible = true;

        float elapsed = 0f;
        while (elapsed < invincibleDuration)
        {
            // 🌟 核心修改：遍历数组里的每一个身体部位，让他们同时切换透明度
            if (playerSprites != null && playerSprites.Length > 0)
            {
                // 先看第一个部位（比如头部）目前的透明度是多少
                float targetAlpha = (playerSprites[0].color.a == 1f) ? 0.2f : 1f;

                // 把这个目标透明度应用到所有部位上
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

        // 🌟 无敌结束，确保所有部位都恢复 100% 不透明
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

    // 玩家攻击打中怪物时（或者吃能量球时）
    public void AddSoul(int amount)
    {
        currentSoul = Mathf.Clamp(currentSoul + amount, 0, maxSoul);

        // 🌟 核心：通知 UI 补充能量
        if (uiManager != null) uiManager.UpdateSoulUI(currentSoul, maxSoul);
    }

    // --- 💀 玩家死亡逻辑 ---
    private void Die()
    {
        isDead = true;
        rb.linearVelocity = Vector2.zero;
        Debug.Log("💀 战败，游戏结束！");
        // 这里可以播放死亡动画或弹出 Restart 菜单
    }
}