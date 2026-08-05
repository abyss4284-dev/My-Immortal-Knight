using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections;

public class PlayerBackstabCounter : MonoBehaviour
{
    [Header("按键设置")]
    public KeyCode counterKey = KeyCode.F;     // 反制触发按键

    [Header("玩家自带灯光设置")]
    public Light2D playerGlobalLight;          // 挂载在玩家身上的 Light2D 组件
    public float activeIntensity = 1f;         // 按下 F 键后的灯光强度
    public float flashDuration = 0.5f;         // 灯光亮起的持续时间（秒）

    [Header("=== 反制成功计数设置 ===")]
    public int maxSuccessfulCounters = 3;      // 成功反制的上限次数（达到后不再变黑）
    private int counterSuccessCount = 0;       // 当前成功反制次数

    private Coroutine lightFlashCoroutine;     // 灯光闪烁协程引用

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
        // 按下 F 键触发逻辑
        if (Input.GetKeyDown(counterKey))
        {
            // 检测 Boss 是否处于转阶段，且当前正处于可反制/预警窗口
            if (BossPhaseController.isTransitioning)
            {
                ExecuteCounterOnBoss();
            }
            else
            {
                // 未命中反制时仅播放普通闪烁
                TriggerPlayerLightFlash();
            }
        }
    }

    /// <summary>
    /// 触发玩家身上的灯光闪烁：强度 0 -> 1 -> 短暂延迟 -> 0[cite: 6]
    /// </summary>
    private void TriggerPlayerLightFlash()
    {
        if (playerGlobalLight == null) return;

        // 如果已经成功应对 3 次，灯光已经常亮，无需再闪烁[cite: 6]
        if (counterSuccessCount >= maxSuccessfulCounters) return;

        if (lightFlashCoroutine != null)
        {
            StopCoroutine(lightFlashCoroutine);
        }

        lightFlashCoroutine = StartCoroutine(PlayerLightFlashRoutine());
    }

    private IEnumerator PlayerLightFlashRoutine()
    {
        playerGlobalLight.intensity = activeIntensity; // 瞬间亮起（强度 1）[cite: 6]

        yield return new WaitForSeconds(flashDuration);  // 短暂延迟[cite: 6]

        // 只有在未达到成功上限时才重新关灯熄灭（变黑）
        if (counterSuccessCount < maxSuccessfulCounters)
        {
            playerGlobalLight.intensity = 0f;          // 恢复为 0[cite: 6]
        }
    }

    /// <summary>
    /// 执行反制 Boss 的逻辑[cite: 6]
    /// </summary>
    private void ExecuteCounterOnBoss()
    {
        BossPhaseController bossPhaseCtrl = FindFirstObjectByType<BossPhaseController>(); //[cite: 6]
        if (bossPhaseCtrl != null)
        {
            bossPhaseCtrl.InterruptBossAttack(); //[cite: 6]
            counterSuccessCount++; // 成功计数 +1

            Debug.Log($"⚡ [PlayerBackstabCounter] 玩家成功按 F 反制了 Boss！当前成功次数：{counterSuccessCount}/{maxSuccessfulCounters}");

            // 🌟 核心逻辑：若已满 3 次，常亮灯光并取消黑屏模式
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
                // 前两次成功时正常触发一次闪烁并熄灭[cite: 6]
                TriggerPlayerLightFlash();
            }
        }
    }
}