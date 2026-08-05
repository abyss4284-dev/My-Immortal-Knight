using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerSpawnManager : MonoBehaviour
{
    [Header("玩家预制体 (Prefab)")]
    public GameObject playerPrefab;

    [Header("UI 预制体 (Canvas Prefab)")]
    public GameObject uiPrefab;

    [Header("=== 仅用于游戏第一次启动的默认出生点 ===")]
    public Transform firstBootSpawnPoint;

    // 静态标记：用于标识当前加载场景是否是因为“复活”触发的
    public static bool isRespawning = false;

    private void Awake()
    {
        // 1. 🌟 修改：使用 InteractPortal 的传送标记
        if (InteractPortal.isTransferring)
        {
            HandlePortalSpawn();
        }
        // 2. 如果是因为死亡复活重新加载的场景/初始开局
        else if (isRespawning)
        {
            HandleRespawnSpawn();
            isRespawning = false; // 重置复活标记
        }
        // 3. 游戏第一次启动或按 P 键重载
        else
        {
            InitializePlayerAndUI();
        }
    }

    private void Update()
    {
        // 监听 P 键强行脱离卡死
        if (Input.GetKeyDown(KeyCode.P))
        {
            Debug.Log("🔄 [强制脱离] 玩家按下 P 键，正在重新加载当前场景...");

            // 备份当前状态
            PlayerController pCtrl = Object.FindFirstObjectByType<PlayerController>();
            PlayerSkillManager pSkill = Object.FindFirstObjectByType<PlayerSkillManager>();
            if (pCtrl != null) PlayerController.savedHealth = pCtrl.currentHealth;
            if (pSkill != null) PlayerController.savedMana = pSkill.currentMana;

            // 优先使用 SceneFader 进行黑幕缓冲重载
            if (SceneFader.Instance != null)
            {
                SceneFader.Instance.FadeToScene(SceneManager.GetActiveScene().name);
            }
            else
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
        }
    }

    /// <summary>
    /// 核心复活逻辑：供 PlayerController 在死亡时调用
    /// </summary>
    public void RespawnPlayer()
    {
        bool hasSavePoint = PlayerPrefs.GetInt("HasSaveData", 0) == 1;

        if (hasSavePoint)
        {
            string savedScene = PlayerPrefs.GetString("SavePoint_Scene", "");
            string currentScene = SceneManager.GetActiveScene().name;

            // 复活时满血满蓝
            PlayerController.savedHealth = -1; // -1 表示初始化满血
            PlayerController.savedMana = -1;

            if (!string.IsNullOrEmpty(savedScene) && savedScene != currentScene)
            {
                // 存档点在其他场景，带黑幕跨场景加载
                isRespawning = true;
                LoadSceneWithFader(savedScene);
            }
            else
            {
                // 存档点就在当前场景，直接将玩家移动过去
                GameObject existingPlayer = GameObject.FindWithTag("Player");
                float x = PlayerPrefs.GetFloat("SavePoint_X");
                float y = PlayerPrefs.GetFloat("SavePoint_Y");
                float z = PlayerPrefs.GetFloat("SavePoint_Z");
                Vector3 targetPos = new Vector3(x, y, z);

                if (existingPlayer != null)
                {
                    existingPlayer.transform.position = targetPos;
                    ResetPlayerStats(existingPlayer);
                }
                else
                {
                    // 若当前场景没有 Player，重新生成
                    SpawnPlayerAtPosition(targetPos);
                }
            }
        }
        else
        {
            // 无存档点，回默认场景 "InitialScene" 复活
            PlayerController.savedHealth = -1;
            PlayerController.savedMana = -1;

            if (SceneManager.GetActiveScene().name != "InitialScene")
            {
                isRespawning = true;
                LoadSceneWithFader("InitialScene");
            }
            else
            {
                // 当前就在 InitialScene，移回 firstBootSpawnPoint
                GameObject existingPlayer = GameObject.FindWithTag("Player");
                Vector3 targetPos = (firstBootSpawnPoint != null) ? firstBootSpawnPoint.position : Vector3.zero;

                if (existingPlayer != null)
                {
                    existingPlayer.transform.position = targetPos;
                    ResetPlayerStats(existingPlayer);
                }
                else
                {
                    InitializePlayerAndUI();
                }
            }
        }
    }

    /// <summary>
    /// 处理复活场景加载后的生成
    /// </summary>
    private void HandleRespawnSpawn()
    {
        bool hasSavePoint = PlayerPrefs.GetInt("HasSaveData", 0) == 1;
        Vector3 spawnPos;

        if (hasSavePoint)
        {
            float x = PlayerPrefs.GetFloat("SavePoint_X");
            float y = PlayerPrefs.GetFloat("SavePoint_Y");
            float z = PlayerPrefs.GetFloat("SavePoint_Z");
            spawnPos = new Vector3(x, y, z);
            Debug.Log($"✨ 在存档点复活！坐标: {spawnPos}");
        }
        else
        {
            spawnPos = (firstBootSpawnPoint != null) ? firstBootSpawnPoint.position : Vector3.zero;
            Debug.Log($"🏡 无存档点，在 InitialScene 默认出生点复活！坐标: {spawnPos}");
        }

        SpawnPlayerAtPosition(spawnPos);
    }

    /// <summary>
    /// 统一生成玩家并生成 UI 的方法
    /// </summary>
    private GameObject SpawnPlayerAtPosition(Vector3 position)
    {
        GameObject newPlayer = Instantiate(playerPrefab, position, Quaternion.identity);
        newPlayer.tag = "Player";

        ResetPlayerStats(newPlayer);

        if (uiPrefab != null && Object.FindFirstObjectByType<UIManager>() == null)
        {
            Instantiate(uiPrefab);
        }

        // 生成完 Player 后，主动通知 UIManager 重新绑定当前真正的玩家对象
        UIManager ui = Object.FindFirstObjectByType<UIManager>();
        if (ui != null)
        {
            ui.ForceRebindPlayer();
        }

        return newPlayer;
    }

    /// <summary>
    /// 重置玩家的状态（满血、满蓝、取消死亡状态）
    /// </summary>
    private void ResetPlayerStats(GameObject playerObj)
    {
        PlayerController pCtrl = playerObj.GetComponent<PlayerController>();
        PlayerSkillManager pSkill = playerObj.GetComponent<PlayerSkillManager>();

        if (pCtrl != null)
        {
            pCtrl.isDead = false;
            pCtrl.currentHealth = pCtrl.maxHealth;
            PlayerController.savedHealth = pCtrl.maxHealth; // 同步重置静态变量
        }

        if (pSkill != null)
        {
            pSkill.currentMana = pSkill.maxMana;
            PlayerController.savedMana = pSkill.maxMana;   // 同步重置静态变量
        }

        // 状态重置后触发 UI 更新
        UIManager ui = Object.FindFirstObjectByType<UIManager>();
        if (ui != null)
        {
            ui.ForceRebindPlayer();
        }
    }

    /// <summary>
    /// 🌟 极简重构：处理 InteractPortal 传送门切关后的玩家生成逻辑
    /// </summary>
    private void HandlePortalSpawn()
    {
        string sourceName = InteractPortal.sourcePortalName; // 修改为 InteractPortal
        string targetPortalName = "";

        if (sourceName.Contains("-"))
        {
            string[] parts = sourceName.Split('-');
            // 自动推理目标场景中对应的门名字（如 SceneA-SceneB 对应 SceneB-SceneA）
            targetPortalName = $"{parts[1]}-{parts[0]}";
        }

        GameObject targetPortalObj = GameObject.Find(targetPortalName);

        if (targetPortalObj != null)
        {
            // 🌟 核心简化：直接生成在传送门的中心位置（transform.position）
            Vector3 spawnPosition = targetPortalObj.transform.position;

            GameObject newPlayer = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
            newPlayer.tag = "Player";

            PlayerController playerScript = newPlayer.GetComponent<PlayerController>();
            PlayerSkillManager skillScript = newPlayer.GetComponent<PlayerSkillManager>();

            // 读取静态保存的血量和法力值
            if (playerScript != null && PlayerController.savedHealth != -1)
            {
                playerScript.currentHealth = PlayerController.savedHealth;
            }

            if (skillScript != null && PlayerController.savedMana != -1)
            {
                skillScript.currentMana = PlayerController.savedMana;
            }

            if (uiPrefab != null && Object.FindFirstObjectByType<UIManager>() == null)
            {
                Instantiate(uiPrefab);
            }

            // 传送生成后通知 UI 进行锁定绑定
            UIManager ui = Object.FindFirstObjectByType<UIManager>();
            if (ui != null)
            {
                ui.ForceRebindPlayer();
            }

            Debug.Log($"🚪 玩家已在传送门 [{targetPortalObj.name}] 中心顺利生成！");
        }
        else
        {
            Debug.LogError($"🚨 [生成失败] 未能在新场景中找到对应名称的传送门: [{targetPortalName}]！");
        }

        // 重置传送标记
        InteractPortal.isTransferring = false;
    }

    public void InitializePlayerAndUI()
    {
        PlayerController.savedHealth = -1; // 标记为首次加载，使用 maxHealth
        PlayerController.savedMana = -1;   // 标记为首次加载，使用 maxMana

        Vector3 spawnPos = (firstBootSpawnPoint != null) ? firstBootSpawnPoint.position : Vector3.zero;
        SpawnPlayerAtPosition(spawnPos);
    }

    /// <summary>
    /// 辅助方法：带黑幕过渡效果地加载目标场景
    /// </summary>
    private void LoadSceneWithFader(string sceneName)
    {
        if (SceneFader.Instance != null)
        {
            SceneFader.Instance.FadeToScene(sceneName);
        }
        else
        {
            SceneManager.LoadScene(sceneName);
        }
    }
}