using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; 

public class UIManager : MonoBehaviour
{
    [Header("=== 能量 UI 设置 ===")]
    public Image soulLiquidImage; // 拖入刚才的 Soul_Liquid

    [Header("=== 血量 UI 设置 ===")]
    public Transform healthBarContainer; // 拖入挂有 Horizontal Layout Group 的 Health_Bar
    public GameObject maskPrefab;        // 拖入你的面具小图片预制体

    // 内部用来记录所有生成的面具图片组件，方便后面换皮肤（空或者满）
    private List<Image> spawnedMasks = new List<Image>();

    [Header("=== 测试用美术资源（可选） ===")]
    public Sprite fullMaskSprite;  // 满血面具图案
    public Sprite emptyMaskSprite; // 碎裂/空面具图案

    // 1. 初始化血量阵列（游戏刚启动，或者换地图时调用）
    public void InitializeHealthUI(int maxHealth)
    {
        // 先清空旧的
        foreach (Transform child in healthBarContainer) { Destroy(child.gameObject); }
        spawnedMasks.Clear();

        // 假设 1 点血 = 1 个面具
        for (int i = 0; i < maxHealth; i++)
        {
            // 🌟 修改：用一个变量接收克隆出来的物体
            GameObject newMask = Instantiate(maskPrefab, healthBarContainer, false);

            // 🌟 核心：强行重置它的缩放和坐标，防止它飞走
            RectTransform rect = newMask.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.localPosition = Vector3.zero; // 局部坐标清零，交给布局组重新排版
                rect.localRotation = Quaternion.identity; // 旋转清零
                rect.localScale = Vector3.one;     // 🌟 极其重要：确保缩放比例是 1（100%），防止缩成无限小
            }

            Image maskImage = newMask.GetComponent<Image>();
            maskImage.sprite = fullMaskSprite;
            spawnedMasks.Add(maskImage);
        }
    }

    // 2. 👑 核心方法：刷新血量显示
    public void UpdateHealthUI(int currentHealth)
    {
        for (int i = 0; i < spawnedMasks.Count; i++)
        {
            if (i < currentHealth)
            {
                spawnedMasks[i].sprite = fullMaskSprite; // 亮起
                spawnedMasks[i].enabled = true; // 或者直接显示
            }
            else
            {
                spawnedMasks[i].sprite = emptyMaskSprite; // 变成空面具
            }
        }
    }

    // 3. 👑 核心方法：刷新能量显示（丝滑渐变）
    public void UpdateSoulUI(int currentSoul, int maxSoul)
    {
        // 计算百分比 (0.0 到 1.0)
        float fillPercentage = (float)currentSoul / maxSoul;

        // 丝滑过渡（如果你想瞬间刷新，直接 soulLiquidImage.fillAmount = fillPercentage;）
        StopAllCoroutines(); // 防止上一次的动画没放完
        StartCoroutine(AnimateSoulBar(fillPercentage));
    }

    private IEnumerator AnimateSoulBar(float targetFill)
    {
        float speed = 5f; // 能量涨落动画速度
        while (Mathf.Abs(soulLiquidImage.fillAmount - targetFill) > 0.005f)
        {
            soulLiquidImage.fillAmount = Mathf.MoveTowards(soulLiquidImage.fillAmount, targetFill, speed * Time.deltaTime);
            yield return null;
        }
        soulLiquidImage.fillAmount = targetFill;
    }
}