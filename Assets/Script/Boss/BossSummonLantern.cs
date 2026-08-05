using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections;

public class BossSummonLantern : MonoBehaviour
{
    [Header("按键与提示设置")]
    public KeyCode interactKey = KeyCode.F; // 交互按键，默认 F
    public GameObject interactUI;          // 按键提示 UI（如 "按 F 召唤"）

    [Header("灯台视觉与光照组件")]
    public GameObject unlitLightObject;     // 未点亮的灯台子物体/Sprite
    public GameObject litLightObject;       // 已点亮（带发光/特效）的灯台子物体/Sprite
    public Light2D pointLight;              // 场景 Light2D 点光组件

    [Header("传送门控制")]
    [Tooltip("需要关闭的传送门（如果为空，将自动按 Tag 'Portal' 在场景中寻找）")]
    public GameObject portalObject;         // 场景中的传送门物体

    [Header("Boss 召唤设置")]
    public GameObject bossPrefab;           // Boss 的预制体 (Prefab)
    public float spawnXOffset = 3f;         // 向右偏移的召唤距离
    public float spawnYOffset = 0f;         // 向上/下微调的召唤距离
    public float summonDelay = 1.5f;        // 按下 F 键后延迟多久召唤 Boss

    [Header("音效/特效 (可选)")]
    public GameObject summonVFXPrefab;      // 在 Boss 出生点播放的召唤法阵/粒子特效

    // 内部状态标记
    private bool isPlayerInRange = false;
    private bool isActivated = false;       // 是否已经使用过（确保只能触发一次）

    private void Start()
    {
        // 初始化灯台状态
        if (unlitLightObject != null) unlitLightObject.SetActive(true);
        if (litLightObject != null) litLightObject.SetActive(false);
        if (interactUI != null) interactUI.SetActive(false);

        // 如果 Inspector 没拖入传送门，尝试自动寻找 Tag 为 "Portal" 的物体
        if (portalObject == null)
        {
            portalObject = GameObject.FindWithTag("Portal");
        }
    }

    private void Update()
    {
        // 只有玩家在区域内、按下 F 键且未激活过时触发
        if (isPlayerInRange && !isActivated && Input.GetKeyDown(interactKey))
        {
            StartCoroutine(SummonBossSequence());
        }
    }

    /// <summary>
    /// 点亮灯台并召唤 Boss 的单次协程
    /// </summary>
    private IEnumerator SummonBossSequence()
    {
        isActivated = true;

        // 1. 隐藏 UI 提示
        if (interactUI != null) interactUI.SetActive(false);

        // 2. 切换灯台视觉与点光
        if (unlitLightObject != null) unlitLightObject.SetActive(false);
        if (litLightObject != null) litLightObject.SetActive(true);
        if (pointLight != null) pointLight.enabled = true;

        // 3. 关闭传送门
        DeactivatePortal();

        Debug.Log("🕯️ [BossSummonLantern] 灯台已被首次点亮！Boss 开始召唤...");

        // 计算 Boss 生成位置
        Vector3 spawnPosition = transform.position + new Vector3(spawnXOffset, spawnYOffset, 0f);

        // 4. 生成法阵/粒子特效
        if (summonVFXPrefab != null)
        {
            GameObject vfx = Instantiate(summonVFXPrefab, spawnPosition, Quaternion.identity);
            Destroy(vfx, summonDelay + 2f);
        }

        // 5. 延迟召唤
        yield return new WaitForSeconds(summonDelay);

        // 6. 生成 Boss
        if (bossPrefab != null)
        {
            GameObject bossObj = Instantiate(bossPrefab, spawnPosition, Quaternion.identity);
            Debug.Log($"⚠️ [BossSummonLantern] Boss [{bossObj.name}] 已成功生成！");
        }
        else
        {
            Debug.LogError("🚨 [BossSummonLantern] 未分配 bossPrefab！");
        }
    }

    /// <summary>
    /// 关闭场景传送门
    /// </summary>
    public void DeactivatePortal()
    {
        if (portalObject != null)
        {
            portalObject.SetActive(false);
            Debug.Log("🌀 [BossSummonLantern] 传送门已被关闭！");
        }
        else
        {
            GameObject[] portals = GameObject.FindGameObjectsWithTag("Portal");
            foreach (GameObject portal in portals)
            {
                portal.SetActive(false);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isActivated && other.CompareTag("Player"))
        {
            isPlayerInRange = true;
            if (interactUI != null) interactUI.SetActive(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = false;
            if (interactUI != null) interactUI.SetActive(false);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 previewPos = transform.position + new Vector3(spawnXOffset, spawnYOffset, 0f);
        Gizmos.DrawWireCube(previewPos, new Vector3(1.5f, 2.5f, 0f));
        Gizmos.DrawLine(transform.position, previewPos);
    }
}