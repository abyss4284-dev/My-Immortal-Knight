using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerSpawnManager : MonoBehaviour
{
    [Header("玩家预制体 (Prefab)")]
    public GameObject playerPrefab; //

    [Header("UI 预制体 (Canvas Prefab)")]
    public GameObject uiPrefab; //

    [Header("传送走出来的脱离间距")]
    public float exitOffset = 1.2f; //[cite: 3]

    [Header("=== 仅用于游戏第一次启动的默认出生点 ===")]
    public Transform firstBootSpawnPoint; //[cite: 3]

    // 🌟 移除 static 限制，让场景重载时能正确跑入开局生成逻辑
    private bool isFirstBoot = true;

    private void Awake()
    {
        if (ScenePortal.isTransferring) //[cite: 3]
        {
            string sourceName = ScenePortal.sourcePortalName; //[cite: 3]
            string targetPortalName = ""; //[cite: 3]

            if (sourceName.Contains("-")) //[cite: 3]
            {
                string[] parts = sourceName.Split('-'); //[cite: 3]
                targetPortalName = $"{parts[1]}-{parts[0]}"; //[cite: 3]
            }

            GameObject targetPortalObj = GameObject.Find(targetPortalName); //[cite: 3]

            if (targetPortalObj != null) //[cite: 3]
            {
                ScenePortal portalScript = targetPortalObj.GetComponent<ScenePortal>(); //[cite: 3]
                Collider2D portalCollider = targetPortalObj.GetComponent<Collider2D>(); //[cite: 3]
                Bounds portalBounds = portalCollider.bounds; //[cite: 3]

                Vector3 spawnPosition = portalBounds.center; //[cite: 3]

                if (portalScript != null) //[cite: 3]
                {
                    if (portalBounds.size.y > portalBounds.size.x) //[cite: 3]
                    {
                        float targetY = portalBounds.min.y + (ScenePortal.relativeHitRatio * portalBounds.size.y); //[cite: 3]
                        spawnPosition = new Vector3(portalBounds.center.x, targetY, 0); //[cite: 3]
                    }
                    else
                    {
                        float targetX = portalBounds.min.x + (ScenePortal.relativeHitRatio * portalBounds.size.x); //[cite: 3]
                        spawnPosition = new Vector3(targetX, portalBounds.center.y, 0); //[cite: 3]
                    }

                    float wallHalfThickness = (portalBounds.size.y > portalBounds.size.x) ? portalBounds.size.x * 0.5f : portalBounds.size.y * 0.5f; //[cite: 3]
                    spawnPosition += portalScript.localExitDirection * (wallHalfThickness + exitOffset); //[cite: 3]

                    portalScript.isPlayerSpawningInside = true; //[cite: 3]
                    Debug.Log($"🔒 [状态逻辑锁定] 已标记 [{targetPortalName}]，在其解除前拒绝触发传送。[cite: 3]");
                }

                GameObject newPlayer = Instantiate(playerPrefab, spawnPosition, Quaternion.identity); //[cite: 3]
                newPlayer.tag = "Player"; //[cite: 3]

                PlayerController playerScript = newPlayer.GetComponent<PlayerController>(); //[cite: 3]
                PlayerSkillManager skillScript = newPlayer.GetComponent<PlayerSkillManager>();

                if (playerScript != null && PlayerController.savedHealth != -1) //[cite: 3]
                {
                    playerScript.currentHealth = PlayerController.savedHealth; //[cite: 3]
                }

                if (uiPrefab != null) //[cite: 3]
                {
                    GameObject newUI = Instantiate(uiPrefab); //[cite: 3]
                    UIManager uiScript = newUI.GetComponentInChildren<UIManager>(); //[cite: 3]
                    if (uiScript != null) //[cite: 3]
                    {
                        uiScript.SetupUI(playerScript); //[cite: 3]
                        if (skillScript != null) uiScript.SetupSkillManager(skillScript);
                    }
                }
            }
            else
            {
                Debug.LogError($"🚨 [找不到接应墙] 场景中没有找到命名为 [{targetPortalName}] 的空气墙！[cite: 3]");
            }

            ScenePortal.isTransferring = false; //[cite: 3]
        }
        else
        {
            // 🌟 核心修复：无论是第一次游戏启动，还是按下 P 键后重载该场景，
            // 只要不是通过传送门切换进来的，都统一执行初始点生成逻辑。
            InitializePlayerAndUI();
        }
    }

    private void Update()
    {
        // 监听 P 键强行脱离卡死
        if (Input.GetKeyDown(KeyCode.P))
        {
            Debug.Log("🔄 [强制脱离] 玩家按下 P 键，正在重新加载当前场景...");

            // 备份当前状态，防止重载场景后残血变满血
            PlayerController pCtrl = Object.FindFirstObjectByType<PlayerController>();
            PlayerSkillManager pSkill = Object.FindFirstObjectByType<PlayerSkillManager>();
            if (pCtrl != null) PlayerController.savedHealth = pCtrl.currentHealth;
            if (pSkill != null) PlayerController.savedMana = pSkill.currentMana;

            // 重新加载当前场景
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    // 游戏开局初始化与 P 键重载复用的核心方法
    public void InitializePlayerAndUI()
    {
        if (firstBootSpawnPoint != null) //[cite: 3]
        {
            GameObject newPlayer = Instantiate(playerPrefab, firstBootSpawnPoint.position, Quaternion.identity); //[cite: 3]
            newPlayer.tag = "Player"; //[cite: 3]

            PlayerController playerScript = newPlayer.GetComponent<PlayerController>();
            PlayerSkillManager skillScript = newPlayer.GetComponent<PlayerSkillManager>();

            if (uiPrefab != null) //[cite: 3]
            {
                GameObject newUI = Instantiate(uiPrefab); //[cite: 3]
                UIManager uiScript = newUI.GetComponentInChildren<UIManager>(); //[cite: 3]
                if (uiScript != null) //[cite: 3]
                {
                    uiScript.SetupUI(playerScript); //[cite: 3]
                    if (skillScript != null) uiScript.SetupSkillManager(skillScript);
                }
            }
            Debug.Log("🎮 [场景初始化完成] Player 与 UI 动态生成并绑定成功。");
        }
    }
}