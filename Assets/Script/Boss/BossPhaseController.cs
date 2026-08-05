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

    [Header("背刺与闪光提示参数")]
    public GameObject attackWarningVFX;        // 攻击前 1 秒的闪光提示特效Prefab/子物体
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
        if (!isTransitioning && !isPhaseTwoActive && bossAI != null && bossAI.maxHealth > 0)
        {
            float healthPercent = (float)bossAI.currentHealth / bossAI.maxHealth;
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

        // 🌟 防护机制：转阶段期间关闭碰撞体 & 锁定重力并冻结速度，防止撞玩家或掉落
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
            Debug.Log($"⚔️ 发起第 {currentBackstabCount} 次背刺轮次/攻击！");

            // 执行一次背刺尝试
            yield return StartCoroutine(ExecuteBackstabAttempt());

            if (isPlayerInterrupted)
            {
                Debug.Log("✨ Boss 攻击被打断，开始重新隐入黑暗...");

                // 🌟 先开始将场景变黑
                SetSceneDarkness(true);

                // 🌟 关键等待：等待场景平滑变暗的 fadeDuration 时间完全结束（场景彻底全黑）
                yield return new WaitForSeconds(fadeDuration);

                // 🌟 场景彻底变黑后，才悄悄恢复视觉层，为下一次背刺显示做准备！
                if (bossAI != null && bossAI.visualTransform != null)
                {
                    bossAI.visualTransform.gameObject.SetActive(true);
                }
            }
        }

        CompletePhaseTransition();
    }

    /// <summary>
    /// 单次背刺逻辑
    /// </summary>
    private IEnumerator ExecuteBackstabAttempt()
    {
        if (bossAI == null) yield break;

        // 1. 触发 Boss 的消失/隐身传送逻辑（内部会计算背刺点并激活 portalObject）
        if (bossAnim != null)
        {
            bossAnim.SetTrigger("Disappear");
        }

        bossAI.OnDisappearStart();

        // 🌟 2. 预警特效位置修正：优先依据传送门 (portalObject) 的位置生成
        Vector3 spawnPosition = transform.position;
        if (bossAI.portalObject != null && bossAI.portalObject.activeSelf)
        {
            spawnPosition = bossAI.portalObject.transform.position;
        }
        else if (bossAI.pivotTransform != null)
        {
            spawnPosition = bossAI.pivotTransform.position;
        }

        GameObject currentVFXInstance = null;
        if (attackWarningVFX != null)
        {
            currentVFXInstance = Instantiate(attackWarningVFX, spawnPosition, Quaternion.identity);
            var spriteRenderer = currentVFXInstance.GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.sortingOrder = 999;
            }
            currentVFXInstance.SetActive(true);
        }

        canBeCountered = true;

        // 3. 预警倒计时（监听玩家是否打制）
        float timer = 0f;
        while (timer < warningDuration)
        {
            if (isPlayerInterrupted)
            {
                canBeCountered = false;

                if (currentVFXInstance != null) Destroy(currentVFXInstance);

                SetSceneDarkness(false);

                if (bossAI.visualTransform != null)
                {
                    bossAI.visualTransform.gameObject.SetActive(true);
                }

                if (bossAnim != null)
                {
                    bossAnim.ResetTrigger("Disappear");
                    bossAnim.SetTrigger("Disappear");
                }

                yield return new WaitForSeconds(1.2f);
                yield break;
            }

            timer += Time.deltaTime;
            yield return null;
        }

        canBeCountered = false;

        if (currentVFXInstance != null)
        {
            Destroy(currentVFXInstance);
        }

        // 4. 执行传送背刺（此时会精确按 Pivot 对齐新坐标）
        bossAI.OnDisappearEnd();

        if (bossAI.visualTransform != null)
        {
            bossAI.visualTransform.gameObject.SetActive(true);
        }

        if (bossAnim != null)
        {
            bossAnim.ResetTrigger("Attack");
            bossAnim.SetTrigger("Attack");
        }

        bossAI.TriggerAttackDamage();

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

        // 🌟 转阶段彻底结束，恢复碰撞体与重力
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

        SetSceneDarkness(false); // 恢复原本灯光

        Debug.Log("🌕 转阶段完成！场景重新点亮，Boss 正式进入二阶段！");
    }
}