using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections;

public class PlayerBackstabCounter : MonoBehaviour
{
    [Header("按键设置")]
    public KeyCode counterKey = KeyCode.F;     // 反制触发按键
    public float counterCooldown = 0.8f;       // 按键冷却时间（防止玩家乱按连发）

    [Header("玩家自带灯光设置")]
    public Light2D playerGlobalLight;          // 挂载在玩家身上的 Light2D 组件
    public float activeIntensity = 1f;         // 按下 F 键后的灯光强度
    public float flashDuration = 0.5f;         // 灯光亮起的持续时间（秒）

    [Header("=== 反制成功计数设置 ===")]
    public int maxSuccessfulCounters = 3;      // 成功反制的上限次数（达到后不再变黑）
    private int counterSuccessCount = 0;       // 当前成功反制次数

    private Coroutine lightFlashCoroutine;     // 灯光闪烁协程引用
    private float lastCounterTime = -999f;     // 上次按键时间（用于冷却检查）

    private void Start()
    {
        // 自动尝试获取玩家子物体上的 Light2D 组件
        if (playerGlobalLight == null)
        {
            playerGlobalLight = GetComponentInChildren<Light2D>();
        }

        // 初始化强度为 0
        if (playerGlobalLight != null)
        {
            playerGlobalLight.intensity = 0f;
        }
        else
        {
            Debug.LogWarning("⚠️ [PlayerBackstabCounter] 未检测到 Player 身上的 Light2D 组件！");
        }
    }

    private void Update()
    {
        // 仅在按下 F 键且过了按键冷却时间后响应
        if (Input.GetKeyDown(counterKey) && Time.time >= lastCounterTime + counterCooldown)
        {
            lastCounterTime = Time.time;

            // 🌟 核心判断：只有当 Boss 处于转阶段且正处于【预警/可反制窗口期 (canBeCountered)】时才响应
            if (BossPhaseController.isTransitioning && BossPhaseController.canBeCountered)
            {
                ExecuteCounterOnBoss();
            }
            // 🌟 非应对窗口期直接忽略：不上亮灯光，也不触发反制
        }
    }

    /// <summary>
    /// 触发玩家身上的灯光闪烁：强度 0 -> 1 -> 短暂延迟 -> 0
    /// </summary>
    private void TriggerPlayerLightFlash()
    {
        if (playerGlobalLight == null) return;

        // 如果已经成功应对 3 次，灯光已经常亮，无需再闪烁
        if (counterSuccessCount >= maxSuccessfulCounters) return;

        if (lightFlashCoroutine != null)
        {
            StopCoroutine(lightFlashCoroutine);
        }

        lightFlashCoroutine = StartCoroutine(PlayerLightFlashRoutine());
    }

    private IEnumerator PlayerLightFlashRoutine()
    {
        playerGlobalLight.intensity = activeIntensity; // 瞬间亮起（强度 1）

        yield return new WaitForSeconds(flashDuration);  // 短暂延迟

        // 只有在未达到成功上限时才重新关灯熄灭（变黑）
        if (counterSuccessCount < maxSuccessfulCounters)
        {
            playerGlobalLight.intensity = 0f;          // 恢复为 0
        }
    }

    /// <summary>
    /// 执行反制 Boss 的逻辑
    /// </summary>
    private void ExecuteCounterOnBoss()
    {
        BossPhaseController bossPhaseCtrl = Object.FindFirstObjectByType<BossPhaseController>();
        if (bossPhaseCtrl != null)
        {
            // 🌟 直接调用返回 void 的 InterruptBossAttack 方法
            bossPhaseCtrl.InterruptBossAttack();
            counterSuccessCount++; // 成功计数 +1

            Debug.Log($"⚡ [PlayerBackstabCounter] 玩家成功按 F 反制了 Boss！当前成功次数：{counterSuccessCount}/{maxSuccessfulCounters}");

            // 若已满 3 次，常亮灯光并取消黑屏模式
            if (counterSuccessCount >= maxSuccessfulCounters)
            {
                if (lightFlashCoroutine != null)
                {
                    StopCoroutine(lightFlashCoroutine);
                }

                if (playerGlobalLight != null)
                {
                    playerGlobalLight.intensity = activeIntensity; // 保持常亮，不再熄灭变黑
                }

                Debug.Log("☀️ [PlayerBackstabCounter] 已成功反制 3 次，场景不再变黑！");
            }
            else
            {
                // 前两次成功时正常触发一次闪烁并熄灭
                TriggerPlayerLightFlash();
            }
        }
    }
}