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

    // 内部自动引用的场景传送门（无需手动拖入）
    private GameObject scenePortalObject;

    [Header("=== 二阶段技能 1：弹幕传送门 ===")]
    [Tooltip("传送门预制体（内含弹幕攻击判定与动画，由自身动画事件控制销毁）")]
    public GameObject barragePortalPrefab;
    [Tooltip("Boss 身上的传送门子物体（若有，死亡时将被强行隐藏）")]
    public GameObject portalChildObject;
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

    [Tooltip("死亡动画的 Trigger 参数名称")]
    public string deathAnimTriggerParam = "Die";

    [Header("=== 二阶段死亡演出设置 ===")]
    [Tooltip("死亡慢动作缩放（0.2 表示正常速度的 20%，即 5 倍减速）")]
    public float deathSlowMotionScale = 0.2f;

    [Header("--- 动态生成白布遮罩配置 ---")]
    [Tooltip("用于生成的白色遮罩 GameObject (图层层级请直接在 Prefab 的 SpriteRenderer 中设置)")]
    public GameObject whiteOverlayPrefab;

    [Tooltip("生成遮罩时的缩放倍数（需保证能盖满屏幕，默认 100x100）")]
    public Vector2 overlaySize = new Vector2(100f, 100f);

    [Tooltip("白布淡入时间（秒）")]
    public float fadeInDuration = 0.6f;

    [Tooltip("全白遮挡保持延迟时间（秒）")]
    public float holdDuration = 0.5f;

    [Tooltip("白布淡出时间（秒）")]
    public float fadeOutDuration = 0.8f;

    [Tooltip("死亡播放完毕后激活的胜利 UI（可选）")]
    public GameObject victoryUI;

    // 内部组件引用
    private BossAIController bossAI;
    private Animator anim;
    private Transform playerTransform;

    private bool hasInitializedPhaseTwo = false;
    private float skillCooldownTimer = 0f;            // 传送门技能冷却计时器
    private float counterBackstabCooldownTimer = 0f;  // 受击反击冷却计时器

    // 排队与占线管理变量
    private bool isPortalSkillQueued = false;         // 传送门技能排队/占线等待标记

    // 受击反击技能内部状态变量
    private int currentHitCount = 0;             // 当前受击次数
    private int targetHitThreshold = 3;          // 随机触发阈值 (3-5)
    private bool isCounterBackstabReady = false; // 是否进入“受击预备反击”状态

    private void Awake()
    {
        bossAI = GetComponent<BossAIController>();
        anim = GetComponentInChildren<Animator>();

        // 🌟 1. 自动在场景中寻找名为 DemonPalace-Town 的传送门物体
        FindScenePortal();

        // 2. 自动隐藏 Boss 身上的技能传送门子物体（防误触发）
        if (portalChildObject == null)
        {
            Transform pTransform = transform.Find("Portal");
            if (pTransform != null) portalChildObject = pTransform.gameObject;
        }
    }

    /// <summary>
    /// 自动检索场景中名为 DemonPalace-Town 的物体（支持未激活/隐藏状态的物体）
    /// </summary>
    private void FindScenePortal()
    {
        if (scenePortalObject != null) return;

        // 尝试寻找激活状态的物体
        scenePortalObject = GameObject.Find("DemonPalace-Town");

        // 如果物体处于禁用(Inactive)状态，GameObject.Find 找不到，采用深度搜寻保底
        if (scenePortalObject == null)
        {
            Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
            foreach (Transform t in allTransforms)
            {
                // 排除预制体资源，仅查找场景中的 GameObject
                if (t.gameObject.name == "DemonPalace-Town" && t.gameObject.scene.isLoaded)
                {
                    scenePortalObject = t.gameObject;
                    break;
                }
            }
        }

        if (scenePortalObject != null)
        {
            Debug.Log("🔍【BossPhaseTwoController】已成功自动定位场景传送门: DemonPalace-Town");
        }
    }

    private void Start()
    {
        FindPlayer();
        ResetHitCounter();
        counterBackstabCooldownTimer = counterBackstabCooldown;
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

        if (counterBackstabCooldownTimer < counterBackstabCooldown)
        {
            counterBackstabCooldownTimer += Time.deltaTime;
        }

        skillCooldownTimer += Time.deltaTime;

        if (skillCooldownTimer >= skillCooldown)
        {
            isPortalSkillQueued = true;
        }

        if (isPortalSkillQueued && bossAI != null && !bossAI.IsBusy)
        {
            ExecutePortalSkill();
        }
    }

    private void InitializePhaseTwo()
    {
        if (anim != null && phaseTwoController != null)
        {
            anim.runtimeAnimatorController = phaseTwoController;
        }

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
        counterBackstabCooldownTimer = counterBackstabCooldown;
        ResetHitCounter();
        hasInitializedPhaseTwo = true;
        Debug.Log("🔥【BossPhaseTwoController】二阶段初始化完成！");
    }

    public bool IsPortalSkillPending()
    {
        return isPortalSkillQueued || (skillCooldownTimer >= skillCooldown);
    }

    public void ExecutePortalSkill()
    {
        isPortalSkillQueued = false;
        skillCooldownTimer = 0f;
        StartCoroutine(BarragePortalSkillRoutine());
    }

    public void OnBossTakeDamage()
    {
        if (!BossPhaseController.isPhaseTwoActive || BossPhaseController.isTransitioning) return;

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

        if (isCounterBackstabReady && counterBackstabCooldownTimer >= counterBackstabCooldown)
        {
            Debug.Log("⚡【受击反击】条件满足！立即打断当前行为并进行闪现背刺！");

            StopAllCoroutines();
            if (bossAI != null) bossAI.StopAllCoroutines();

            if (anim != null)
            {
                anim.SetBool(portalSkillBoolParam, false);
            }

            skillCooldownTimer = 0f;

            ResetHitCounter();
            counterBackstabCooldownTimer = 0f;

            StartCoroutine(InstantCounterBackstabRoutine());
        }
        else if (isCounterBackstabReady)
        {
            Debug.Log($"⏳ 反击预备就绪，但反击技能冷却中... 剩余冷却: {(counterBackstabCooldown - counterBackstabCooldownTimer):F1}s");
        }
    }

    private void ResetHitCounter()
    {
        currentHitCount = 0;
        isCounterBackstabReady = false;
        isPortalSkillQueued = false;
        targetHitThreshold = Random.Range(3, 6);
    }

    private IEnumerator InstantCounterBackstabRoutine()
    {
        if (bossAI == null) yield break;

        yield return StartCoroutine(bossAI.ExecuteBackstabRoutine(withPortal: false));

        skillCooldownTimer = 0f;
    }

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
        skillCooldownTimer = 0f;
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
        // 1. 强行终止 Boss 主 AI 和二阶段的所有活动协程
        if (bossAI != null)
        {
            bossAI.StopAllCoroutines();
            bossAI.IsBusy = true;
            bossAI.enabled = false;
        }
        StopAllCoroutines();

        // 2. 强行禁用技能传送门子物体，防止误触发激活
        if (portalChildObject != null)
        {
            portalChildObject.SetActive(false);
        }

        // 3. 禁用碰撞体与刚体
        Collider2D bossCol = GetComponent<Collider2D>();
        if (bossCol != null) bossCol.enabled = false;

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
        }

        // 4. 重置动画参数，并播放指定的死亡动画
        if (anim != null)
        {
            anim.SetBool(portalSkillBoolParam, false);
            anim.ResetTrigger("Attack");
            anim.ResetTrigger("Disappear");
            anim.ResetTrigger("Hit");
            anim.SetFloat("Speed", 0f);

            // 触发死亡 Trigger
            anim.ResetTrigger(deathAnimTriggerParam);
            anim.SetTrigger(deathAnimTriggerParam);
        }

        // 5. 开启慢动作（子弹时间）
        Time.timeScale = deathSlowMotionScale;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        // 6. 启动独立的白布过渡与胜利 UI 显示
        SpawnWhiteOverlayProcess();

        Debug.Log("💀【Boss】死亡流程已启动，播放死亡动画中...");
    }

    /// <summary>
    /// 🌟 动画事件（Animation Event）：在死亡动画最后一帧由 BossAnimationRelay 中继调用
    /// 作用：重新激活场景传送门 DemonPalace-Town，并销毁 Boss 物体
    /// </summary>
    public void OnDeathAnimFinished()
    {
        Debug.Log("🎬【Animation Event】Boss 死亡动画最后一帧到达，恢复游戏逻辑！");

        // 1. 恢复正常游戏时间缩放（防止子弹时间停留在 0.2）
        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = 0.02f;

        // 🌟 2. 确保引用并重新激活场景中的出口传送门 DemonPalace-Town
        if (scenePortalObject == null)
        {
            FindScenePortal();
        }

        if (scenePortalObject != null)
        {
            scenePortalObject.SetActive(true);
            Debug.Log("🌀【通关】场景传送门 DemonPalace-Town 已成功激活！");
        }
        else
        {
            Debug.LogError("🚨 [BossPhaseTwoController] 未能在场景中找到名为 'DemonPalace-Town' 的物体！");
        }

        // 3. 彻底销毁 Boss 物体
        Destroy(gameObject);
    }

    /// <summary>
    /// 生成并独立托管白布遮罩与胜利 UI 逻辑
    /// </summary>
    private void SpawnWhiteOverlayProcess()
    {
        GameObject instanceObj = null;

        if (whiteOverlayPrefab != null)
        {
            instanceObj = Instantiate(whiteOverlayPrefab, Vector3.zero, Quaternion.identity);
        }
        else
        {
            instanceObj = new GameObject("DeathWhiteOverlay_Fallback");
            instanceObj.transform.position = Vector3.zero;
        }

        instanceObj.transform.localScale = new Vector3(overlaySize.x, overlaySize.y, 1f);

        SpriteRenderer sr = instanceObj.GetComponentInChildren<SpriteRenderer>();
        if (sr == null)
        {
            sr = instanceObj.AddComponent<SpriteRenderer>();
        }

        sr.color = new Color(1f, 1f, 1f, 0f);

        // 动态添加独立 Runner，确保 Boss 被 Destroy 后白布遮罩与胜利界面依然正常运行
        WhiteOverlayFadeRunner runner = instanceObj.AddComponent<WhiteOverlayFadeRunner>();
        runner.StartFadeProcess(sr, fadeInDuration, holdDuration, fadeOutDuration, victoryUI);
    }
}

/// <summary>
/// 独立托管白布淡入淡出及胜利 UI 的辅助组件（运行时自动挂载至遮罩物体）
/// </summary>
public class WhiteOverlayFadeRunner : MonoBehaviour
{
    public void StartFadeProcess(SpriteRenderer sr, float fadeIn, float hold, float fadeOut, GameObject victoryUI)
    {
        StartCoroutine(FadeRoutine(sr, fadeIn, hold, fadeOut, victoryUI));
    }

    private IEnumerator FadeRoutine(SpriteRenderer sr, float fadeIn, float hold, float fadeOut, GameObject victoryUI)
    {
        // 1. 淡入 (0 -> 1)
        float elapsed = 0f;
        while (elapsed < fadeIn)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Clamp01(elapsed / fadeIn);
            if (sr != null) sr.color = new Color(1f, 1f, 1f, alpha);
            yield return null;
        }
        if (sr != null) sr.color = new Color(1f, 1f, 1f, 1f);

        // 2. 保持全白
        if (hold > 0f)
        {
            yield return new WaitForSecondsRealtime(hold);
        }

        // 3. 淡出 (1 -> 0)
        elapsed = 0f;
        while (elapsed < fadeOut)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Clamp01(1f - (elapsed / fadeOut));
            if (sr != null) sr.color = new Color(1f, 1f, 1f, alpha);
            yield return null;
        }
        if (sr != null) sr.color = new Color(1f, 1f, 1f, 0f);

        // 4. 弹出胜利界面
        if (victoryUI != null)
        {
            victoryUI.SetActive(true);
        }

        // 5. 销毁白布遮罩物体本身
        Destroy(gameObject);
    }
}