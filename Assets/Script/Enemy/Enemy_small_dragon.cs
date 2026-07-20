using System.Collections;
using UnityEngine;

public class Enemy_small_dragon : MonoBehaviour
{
    [Header("基础组件引用")]
    public Transform player;
    public Transform graphicsNode;
    private Rigidbody2D rb;
    private Animator anim;

    [Header("移动设置")]
    public float moveSpeed = 2f;
    public float chaseRange = 5f;
    private bool isChasing = false;          // 👈 新增：当前是否处于警觉/追击状态

    [Header("视野设置")]
    [Range(0f, 180f)]
    public float viewAngle = 90f;            // 👈 新增：视野夹角（比如 90 度，意味着左右各 45 度的扇形区域）

    [Header("攻击设置")]
    public float attackRange = 1.2f;
    public float attackRate = 1.5f;
    private float nextAttackTime = 1.0f;
    private bool isAttacking = false;
    private Coroutine activeAttackCoroutine; // 👈 记录当前正在运行的攻击协程

    [Header("受击状态锁")]
    private bool isHurting = false;          // 👈 是否正处于挨打僵直中

    [Header("生命值设置")]
    public int maxHealth = 30;
    private int currentHealth;
    private bool isDead = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = graphicsNode.GetComponent<Animator>();
        currentHealth = maxHealth;

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }
    }

    void Update()
    {
        // 如果死了、正在挨打僵直中、或者正在攻击中，直接阻断一切其他AI行为
        if (isDead || isHurting || isAttacking || player == null) return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        // 🌟 1. 核心判定：如果当前没在追击，检查玩家是否进入了“正脸视野扇形区”
        if (!isChasing)
        {
            if (distanceToPlayer <= chaseRange && IsPlayerInFieldOfView())
            {
                isChasing = true; // 惊醒，进入追击状态
            }
        }

        // 🌟 2. 行为分支
        if (isChasing)
        {
            if (distanceToPlayer <= attackRange)
            {
                StopMoving();
                if (Time.time >= nextAttackTime)
                {
                    activeAttackCoroutine = StartCoroutine(AttackRoutine());
                }
            }
            else if (distanceToPlayer <= chaseRange)
            {
                LookAtPlayer();
                MoveTowardsPlayer();
            }
            else
            {
                // 玩家跑得太远，脱离仇恨范围，重置为巡逻/静止状态
                isChasing = false;
                StopMoving();
            }
        }
        else
        {
            // 没发现玩家时静止（你也可以在这里接入你的巡逻代码）
            StopMoving();
        }
    }

    // 🌟 新增：判断玩家是否在敌人的“正脸面朝方向”的视野扇形内
    private bool IsPlayerInFieldOfView()
    {
        if (player == null || graphicsNode == null) return false;

        // 1. 获取怪物当前真实的“面朝方向”向量（基于 graphicsNode 的旋转）
        Vector2 facingDir = Vector2.right;
        float normalizedY = Mathf.Repeat(Mathf.Abs(graphicsNode.localEulerAngles.y), 360f);

        // 如果围绕 Y 轴旋转了 180 度左右，说明面朝左
        if (Mathf.Abs(normalizedY - 180f) < 1f)
        {
            facingDir = Vector2.left;
        }

        // 2. 计算怪物指向玩家的方向向量
        Vector2 directionToPlayer = ((Vector2)player.position - (Vector2)transform.position).normalized;

        // 3. 计算面朝方向与玩家方向之间的夹角
        float angle = Vector2.Angle(facingDir, directionToPlayer);

        // 如果夹角在视野设定范围内（比如小于 45 度），说明玩家在扇形内
        return angle <= viewAngle * 0.5f;
    }

    private void MoveTowardsPlayer()
    {
        float direction = (player.position.x - transform.position.x > 0) ? 1f : -1f;
        rb.linearVelocity = new Vector2(direction * moveSpeed, rb.linearVelocity.y);
        if (anim != null) anim.SetFloat("Speed", 1f);
    }

    private void StopMoving()
    {
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        if (anim != null) anim.SetFloat("Speed", 0f);
    }

    private void LookAtPlayer()
    {
        if (player == null || graphicsNode == null) return;

        // 计算玩家和怪物的【纯水平（X轴）】距离差距
        float directionToPlayer = player.position.x - transform.position.x;

        // 🌟 防抖死区逻辑
        if (Mathf.Abs(directionToPlayer) < 0.2f)
        {
            return;
        }

        // 距离足够时，才正常进行方向判断
        if (directionToPlayer > 0f)
        {
            graphicsNode.localRotation = Quaternion.Euler(0f, 0f, 0f);
        }
        else if (directionToPlayer < 0f)
        {
            graphicsNode.localRotation = Quaternion.Euler(0f, 180f, 0f);
        }
    }

    // --- 攻击行为协程 ---
    private IEnumerator AttackRoutine()
    {
        isAttacking = true;
        nextAttackTime = Time.time + attackRate;

        LookAtPlayer();
        StopMoving();

        if (anim != null) anim.SetTrigger("AttackTrigger");

        // 攻击前摇（等待动画挥爪到特定帧）
        yield return new WaitForSeconds(0.2f);

        float finalDistance = Vector2.Distance(transform.position, player.position);
        if (finalDistance <= attackRange && !isDead && !isHurting) // 确保产生伤害前没有被打断
        {
            Debug.Log("💥 怪物成功抓伤了玩家！");
        }

        // 攻击后摇硬直
        yield return new WaitForSeconds(0.3f);
        isAttacking = false;
        activeAttackCoroutine = null;
    }

    // --- 受伤与打断逻辑 ---
    public void TakeDamage(int damage)
    {
        if (isDead) return;

        // 🌟 细节提升：如果从背后偷袭怪物，怪物被打后应该立即触发惊醒，转身反击
        isChasing = true;

        // 如果被打的时候它正在准备攻击，强制掐断攻击协程
        if (activeAttackCoroutine != null)
        {
            StopCoroutine(activeAttackCoroutine);
            activeAttackCoroutine = null;
        }

        // 重置攻击状态状态锁，强行退出攻击逻辑
        isAttacking = false;

        // 扣血与状态计算
        currentHealth -= damage;
        Debug.Log($"{gameObject.name} 受到 {damage} 点伤害，剩余血量: {currentHealth}");

        if (currentHealth <= 0)
        {
            Die();
            return; // 死了就不用进受击硬直了
        }

        // 开启受击硬直锁，强制播放受伤动画
        StopMoving();
        if (anim != null) anim.SetTrigger("HurtTrigger");

        // 开启受击打断恢复协程
        StartCoroutine(HurtRecoveryRoutine());
    }

    // 挨打硬直恢复计时器
    private IEnumerator HurtRecoveryRoutine()
    {
        isHurting = true;

        // 让怪物无法反击并停顿一段时间
        yield return new WaitForSeconds(0.3f);

        isHurting = false;
    }

    private void Die()
    {
        isDead = true;
        isChasing = false;
        if (activeAttackCoroutine != null) StopCoroutine(activeAttackCoroutine);
        StopMoving();

        if (anim != null) anim.SetTrigger("DieTrigger");

        GetComponent<Collider2D>().enabled = false;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;

        Destroy(gameObject, 2f);
        Debug.Log($"💀 {gameObject.name} 已死亡！");
    }

    private void OnDrawGizmosSelected()
    {
        // 绘制检测半径
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, chaseRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // 🌟 绘制黄色的视野扇形边界（便于在 Scene 视窗调试角度大小）
        if (graphicsNode != null)
        {
            Vector3 facingDir = Vector3.right;
            float normalizedY = Mathf.Repeat(Mathf.Abs(graphicsNode.localEulerAngles.y), 360f);
            if (Mathf.Abs(normalizedY - 180f) < 1f)
            {
                facingDir = Vector3.left;
            }

            Vector3 leftBoundary = Quaternion.AngleAxis(-viewAngle * 0.5f, Vector3.forward) * facingDir;
            Vector3 rightBoundary = Quaternion.AngleAxis(viewAngle * 0.5f, Vector3.forward) * facingDir;

            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, leftBoundary * chaseRange);
            Gizmos.DrawRay(transform.position, rightBoundary * chaseRange);
        }
    }
}