using UnityEngine;
using System.Collections;
using UnityEngine.Rendering.Universal; // URP Light2D 命名空间

public class BossPhaseController : MonoBehaviour
{
    [Header("UI 设置")]
    [Tooltip("Boss 转阶段期间需要激活显示的 UI (如按 F 提示/Prompt UI)")]
    public GameObject phaseUI;

    [Header("灯光控制参数")]
    [Tooltip("黑暗状态下的灯光强度（设为 0 即全黑，设为 0.05 可保留微弱可见度）")]
    public float darkIntensity = 0f;
    [Tooltip("变暗/恢复的平滑过渡时间（秒）")]
    public float fadeDuration = 0.8f;
    [Tooltip("转阶段时的灯光颜色（可选：如暗红 Color.red 或暗紫，若不勾选使用原色则保持原色）")]
    public bool changeColorOnDark = false;
    public Color darkColor = new Color(0.2f, 0f, 0f, 1f); // 默认暗红色

    [Header("背刺与提示参数")]
    public GameObject attackWarningVFX;        // 预警特效 Prefab
    public float warningDuration = 1.0f;       // 预警（可反制）时间（1秒）

    [Header("状态标记（静态变量：供其它脚本直接调用）")]
    public static bool isTransitioning = false;  // 是否正在进行转阶段演出/背刺小游戏
    public static bool isPhaseTwoActive = false; // 记录 Boss 是否已正式进入二阶段
    public static bool canBeCountered = false;   // 当前是否处于玩家按 F 可反制的窗口期

    // 内部自动获取的组件与引用
    private BossAIController bossAI;
    private Animator bossAnim;
    private Transform playerTransform;
    private Light2D globalLight;               // 动态获取到的全局灯光组件
    private Collider2D bossCollider;           // Boss 的碰撞体
    private Rigidbody2D bossRb;                // Boss 的刚体
    private float originalGravity = 1f;        // 记录 Boss 初始重力

    private float originalIntensity = 1f;       // 保存初始灯光强度
    private Color originalColor = Color.white;  // 保存初始灯光颜色
    private Coroutine fadeLightCoroutine;      // 用于平滑过渡灯光的协程引用

    private int currentBackstabCount = 0;       // 当前已发起的背刺次数
    private bool isPlayerInterrupted = false;  // 玩家是否成功打断了攻击

    private void Awake()
    {
        // 重新进入场景时重置静态变量状态，防止旧数据残留
        isTransitioning = false;
        isPhaseTwoActive = false;
        canBeCountered = false;

        bossAI = GetComponent<BossAIController>();
        bossAnim = GetComponentInChildren<Animator>();
        bossCollider = GetComponent<Collider2D>();
        bossRb = GetComponent<Rigidbody2D>();

        if (bossRb != null)
        {
            originalGravity = bossRb.gravityScale;
        }

        // 自动查找 UI（如果在 Inspector 中没拖）
        if (phaseUI == null)
        {
            Transform foundUI = transform.Find("PhaseUI");
            if (foundUI != null) phaseUI = foundUI.gameObject;
        }

        if (phaseUI != null)
        {
            phaseUI.SetActive(false);
        }
    }

    private void Start()
    {
        FindGlobalLightInLantern();
        FindPlayer();
    }

    private void Update()
    {
        if (playerTransform == null) FindPlayer();
        if (globalLight == null) FindGlobalLightInLantern();

        // 血量低于 60% 触发转阶段
        if (!isTransitioning && !isPhaseTwoActive && bossAI != null && BossAIController.maxHealth > 0)
        {
            float healthPercent = (float)BossAIController.currentHealth / BossAIController.maxHealth;
            if (healthPercent <= 0.6f)
            {
                StartCoroutine(StartPhaseTransitionRoutine());
            }
        }
    }

    private void FindGlobalLightInLantern()
    {
        GameObject lanternObj = GameObject.Find("Lantern");
        if (lanternObj != null)
        {
            globalLight = lanternObj.GetComponentInChildren<Light2D>(true);
            if (globalLight != null)
            {
                originalIntensity = globalLight.intensity;
                originalColor = globalLight.color;
            }
        }
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
    /// 转阶段总控流程
    /// </summary>
    private IEnumerator StartPhaseTransitionRoutine()
    {
        isTransitioning = true;

        if (phaseUI != null)
        {
            phaseUI.SetActive(true);
        }

        if (bossAI != null)
        {
            bossAI.enabled = false; // 暂停 Boss 常规 Update AI 逻辑
        }

        // 防护机制：转阶段期间关闭碰撞体 & 锁定重力并冻结速度，防止撞玩家或掉落
        if (bossCollider != null)
        {
            bossCollider.enabled = false;
        }
        if (bossRb != null)
        {
            bossRb.gravityScale = 0f;
            bossRb.linearVelocity = Vector2.zero;
        }

        Debug.Log("🌑 血量低于 60%，Boss 开始进入转阶段流程！");

        // 渐变降低灯光强度（变黑）
        SetSceneDarkness(true);

        yield return new WaitForSeconds(1.5f); // 沉浸感停顿

        currentBackstabCount = 0;

        while (currentBackstabCount < 3)
        {
            float waitTime = Random.Range(3f, 5f);
            yield return new WaitForSeconds(waitTime);

            isPlayerInterrupted = false;

            currentBackstabCount++;
            Debug.Log($"⚔️ 发起第 {currentBackstabCount} 次背刺轮次！");

            // 执行单次背刺尝试
            yield return StartCoroutine(ExecuteBackstabAttempt());

            // 本轮攻击结束后，如果未达到 3 次，Boss 再次在暗中隐形，维持黑暗准备下一次攻击
            if (currentBackstabCount < 3)
            {
                if (bossAI != null && bossAI.visualTransform != null)
                {
                    bossAI.visualTransform.gameObject.SetActive(false);
                }
                SetSceneDarkness(true); // 确保维持黑暗状态
            }
        }

        CompletePhaseTransition();
    }

    /// <summary>
    /// 单次背刺逻辑（第一时间锁定坐标，且未反制时前两次攻击全程保持黑暗）
    /// </summary>
    private IEnumerator ExecuteBackstabAttempt()
    {
        if (bossAI == null) yield break;

        // 🌟 1. 【坐标第一时间锁定】：在 Warning 生成时刻，立即计算并锁定背刺的绝对目标坐标
        Vector3 targetPosition = transform.position;
        if (playerTransform != null)
        {
            PlayerController playerCtrl = playerTransform.GetComponent<PlayerController>();
            float playerFacingDir = (playerCtrl != null && playerCtrl.facingDirectionParam == "left") ? -1f : 1f;

            float targetX = playerTransform.position.x - (playerFacingDir * bossAI.offsetBehindPlayer);
            float targetY = playerTransform.position.y + bossAI.offsetUpPlayer;
            targetPosition = new Vector3(targetX, targetY, playerTransform.position.z);
        }

        // 🌟 2. 生成预警特效在锁定的绝对坐标上
        GameObject currentVFXInstance = null;
        if (attackWarningVFX != null)
        {
            currentVFXInstance = Instantiate(attackWarningVFX, targetPosition, Quaternion.identity);

            SpriteRenderer sr = currentVFXInstance.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = 999;
        }

        // 预警期间 Boss 保持隐形/隐藏
        if (bossAI.visualTransform != null)
        {
            bossAI.visualTransform.gameObject.SetActive(false);
        }

        // 3. 开启玩家反制窗口期
        canBeCountered = true;

        // 4. 预警倒计时（监听玩家是否按下 F 进行反制）
        float timer = 0f;
        while (timer < warningDuration)
        {
            // 🌟 分支 A：玩家按 F 成功反制打断
            if (isPlayerInterrupted)
            {
                canBeCountered = false; // 关闭反制窗口期

                if (currentVFXInstance != null) Destroy(currentVFXInstance);

                // 反击打断成功：Boss 瞬间出现在锁定的目标点并呈现打断/受击表现
                transform.position = targetPosition;
                if (bossAI.visualTransform != null)
                {
                    bossAI.visualTransform.gameObject.SetActive(true);
                }

                // 成功打断时，灯光短暂变亮以提供“反击成功”的强烈视觉反馈
                SetSceneDarkness(false);

                if (bossAnim != null)
                {
                    bossAnim.ResetTrigger("Disappear");
                    bossAnim.SetTrigger("Disappear");
                }

                yield return new WaitForSeconds(1.2f);
                yield break; // 成功打断，提前退出单次背刺
            }

            timer += Time.deltaTime;
            yield return null;
        }

        // 🌟 分支 B：玩家未按 F（选择走位规避或超时）
        canBeCountered = false; // 窗口期结束
        if (currentVFXInstance != null) Destroy(currentVFXInstance);

        // 5. 将 Boss 严格传送至【步骤 1 锁定好的 targetPosition】，玩家跑开了就能躲掉伤害！
        transform.position = targetPosition;

        // 显示 Boss 视觉
        if (bossAI.visualTransform != null)
        {
            bossAI.visualTransform.gameObject.SetActive(true);
        }

        // 🌟 6. 【灯光控制】：如果不是最后一次攻击（第 1、2 次），场景保持黑暗，决不变亮！
        // 只有到了第 3 次（最后一次攻击），才开始点亮场景恢复光明！
        if (currentBackstabCount == 3)
        {
            SetSceneDarkness(false);
        }

        // 7. 播放背刺攻击动画与判定伤害
        if (bossAnim != null)
        {
            bossAnim.ResetTrigger("Attack");
            bossAnim.SetTrigger("Attack");
        }

        // 判定伤害（若玩家已走出预警点范围，将不会被命中）
        bossAI.TriggerAttackDamage();

        // 8. 攻击后摇等待
        yield return new WaitForSeconds(bossAI.skillPostDelay);
    }

    /// <summary>
    /// 供玩家反制脚本（PlayerBackstabCounter）调用的打断接口
    /// </summary>
    public void InterruptBossAttack()
    {
        if (isTransitioning && canBeCountered)
        {
            isPlayerInterrupted = true;
        }
    }

    private void SetSceneDarkness(bool isDark)
    {
        if (globalLight == null) FindGlobalLightInLantern();

        if (globalLight != null)
        {
            float targetIntensity = isDark ? darkIntensity : originalIntensity;
            Color targetColor = (isDark && changeColorOnDark) ? darkColor : originalColor;

            if (fadeLightCoroutine != null)
            {
                StopCoroutine(fadeLightCoroutine);
            }

            fadeLightCoroutine = StartCoroutine(FadeLightRoutine(targetIntensity, targetColor, fadeDuration));
        }
    }

    private IEnumerator FadeLightRoutine(float targetIntensity, Color targetColor, float duration)
    {
        float startIntensity = globalLight.intensity;
        Color startColor = globalLight.color;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            globalLight.intensity = Mathf.Lerp(startIntensity, targetIntensity, t);
            globalLight.color = Color.Lerp(startColor, targetColor, t);

            yield return null;
        }

        globalLight.intensity = targetIntensity;
        globalLight.color = targetColor;
    }

    private void CompletePhaseTransition()
    {
        isTransitioning = false;
        isPhaseTwoActive = true;
        canBeCountered = false;

        if (phaseUI != null)
        {
            phaseUI.SetActive(false);
        }

        // 转阶段彻底结束，恢复碰撞体与重力
        if (bossCollider != null)
        {
            bossCollider.enabled = true;
        }

        if (bossRb != null)
        {
            bossRb.gravityScale = originalGravity;
        }

        if (bossAI != null)
        {
            if (bossAI.visualTransform != null)
            {
                bossAI.visualTransform.gameObject.SetActive(true);
            }

            bossAI.enabled = true; // 恢复 Boss 常规 AI 逻辑
        }

        SetSceneDarkness(false); // 确保恢复原本正常灯光

        Debug.Log("🌕 转阶段完成！场景重新点亮，Boss 正式进入二阶段！");
    }
}