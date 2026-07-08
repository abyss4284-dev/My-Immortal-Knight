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

    [Header("攻击设置")]
    public float attackRange = 1.2f;
    public float attackRate = 1.5f;
    private float nextAttackTime = 1.0f;
    private bool isAttacking = false;
    private Coroutine activeAttackCoroutine; // 👈 记录当前正在运行的攻击协程

    [Header("受击状态锁")]
    private bool isHurting = false;          // 👈 新增：是否正处于挨打僵直中

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
        // 👈 核心改动：如果死了、正在挨打僵直中、或者正在攻击中，直接阻断一切其他AI行为（如移动、转身、再次攻击）
        if (isDead || isHurting || isAttacking || player == null) return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        if (distanceToPlayer <= attackRange)
        {
            StopMoving();
            if (Time.time >= nextAttackTime)
            {
                // 👈 将开启的协程赋值给变量，方便以后随时掐断它
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
            StopMoving();
        }
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

        // 1. 计算玩家和怪物的【纯水平（X轴）】距离差距
        float directionToPlayer = player.position.x - transform.position.x;

        // 2. 🌟 核心防抖改动：引入死区逻辑 🌟
        // 如果玩家跟怪物的横向距离小于 0.2f（说明贴得太近了，比如在头顶或正核心挤着）
        // 此时怪物选择“保持当前方向不变”，直接拒绝执行转头代码，防止频繁抽搐翻转
        if (Mathf.Abs(directionToPlayer) < 0.2f)
        {
            return;
        }

        // 3. 距离足够时，才正常进行方向判断
        if (directionToPlayer > 0f)
        {
            // 玩家在右边
            graphicsNode.localRotation = Quaternion.Euler(0f, 0f, 0f);
        }
        else if (directionToPlayer < 0f)
        {
            // 玩家在左边
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
        if (finalDistance <= attackRange && !isDead && !isHurting) // 👈 确保产生伤害前没有被打断
        {
            Debug.Log("💥 怪物成功抓伤了玩家！");
        }

        // 攻击后摇硬直
        yield return new WaitForSeconds(0.3f);
        isAttacking = false;
        activeAttackCoroutine = null;
    }

    // --- 👑 核心改动：受伤与打断逻辑 ---
    public void TakeDamage(int damage)
    {
        if (isDead) return;

        // 1. 如果被打的时候它正在准备攻击，强制掐断攻击协程
        if (activeAttackCoroutine != null)
        {
            StopCoroutine(activeAttackCoroutine);
            activeAttackCoroutine = null;
        }

        // 2. 重置攻击状态状态锁，强行退出攻击逻辑
        isAttacking = false;

        // 3. 扣血与状态计算
        currentHealth -= damage;
        Debug.Log($"{gameObject.name} 受到 {damage} 点伤害，剩余血量: {currentHealth}");

        if (currentHealth <= 0)
        {
            Die();
            return; // 死了就不用进受击硬直了
        }

        // 4. 开启受击硬直锁，强制播放受伤动画
        StopMoving();
        if (anim != null) anim.SetTrigger("HurtTrigger");

        // 开启受击打断恢复协程
        StartCoroutine(HurtRecoveryRoutine());
    }

    // 挨打硬直恢复计时器
    private IEnumerator HurtRecoveryRoutine()
    {
        isHurting = true;

        // 让怪物无法反击并停顿一段时间（比如 0.3 秒，根据你的 Hurt 序列帧播放完的时长微调）
        yield return new WaitForSeconds(0.3f);

        isHurting = false;
    }

    private void Die()
    {
        isDead = true;
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
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, chaseRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}