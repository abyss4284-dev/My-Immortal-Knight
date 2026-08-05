using UnityEngine;

public class BoundaryKillZone : MonoBehaviour
{
    [Header("掉落伤害设置")]
    public int fallDamage = 1; // 掉出边界扣除的生命值

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 确保触发碰撞的对象是玩家
        if (other.CompareTag("Player"))
        {
            HandlePlayerFall(other.gameObject);
        }
    }

    /// <summary>
    /// 处理玩家掉落边界的核心逻辑
    /// </summary>
    private void HandlePlayerFall(GameObject playerObj)
    {
        PlayerController playerController = playerObj.GetComponent<PlayerController>();
        PlayerSpawnManager spawnManager = Object.FindFirstObjectByType<PlayerSpawnManager>();

        if (playerController == null)
        {
            Debug.LogWarning("⚠️ [BoundaryKillZone] 未在玩家对象上找到 PlayerController 组件！");
            return;
        }

        // 1. 扣除生命值并同步保存静态变量
        playerController.currentHealth -= fallDamage;
        PlayerController.savedHealth = playerController.currentHealth; // 🌟 记录扣血后的剩余血量

        Debug.Log($"⚠️ 玩家掉出安全边界！扣除 {fallDamage} 点生命值，剩余血量: {PlayerController.savedHealth}");

        // 2. 判断生命值是否耗尽
        if (playerController.currentHealth <= 0)
        {
            Debug.Log("💀 玩家因掉落边界生命值归零，触发死亡复活！");
            playerController.isDead = true;

            // 销毁当前玩家实例
            Destroy(playerObj);

            // 调用生成管理器执行完整复活流程（复活会重置满血）
            if (spawnManager != null)
            {
                spawnManager.RespawnPlayer();
            }
            else
            {
                Debug.LogError("🚨 [BoundaryKillZone] 场景中未找到 PlayerSpawnManager！");
            }
        }
        else
        {
            // 3. 生命值仍大于 0：销毁旧玩家，并在初始点重新生成（继承扣血后的血量）
            Debug.Log("↺ 玩家掉落但生命值尚存，正在返回初始出生点...");

            // 获取 PlayerSpawnManager 配置的初始点坐标，若未配置则默认为 (0,0,0)
            Vector3 resetPosition = (spawnManager != null && spawnManager.firstBootSpawnPoint != null)
                ? spawnManager.firstBootSpawnPoint.position
                : Vector3.zero;

            // 先销毁掉落的旧玩家
            Destroy(playerObj);

            // 🌟 核心修复：直接使用 Instantiate 生成新玩家，不调用会清空血量的 InitializePlayerAndUI()
            if (spawnManager != null && spawnManager.playerPrefab != null)
            {
                GameObject newPlayer = Instantiate(spawnManager.playerPrefab, resetPosition, Quaternion.identity);
                newPlayer.tag = "Player";

                // 手动把扣血后的 savedHealth 赋予新 Player
                PlayerController newPC = newPlayer.GetComponent<PlayerController>();
                PlayerSkillManager newPS = newPlayer.GetComponent<PlayerSkillManager>();

                if (newPC != null) newPC.currentHealth = PlayerController.savedHealth;
                if (newPS != null && PlayerController.savedMana != -1) newPS.currentMana = PlayerController.savedMana;

                // 重新绑定 UI
                UIManager ui = Object.FindFirstObjectByType<UIManager>();
                if (ui != null)
                {
                    ui.ForceRebindPlayer();
                }
            }
            else
            {
                Debug.LogError("🚨 [BoundaryKillZone] 未找到 PlayerSpawnManager 或未配置 PlayerPrefab！");
            }
        }
    }
}