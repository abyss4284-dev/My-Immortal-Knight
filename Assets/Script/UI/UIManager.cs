using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("=== 能量/蓝条 UI 设置 ===")]
    public Image soulLiquidImage; // 绑定原先的能量条[cite: 2]

    [Header("=== 血量 UI 设置 ===")]
    public Transform healthBarContainer; //[cite: 2]
    public GameObject maskPrefab;        //[cite: 2]

    private List<Image> spawnedMasks = new List<Image>(); //[cite: 2]

    [Header("=== 测试用美术资源（可选） ===")]
    public Sprite fullMaskSprite;  //[cite: 2]
    public Sprite emptyMaskSprite; //[cite: 2]

    // 缓存追踪的玩家与其技能管理组件
    private PlayerController targetPlayer;
    private PlayerSkillManager targetSkillManager; // 🌟 新增：追踪技能脚本

    public void SetupUI(PlayerController player)
    {
        targetPlayer = player;

        if (targetPlayer != null)
        {
            InitializeHealthUI(targetPlayer.maxHealth); //[cite: 2]
            UpdateHealthUI(targetPlayer.currentHealth); //[cite: 2]

            // 🌟 尝试在玩家身上直接抓取技能组件
            targetSkillManager = player.GetComponent<PlayerSkillManager>();
            if (targetSkillManager != null)
            {
                UpdateSoulUI(targetSkillManager.currentMana, targetSkillManager.maxMana);
            }
        }
    }

    // 🌟 新增：供 PlayerSkillManager 诞生时反向注册
    public void SetupSkillManager(PlayerSkillManager skillManager)
    {
        targetSkillManager = skillManager;
        if (targetSkillManager != null)
        {
            UpdateSoulUI(targetSkillManager.currentMana, targetSkillManager.maxMana);
        }
    }

    void Update()
    {
        if (targetPlayer != null)
        {
            UpdateHealthUI(targetPlayer.currentHealth); //[cite: 2]
        }

        // 🌟 修改：监控 SkillManager 的蓝量数值，代替原本的 PlayerController 蓝量监控[cite: 2]
        if (targetSkillManager != null)
        {
            float targetFill = (float)targetSkillManager.currentMana / targetSkillManager.maxMana;
            if (Mathf.Abs(soulLiquidImage.fillAmount - targetFill) > 0.001f) //[cite: 2]
            {
                soulLiquidImage.fillAmount = Mathf.MoveTowards(soulLiquidImage.fillAmount, targetFill, 5f * Time.deltaTime); //[cite: 2]
            }
        }
    }

    public void InitializeHealthUI(int maxHealth)
    {
        foreach (Transform child in healthBarContainer) { Destroy(child.gameObject); } //[cite: 2]
        spawnedMasks.Clear(); //[cite: 2]

        for (int i = 0; i < maxHealth; i++) //[cite: 2]
        {
            GameObject newMask = Instantiate(maskPrefab, healthBarContainer, false); //[cite: 2]

            RectTransform rect = newMask.GetComponent<RectTransform>(); //[cite: 2]
            if (rect != null) //[cite: 2]
            {
                rect.localPosition = Vector3.zero; //[cite: 2]
                rect.localRotation = Quaternion.identity; //[cite: 2]
                rect.localScale = Vector3.one; //[cite: 2]
            }

            Image maskImage = newMask.GetComponent<Image>(); //[cite: 2]
            maskImage.sprite = fullMaskSprite; //[cite: 2]
            spawnedMasks.Add(maskImage); //[cite: 2]
        }
    }

    public void UpdateHealthUI(int currentHealth)
    {
        for (int i = 0; i < spawnedMasks.Count; i++) //[cite: 2]
        {
            if (i < currentHealth) //[cite: 2]
            {
                spawnedMasks[i].sprite = fullMaskSprite; //[cite: 2]
                spawnedMasks[i].enabled = true; //[cite: 2]
            }
            else
            {
                spawnedMasks[i].sprite = emptyMaskSprite; //[cite: 2]
            }
        }
    }

    public void UpdateSoulUI(int currentSoul, int maxSoul)
    {
        if (maxSoul == 0) return; //[cite: 2]

        float fillPercentage = (float)currentSoul / maxSoul; //[cite: 2]

        StopAllCoroutines(); //[cite: 2]
        StartCoroutine(AnimateSoulBar(fillPercentage)); //[cite: 2]
    }

    private IEnumerator AnimateSoulBar(float targetFill)
    {
        float speed = 5f; //[cite: 2]
        while (Mathf.Abs(soulLiquidImage.fillAmount - targetFill) > 0.005f) //[cite: 2]
        {
            soulLiquidImage.fillAmount = Mathf.MoveTowards(soulLiquidImage.fillAmount, targetFill, speed * Time.deltaTime); //[cite: 2]
            yield return null; //[cite: 2]
        }
        soulLiquidImage.fillAmount = targetFill; //[cite: 2]
    }
}