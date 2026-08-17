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
        // 🌟 仅保留空气墙 (ScenePortal) 传送判断
        if (ScenePortal.isTransferring)
        {
            HandlePortalSpawn();
        }
        // 如果是因为死亡复活重新加载的场景
        else if (isRespawning)
        {
            HandleRespawnSpawn();
            isRespawning = false; // 重置复活标记
        }
        // 游戏第一次启动或直接加载场景
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

            PlayerController pCtrl = Object.FindFirstObjectByType<PlayerController>();
            PlayerSkillManager pSkill = Object.FindFirstObjectByType<PlayerSkillManager>();
            if (pCtrl != null) PlayerController.savedHealth = pCtrl.currentHealth;
            if (pSkill != null) PlayerController.savedMana = pSkill.currentMana;

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
    /// 处理 ScenePortal 空气墙切关后的玩家生成逻辑（固定 Y = -4）
    /// </summary>
    private void HandlePortalSpawn()
    {
        string sourceName = ScenePortal.sourcePortalName;
        string targetPortalName = "";

        if (!string.IsNullOrEmpty(sourceName) && sourceName.Contains("-"))
        {
            string[] parts = sourceName.Split('-');
            // 推理目标场景中对应的门名字（如 SceneA-SceneB 对应 SceneB-SceneA）
            targetPortalName = $"{parts[1]}-{parts[0]}";
        }

        GameObject targetPortalObj = GameObject.Find(targetPortalName);

        if (targetPortalObj != null)
        {
            // 获取目标空气墙位置，并将 Y 轴高度固定设为 -4
            Vector3 spawnPosition = targetPortalObj.transform.position;
            spawnPosition.y = -4f;

            // 智能检测：只有当生成坐标踩在空气墙碰撞盒内部时，才开启防二次传送锁
            ScenePortal portalScript = targetPortalObj.GetComponent<ScenePortal>();
            Collider2D portalCollider = targetPortalObj.GetComponent<Collider2D>();

            if (portalScript != null)
            {
                if (portalCollider != null)
                {
                    portalScript.isPlayerSpawningInside = portalCollider.OverlapPoint(spawnPosition);
                }
                else
                {
                    portalScript.isPlayerSpawningInside = false;
                }
            }

            // 生成玩家
            GameObject newPlayer = SpawnPlayerAtPosition(spawnPosition);

            // 继承上一场景的血量与法力值
            PlayerController playerScript = newPlayer.GetComponent<PlayerController>();
            PlayerSkillManager skillScript = newPlayer.GetComponent<PlayerSkillManager>();

            if (playerScript != null && PlayerController.savedHealth != -1)
            {
                playerScript.currentHealth = PlayerController.savedHealth;
            }

            if (skillScript != null && PlayerController.savedMana != -1)
            {
                skillScript.currentMana = PlayerController.savedMana;
            }

            Debug.Log($"🚪 玩家已在空气墙 [{targetPortalObj.name}] 位置成功生成！(X={spawnPosition.x}, Y=-4)");
        }
        else
        {
            Debug.LogError($"🚨 [生成失败] 未能在新场景中找到名称为 [{targetPortalName}] 的空气墙！已生成在默认出生点。");
            InitializePlayerAndUI();
        }

        // 清空 ScenePortal 静态数据
        ScenePortal.isTransferring = false;
        ScenePortal.sourcePortalName = "";
    }

    public void RespawnPlayer()
    {
        bool hasSavePoint = PlayerPrefs.GetInt("HasSaveData", 0) == 1;

        if (hasSavePoint)
        {
            string savedScene = PlayerPrefs.GetString("SavePoint_Scene", "");
            string currentScene = SceneManager.GetActiveScene().name;

            PlayerController.savedHealth = -1;
            PlayerController.savedMana = -1;

            if (!string.IsNullOrEmpty(savedScene) && savedScene != currentScene)
            {
                isRespawning = true;
                LoadSceneWithFader(savedScene);
            }
            else
            {
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
                    SpawnPlayerAtPosition(targetPos);
                }
            }
        }
        else
        {
            PlayerController.savedHealth = -1;
            PlayerController.savedMana = -1;

            if (SceneManager.GetActiveScene().name != "InitialScene")
            {
                isRespawning = true;
                LoadSceneWithFader("InitialScene");
            }
            else
            {
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

    private GameObject SpawnPlayerAtPosition(Vector3 position)
    {
        GameObject newPlayer = Instantiate(playerPrefab, position, Quaternion.identity);
        newPlayer.tag = "Player";

        ResetPlayerStats(newPlayer);

        if (uiPrefab != null && Object.FindFirstObjectByType<UIManager>() == null)
        {
            Instantiate(uiPrefab);
        }

        UIManager ui = Object.FindFirstObjectByType<UIManager>();
        if (ui != null)
        {
            ui.ForceRebindPlayer();
        }

        return newPlayer;
    }

    private void ResetPlayerStats(GameObject playerObj)
    {
        PlayerController pCtrl = playerObj.GetComponent<PlayerController>();
        PlayerSkillManager pSkill = playerObj.GetComponent<PlayerSkillManager>();

        if (pCtrl != null)
        {
            pCtrl.isDead = false;
            pCtrl.currentHealth = pCtrl.maxHealth;
            PlayerController.savedHealth = pCtrl.maxHealth;
        }

        if (pSkill != null)
        {
            pSkill.currentMana = pSkill.maxMana;
            PlayerController.savedMana = pSkill.maxMana;
        }

        UIManager ui = Object.FindFirstObjectByType<UIManager>();
        if (ui != null)
        {
            ui.ForceRebindPlayer();
        }
    }

    public void InitializePlayerAndUI()
    {
        PlayerController.savedHealth = -1;
        PlayerController.savedMana = -1;

        Vector3 spawnPos = (firstBootSpawnPoint != null) ? firstBootSpawnPoint.position : Vector3.zero;
        SpawnPlayerAtPosition(spawnPos);
    }

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