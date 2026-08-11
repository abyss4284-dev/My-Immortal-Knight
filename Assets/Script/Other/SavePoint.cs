using UnityEngine;
using UnityEngine.SceneManagement;

public class SavePoint : MonoBehaviour
{
    [Header("状态子物体引用")]
    [Tooltip("熄灭状态的灯子物体")]
    public GameObject unlitLamp;
    [Tooltip("点亮状态的灯子物体")]
    public GameObject litLamp;

    [Header("UI 提示设置")]
    [Tooltip("靠近时显示的 UI 提示物体 (如包含 '按 F 存档' 文字的 GameObject)")]
    public GameObject interactHintUI;

    [Header("交互设置")]
    [Tooltip("触发交互的按键，默认是 F 键")]
    public KeyCode interactKey = KeyCode.F;
    [Tooltip("当玩家站在存档点上时，生成复活点的垂直偏移量（避免玩家生成在地下）")]
    public Vector3 spawnOffset = new Vector3(0, 0.5f, 0);

    private bool isPlayerInRange = false; // 玩家是否在检测范围内
    private bool isActivated = false;     // 该存档点是否已被点亮
    private GameObject playerObj;        // 记录范围内的玩家物体

    void Start()
    {
        // 默认显示熄灭状态，隐藏点亮状态
        SetLampState(isLit: false);

        // 🌟 默认隐藏 UI 提示
        if (interactHintUI != null)
        {
            interactHintUI.SetActive(false);
        }
    }

    void Update()
    {
        // 当玩家在范围内、按下交互键时触发
        if (isPlayerInRange && Input.GetKeyDown(interactKey))
        {
            ActivateSavePoint();
            PlayerSkillManager.ResetRebirthCount();
        }
    }

    /// <summary>
    /// 激活/使用存档点逻辑
    /// </summary>
    private void ActivateSavePoint()
    {
        // 1. 切换灯的外观状态
        if (!isActivated)
        {
            isActivated = true;
            SetLampState(isLit: true);
        }

        // 2. 💖 恢复玩家的血量和法力值
        HealAndRestorePlayer();

        // 3. 计算玩家后续复活的目标坐标（灯的坐标 + 偏移量）
        Vector3 respawnPosition = transform.position + spawnOffset;

        // 4. 💾 写入本地存档数据
        UpdateSaveData(respawnPosition);

        Debug.Log($"✨ [存档成功] 玩家状态已完全恢复！复活点已更新为: {respawnPosition}");
    }

    /// <summary>
    /// 🌟 将玩家血量和法力值回复满（完全只操作数据，不碰 UI）
    /// </summary>
    private void HealAndRestorePlayer()
    {
        if (playerObj == null)
        {
            playerObj = GameObject.FindWithTag("Player");
        }

        if (playerObj != null)
        {
            PlayerController playerCtrl = playerObj.GetComponent<PlayerController>();
            PlayerSkillManager skillManager = playerObj.GetComponent<PlayerSkillManager>();

            // 1. 恢复血量 (PlayerController)
            if (playerCtrl != null)
            {
                playerCtrl.currentHealth = playerCtrl.maxHealth;
                PlayerController.savedHealth = playerCtrl.maxHealth;
            }

            // 2. 恢复蓝量 (PlayerSkillManager)
            if (skillManager != null)
            {
                skillManager.currentMana = skillManager.maxMana;
                PlayerController.savedMana = skillManager.maxMana;
            }
        }
    }

    /// <summary>
    /// 控制子物体的显示/隐藏
    /// </summary>
    private void SetLampState(bool isLit)
    {
        if (unlitLamp != null) unlitLamp.SetActive(!isLit);
        if (litLamp != null) litLamp.SetActive(isLit);
    }

    /// <summary>
    /// 保存存档数据到 PlayerPrefs
    /// </summary>
    private void UpdateSaveData(Vector3 newSpawnPosition)
    {
        PlayerPrefs.SetFloat("SavePoint_X", newSpawnPosition.x);
        PlayerPrefs.SetFloat("SavePoint_Y", newSpawnPosition.y);
        PlayerPrefs.SetFloat("SavePoint_Z", newSpawnPosition.z);
        // 保存当前场景名，适配跨场景复活
        PlayerPrefs.SetString("SavePoint_Scene", SceneManager.GetActiveScene().name);
        PlayerPrefs.SetInt("HasSaveData", 1); // 标记已有存档
        PlayerPrefs.Save();
    }

    #region 触发器检测 (Trigger)
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            isPlayerInRange = true;
            playerObj = collision.gameObject;

            // 🌟 玩家靠近，显示 UI 提示框
            if (interactHintUI != null)
            {
                interactHintUI.SetActive(true);
            }

            if (!isActivated)
            {
                Debug.Log($"💡 [提示] 按下 {interactKey} 键点亮存档点并回复满状态");
            }
            else
            {
                Debug.Log($"💡 [提示] 按下 {interactKey} 键更新存档并回复满状态");
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            isPlayerInRange = false;

            // 🌟 玩家离开，隐藏 UI 提示框
            if (interactHintUI != null)
            {
                interactHintUI.SetActive(false);
            }
        }
    }
    #endregion
}