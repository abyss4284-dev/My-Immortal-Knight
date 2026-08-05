using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("=== 血量 (面具/容器) UI 设置 ===")]
    public Transform healthBarContainer;
    public GameObject maskPrefab;
    public Sprite fullMaskSprite;
    public Sprite emptyMaskSprite;

    [Header("=== 蓝条 (魂/法力) UI 设置 ===")]
    public Image soulLiquidImage;

    // 内部引用的组件
    private PlayerController targetPlayer;
    private PlayerSkillManager targetSkillManager;

    private List<Image> spawnedMasks = new List<Image>();

    // 状态记录
    private int lastRecordedHealth = -999;
    private int lastRecordedMaxHealth = -999;

    void Start()
    {
        // 游戏启动时，强行重绑定一次
        ForceRebindPlayer();
    }

    void Update()
    {
        // 🌟 1. 防御：如果丢失了引用，或者绑定的 Player 被销毁了，重新搜寻
        if (targetPlayer == null || targetSkillManager == null)
        {
            ForceRebindPlayer();
            if (targetPlayer == null) return; // 真的没找到玩家，直接退出本次 Update
        }

        // 🌟 2. 监控最大血量（发生变化时重新生成面具）
        if (targetPlayer.maxHealth != lastRecordedMaxHealth)
        {
            lastRecordedMaxHealth = targetPlayer.maxHealth;
            RebuildHealthContainers(targetPlayer.maxHealth);
            UpdateMaskSprites(targetPlayer.currentHealth);
            lastRecordedHealth = targetPlayer.currentHealth;
        }

        // 🌟 3. 监控当前血量变化
        if (targetPlayer.currentHealth != lastRecordedHealth)
        {
            lastRecordedHealth = targetPlayer.currentHealth;
            UpdateMaskSprites(targetPlayer.currentHealth);
        }

        // 🌟 4. 监控蓝量变化 (直接赋值，排除 MoveTowards 速度极慢导致的卡住现象)
        if (targetSkillManager != null && soulLiquidImage != null)
        {
            if (targetSkillManager.maxMana > 0)
            {
                float fillRatio = (float)targetSkillManager.currentMana / targetSkillManager.maxMana;
                soulLiquidImage.fillAmount = fillRatio;
            }
        }
    }

    /// <summary>
    /// 🌟 强制重新绑定当前场景中【激活状态】的 Player
    /// </summary>
    public void ForceRebindPlayer()
    {
        // 1. 查找所有带 Player 标签的物体
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        foreach (GameObject p in players)
        {
            // 确保只绑定当前处于激活状态的 Player
            if (p.activeInHierarchy)
            {
                targetPlayer = p.GetComponent<PlayerController>();
                targetSkillManager = p.GetComponent<PlayerSkillManager>();

                // 强制重置记录值，保证在下一帧 Update 时触发 UI 重新渲染
                lastRecordedHealth = -999;
                lastRecordedMaxHealth = -999;

                Debug.Log($"✅ [UIManager] 成功绑定活跃玩家: {p.name} (Instance ID: {p.GetInstanceID()})");
                break;
            }
        }

        if (targetPlayer == null)
        {
            Debug.LogWarning("⚠️ [UIManager] 场景中未找到激活的 Player 物体！");
        }
    }

    /// <summary>
    /// 重新生成面具图标
    /// </summary>
    private void RebuildHealthContainers(int maxHealth)
    {
        if (healthBarContainer == null || maskPrefab == null) return;

        // 清空旧面具
        foreach (Transform child in healthBarContainer)
        {
            Destroy(child.gameObject);
        }
        spawnedMasks.Clear();

        // 实例化新面具
        for (int i = 0; i < maxHealth; i++)
        {
            GameObject newMask = Instantiate(maskPrefab, healthBarContainer, false);

            RectTransform rect = newMask.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
            }

            Image maskImage = newMask.GetComponent<Image>();
            if (maskImage != null)
            {
                spawnedMasks.Add(maskImage);
            }
        }
    }

    /// <summary>
    /// 更新面具的图片显示
    /// </summary>
    private void UpdateMaskSprites(int currentHealth)
    {
        for (int i = 0; i < spawnedMasks.Count; i++)
        {
            if (spawnedMasks[i] == null) continue;

            if (i < currentHealth)
            {
                if (fullMaskSprite != null) spawnedMasks[i].sprite = fullMaskSprite;
                spawnedMasks[i].enabled = true;
            }
            else
            {
                if (emptyMaskSprite != null)
                {
                    spawnedMasks[i].sprite = emptyMaskSprite;
                    spawnedMasks[i].enabled = true;
                }
                else
                {
                    spawnedMasks[i].enabled = false;
                }
            }
        }
    }
}